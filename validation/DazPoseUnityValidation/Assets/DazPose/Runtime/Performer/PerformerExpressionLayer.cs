using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer
{
    internal enum PerformerExpressionPropertyKind
    {
        BlendShape,
        LocalPosition,
        LocalRotation
    }

    internal struct PerformerExpressionContribution
    {
        public float Scalar;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Weight;
    }

    internal struct PerformerExpressionStreamBinding
    {
        public PerformerExpressionPropertyKind Kind;
        public PropertyStreamHandle PropertyHandle;
        public TransformStreamHandle TransformHandle;
        public int SourceStart;
        public int SourceCount;
        public int TargetStart;
        public int TargetCount;
    }

    internal struct PerformerExpressionJob : IAnimationJob
    {
        [ReadOnly] public NativeArray<PerformerExpressionStreamBinding> Bindings;
        [ReadOnly] public NativeArray<PerformerExpressionContribution> SourceContributions;
        [ReadOnly] public NativeArray<PerformerExpressionContribution> TargetContributions;
        public float Progress;
        public bool Enabled;

        public void ProcessRootMotion(AnimationStream stream) { }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!stream.isValid || !Enabled || !Bindings.IsCreated) return;
            var progress = Mathf.Clamp01(Progress);
            for (var index = 0; index < Bindings.Length; index++)
            {
                var binding = Bindings[index];
                if (binding.Kind == PerformerExpressionPropertyKind.BlendShape)
                {
                    if (!binding.PropertyHandle.IsValid(stream)) continue;
                    var incoming = binding.PropertyHandle.GetFloat(stream);
                    var value = incoming;
                    var totalWeight = 0f;
                    AccumulateScalar(SourceContributions, binding.SourceStart, binding.SourceCount,
                        1f - progress, ref value, ref totalWeight);
                    AccumulateScalar(TargetContributions, binding.TargetStart, binding.TargetCount,
                        progress, ref value, ref totalWeight);
                    binding.PropertyHandle.SetFloat(stream, incoming * (1f - Mathf.Clamp01(totalWeight))
                        + (value - incoming));
                    continue;
                }

                if (!binding.TransformHandle.IsValid(stream)) continue;
                if (binding.Kind == PerformerExpressionPropertyKind.LocalPosition)
                {
                    var incoming = binding.TransformHandle.GetLocalPosition(stream);
                    var weightedTargets = Vector3.zero;
                    var totalWeight = 0f;
                    AccumulatePosition(SourceContributions, binding.SourceStart, binding.SourceCount,
                        1f - progress, ref weightedTargets, ref totalWeight);
                    AccumulatePosition(TargetContributions, binding.TargetStart, binding.TargetCount,
                        progress, ref weightedTargets, ref totalWeight);
                    var weight = Mathf.Clamp01(totalWeight);
                    binding.TransformHandle.SetLocalPosition(stream, incoming * (1f - weight) + weightedTargets);
                    continue;
                }

                var incomingRotation = binding.TransformHandle.GetLocalRotation(stream);
                var quaternionSum = Vector4.zero;
                var rotationWeight = 0f;
                AccumulateRotation(SourceContributions, binding.SourceStart, binding.SourceCount,
                    1f - progress, incomingRotation, ref quaternionSum, ref rotationWeight);
                AccumulateRotation(TargetContributions, binding.TargetStart, binding.TargetCount,
                    progress, incomingRotation, ref quaternionSum, ref rotationWeight);
                var remainingWeight = 1f - Mathf.Clamp01(rotationWeight);
                quaternionSum.x += incomingRotation.x * remainingWeight;
                quaternionSum.y += incomingRotation.y * remainingWeight;
                quaternionSum.z += incomingRotation.z * remainingWeight;
                quaternionSum.w += incomingRotation.w * remainingWeight;
                binding.TransformHandle.SetLocalRotation(stream, Normalize(quaternionSum, incomingRotation));
            }
        }

        private static void AccumulateScalar(NativeArray<PerformerExpressionContribution> values,
            int start, int count, float sideWeight, ref float weightedTargets, ref float totalWeight)
        {
            for (var index = 0; index < count; index++)
            {
                var value = values[start + index];
                var weight = value.Weight * sideWeight;
                weightedTargets += value.Scalar * weight;
                totalWeight += weight;
            }
        }

        private static void AccumulatePosition(NativeArray<PerformerExpressionContribution> values,
            int start, int count, float sideWeight, ref Vector3 weightedTargets, ref float totalWeight)
        {
            for (var index = 0; index < count; index++)
            {
                var value = values[start + index];
                var weight = value.Weight * sideWeight;
                weightedTargets += value.Position * weight;
                totalWeight += weight;
            }
        }

        private static void AccumulateRotation(NativeArray<PerformerExpressionContribution> values,
            int start, int count, float sideWeight, Quaternion incoming,
            ref Vector4 quaternionSum, ref float totalWeight)
        {
            for (var index = 0; index < count; index++)
            {
                var value = values[start + index];
                var weight = value.Weight * sideWeight;
                var target = value.Rotation;
                if (Dot(incoming, target) < 0f)
                {
                    target.x = -target.x; target.y = -target.y;
                    target.z = -target.z; target.w = -target.w;
                }
                quaternionSum.x += target.x * weight;
                quaternionSum.y += target.y * weight;
                quaternionSum.z += target.z * weight;
                quaternionSum.w += target.w * weight;
                totalWeight += weight;
            }
        }

        private static float Dot(Quaternion left, Quaternion right)
            => left.x * right.x + left.y * right.y + left.z * right.z + left.w * right.w;

        private static Quaternion Normalize(Vector4 value, Quaternion fallback)
        {
            var lengthSquared = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
            if (lengthSquared <= 1e-12f || float.IsNaN(lengthSquared) || float.IsInfinity(lengthSquared)) return fallback;
            var inverseLength = 1f / Mathf.Sqrt(lengthSquared);
            return new Quaternion(value.x * inverseLength, value.y * inverseLength,
                value.z * inverseLength, value.w * inverseLength);
        }
    }

    internal sealed class PerformerExpressionLayer : IDisposable
    {
        private const float Epsilon = 0.00001f;
        private const float QuaternionEpsilon = 1e-8f;
        private readonly Animator _animator;
        private readonly AnimationScriptPlayable _playable;
        private Dictionary<string, List<Contribution>> _source = new Dictionary<string, List<Contribution>>(StringComparer.Ordinal);
        private Dictionary<string, List<Contribution>> _target = new Dictionary<string, List<Contribution>>(StringComparer.Ordinal);
        private NativeArray<PerformerExpressionStreamBinding> _bindings;
        private NativeArray<PerformerExpressionContribution> _sourceContributions;
        private NativeArray<PerformerExpressionContribution> _targetContributions;
        private float _elapsed;
        private float _duration;
        private bool _disposed;
        private bool _bypassedForAcceptance;

        public PerformerExpressionLayer(Animator animator, PlayableGraph graph, Playable input)
        {
            _animator = animator ?? throw new ArgumentNullException(nameof(animator));
            _playable = AnimationScriptPlayable.Create(graph, new PerformerExpressionJob(), 1);
            _playable.SetProcessInputs(true);
            _playable.SetInputWeight(0, 1f);
            if (!graph.Connect(input, 0, _playable, 0)) throw new InvalidOperationException("Could not connect the performer Expression layer.");
        }

        public Playable OutputPlayable => _playable;
        internal bool HasNativeAllocations
            => _bindings.IsCreated || _sourceContributions.IsCreated || _targetContributions.IsCreated;
        public bool IsTransitioning => _duration > Epsilon && _elapsed < _duration;
        public float Progress => _duration <= Epsilon ? 1f : Mathf.Clamp01(_elapsed / _duration);
        public float EvaluatedProgress => Smooth(Progress);

        internal void SetBypassedForAcceptance(bool bypassed)
        {
            ThrowIfDisposed();
            _bypassedForAcceptance = bypassed;
            UpdateJob();
        }

        public void SetExpression(PerformerExpression expression, float intensity, float blendTime)
        {
            ThrowIfDisposed();
            if (expression == null) throw new ArgumentNullException(nameof(expression));
            if (expression.Clip == null) throw new InvalidOperationException("PerformerExpression has no AnimationClip.");
            if ((expression.Channels == null || expression.Channels.Length == 0)
                && (expression.BoneChannels == null || expression.BoneChannels.Length == 0))
                throw new InvalidOperationException("PerformerExpression has no blendshape or facial-bone channel metadata.");
            if (!IsFinite(intensity) || !IsFinite(blendTime))
                throw new ArgumentOutOfRangeException(nameof(intensity), "Expression intensity and blend time must be finite values.");
            var next = Resolve(expression, Mathf.Clamp01(intensity));
            Begin(next, blendTime);
        }

        public void Clear(float blendTime)
        {
            ThrowIfDisposed();
            if (!IsFinite(blendTime)) throw new ArgumentOutOfRangeException(nameof(blendTime), "Expression blend time must be finite.");
            Begin(new Dictionary<string, List<Contribution>>(StringComparer.Ordinal), blendTime);
        }

        public void Advance(float deltaTime)
        {
            if (_disposed) return;
            var wasTransitioning = IsTransitioning;
            _elapsed = Mathf.Min(_duration, _elapsed + Mathf.Max(0f, deltaTime));
            if (wasTransitioning && !IsTransitioning) Settle();
            else UpdateJob();
        }

        private void Begin(Dictionary<string, List<Contribution>> next, float blendTime)
        {
            ThrowIfDisposed();
            if (!IsFinite(blendTime)) throw new ArgumentOutOfRangeException(nameof(blendTime), "Expression blend time must be finite.");
            var source = Collapse();
            var duration = Mathf.Max(0f, blendTime);
            _source = source;
            _target = next;
            _elapsed = 0f;
            _duration = duration;
            BuildBindings();
            if (_duration <= Epsilon) Settle();
            else UpdateJob();
        }

        private void Settle()
        {
            _source = Clone(_target);
            _target = Clone(_target);
            _duration = 0f;
            _elapsed = 0f;
            BuildBindings();
            UpdateJob();
        }

        private Dictionary<string, List<Contribution>> Collapse()
        {
            if (_duration <= Epsilon) return Clone(_target);
            var progress = EvaluatedProgress;
            var result = new Dictionary<string, List<Contribution>>(StringComparer.Ordinal);
            foreach (var key in _source.Keys.Concat(_target.Keys).Distinct(StringComparer.Ordinal))
            {
                if (_source.TryGetValue(key, out var source))
                    AddScaled(result, key, source, 1f - progress);
                if (_target.TryGetValue(key, out var target))
                    AddScaled(result, key, target, progress);
            }
            return result;
        }

        private static void AddScaled(Dictionary<string, List<Contribution>> destination,
            string key, List<Contribution> contributions, float scale)
        {
            if (scale <= Epsilon) return;
            foreach (var original in contributions)
            {
                var weight = original.Weight * scale;
                if (weight <= Epsilon) continue;
                if (!destination.TryGetValue(key, out var values))
                    destination.Add(key, values = new List<Contribution>());
                var existing = values.FindIndex(value => value.SameTarget(original));
                if (existing >= 0)
                {
                    var merged = values[existing];
                    merged.Weight = Mathf.Min(1f, merged.Weight + weight);
                    values[existing] = merged;
                }
                else
                {
                    var contribution = original;
                    contribution.Weight = weight;
                    values.Add(contribution);
                }
            }
        }

        private Dictionary<string, List<Contribution>> Resolve(PerformerExpression expression, float intensity)
        {
            var result = new Dictionary<string, List<Contribution>>(StringComparer.Ordinal);
            var morphKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var channel in expression.Channels ?? Array.Empty<PerformerExpressionChannel>())
            {
                if (string.IsNullOrWhiteSpace(channel.BlendShapeName) || !IsFinite(channel.TargetWeight))
                    throw new InvalidOperationException("PerformerExpression contains an empty blendshape name or non-finite target weight.");
                if (channel.RendererPath == null) throw new InvalidOperationException("PerformerExpression contains a null renderer path.");
                var key = "M|" + channel.RendererPath + "|" + channel.BlendShapeName;
                if (IsRuntimeOwnedChannel(channel.BlendShapeName)
                    || PerformerLipSyncMorphCatalog.IsOwnedBinding(channel.RendererPath, channel.BlendShapeName))
                    throw new InvalidOperationException("PerformerExpression channel '" + key
                        + "' is owned by autonomous breathing, blink, or speech and cannot be driven by an Expression.");
                if (!morphKeys.Add(key)) throw new InvalidOperationException("PerformerExpression contains duplicate channel '" + key + "'.");
                var matches = _animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(renderer =>
                    string.Equals(HierarchyPath(_animator.transform, renderer.transform), channel.RendererPath, StringComparison.Ordinal)
                    && renderer.sharedMesh != null
                    && Enumerable.Range(0, renderer.sharedMesh.blendShapeCount).Count(index =>
                        string.Equals(renderer.sharedMesh.GetBlendShapeName(index), channel.BlendShapeName, StringComparison.Ordinal)) == 1).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("PerformerExpression channel '" + key + "' does not uniquely resolve under the performer Animator.");
                AddResolved(result, key, new Contribution
                {
                    Kind = PerformerExpressionPropertyKind.BlendShape, Path = channel.RendererPath,
                    Name = channel.BlendShapeName, Weight = intensity, Scalar = channel.TargetWeight
                });
            }

            var bonePropertyKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var channel in expression.BoneChannels ?? Array.Empty<PerformerExpressionBoneChannel>())
            {
                var properties = channel.Properties;
                if (string.IsNullOrWhiteSpace(channel.TransformPath) || string.IsNullOrWhiteSpace(channel.DazBoneId)
                    || IsForbiddenGazeTarget(channel.DazBoneId, channel.TransformPath)
                    || properties == PerformerExpressionBoneProperties.None
                    || (properties & ~(PerformerExpressionBoneProperties.LocalPosition | PerformerExpressionBoneProperties.LocalRotation)) != 0)
                    throw new InvalidOperationException("PerformerExpression contains invalid facial-bone metadata or attempts to own a gaze bone.");
                var transformMatches = _animator.GetComponentsInChildren<Transform>(true).Where(transform =>
                    string.Equals(HierarchyPath(_animator.transform, transform), channel.TransformPath, StringComparison.Ordinal)).ToArray();
                if (transformMatches.Length != 1)
                    throw new InvalidOperationException("PerformerExpression facial-bone path '" + channel.TransformPath + "' does not uniquely resolve under the performer Animator.");
                // Downstream expression ownership must remain facial: imported metadata
                // is not permission to move a shoulder ancestor after spatial arm IK.
                Transform head = _animator.GetComponentsInChildren<Transform>(true)
                    .SingleOrDefault(candidate => candidate.name == "head");
                if (_animator.GetComponent<HandGrip.PerformerHandGripController>() != null
                    && (head == null || !transformMatches[0].IsChildOf(head)))
                    throw new InvalidOperationException("Expression transform is outside the head/face-only ownership subtree: " + channel.TransformPath);

                if ((properties & PerformerExpressionBoneProperties.LocalPosition) != 0)
                {
                    if (!IsFinite(channel.TargetLocalPosition))
                        throw new InvalidOperationException("PerformerExpression facial bone '" + channel.DazBoneId + "' has a non-finite target local position.");
                    var key = "B|" + channel.TransformPath + "|P";
                    if (!bonePropertyKeys.Add(key)) throw new InvalidOperationException("PerformerExpression contains duplicate facial-bone property '" + key + "'.");
                    AddResolved(result, key, new Contribution
                    {
                        Kind = PerformerExpressionPropertyKind.LocalPosition, Path = channel.TransformPath,
                        Name = channel.DazBoneId, Weight = intensity, Position = channel.TargetLocalPosition
                    });
                }
                if ((properties & PerformerExpressionBoneProperties.LocalRotation) != 0)
                {
                    var rotation = channel.TargetLocalRotation;
                    if (!IsFinite(rotation) || rotation.x * rotation.x + rotation.y * rotation.y
                        + rotation.z * rotation.z + rotation.w * rotation.w <= QuaternionEpsilon)
                        throw new InvalidOperationException("PerformerExpression facial bone '" + channel.DazBoneId + "' has an invalid target local rotation.");
                    rotation = Normalize(rotation);
                    var key = "B|" + channel.TransformPath + "|R";
                    if (!bonePropertyKeys.Add(key)) throw new InvalidOperationException("PerformerExpression contains duplicate facial-bone property '" + key + "'.");
                    AddResolved(result, key, new Contribution
                    {
                        Kind = PerformerExpressionPropertyKind.LocalRotation, Path = channel.TransformPath,
                        Name = channel.DazBoneId, Weight = intensity, Rotation = rotation
                    });
                }
            }
            return result;
        }

        private static void AddResolved(Dictionary<string, List<Contribution>> result, string key, Contribution contribution)
            => result.Add(key, new List<Contribution> { contribution });

        private void BuildBindings()
        {
            var keys = _source.Keys.Concat(_target.Keys).Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray();
            var bindingValues = new PerformerExpressionStreamBinding[keys.Length];
            var sourceValues = new List<PerformerExpressionContribution>();
            var targetValues = new List<PerformerExpressionContribution>();
            for (var index = 0; index < keys.Length; index++)
            {
                _source.TryGetValue(keys[index], out var source);
                _target.TryGetValue(keys[index], out var target);
                var descriptor = target != null && target.Count > 0 ? target[0] : source[0];
                var binding = new PerformerExpressionStreamBinding
                {
                    Kind = descriptor.Kind,
                    SourceStart = sourceValues.Count,
                    SourceCount = Append(sourceValues, source),
                    TargetStart = targetValues.Count,
                    TargetCount = Append(targetValues, target)
                };
                if (descriptor.Kind == PerformerExpressionPropertyKind.BlendShape)
                {
                    var transform = string.IsNullOrEmpty(descriptor.Path) ? _animator.transform : _animator.transform.Find(descriptor.Path);
                    var renderers = transform == null ? Array.Empty<SkinnedMeshRenderer>()
                        : transform.GetComponents<SkinnedMeshRenderer>().Where(renderer => renderer.sharedMesh != null
                            && Enumerable.Range(0, renderer.sharedMesh.blendShapeCount).Any(shape =>
                                string.Equals(renderer.sharedMesh.GetBlendShapeName(shape), descriptor.Name, StringComparison.Ordinal))).ToArray();
                    if (renderers.Length != 1)
                        throw new InvalidOperationException("PerformerExpression renderer path '" + descriptor.Path
                            + "' no longer uniquely resolves blendshape '" + descriptor.Name + "'.");
                    binding.PropertyHandle = _animator.BindStreamProperty(renderers[0].transform,
                        typeof(SkinnedMeshRenderer), "blendShape." + descriptor.Name);
                }
                else
                {
                    var transform = _animator.GetComponentsInChildren<Transform>(true).SingleOrDefault(candidate =>
                        string.Equals(HierarchyPath(_animator.transform, candidate), descriptor.Path, StringComparison.Ordinal));
                    if (transform == null) throw new InvalidOperationException("PerformerExpression facial-bone path '" + descriptor.Path + "' no longer resolves uniquely.");
                    binding.TransformHandle = _animator.BindStreamTransform(transform);
                }
                bindingValues[index] = binding;
            }

            NativeArray<PerformerExpressionStreamBinding> nextBindings = default;
            NativeArray<PerformerExpressionContribution> nextSource = default;
            NativeArray<PerformerExpressionContribution> nextTarget = default;
            try
            {
                nextBindings = new NativeArray<PerformerExpressionStreamBinding>(bindingValues, Allocator.Persistent);
                nextSource = new NativeArray<PerformerExpressionContribution>(sourceValues.ToArray(), Allocator.Persistent);
                nextTarget = new NativeArray<PerformerExpressionContribution>(targetValues.ToArray(), Allocator.Persistent);
            }
            catch
            {
                if (nextBindings.IsCreated) nextBindings.Dispose();
                if (nextSource.IsCreated) nextSource.Dispose();
                if (nextTarget.IsCreated) nextTarget.Dispose();
                throw;
            }
            DisposeArrays();
            _bindings = nextBindings;
            _sourceContributions = nextSource;
            _targetContributions = nextTarget;
        }

        private static int Append(List<PerformerExpressionContribution> destination, List<Contribution> source)
        {
            if (source == null) return 0;
            foreach (var value in source)
                destination.Add(new PerformerExpressionContribution
                {
                    Scalar = value.Scalar, Position = value.Position, Rotation = value.Rotation, Weight = value.Weight
                });
            return source.Count;
        }

        private void UpdateJob()
        {
            if (!_playable.IsValid()) return;
            _playable.SetJobData(new PerformerExpressionJob
            {
                Bindings = _bindings,
                SourceContributions = _sourceContributions,
                TargetContributions = _targetContributions,
                Progress = EvaluatedProgress,
                Enabled = !_bypassedForAcceptance
            });
        }

        private static bool IsRuntimeOwnedChannel(string blendShapeName)
        {
            switch (blendShapeName)
            {
                case "Breathe":
                case "EX_Breathe":
                case "Genesis8Female__EX_Breathe":
                case "BreatheBelly":
                case "EX_BreatheBelly":
                case "Genesis8Female__EX_BreatheBelly":
                case "eCTRLEyesClosedL":
                case "eCTRLEyesClosedR":
                case "Genesis8Female__eCTRLEyesClosedL":
                case "Genesis8Female__eCTRLEyesClosedR":
                    return true;
                default:
                    return false;
            }
        }

        private static Dictionary<string, List<Contribution>> Clone(Dictionary<string, List<Contribution>> source)
        {
            var result = new Dictionary<string, List<Contribution>>(StringComparer.Ordinal);
            foreach (var pair in source) result.Add(pair.Key, new List<Contribution>(pair.Value));
            return result;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(Quaternion value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        private static bool IsForbiddenGazeTarget(string boneId, string transformPath)
        {
            if (boneId == "head" || boneId == "lEye" || boneId == "rEye") return true;
            var separator = transformPath.LastIndexOf('/', transformPath.Length - 1);
            var finalSegment = separator >= 0 ? transformPath.Substring(separator + 1) : transformPath;
            return finalSegment == "head" || finalSegment == "lEye" || finalSegment == "rEye";
        }
        private static Quaternion Normalize(Quaternion value)
        {
            var inverseLength = 1f / Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            return new Quaternion(value.x * inverseLength, value.y * inverseLength, value.z * inverseLength, value.w * inverseLength);
        }
        private static float Smooth(float value) => value * value * (3f - 2f * value);
        private static string HierarchyPath(Transform root, Transform target)
        {
            var segments = new Stack<string>();
            while (target != null && target != root) { segments.Push(target.name); target = target.parent; }
            return string.Join("/", segments);
        }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(PerformerExpressionLayer)); }
        private void DisposeArrays()
        {
            if (_bindings.IsCreated) _bindings.Dispose();
            if (_sourceContributions.IsCreated) _sourceContributions.Dispose();
            if (_targetContributions.IsCreated) _targetContributions.Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_playable.IsValid()) _playable.Destroy();
            DisposeArrays();
        }

        private struct Contribution
        {
            public PerformerExpressionPropertyKind Kind;
            public string Path;
            public string Name;
            public float Scalar;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Weight;

            public bool SameTarget(Contribution other)
                => Kind == other.Kind && Path == other.Path && Name == other.Name
                    && Scalar.Equals(other.Scalar) && Position.Equals(other.Position) && Rotation.Equals(other.Rotation);
        }
    }
}
