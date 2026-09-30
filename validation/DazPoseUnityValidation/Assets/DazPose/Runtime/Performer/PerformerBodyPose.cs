using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer
{
    internal sealed class PerformerPoseSnapshot
    {
        public readonly PerformerPoseTransformState[] Transforms;
        public readonly float[] BlendShapes;

        public PerformerPoseSnapshot(int transformCount, int blendShapeCount)
        {
            Transforms = new PerformerPoseTransformState[transformCount];
            BlendShapes = new float[blendShapeCount];
        }
    }

    internal sealed class PerformerBodyPose : IDisposable
    {
        private const float StaticPoseSampleTimeSeconds = 0.5f;

        private readonly Animator _animator;
        private readonly PlayableGraph _graph;
        private readonly Transform[] _transforms;
        private readonly SkinnedMeshRenderer[] _blendShapeRenderers;
        private readonly PerformerPoseSnapshot _neutralState;

        private NativeArray<TransformStreamHandle> _transformHandles;
        private NativeArray<PropertyStreamHandle> _blendShapeHandles;
        private NativeArray<PerformerPoseTransformState> _sourceTransforms;
        private NativeArray<PerformerPoseTransformState> _targetTransforms;
        private NativeArray<float> _sourceBlendShapes;
        private NativeArray<float> _targetBlendShapes;
        private NativeArray<PerformerPoseTransformState> _evaluatedBaseTransforms;
        private NativeArray<float> _evaluatedBaseBlendShapes;

        private AnimationScriptPlayable _sourceStatePlayable;
        private AnimationScriptPlayable _targetStatePlayable;
        private AnimationMixerPlayable _mixer;
        private AnimationScriptPlayable _extrapolationPlayable;
        private AnimationScriptPlayable _baseCapturePlayable;
        private PoseTransition _activeTransition;
        private float _elapsedSeconds;
        private bool _isTransitioning;
        private bool _disposed;

        public Playable OutputPlayable => _baseCapturePlayable;
        public PerformerPose SettledPose { get; private set; }
        public PerformerPose DesiredPose { get; private set; }
        public bool IsTransitioning => _isTransitioning;
        public float TransitionProgress { get; private set; }
        public float TrajectoryProgress { get; private set; }
        public PoseTransition ActiveTransition => _activeTransition;
        public PerformerPoseSnapshot NeutralState => _neutralState;

        public PerformerBodyPose(Animator animator, PlayableGraph graph, PerformerPoseSnapshot neutralState = null)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            if (!graph.IsValid()) throw new ArgumentException("A valid PlayableGraph is required.", nameof(graph));

            _animator = animator;
            _graph = graph;
            _transforms = animator.GetComponentsInChildren<Transform>(true);
            _blendShapeRenderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _neutralState = neutralState ?? CaptureLiveState();
            if (_neutralState.Transforms.Length != _transforms.Length
                || _neutralState.BlendShapes.Length != CountBlendShapes())
                throw new InvalidOperationException("The Animator hierarchy changed while the performer was disabled. Re-enable after refreshing its neutral pose state.");

            try
            {
                CreateGraphNodes();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void SetPose(PerformerPose pose, PoseTransition transition)
        {
            ThrowIfDisposed();
            if (pose == null) throw new ArgumentNullException(nameof(pose));
            if (pose.Clip == null) throw new ArgumentException("The PerformerPose has no AnimationClip assigned.", nameof(pose));
            transition.Validate();
            if (pose == DesiredPose) return;

            var currentState = CaptureEvaluatedBaseState();
            var targetState = SamplePose(pose.Clip, currentState);

            DesiredPose = pose;
            _activeTransition = transition;
            _elapsedSeconds = 0f;
            TransitionProgress = 0f;
            _isTransitioning = transition.Duration > 0f && SettledPose != null;

            if (!_isTransitioning)
            {
                CopyStateToNative(targetState, _sourceTransforms, _sourceBlendShapes);
                CopyStateToNative(targetState, _targetTransforms, _targetBlendShapes);
                SetMixerWeights(0f);
                SetTrajectoryProgress(1f);
                SettledPose = pose;
                TransitionProgress = 1f;
                _activeTransition = default;
                _graph.Evaluate(0f);
                return;
            }

            CopyStateToNative(currentState, _sourceTransforms, _sourceBlendShapes);
            CopyStateToNative(targetState, _targetTransforms, _targetBlendShapes);
            SetMixerWeights(0f);
            SetTrajectoryProgress(0f);
            _graph.Evaluate(0f);
        }

        public void Advance(float deltaTime)
        {
            if (_disposed || !_isTransitioning || !_graph.IsValid()) return;

            _elapsedSeconds += Mathf.Max(0f, deltaTime);
            var progress = Mathf.Clamp01(_elapsedSeconds / _activeTransition.Duration);
            TransitionProgress = progress;
            var trajectory = PoseTransitionTrajectory.Evaluate(progress, _activeTransition);
            SetMixerWeights(trajectory);
            SetTrajectoryProgress(trajectory);

            if (progress >= 1f) CompleteTransition();
        }

        public PerformerPoseSnapshot CaptureEvaluatedBaseState()
        {
            ThrowIfDisposed();
            if (_graph.IsValid()) _graph.Evaluate(0f);
            return CopyEvaluatedBaseState();
        }

        public PerformerPoseSnapshot CaptureTransitionSourceState()
        {
            ThrowIfDisposed();
            var snapshot = new PerformerPoseSnapshot(_sourceTransforms.Length, _sourceBlendShapes.Length);
            for (var index = 0; index < _sourceTransforms.Length; index++)
                snapshot.Transforms[index] = _sourceTransforms[index];
            for (var index = 0; index < _sourceBlendShapes.Length; index++)
                snapshot.BlendShapes[index] = _sourceBlendShapes[index];
            return snapshot;
        }

        public int GetBlendShapeStreamIndex(SkinnedMeshRenderer renderer, int shapeIndex)
        {
            var streamIndex = 0;
            foreach (var candidate in _blendShapeRenderers)
            {
                var mesh = candidate == null ? null : candidate.sharedMesh;
                if (mesh == null) continue;
                if (candidate == renderer)
                    return shapeIndex >= 0 && shapeIndex < mesh.blendShapeCount ? streamIndex + shapeIndex : -1;
                streamIndex += mesh.blendShapeCount;
            }
            return -1;
        }

        public int GetTransformStreamIndex(Transform transform)
        {
            for (var index = 0; index < _transforms.Length; index++)
                if (_transforms[index] == transform) return index;
            return -1;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DestroyCreatedPlayables();
            DisposeNativeArrays();
            _sourceStatePlayable = default;
            _targetStatePlayable = default;
            _mixer = default;
            _extrapolationPlayable = default;
            _isTransitioning = false;
            _activeTransition = default;
        }

        private void CreateGraphNodes()
        {
            var blendShapeCount = CountBlendShapes();
            _transformHandles = new NativeArray<TransformStreamHandle>(_transforms.Length, Allocator.Persistent);
            _blendShapeHandles = new NativeArray<PropertyStreamHandle>(blendShapeCount, Allocator.Persistent);
            _sourceTransforms = new NativeArray<PerformerPoseTransformState>(_transforms.Length, Allocator.Persistent);
            _targetTransforms = new NativeArray<PerformerPoseTransformState>(_transforms.Length, Allocator.Persistent);
            _sourceBlendShapes = new NativeArray<float>(blendShapeCount, Allocator.Persistent);
            _targetBlendShapes = new NativeArray<float>(blendShapeCount, Allocator.Persistent);
            _evaluatedBaseTransforms = new NativeArray<PerformerPoseTransformState>(_transforms.Length, Allocator.Persistent);
            _evaluatedBaseBlendShapes = new NativeArray<float>(blendShapeCount, Allocator.Persistent);

            for (var index = 0; index < _transforms.Length; index++)
                _transformHandles[index] = _animator.BindStreamTransform(_transforms[index]);

            var handleIndex = 0;
            foreach (var renderer in _blendShapeRenderers)
            {
                var mesh = renderer == null ? null : renderer.sharedMesh;
                if (mesh == null) continue;
                for (var shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
                {
                    var rendererPath = PerformerLipSyncMorphCatalog.GetRelativePath(_animator.transform, renderer.transform);
                    var shapeName = mesh.GetBlendShapeName(shapeIndex);
                    if (PerformerLipSyncMorphCatalog.IsOwnedBinding(rendererPath, shapeName))
                    {
                        // An unbound handle is skipped by each BodyPose job. SALSA remains the only
                        // graph/runtime writer for this exact renderer/name pair.
                        _blendShapeHandles[handleIndex++] = default;
                        continue;
                    }
                    var propertyName = "blendShape." + mesh.GetBlendShapeName(shapeIndex);
                    _blendShapeHandles[handleIndex++] = _animator.BindStreamProperty(
                        renderer.transform, typeof(SkinnedMeshRenderer), propertyName);
                }
            }

            var neutral = CaptureLiveState();
            CopyStateToNative(neutral, _sourceTransforms, _sourceBlendShapes);
            CopyStateToNative(neutral, _targetTransforms, _targetBlendShapes);
            CopyStateToNative(neutral, _evaluatedBaseTransforms, _evaluatedBaseBlendShapes);

            _sourceStatePlayable = AnimationScriptPlayable.Create(_graph, CreateStateJob(_sourceTransforms, _sourceBlendShapes), 0);
            _targetStatePlayable = AnimationScriptPlayable.Create(_graph, CreateStateJob(_targetTransforms, _targetBlendShapes), 0);
            _mixer = AnimationMixerPlayable.Create(_graph, 2);
            if (!_graph.Connect(_sourceStatePlayable, 0, _mixer, 0)
                || !_graph.Connect(_targetStatePlayable, 0, _mixer, 1))
                throw new InvalidOperationException("Could not connect the performer pose snapshots to their mixer.");

            var extrapolationJob = new PerformerPoseExtrapolationJob
            {
                TransformHandles = _transformHandles,
                BlendShapeHandles = _blendShapeHandles,
                SourceTransforms = _sourceTransforms,
                TargetTransforms = _targetTransforms,
                SourceBlendShapes = _sourceBlendShapes,
                TargetBlendShapes = _targetBlendShapes,
                Progress = 0f
            };
            _extrapolationPlayable = AnimationScriptPlayable.Create(_graph, extrapolationJob, 1);
            _extrapolationPlayable.SetProcessInputs(true);
            if (!_graph.Connect(_mixer, 0, _extrapolationPlayable, 0))
                throw new InvalidOperationException("Could not connect the pose mixer to its extrapolation job.");

            var captureJob = new PerformerBaseStateCaptureJob
            {
                TransformHandles = _transformHandles,
                BlendShapeHandles = _blendShapeHandles,
                CapturedTransforms = _evaluatedBaseTransforms,
                CapturedBlendShapes = _evaluatedBaseBlendShapes
            };
            _baseCapturePlayable = AnimationScriptPlayable.Create(_graph, captureJob, 1);
            _baseCapturePlayable.SetProcessInputs(true);
            _baseCapturePlayable.SetInputWeight(0, 1f);
            if (!_graph.Connect(_extrapolationPlayable, 0, _baseCapturePlayable, 0))
                throw new InvalidOperationException("Could not connect the base-pose capture stage.");

            SetMixerWeights(0f);
            SetTrajectoryProgress(0f);
        }

        private PerformerPoseStateJob CreateStateJob(
            NativeArray<PerformerPoseTransformState> transformValues, NativeArray<float> blendShapeValues)
        {
            return new PerformerPoseStateJob
            {
                TransformHandles = _transformHandles,
                BlendShapeHandles = _blendShapeHandles,
                TransformValues = transformValues,
                BlendShapeValues = blendShapeValues
            };
        }

        private void CompleteTransition()
        {
            if (DesiredPose == null) return;
            CopyNativeState(_targetTransforms, _targetBlendShapes, _sourceTransforms, _sourceBlendShapes);
            SetMixerWeights(0f);
            SetTrajectoryProgress(1f);
            SettledPose = DesiredPose;
            _isTransitioning = false;
            _elapsedSeconds = 0f;
            TransitionProgress = 1f;
            _activeTransition = default;
        }

        private void SetMixerWeights(float trajectory)
        {
            if (!_mixer.IsValid()) return;
            var targetWeight = Mathf.Clamp01(trajectory);
            _mixer.SetInputWeight(0, 1f - targetWeight);
            _mixer.SetInputWeight(1, targetWeight);
        }

        private void SetTrajectoryProgress(float progress)
        {
            TrajectoryProgress = progress;
            if (!_extrapolationPlayable.IsValid()) return;
            var job = _extrapolationPlayable.GetJobData<PerformerPoseExtrapolationJob>();
            job.Progress = progress;
            _extrapolationPlayable.SetJobData(job);
        }

        private PerformerPoseSnapshot SamplePose(AnimationClip clip, PerformerPoseSnapshot stateToRestore)
        {
            var speechState = CaptureSpeechOwnedWeights();
            try
            {
                RestoreNeutralState();
                clip.SampleAnimation(_animator.gameObject, StaticPoseSampleTimeSeconds);
                return CaptureLiveState();
            }
            finally
            {
                RestoreState(stateToRestore);
                RestoreSpeechOwnedWeights(speechState);
            }
        }

        private PerformerPoseSnapshot CaptureLiveState()
        {
            var snapshot = new PerformerPoseSnapshot(_transforms.Length, CountBlendShapes());
            for (var index = 0; index < _transforms.Length; index++)
            {
                var transform = _transforms[index];
                snapshot.Transforms[index] = new PerformerPoseTransformState
                {
                    LocalPosition = transform.localPosition,
                    LocalRotation = transform.localRotation,
                    LocalScale = transform.localScale
                };
            }

            var blendShapeIndex = 0;
            foreach (var renderer in _blendShapeRenderers)
            {
                var mesh = renderer == null ? null : renderer.sharedMesh;
                if (mesh == null) continue;
                for (var shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
                {
                    var rendererPath = PerformerLipSyncMorphCatalog.GetRelativePath(_animator.transform, renderer.transform);
                    var shapeName = mesh.GetBlendShapeName(shapeIndex);
                    snapshot.BlendShapes[blendShapeIndex++] = PerformerLipSyncMorphCatalog.IsOwnedBinding(rendererPath, shapeName)
                        ? PerformerLipSyncMorphCatalog.RestUnityWeight
                        : renderer.GetBlendShapeWeight(shapeIndex);
                }
            }
            return snapshot;
        }

        private PerformerPoseSnapshot CopyEvaluatedBaseState()
        {
            var snapshot = new PerformerPoseSnapshot(_evaluatedBaseTransforms.Length, _evaluatedBaseBlendShapes.Length);
            for (var index = 0; index < _evaluatedBaseTransforms.Length; index++)
                snapshot.Transforms[index] = _evaluatedBaseTransforms[index];
            for (var index = 0; index < _evaluatedBaseBlendShapes.Length; index++)
                snapshot.BlendShapes[index] = _evaluatedBaseBlendShapes[index];
            return snapshot;
        }

        private void RestoreNeutralState()
        {
            for (var index = 0; index < _transforms.Length; index++)
            {
                var transform = _transforms[index];
                var state = _neutralState.Transforms[index];
                transform.localPosition = state.LocalPosition;
                transform.localRotation = state.LocalRotation;
                transform.localScale = state.LocalScale;
            }

            var blendShapeIndex = 0;
            foreach (var renderer in _blendShapeRenderers)
            {
                var mesh = renderer == null ? null : renderer.sharedMesh;
                if (mesh == null) continue;
                for (var shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
                {
                    var rendererPath = PerformerLipSyncMorphCatalog.GetRelativePath(_animator.transform, renderer.transform);
                    var shapeName = mesh.GetBlendShapeName(shapeIndex);
                    var weight = _neutralState.BlendShapes[blendShapeIndex++];
                    if (!PerformerLipSyncMorphCatalog.IsOwnedBinding(rendererPath, shapeName))
                        renderer.SetBlendShapeWeight(shapeIndex, weight);
                }
            }
        }

        private void RestoreState(PerformerPoseSnapshot snapshot)
        {
            if (snapshot == null) return;
            for (var index = 0; index < _transforms.Length; index++)
            {
                var transform = _transforms[index];
                var state = snapshot.Transforms[index];
                transform.localPosition = state.LocalPosition;
                transform.localRotation = state.LocalRotation;
                transform.localScale = state.LocalScale;
            }

            var blendShapeIndex = 0;
            foreach (var renderer in _blendShapeRenderers)
            {
                var mesh = renderer == null ? null : renderer.sharedMesh;
                if (mesh == null) continue;
                for (var shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
                {
                    var rendererPath = PerformerLipSyncMorphCatalog.GetRelativePath(_animator.transform, renderer.transform);
                    var shapeName = mesh.GetBlendShapeName(shapeIndex);
                    var weight = snapshot.BlendShapes[blendShapeIndex++];
                    if (!PerformerLipSyncMorphCatalog.IsOwnedBinding(rendererPath, shapeName))
                        renderer.SetBlendShapeWeight(shapeIndex, weight);
                }
            }
        }

        private PerformerLipSyncMorphWeight[] CaptureSpeechOwnedWeights()
        {
            var values = new List<PerformerLipSyncMorphWeight>();
            foreach (var renderer in _blendShapeRenderers)
            {
                var mesh = renderer == null ? null : renderer.sharedMesh;
                if (mesh == null) continue;
                var rendererPath = PerformerLipSyncMorphCatalog.GetRelativePath(_animator.transform, renderer.transform);
                for (var shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
                {
                    var name = mesh.GetBlendShapeName(shapeIndex);
                    if (!PerformerLipSyncMorphCatalog.IsOwnedBinding(rendererPath, name)) continue;
                    values.Add(new PerformerLipSyncMorphWeight(renderer, shapeIndex,
                        renderer.GetBlendShapeWeight(shapeIndex)));
                }
            }
            return values.ToArray();
        }

        private static void RestoreSpeechOwnedWeights(IEnumerable<PerformerLipSyncMorphWeight> values)
        {
            if (values == null) return;
            foreach (var value in values)
                if (value.Renderer != null && value.Renderer.sharedMesh != null
                    && value.Index >= 0 && value.Index < value.Renderer.sharedMesh.blendShapeCount)
                    value.Renderer.SetBlendShapeWeight(value.Index, value.Weight);
        }

        private void CopyStateToNative(PerformerPoseSnapshot source,
            NativeArray<PerformerPoseTransformState> destinationTransforms, NativeArray<float> destinationBlendShapes)
        {
            for (var index = 0; index < source.Transforms.Length; index++)
                destinationTransforms[index] = source.Transforms[index];
            for (var index = 0; index < source.BlendShapes.Length; index++)
                destinationBlendShapes[index] = source.BlendShapes[index];
        }

        private static void CopyNativeState(
            NativeArray<PerformerPoseTransformState> sourceTransforms, NativeArray<float> sourceBlendShapes,
            NativeArray<PerformerPoseTransformState> destinationTransforms, NativeArray<float> destinationBlendShapes)
        {
            for (var index = 0; index < sourceTransforms.Length; index++)
                destinationTransforms[index] = sourceTransforms[index];
            for (var index = 0; index < sourceBlendShapes.Length; index++)
                destinationBlendShapes[index] = sourceBlendShapes[index];
        }

        private int CountBlendShapes()
        {
            var count = 0;
            foreach (var renderer in _blendShapeRenderers)
            {
                if (renderer != null && renderer.sharedMesh != null)
                    count += renderer.sharedMesh.blendShapeCount;
            }
            return count;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PerformerBodyPose));
        }

        private readonly struct PerformerLipSyncMorphWeight
        {
            public PerformerLipSyncMorphWeight(SkinnedMeshRenderer renderer, int index, float weight)
            {
                Renderer = renderer;
                Index = index;
                Weight = weight;
            }

            public SkinnedMeshRenderer Renderer { get; }
            public int Index { get; }
            public float Weight { get; }
        }

        private void DisposeNativeArrays()
        {
            if (_transformHandles.IsCreated) _transformHandles.Dispose();
            if (_blendShapeHandles.IsCreated) _blendShapeHandles.Dispose();
            if (_sourceTransforms.IsCreated) _sourceTransforms.Dispose();
            if (_targetTransforms.IsCreated) _targetTransforms.Dispose();
            if (_sourceBlendShapes.IsCreated) _sourceBlendShapes.Dispose();
            if (_targetBlendShapes.IsCreated) _targetBlendShapes.Dispose();
            if (_evaluatedBaseTransforms.IsCreated) _evaluatedBaseTransforms.Dispose();
            if (_evaluatedBaseBlendShapes.IsCreated) _evaluatedBaseBlendShapes.Dispose();
            _transformHandles = default;
            _blendShapeHandles = default;
            _sourceTransforms = default;
            _targetTransforms = default;
            _sourceBlendShapes = default;
            _targetBlendShapes = default;
            _evaluatedBaseTransforms = default;
            _evaluatedBaseBlendShapes = default;
        }

        private void DestroyCreatedPlayables()
        {
            if (_baseCapturePlayable.IsValid()) _baseCapturePlayable.Destroy();
            if (_extrapolationPlayable.IsValid()) _extrapolationPlayable.Destroy();
            if (_mixer.IsValid()) _mixer.Destroy();
            if (_sourceStatePlayable.IsValid()) _sourceStatePlayable.Destroy();
            if (_targetStatePlayable.IsValid()) _targetStatePlayable.Destroy();
            _baseCapturePlayable = default;
        }

    }
}
