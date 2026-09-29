using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer
{
    [Serializable]
    public struct BreathingBoneChannel
    {
        public string boneName;
        public Vector3 fullInhaleLocalRotationDelta;
        public Vector3 fullInhaleLocalPositionDelta;
    }

    internal struct PerformerBreathingSettings
    {
        public bool BreathingEnabled;
        public float BreathsPerMinute;
        public AnimationCurve BreathingCurve;
        public bool MorphBreathingEnabled;
        public float MorphBreathingStrength;
        public float BreatheStrength;
        public float BreatheBellyStrength;
        public bool BoneBreathingEnabled;
        public float BoneBreathingStrength;
    }

    internal struct PerformerBreathingMorphStreamBinding
    {
        public PropertyStreamHandle Handle;
        public float PositiveMaximumWeight;
        public int Channel;
    }

    internal struct PerformerBreathingBoneStreamBinding
    {
        public TransformStreamHandle Handle;
        public Vector3 FullInhaleLocalRotationDelta;
        public Vector3 FullInhaleLocalPositionDelta;
    }

    internal readonly struct PerformerBreathingMorphBindingInfo
    {
        public readonly string SemanticName;
        public readonly string ImportedBlendShapeName;
        public readonly SkinnedMeshRenderer Renderer;
        public readonly int BlendShapeIndex;
        public readonly int BaseBlendShapeIndex;
        public readonly float PositiveMaximumWeight;
        public readonly string RendererPath;

        public PerformerBreathingMorphBindingInfo(string semanticName, string importedBlendShapeName,
            SkinnedMeshRenderer renderer, int blendShapeIndex, int baseBlendShapeIndex,
            float positiveMaximumWeight, string rendererPath)
        {
            SemanticName = semanticName;
            ImportedBlendShapeName = importedBlendShapeName;
            Renderer = renderer;
            BlendShapeIndex = blendShapeIndex;
            BaseBlendShapeIndex = baseBlendShapeIndex;
            PositiveMaximumWeight = positiveMaximumWeight;
            RendererPath = rendererPath;
        }
    }

    internal readonly struct PerformerBreathingBoneBindingInfo
    {
        public readonly Transform Bone;
        public readonly int BaseTransformIndex;
        public readonly Vector3 FullInhaleLocalRotationDelta;
        public readonly Vector3 FullInhaleLocalPositionDelta;

        public PerformerBreathingBoneBindingInfo(Transform bone, int baseTransformIndex,
            Vector3 fullInhaleLocalRotationDelta, Vector3 fullInhaleLocalPositionDelta)
        {
            Bone = bone;
            BaseTransformIndex = baseTransformIndex;
            FullInhaleLocalRotationDelta = fullInhaleLocalRotationDelta;
            FullInhaleLocalPositionDelta = fullInhaleLocalPositionDelta;
        }
    }

    internal struct PerformerBreathingJob : IAnimationJob
    {
        [ReadOnly] public NativeArray<PerformerBreathingMorphStreamBinding> MorphBindings;
        [ReadOnly] public NativeArray<PerformerBreathingBoneStreamBinding> BoneBindings;
        public bool BreathingEnabled;
        public bool MorphBreathingEnabled;
        public bool BoneBreathingEnabled;
        public float BreathValue;
        public float MorphBreathingStrength;
        public float BreatheStrength;
        public float BreatheBellyStrength;
        public float BoneBreathingStrength;

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!stream.isValid || !BreathingEnabled || BreathValue <= 0f) return;

            if (MorphBreathingEnabled && MorphBreathingStrength > 0f)
            {
                for (var index = 0; index < MorphBindings.Length; index++)
                {
                    var binding = MorphBindings[index];
                    if (!binding.Handle.IsValid(stream)) continue;
                    var relativeStrength = binding.Channel == 0 ? BreatheStrength : BreatheBellyStrength;
                    var contribution = BreathValue * MorphBreathingStrength
                                      * Mathf.Max(0f, relativeStrength)
                                      * PerformerBreathing.MorphWeightAmplitude;
                    if (contribution <= 0f) continue;

                    var baseWeight = binding.Handle.GetFloat(stream);
                    var positiveRoom = Mathf.Max(0f, binding.PositiveMaximumWeight - baseWeight);
                    if (positiveRoom <= 0f) continue;
                    binding.Handle.SetFloat(stream, baseWeight + Mathf.Min(contribution, positiveRoom));
                }
            }

            if (!BoneBreathingEnabled || BoneBreathingStrength <= 0f) return;
            var inhale = BreathValue * BoneBreathingStrength;
            for (var index = 0; index < BoneBindings.Length; index++)
            {
                var binding = BoneBindings[index];
                var handle = binding.Handle;
                if (!handle.IsValid(stream)) continue;

                if (binding.FullInhaleLocalPositionDelta != Vector3.zero)
                {
                    var basePosition = handle.GetLocalPosition(stream);
                    handle.SetLocalPosition(stream, basePosition + binding.FullInhaleLocalPositionDelta * inhale);
                }

                if (binding.FullInhaleLocalRotationDelta != Vector3.zero)
                {
                    var baseRotation = handle.GetLocalRotation(stream);
                    handle.SetLocalRotation(stream,
                        baseRotation * Quaternion.Euler(binding.FullInhaleLocalRotationDelta * inhale));
                }
            }
        }
    }

    internal sealed class PerformerBreathing : IDisposable
    {
        // Unity blendshape weights are in the mesh's frame-weight units. This modest
        // full-strength amplitude leaves room for pose-authored values while allowing
        // the smoke harness to exaggerate the effect up to twice its default range.
        public const float MorphWeightAmplitude = 12f;

        private readonly Animator _animator;
        private readonly PlayableGraph _graph;
        private readonly List<PerformerBreathingMorphBindingInfo> _morphBindingInfo =
            new List<PerformerBreathingMorphBindingInfo>();
        private readonly List<PerformerBreathingBoneBindingInfo> _boneBindingInfo =
            new List<PerformerBreathingBoneBindingInfo>();
        private readonly List<string> _diagnostics = new List<string>();
        private NativeArray<PerformerBreathingMorphStreamBinding> _morphBindings;
        private NativeArray<PerformerBreathingBoneStreamBinding> _boneBindings;
        private AnimationScriptPlayable _breathingPlayable;
        private PerformerBreathingSettings _settings;
        private float _phase;
        private float _breathValue;
        private bool _disposed;

        public Playable OutputPlayable => _breathingPlayable;
        public float BreathPhase => _phase;
        public float BreathValue => _breathValue;
        public IReadOnlyList<PerformerBreathingMorphBindingInfo> MorphBindings => _morphBindingInfo;
        public IReadOnlyList<PerformerBreathingBoneBindingInfo> BoneBindings => _boneBindingInfo;
        public IReadOnlyList<string> Diagnostics => _diagnostics;
        public int BreatheBindingCount => _morphBindingInfo.Count(binding => binding.SemanticName == "Breathe");
        public int BreatheBellyBindingCount => _morphBindingInfo.Count(binding => binding.SemanticName == "BreatheBelly");

        public PerformerBreathing(Animator animator, PlayableGraph graph, Playable basePoseOutput,
            PerformerBodyPose bodyPose, IReadOnlyList<BreathingBoneChannel> boneChannels, float initialPhase)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            if (!graph.IsValid()) throw new ArgumentException("A valid PlayableGraph is required.", nameof(graph));
            if (!basePoseOutput.IsValid()) throw new ArgumentException("A valid base-pose playable is required.", nameof(basePoseOutput));
            if (bodyPose == null) throw new ArgumentNullException(nameof(bodyPose));

            _animator = animator;
            _graph = graph;
            _phase = Mathf.Repeat(initialPhase, 1f);

            try
            {
                var morphStreamBindings = ResolveMorphBindings(animator, bodyPose);
                var boneStreamBindings = ResolveBoneBindings(animator, bodyPose, boneChannels);
                _morphBindings = new NativeArray<PerformerBreathingMorphStreamBinding>(
                    morphStreamBindings.ToArray(), Allocator.Persistent);
                _boneBindings = new NativeArray<PerformerBreathingBoneStreamBinding>(
                    boneStreamBindings.ToArray(), Allocator.Persistent);

                var job = new PerformerBreathingJob
                {
                    MorphBindings = _morphBindings,
                    BoneBindings = _boneBindings
                };
                _breathingPlayable = AnimationScriptPlayable.Create(_graph, job, 1);
                _breathingPlayable.SetProcessInputs(true);
                _breathingPlayable.SetInputWeight(0, 1f);
                if (!_graph.Connect(basePoseOutput, 0, _breathingPlayable, 0))
                    throw new InvalidOperationException("Could not connect the performer breathing layer.");
                UpdateJobData();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Configure(PerformerBreathingSettings settings)
        {
            _settings = settings;
        }

        public void Advance(float deltaTime)
        {
            if (_disposed) return;
            if (_settings.BreathingEnabled)
            {
                var breathsPerMinute = Mathf.Max(0f, _settings.BreathsPerMinute);
                _phase = Mathf.Repeat(_phase + Mathf.Max(0f, deltaTime) * breathsPerMinute / 60f, 1f);
            }

            var curve = _settings.BreathingCurve;
            _breathValue = curve == null ? 0f : Mathf.Clamp01(curve.Evaluate(_phase));
            UpdateJobData();
        }

        internal void SetPhaseForAcceptance(float phase)
        {
            if (_disposed) return;
            _phase = Mathf.Repeat(phase, 1f);
            var curve = _settings.BreathingCurve;
            _breathValue = curve == null ? 0f : Mathf.Clamp01(curve.Evaluate(_phase));
            UpdateJobData();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_breathingPlayable.IsValid()) _breathingPlayable.Destroy();
            if (_morphBindings.IsCreated) _morphBindings.Dispose();
            if (_boneBindings.IsCreated) _boneBindings.Dispose();
            _morphBindings = default;
            _boneBindings = default;
            _breathingPlayable = default;
        }

        private List<PerformerBreathingMorphStreamBinding> ResolveMorphBindings(
            Animator animator, PerformerBodyPose bodyPose)
        {
            var results = new List<PerformerBreathingMorphStreamBinding>();
            var renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            AddMorphChannel("Breathe", 0, renderers, bodyPose, results);
            AddMorphChannel("BreatheBelly", 1, renderers, bodyPose, results);
            return results;
        }

        private void AddMorphChannel(string semanticName, int channel,
            IReadOnlyList<SkinnedMeshRenderer> renderers, PerformerBodyPose bodyPose,
            ICollection<PerformerBreathingMorphStreamBinding> results)
        {
            var matches = 0;
            foreach (var renderer in renderers)
            {
                var mesh = renderer == null ? null : renderer.sharedMesh;
                if (mesh == null) continue;

                var shapeIndex = FindImportedBlendShape(mesh, semanticName, out var importedName);
                if (shapeIndex < 0) continue;
                var frameCount = mesh.GetBlendShapeFrameCount(shapeIndex);
                var positiveMaximumWeight = 0f;
                for (var frame = 0; frame < frameCount; frame++)
                    positiveMaximumWeight = Mathf.Max(positiveMaximumWeight,
                        mesh.GetBlendShapeFrameWeight(shapeIndex, frame));
                if (positiveMaximumWeight <= 0f)
                {
                    AddDiagnostic("Morph '" + semanticName + "' on renderer '" + renderer.name
                                  + "' has no positive frame weight; that binding is disabled.");
                    continue;
                }

                var baseBlendShapeIndex = bodyPose.GetBlendShapeStreamIndex(renderer, shapeIndex);
                if (baseBlendShapeIndex < 0)
                {
                    AddDiagnostic("Morph '" + semanticName + "' on renderer '" + renderer.name
                                  + "' could not be mapped to the base-pose stream; that binding is disabled.");
                    continue;
                }

                var propertyName = "blendShape." + importedName;
                var handle = _animator.BindStreamProperty(renderer.transform,
                    typeof(SkinnedMeshRenderer), propertyName);
                results.Add(new PerformerBreathingMorphStreamBinding
                {
                    Handle = handle,
                    PositiveMaximumWeight = positiveMaximumWeight,
                    Channel = channel
                });
                var path = HierarchyPath(_animator.transform, renderer.transform);
                _morphBindingInfo.Add(new PerformerBreathingMorphBindingInfo(semanticName, importedName,
                    renderer, shapeIndex, baseBlendShapeIndex, positiveMaximumWeight, path));
                matches++;
            }

            if (matches == 0)
            {
                AddDiagnostic("Morph '" + semanticName + "' was not found on any SkinnedMeshRenderer under '"
                              + _animator.name + "'. Only this breathing morph channel is disabled.");
            }
        }

        private List<PerformerBreathingBoneStreamBinding> ResolveBoneBindings(
            Animator animator, PerformerBodyPose bodyPose, IReadOnlyList<BreathingBoneChannel> channels)
        {
            var results = new List<PerformerBreathingBoneStreamBinding>();
            if (channels == null) return results;
            var transforms = animator.GetComponentsInChildren<Transform>(true);
            foreach (var channel in channels)
            {
                if (string.IsNullOrWhiteSpace(channel.boneName)) continue;
                var matches = transforms.Where(transform => string.Equals(
                    transform.name, channel.boneName, StringComparison.Ordinal)).ToArray();
                if (matches.Length != 1)
                {
                    AddDiagnostic("Breathing bone '" + channel.boneName + "' resolved to " + matches.Length
                                  + " transforms under '" + animator.name + "'; that bone channel is disabled.");
                    continue;
                }

                var transformIndex = bodyPose.GetTransformStreamIndex(matches[0]);
                if (transformIndex < 0)
                {
                    AddDiagnostic("Breathing bone '" + channel.boneName
                                  + "' could not be mapped to the base-pose stream; that bone channel is disabled.");
                    continue;
                }

                results.Add(new PerformerBreathingBoneStreamBinding
                {
                    Handle = animator.BindStreamTransform(matches[0]),
                    FullInhaleLocalRotationDelta = channel.fullInhaleLocalRotationDelta,
                    FullInhaleLocalPositionDelta = channel.fullInhaleLocalPositionDelta
                });
                _boneBindingInfo.Add(new PerformerBreathingBoneBindingInfo(matches[0], transformIndex,
                    channel.fullInhaleLocalRotationDelta, channel.fullInhaleLocalPositionDelta));
            }
            return results;
        }

        private void AddDiagnostic(string message)
        {
            _diagnostics.Add(message);
            Debug.LogWarning(message, _animator);
        }

        private void UpdateJobData()
        {
            if (!_breathingPlayable.IsValid()) return;
            var job = _breathingPlayable.GetJobData<PerformerBreathingJob>();
            job.BreathingEnabled = _settings.BreathingEnabled;
            job.MorphBreathingEnabled = _settings.MorphBreathingEnabled;
            job.BoneBreathingEnabled = _settings.BoneBreathingEnabled;
            job.BreathValue = _breathValue;
            job.MorphBreathingStrength = Mathf.Max(0f, _settings.MorphBreathingStrength);
            job.BreatheStrength = Mathf.Max(0f, _settings.BreatheStrength);
            job.BreatheBellyStrength = Mathf.Max(0f, _settings.BreatheBellyStrength);
            job.BoneBreathingStrength = Mathf.Max(0f, _settings.BoneBreathingStrength);
            _breathingPlayable.SetJobData(job);
        }

        private static int FindImportedBlendShape(Mesh mesh, string semanticName, out string importedName)
        {
            var candidates = new[]
            {
                semanticName,
                "EX_" + semanticName,
                "Genesis8Female__EX_" + semanticName
            };
            foreach (var candidate in candidates)
            {
                for (var index = 0; index < mesh.blendShapeCount; index++)
                {
                    if (!string.Equals(mesh.GetBlendShapeName(index), candidate, StringComparison.Ordinal)) continue;
                    importedName = candidate;
                    return index;
                }
            }
            importedName = string.Empty;
            return -1;
        }

        private static string HierarchyPath(Transform root, Transform target)
        {
            var segments = new Stack<string>();
            var current = target;
            while (current != null && current != root)
            {
                segments.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", segments);
        }
    }
}
