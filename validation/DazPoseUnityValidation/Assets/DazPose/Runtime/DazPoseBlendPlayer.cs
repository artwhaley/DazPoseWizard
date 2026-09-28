using System;
using System.Globalization;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.UnityValidation
{
    public enum DazPoseBlendEase
    {
        Linear,
        SmoothStep
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("DAZ Pose/Runtime Pose Blend Player")]
    public sealed class DazPoseBlendPlayer : MonoBehaviour
    {
        private const float PoseSampleTimeSeconds = 0.5f;

        [SerializeField] private AnimationClip currentPoseDebug;
        [SerializeField] private AnimationClip targetPoseDebug;
        [SerializeField] private AnimationClip pendingPoseDebug;
        [SerializeField, Range(0f, 1f)] private float blendProgressDebug;
        [SerializeField] private float trajectoryProgressDebug;
        [SerializeField] private float activeDurationDebug;
        [SerializeField] private DazPoseBlendEase activeEaseDebug;
        [SerializeField, Range(0f, 1f)] private float activeWindupDebug;
        [SerializeField, Range(0f, 1f)] private float activeOvershootDebug;
        [SerializeField] private float pendingDurationDebug;
        [SerializeField] private DazPoseBlendEase pendingEaseDebug;
        [SerializeField, Range(0f, 1f)] private float pendingWindupDebug;
        [SerializeField, Range(0f, 1f)] private float pendingOvershootDebug;

        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationScriptPlayable _extrapolationPlayable;
        private AnimationClipPlayable[] _clipPlayables;
        private AnimationClip _currentPose;
        private AnimationClip _targetPose;
        private DazPoseTransitionOptions _activeOptions;
        private PoseCommand _pendingCommand;
        private bool _hasPendingCommand;
        private float _blendElapsedSeconds;
        private int _currentInput = -1;
        private int _targetInput = -1;
        private bool _isBlending;
        private bool _mixerWeightsStayedInRange = true;

        private Transform[] _transforms;
        private TransformSnapshot[] _bindPose;
        private TransformSnapshot[] _sampleRestorePose;
        private NativeArray<TransformStreamHandle> _transformHandles;
        private NativeArray<DazPoseTransformEndpoint> _sourcePoseBuffer;
        private NativeArray<DazPoseTransformEndpoint> _targetPoseBuffer;

        public AnimationClip CurrentPose => _currentPose;
        public AnimationClip TargetPose => _targetPose;
        public AnimationClip PendingPose => _hasPendingCommand ? _pendingCommand.Pose : null;
        public bool HasPendingPose => _hasPendingCommand;
        public bool IsBlending => _isBlending;
        public float BlendProgress => blendProgressDebug;
        public float TrajectoryProgress => trajectoryProgressDebug;
        public DazPoseTransitionOptions ActiveTransitionOptions => _activeOptions;
        public DazPoseTransitionOptions PendingTransitionOptions => _hasPendingCommand ? _pendingCommand.Options : default;
        public bool MixerWeightsStayedInRange => _mixerWeightsStayedInRange;
        public int GraphPlayableCount => _graph.IsValid() ? _graph.GetPlayableCount() : 0;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            CreateGraph();
        }

        public void SetPose(AnimationClip pose, float blendSeconds)
        {
            if (float.IsNaN(blendSeconds) || float.IsInfinity(blendSeconds))
                throw new ArgumentOutOfRangeException(nameof(blendSeconds), "Blend duration must be a finite number of seconds.");

            var options = new DazPoseTransitionOptions(Mathf.Max(0f, blendSeconds),
                DazPoseBlendEase.SmoothStep, 0f, 0f);
            SetPose(pose, options);
        }

        public void SetPose(AnimationClip pose, DazPoseTransitionOptions options)
        {
            if (pose == null) throw new ArgumentNullException(nameof(pose));
            if (!_graph.IsValid()) throw new InvalidOperationException("DazPoseBlendPlayer is not active in Play Mode.");

            var command = new PoseCommand(pose, options);
            if (_isBlending)
            {
                if (pose == _targetPose) return;

                var replacingPending = _hasPendingCommand;
                _pendingCommand = command;
                _hasPendingCommand = true;
                RefreshDebugState();
                Debug.Log((replacingPending ? "Pose command replaced in queue: " : "Pose command queued: ")
                    + FormatCommand(command) + ". It will start after " + _targetPose.name + " finishes.", this);
                return;
            }

            if (pose == _currentPose) return;
            if (_currentInput < 0)
            {
                var initialPlayable = CreateStaticPosePlayable(pose);
                ConnectPose(initialPlayable, 0);
                _currentInput = 0;
                _currentPose = pose;
                _mixer.SetInputWeight(_currentInput, 1f);
                _mixer.SetInputWeight(1, 0f);
                RecordMixerWeights();
                SetTrajectoryProgress(1f);
                RefreshDebugState();
                Debug.Log("Initial pose set to '" + pose.name + "'; transition options are ignored until a source pose exists.", this);
                return;
            }

            BeginTransition(command);
        }

        private void Update()
        {
            if (!_isBlending || !_graph.IsValid()) return;

            _blendElapsedSeconds += Mathf.Max(0f, Time.deltaTime);
            var linearProgress = _activeOptions.DurationSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(_blendElapsedSeconds / _activeOptions.DurationSeconds);
            var trajectoryProgress = DazPoseTransitionTrajectory.Evaluate(linearProgress, _activeOptions);
            SetMixerProgress(trajectoryProgress);
            SetTrajectoryProgress(trajectoryProgress);
            blendProgressDebug = linearProgress;

            if (linearProgress >= 1f) CompleteTransition();
        }

        private void BeginTransition(PoseCommand command)
        {
            if (command.Options.DurationSeconds > 0f
                && (command.Options.WindupFraction > 0f || command.Options.OvershootFraction > 0f))
                CaptureTransitionEndpoints(_currentPose, command.Pose);

            _targetInput = 1 - _currentInput;
            DisconnectInput(_targetInput);
            var targetPlayable = CreateStaticPosePlayable(command.Pose);
            ConnectPose(targetPlayable, _targetInput);
            _targetPose = command.Pose;
            _activeOptions = command.Options;
            _blendElapsedSeconds = 0f;
            _isBlending = command.Options.DurationSeconds > 0f;
            blendProgressDebug = 0f;
            SetMixerProgress(0f);
            SetTrajectoryProgress(0f);
            RefreshDebugState();

            if (command.Options.DurationSeconds <= 0f)
            {
                Debug.Log("Pose snap: " + _currentPose.name + " → " + command.Pose.name
                    + " | zero duration; windup and overshoot ignored.", this);
            }
            else
            {
                Debug.Log("Pose transition:\n" + _currentPose.name + " → " + command.Pose.name
                    + "\nduration: " + command.Options.DurationSeconds.ToString("F2", CultureInfo.InvariantCulture) + " sec"
                    + "\nease: " + command.Options.Ease
                    + "\nwindup: " + command.Options.WindupFraction.ToString("F2", CultureInfo.InvariantCulture)
                    + "\novershoot: " + command.Options.OvershootFraction.ToString("F2", CultureInfo.InvariantCulture) + ".", this);
            }

            if (!_isBlending) CompleteTransition();
        }

        private void CompleteTransition()
        {
            if (_targetPose == null) return;

            SetMixerProgress(1f);
            SetTrajectoryProgress(1f);
            var obsoleteInput = _currentInput;
            DisconnectInput(obsoleteInput);
            _currentInput = _targetInput;
            _currentPose = _targetPose;
            _targetInput = -1;
            _targetPose = null;
            _activeOptions = default;
            _isBlending = false;
            _blendElapsedSeconds = 0f;
            blendProgressDebug = 1f;
            trajectoryProgressDebug = 1f;

            var queuedCommand = _pendingCommand;
            var hadQueuedCommand = _hasPendingCommand;
            ClearPendingCommand();
            RefreshDebugState();

            if (hadQueuedCommand && queuedCommand.Pose != _currentPose)
                BeginTransition(queuedCommand);
        }

        private void CaptureTransitionEndpoints(AnimationClip source, AnimationClip target)
        {
            if (source == null || target == null)
                throw new InvalidOperationException("Windup and overshoot require both source and target pose clips.");

            CaptureSnapshot(_sampleRestorePose);
            try
            {
                RestoreSnapshot(_bindPose);
                source.SampleAnimation(gameObject, PoseSampleTimeSeconds);
                CopyCurrentPoseTo(_sourcePoseBuffer);

                RestoreSnapshot(_bindPose);
                target.SampleAnimation(gameObject, PoseSampleTimeSeconds);
                CopyCurrentPoseTo(_targetPoseBuffer);
            }
            finally
            {
                RestoreSnapshot(_sampleRestorePose);
            }
        }

        private void CopyCurrentPoseTo(NativeArray<DazPoseTransformEndpoint> destination)
        {
            for (var index = 0; index < _transforms.Length; index++)
            {
                var item = _transforms[index];
                destination[index] = item == null
                    ? default
                    : new DazPoseTransformEndpoint
                    {
                        LocalPosition = item.localPosition,
                        LocalRotation = item.localRotation
                    };
            }
        }

        private AnimationClipPlayable CreateStaticPosePlayable(AnimationClip pose)
        {
            var playable = AnimationClipPlayable.Create(_graph, pose);
            try
            {
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                playable.SetTime(PoseSampleTimeSeconds);
                playable.SetSpeed(0d);
                return playable;
            }
            catch
            {
                if (playable.IsValid()) playable.Destroy();
                throw;
            }
        }

        private void ConnectPose(AnimationClipPlayable playable, int input)
        {
            if (!_graph.Connect(playable, 0, _mixer, input))
            {
                if (playable.IsValid()) playable.Destroy();
                throw new InvalidOperationException("Could not connect the static pose playable to mixer input " + input + ".");
            }
            _clipPlayables[input] = playable;
        }

        private void DisconnectInput(int input)
        {
            if (input < 0 || _clipPlayables == null || input >= _clipPlayables.Length) return;
            if (_graph.IsValid() && _mixer.IsValid()) _graph.Disconnect(_mixer, input);
            if (_clipPlayables[input].IsValid()) _clipPlayables[input].Destroy();
            _clipPlayables[input] = default;
            if (_mixer.IsValid()) _mixer.SetInputWeight(input, 0f);
        }

        private void CreateGraph()
        {
            DestroyGraph();
            _animator = GetComponent<Animator>();
            if (_animator == null) throw new InvalidOperationException("DazPoseBlendPlayer requires an Animator on the Genesis8Female animation root.");

            EnsureBindPoseSnapshot();
            RestoreSnapshot(_bindPose);
            _graph = PlayableGraph.Create("DAZ Pose Blend Player - " + name);
            try
            {
                _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                _mixer = AnimationMixerPlayable.Create(_graph, 2);
                _clipPlayables = new AnimationClipPlayable[2];

                _transformHandles = new NativeArray<TransformStreamHandle>(_transforms.Length, Allocator.Persistent);
                _sourcePoseBuffer = new NativeArray<DazPoseTransformEndpoint>(_transforms.Length, Allocator.Persistent);
                _targetPoseBuffer = new NativeArray<DazPoseTransformEndpoint>(_transforms.Length, Allocator.Persistent);
                for (var index = 0; index < _transforms.Length; index++)
                    _transformHandles[index] = _animator.BindStreamTransform(_transforms[index]);

                var job = new DazPoseTransitionExtrapolationJob
                {
                    TransformHandles = _transformHandles,
                    SourcePose = _sourcePoseBuffer,
                    TargetPose = _targetPoseBuffer,
                    Progress = 0f
                };
                _extrapolationPlayable = AnimationScriptPlayable.Create(_graph, job, 1);
                _extrapolationPlayable.SetProcessInputs(true);
                if (!_graph.Connect(_mixer, 0, _extrapolationPlayable, 0))
                    throw new InvalidOperationException("Could not connect the pose mixer to its transition extrapolation job.");

                var output = AnimationPlayableOutput.Create(_graph, "DAZ Pose Blend Output", _animator);
                output.SetSourcePlayable(_extrapolationPlayable);
                _mixer.SetInputWeight(0, 0f);
                _mixer.SetInputWeight(1, 0f);
                _graph.Play();
                _currentInput = -1;
                _targetInput = -1;
                _currentPose = null;
                _targetPose = null;
                _activeOptions = default;
                _isBlending = false;
                _mixerWeightsStayedInRange = true;
                blendProgressDebug = 0f;
                trajectoryProgressDebug = 0f;
                ClearPendingCommand();
                RefreshDebugState();
                RecordMixerWeights();
            }
            catch
            {
                DestroyGraph();
                throw;
            }
        }

        private void EnsureBindPoseSnapshot()
        {
            if (_transforms != null && _bindPose != null && _transforms.Length == _bindPose.Length) return;

            _transforms = GetComponentsInChildren<Transform>(true);
            _bindPose = new TransformSnapshot[_transforms.Length];
            _sampleRestorePose = new TransformSnapshot[_transforms.Length];
            CaptureSnapshot(_bindPose);
        }

        private void CaptureSnapshot(TransformSnapshot[] destination)
        {
            for (var index = 0; index < _transforms.Length; index++)
            {
                var item = _transforms[index];
                destination[index] = item == null
                    ? default
                    : new TransformSnapshot(item, item.localPosition, item.localRotation, item.localScale);
            }
        }

        private static void RestoreSnapshot(TransformSnapshot[] snapshot)
        {
            if (snapshot == null) return;
            foreach (var item in snapshot)
            {
                if (item.Transform == null) continue;
                item.Transform.localPosition = item.LocalPosition;
                item.Transform.localRotation = item.LocalRotation;
                item.Transform.localScale = item.LocalScale;
            }
        }

        private void SetMixerProgress(float progress)
        {
            if (!_mixer.IsValid()) return;
            var targetWeight = Mathf.Clamp01(progress);
            var sourceWeight = 1f - targetWeight;
            if (_currentInput >= 0) _mixer.SetInputWeight(_currentInput, sourceWeight);
            if (_targetInput >= 0) _mixer.SetInputWeight(_targetInput, targetWeight);
            RecordMixerWeights();
        }

        private void RecordMixerWeights()
        {
            if (!_mixer.IsValid()) return;
            for (var input = 0; input < 2; input++)
            {
                var weight = _mixer.GetInputWeight(input);
                if (float.IsNaN(weight) || float.IsInfinity(weight) || weight < 0f || weight > 1f)
                    _mixerWeightsStayedInRange = false;
            }
        }

        private void SetTrajectoryProgress(float progress)
        {
            trajectoryProgressDebug = progress;
            if (!_extrapolationPlayable.IsValid()) return;
            var job = _extrapolationPlayable.GetJobData<DazPoseTransitionExtrapolationJob>();
            job.Progress = progress;
            _extrapolationPlayable.SetJobData(job);
        }

        private void ClearPendingCommand()
        {
            _pendingCommand = default;
            _hasPendingCommand = false;
        }

        private void RefreshDebugState()
        {
            currentPoseDebug = _currentPose;
            targetPoseDebug = _targetPose;
            pendingPoseDebug = _hasPendingCommand ? _pendingCommand.Pose : null;

            activeDurationDebug = _isBlending ? _activeOptions.DurationSeconds : 0f;
            activeEaseDebug = _activeOptions.Ease;
            activeWindupDebug = _isBlending ? _activeOptions.WindupFraction : 0f;
            activeOvershootDebug = _isBlending ? _activeOptions.OvershootFraction : 0f;

            pendingDurationDebug = _hasPendingCommand ? _pendingCommand.Options.DurationSeconds : 0f;
            pendingEaseDebug = _hasPendingCommand ? _pendingCommand.Options.Ease : default;
            pendingWindupDebug = _hasPendingCommand ? _pendingCommand.Options.WindupFraction : 0f;
            pendingOvershootDebug = _hasPendingCommand ? _pendingCommand.Options.OvershootFraction : 0f;
        }

        private static string FormatCommand(PoseCommand command)
        {
            var options = command.Options;
            return "'" + command.Pose.name + "' (" + options.DurationSeconds.ToString("F2", CultureInfo.InvariantCulture)
                + " sec, " + options.Ease
                + ", windup " + options.WindupFraction.ToString("P0", CultureInfo.InvariantCulture)
                + ", overshoot " + options.OvershootFraction.ToString("P0", CultureInfo.InvariantCulture) + ")";
        }

        private void OnDisable() => DestroyGraph();
        private void OnDestroy() => DestroyGraph();

        private void DestroyGraph()
        {
            if (_graph.IsValid()) _graph.Destroy();
            DisposeNativeBuffers();

            _graph = default;
            _mixer = default;
            _extrapolationPlayable = default;
            _clipPlayables = null;
            _animator = null;
            _currentInput = -1;
            _targetInput = -1;
            _currentPose = null;
            _targetPose = null;
            _activeOptions = default;
            _isBlending = false;
            _blendElapsedSeconds = 0f;
            blendProgressDebug = 0f;
            trajectoryProgressDebug = 0f;
            _mixerWeightsStayedInRange = true;
            ClearPendingCommand();
            RefreshDebugState();
        }

        private void DisposeNativeBuffers()
        {
            if (_transformHandles.IsCreated) _transformHandles.Dispose();
            if (_sourcePoseBuffer.IsCreated) _sourcePoseBuffer.Dispose();
            if (_targetPoseBuffer.IsCreated) _targetPoseBuffer.Dispose();
            _transformHandles = default;
            _sourcePoseBuffer = default;
            _targetPoseBuffer = default;
        }

        private readonly struct PoseCommand
        {
            public readonly AnimationClip Pose;
            public readonly DazPoseTransitionOptions Options;

            public PoseCommand(AnimationClip pose, DazPoseTransitionOptions options)
            {
                Pose = pose;
                Options = options;
            }
        }

        private readonly struct TransformSnapshot
        {
            public readonly Transform Transform;
            public readonly Vector3 LocalPosition;
            public readonly Quaternion LocalRotation;
            public readonly Vector3 LocalScale;

            public TransformSnapshot(Transform transform, Vector3 position, Quaternion rotation, Vector3 scale)
            {
                Transform = transform;
                LocalPosition = position;
                LocalRotation = rotation;
                LocalScale = scale;
            }
        }
    }
}
