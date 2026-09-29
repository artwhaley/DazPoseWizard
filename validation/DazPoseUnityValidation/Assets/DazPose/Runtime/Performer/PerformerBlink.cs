using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer
{
    public enum PerformerBlinkState
    {
        Waiting,
        Closing,
        Closed,
        Opening
    }

    internal struct PerformerBlinkSettings
    {
        public bool Enabled;
        public int Seed;
        public float Strength;
        public float MinimumIntervalSeconds;
        public float MaximumIntervalSeconds;
        public float CloseDurationSeconds;
        public float ClosedDurationSeconds;
        public float OpenDurationSeconds;
    }

    internal struct PerformerBlinkStreamBinding
    {
        public PropertyStreamHandle Handle;
        public float PositiveMaximumWeight;
    }

    internal readonly struct PerformerBlinkBindingInfo
    {
        public readonly string SemanticName;
        public readonly string ImportedBlendShapeName;
        public readonly SkinnedMeshRenderer Renderer;
        public readonly int BlendShapeIndex;
        public readonly int BaseBlendShapeIndex;
        public readonly float PositiveMaximumWeight;
        public readonly string RendererPath;

        public PerformerBlinkBindingInfo(string semanticName, string importedBlendShapeName,
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

    internal struct PerformerBlinkJob : IAnimationJob
    {
        [ReadOnly] public NativeArray<PerformerBlinkStreamBinding> Bindings;
        public bool BindingsValid;
        public float Closure;

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!stream.isValid || !BindingsValid || Closure <= 0f) return;
            var closure = Mathf.Clamp01(Closure);
            if (closure <= 0f) return;

            for (var index = 0; index < Bindings.Length; index++)
            {
                var binding = Bindings[index];
                if (!binding.Handle.IsValid(stream)) continue;
                var baseWeight = binding.Handle.GetFloat(stream);
                binding.Handle.SetFloat(stream,
                    PerformerBlink.ComposeWeight(baseWeight, binding.PositiveMaximumWeight, closure));
            }
        }
    }

    internal sealed class PerformerBlink : IDisposable
    {
        private const uint RandomStream = 0xB11A0003u;
        private const string LeftImportedName = "Genesis8Female__eCTRLEyesClosedL";
        private const string RightImportedName = "Genesis8Female__eCTRLEyesClosedR";
        private const float MinimumDuration = 0.005f;
        private const float DisabledOpenDuration = 0.12f;

        private readonly Animator _animator;
        private readonly PlayableGraph _graph;
        private readonly List<string> _diagnostics = new List<string>();
        private readonly List<PerformerBlinkBindingInfo> _bindingInfo =
            new List<PerformerBlinkBindingInfo>();
        private NativeArray<PerformerBlinkStreamBinding> _bindings;
        private AnimationScriptPlayable _blinkPlayable;
        private PerformerBlinkSettings _settings;
        private PerformerDeterministicRandom _random;
        private PerformerBlinkState _state;
        private float _closure;
        private float _countdown;
        private float _phaseElapsed;
        private float _phaseDuration;
        private float _phaseStartClosure;
        private bool _wasEnabled;
        private bool _hasAcceptanceOverride;
        private float _acceptanceClosure;
        private bool _disposed;

        public Playable OutputPlayable => _blinkPlayable;
        public bool IsAvailable { get; private set; }
        public float Closure => Mathf.Clamp01(_closure * Mathf.Clamp01(_settings.Strength));
        public float TimelineClosure => _closure;
        public float Countdown => _state == PerformerBlinkState.Waiting
            ? Mathf.Max(0f, _countdown) : 0f;
        public PerformerBlinkState State => _state;
        public IReadOnlyList<string> Diagnostics => _diagnostics;
        public IReadOnlyList<PerformerBlinkBindingInfo> Bindings => _bindingInfo;

        public PerformerBlink(Animator animator, PlayableGraph graph, Playable gazeOutput,
            PerformerBodyPose bodyPose, PerformerBlinkSettings settings)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            if (!graph.IsValid()) throw new ArgumentException("A valid PlayableGraph is required.", nameof(graph));
            if (!gazeOutput.IsValid()) throw new ArgumentException("A valid gaze playable is required.", nameof(gazeOutput));
            if (bodyPose == null) throw new ArgumentNullException(nameof(bodyPose));

            _animator = animator;
            _graph = graph;
            _state = PerformerBlinkState.Waiting;
            _settings = Sanitize(settings);
            ResetRandom(_settings.Seed);

            try
            {
                var resolved = ResolveBindings(animator, bodyPose);
                _bindings = new NativeArray<PerformerBlinkStreamBinding>(resolved.ToArray(), Allocator.Persistent);
                IsAvailable = _bindingInfo.Any(binding => binding.SemanticName == "Left Blink")
                              && _bindingInfo.Any(binding => binding.SemanticName == "Right Blink");
                if (!IsAvailable)
                    AddDiagnostic("Autonomous blink disabled: both exact canonical Lara blink morphs must resolve. "
                                  + "Expected '" + LeftImportedName + "' and '" + RightImportedName + "'.");

                var job = new PerformerBlinkJob
                {
                    Bindings = _bindings,
                    BindingsValid = IsAvailable
                };
                _blinkPlayable = AnimationScriptPlayable.Create(_graph, job, 1);
                _blinkPlayable.SetProcessInputs(true);
                _blinkPlayable.SetInputWeight(0, 1f);
                if (!_graph.Connect(gazeOutput, 0, _blinkPlayable, 0))
                    throw new InvalidOperationException("Could not connect the performer blink layer.");
                UpdateJobData();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Configure(PerformerBlinkSettings settings)
        {
            settings = Sanitize(settings);
            if (settings.Seed != _settings.Seed)
            {
                _settings = settings;
                ResetRandom(settings.Seed);
                _state = PerformerBlinkState.Waiting;
                _closure = 0f;
                _countdown = 0f;
                _wasEnabled = false;
            }
            else
            {
                _settings = settings;
            }
            UpdateJobData();
        }

        public void Advance(float deltaTime)
        {
            if (_disposed) return;
            deltaTime = Mathf.Max(0f, deltaTime);
            if (_hasAcceptanceOverride)
            {
                _closure = _acceptanceClosure;
                UpdateJobData();
                return;
            }

            if (!_settings.Enabled)
            {
                _wasEnabled = false;
                _countdown = 0f;
                if (_closure > 0f)
                {
                    _state = PerformerBlinkState.Opening;
                    _closure = Mathf.MoveTowards(_closure, 0f,
                        deltaTime / Mathf.Max(MinimumDuration, DisabledOpenDuration));
                    if (_closure <= 0.0001f)
                    {
                        _closure = 0f;
                        _state = PerformerBlinkState.Waiting;
                    }
                }
                else
                {
                    _closure = 0f;
                    _state = PerformerBlinkState.Waiting;
                }
                UpdateJobData();
                return;
            }

            if (!_wasEnabled)
            {
                _wasEnabled = true;
                if (_closure > 0f)
                {
                    BeginPhase(PerformerBlinkState.Opening, _settings.OpenDurationSeconds, _closure);
                }
                else
                {
                    _state = PerformerBlinkState.Waiting;
                    _countdown = SampleInterval();
                }
            }

            var remaining = deltaTime;
            var transitionCount = 0;
            while (transitionCount++ < 8)
            {
                if (_state == PerformerBlinkState.Waiting)
                {
                    if (remaining <= 0f) break;
                    var consumed = Mathf.Min(remaining, _countdown);
                    _countdown -= consumed;
                    remaining -= consumed;
                    if (_countdown > 0f) break;
                    BeginPhase(PerformerBlinkState.Closing, _settings.CloseDurationSeconds, 0f);
                    continue;
                }

                if (remaining <= 0f) break;
                var phaseRemaining = Mathf.Max(0f, _phaseDuration - _phaseElapsed);
                var phaseConsumed = Mathf.Min(remaining, phaseRemaining);
                _phaseElapsed += phaseConsumed;
                remaining -= phaseConsumed;
                EvaluatePhase();
                if (_phaseElapsed + 0.000001f < _phaseDuration) break;

                switch (_state)
                {
                    case PerformerBlinkState.Closing:
                        _closure = 1f;
                        BeginPhase(PerformerBlinkState.Closed, _settings.ClosedDurationSeconds, 1f);
                        break;
                    case PerformerBlinkState.Closed:
                        BeginPhase(PerformerBlinkState.Opening, _settings.OpenDurationSeconds, 1f);
                        break;
                    case PerformerBlinkState.Opening:
                        _closure = 0f;
                        _state = PerformerBlinkState.Waiting;
                        _countdown = SampleInterval();
                        break;
                }
            }

            UpdateJobData();
        }

        internal void SetClosureForAcceptance(float closure)
        {
            _hasAcceptanceOverride = true;
            _acceptanceClosure = Mathf.Clamp01(closure);
            _closure = _acceptanceClosure;
            UpdateJobData();
        }

        internal void ClearClosureOverrideForAcceptance()
        {
            _hasAcceptanceOverride = false;
            UpdateJobData();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_blinkPlayable.IsValid()) _blinkPlayable.Destroy();
            if (_bindings.IsCreated) _bindings.Dispose();
            _bindings = default;
            _blinkPlayable = default;
        }

        internal static float ComposeWeight(float baseWeight, float usefulMaximumWeight, float closure)
        {
            closure = Mathf.Clamp01(closure);
            if (closure <= 0f) return baseWeight;
            return baseWeight + closure * (usefulMaximumWeight - baseWeight);
        }

        internal static float[] SampleIntervalsForAcceptance(int seed, float minimum, float maximum,
            int count)
        {
            var random = new PerformerDeterministicRandom(seed, RandomStream);
            var intervals = new float[Mathf.Max(0, count)];
            for (var index = 0; index < intervals.Length; index++)
                intervals[index] = random.Range(minimum, maximum);
            return intervals;
        }

        private List<PerformerBlinkStreamBinding> ResolveBindings(Animator animator,
            PerformerBodyPose bodyPose)
        {
            var result = new List<PerformerBlinkStreamBinding>();
            var foundLeft = 0;
            var foundRight = 0;
            foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = renderer == null ? null : renderer.sharedMesh;
                if (mesh == null) continue;
                for (var shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
                {
                    var importedName = mesh.GetBlendShapeName(shapeIndex);
                    var semanticName = string.Equals(importedName, LeftImportedName, StringComparison.Ordinal)
                        ? "Left Blink"
                        : string.Equals(importedName, RightImportedName, StringComparison.Ordinal)
                            ? "Right Blink" : null;
                    if (semanticName == null) continue;

                    var positiveMaximum = 0f;
                    for (var frame = 0; frame < mesh.GetBlendShapeFrameCount(shapeIndex); frame++)
                        positiveMaximum = Mathf.Max(positiveMaximum,
                            mesh.GetBlendShapeFrameWeight(shapeIndex, frame));
                    if (positiveMaximum <= 0f)
                    {
                        AddDiagnostic("Exact blink morph '" + importedName + "' on '" + renderer.name
                                      + "' has no positive frame weight and was not bound.");
                        continue;
                    }

                    var baseIndex = bodyPose.GetBlendShapeStreamIndex(renderer, shapeIndex);
                    if (baseIndex < 0)
                    {
                        AddDiagnostic("Exact blink morph '" + importedName + "' on '" + renderer.name
                                      + "' could not be mapped to the base-pose stream and was not bound.");
                        continue;
                    }

                    var handle = animator.BindStreamProperty(renderer.transform,
                        typeof(SkinnedMeshRenderer), "blendShape." + importedName);
                    result.Add(new PerformerBlinkStreamBinding
                    {
                        Handle = handle,
                        PositiveMaximumWeight = positiveMaximum
                    });
                    _bindingInfo.Add(new PerformerBlinkBindingInfo(semanticName, importedName,
                        renderer, shapeIndex, baseIndex, positiveMaximum,
                        HierarchyPath(animator.transform, renderer.transform)));
                    if (semanticName == "Left Blink") foundLeft++;
                    else foundRight++;
                }
            }

            if (foundLeft == 0)
                AddDiagnostic("Canonical left blink morph '" + LeftImportedName
                              + "' was not found by exact name under '" + animator.name + "'.");
            if (foundRight == 0)
                AddDiagnostic("Canonical right blink morph '" + RightImportedName
                              + "' was not found by exact name under '" + animator.name + "'.");
            return result;
        }

        private void BeginPhase(PerformerBlinkState state, float duration, float startClosure)
        {
            _state = state;
            _phaseElapsed = 0f;
            _phaseDuration = Mathf.Max(MinimumDuration, duration);
            _phaseStartClosure = Mathf.Clamp01(startClosure);
            if (state == PerformerBlinkState.Closing) _closure = 0f;
        }

        private void EvaluatePhase()
        {
            var progress = Mathf.Clamp01(_phaseElapsed / _phaseDuration);
            var eased = Mathf.SmoothStep(0f, 1f, progress);
            switch (_state)
            {
                case PerformerBlinkState.Closing:
                    _closure = eased;
                    break;
                case PerformerBlinkState.Closed:
                    _closure = 1f;
                    break;
                case PerformerBlinkState.Opening:
                    _closure = _phaseStartClosure * (1f - eased);
                    break;
            }
        }

        private float SampleInterval()
        {
            return _random.Range(_settings.MinimumIntervalSeconds,
                _settings.MaximumIntervalSeconds);
        }

        private void ResetRandom(int seed)
        {
            _random = new PerformerDeterministicRandom(seed, RandomStream);
        }

        private void AddDiagnostic(string message)
        {
            _diagnostics.Add(message);
            Debug.LogError(message, _animator);
        }

        private void UpdateJobData()
        {
            if (!_blinkPlayable.IsValid()) return;
            var job = _blinkPlayable.GetJobData<PerformerBlinkJob>();
            job.BindingsValid = IsAvailable;
            job.Closure = Closure;
            _blinkPlayable.SetJobData(job);
        }

        private static PerformerBlinkSettings Sanitize(PerformerBlinkSettings settings)
        {
            settings.Strength = Mathf.Clamp01(settings.Strength);
            settings.MinimumIntervalSeconds = Mathf.Max(0.1f, settings.MinimumIntervalSeconds);
            settings.MaximumIntervalSeconds = Mathf.Max(settings.MinimumIntervalSeconds,
                settings.MaximumIntervalSeconds);
            settings.CloseDurationSeconds = Mathf.Max(MinimumDuration, settings.CloseDurationSeconds);
            settings.ClosedDurationSeconds = Mathf.Max(MinimumDuration, settings.ClosedDurationSeconds);
            settings.OpenDurationSeconds = Mathf.Max(MinimumDuration, settings.OpenDurationSeconds);
            return settings;
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
