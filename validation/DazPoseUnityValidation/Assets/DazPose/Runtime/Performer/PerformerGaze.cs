using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer
{
    public enum GazeCompletion
    {
        Acquired,
        Superseded,
        TargetLost,
        PerformerDisabled
    }

    internal struct PerformerGazeSettings
    {
        public bool GazeEnabled;
        public float AcquireToleranceDegrees;
        public bool HeadEnabled;
        public float HeadWeight;
        public float HeadResponse;
        public float HeadMaxYaw;
        public float HeadMaxPitch;
        public bool EyesEnabled;
        public float EyeWeight;
        public float EyeResponse;
        public float EyeMaxYaw;
        public float EyeMaxPitch;
        public float ReleaseResponse;
    }

    internal readonly struct PerformerGazeBoneCalibration
    {
        public readonly string SemanticName;
        public readonly Transform Bone;
        public readonly Vector3 LocalAim;
        public readonly Vector3 LocalUp;
        public readonly Vector3 LocalRight;
        public readonly string HierarchyPath;

        public PerformerGazeBoneCalibration(string semanticName, Transform bone, Vector3 localAim,
            Vector3 localUp, Vector3 localRight, string hierarchyPath)
        {
            SemanticName = semanticName;
            Bone = bone;
            LocalAim = localAim;
            LocalUp = localUp;
            LocalRight = localRight;
            HierarchyPath = hierarchyPath;
        }
    }

    internal readonly struct EffectiveGazeInput
    {
        public readonly Vector3 HeadTargetWorldPosition;
        public readonly Vector3 EyeTargetWorldPosition;
        public readonly Quaternion PreferredHeadBias;

        public EffectiveGazeInput(Vector3 headTargetWorldPosition, Vector3 eyeTargetWorldPosition,
            Quaternion preferredHeadBias)
        {
            HeadTargetWorldPosition = headTargetWorldPosition;
            EyeTargetWorldPosition = eyeTargetWorldPosition;
            PreferredHeadBias = preferredHeadBias;
        }
    }

    internal struct PerformerGazeJob : IAnimationJob
    {
        public TransformStreamHandle Head;
        public TransformStreamHandle LeftEye;
        public TransformStreamHandle RightEye;
        public Vector3 HeadLocalAim;
        public Vector3 HeadLocalUp;
        public Vector3 LeftEyeLocalAim;
        public Vector3 LeftEyeLocalUp;
        public Vector3 RightEyeLocalAim;
        public Vector3 RightEyeLocalUp;
        public Vector3 HeadAimDirection;
        public Vector3 LeftEyeAimDirection;
        public Vector3 RightEyeAimDirection;
        public Quaternion PreferredHeadBias;
        public bool BindingsValid;
        public bool GazeEnabled;
        public bool HeadEnabled;
        public bool EyesEnabled;
        public float GazeWeight;
        public float HeadWeight;
        public float HeadMaxYaw;
        public float HeadMaxPitch;
        public float EyeWeight;
        public float EyeMaxYaw;
        public float EyeMaxPitch;

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!stream.isValid || !BindingsValid) return;
            var applyGaze = GazeEnabled && GazeWeight > 0f;
            var applyHeadBias = Quaternion.Angle(Quaternion.identity, PreferredHeadBias) > 0.001f;
            if (!applyGaze && !applyHeadBias) return;

            var authoredHeadRotation = Head.GetRotation(stream);
            var authoredLeftEyeRotation = LeftEye.GetRotation(stream);
            var authoredRightEyeRotation = RightEye.GetRotation(stream);

            var biasedHeadRotation = authoredHeadRotation * PreferredHeadBias;
            var headDelta = Quaternion.identity;
            if (applyGaze && HeadEnabled && HeadWeight > 0f)
            {
                var headAimCorrection = SolveClampedCorrection(biasedHeadRotation, HeadLocalAim, HeadLocalUp,
                    HeadAimDirection, HeadMaxYaw, HeadMaxPitch);
                var weight = Mathf.Clamp01(GazeWeight * HeadWeight);
                var weightedCorrection = Quaternion.Slerp(Quaternion.identity, headAimCorrection, weight);
                var finalHeadRotation = weightedCorrection * biasedHeadRotation;
                headDelta = finalHeadRotation * Quaternion.Inverse(authoredHeadRotation);
                Head.SetRotation(stream, finalHeadRotation);
            }
            else if (applyHeadBias)
            {
                headDelta = biasedHeadRotation * Quaternion.Inverse(authoredHeadRotation);
                Head.SetRotation(stream, biasedHeadRotation);
            }

            if (!applyGaze || !EyesEnabled || EyeWeight <= 0f) return;

            var leftEyeAfterHead = headDelta * authoredLeftEyeRotation;
            var rightEyeAfterHead = headDelta * authoredRightEyeRotation;
            var eyeWeight = Mathf.Clamp01(GazeWeight * EyeWeight);

            var leftCorrection = SolveClampedCorrection(leftEyeAfterHead, LeftEyeLocalAim, LeftEyeLocalUp,
                LeftEyeAimDirection, EyeMaxYaw, EyeMaxPitch);
            var rightCorrection = SolveClampedCorrection(rightEyeAfterHead, RightEyeLocalAim, RightEyeLocalUp,
                RightEyeAimDirection, EyeMaxYaw, EyeMaxPitch);

            LeftEye.SetRotation(stream,
                Quaternion.Slerp(Quaternion.identity, leftCorrection, eyeWeight) * leftEyeAfterHead);
            RightEye.SetRotation(stream,
                Quaternion.Slerp(Quaternion.identity, rightCorrection, eyeWeight) * rightEyeAfterHead);
        }

        private static Quaternion SolveClampedCorrection(Quaternion boneWorldRotation, Vector3 localAim,
            Vector3 localUp, Vector3 targetDirection, float maxYaw, float maxPitch)
        {
            if (targetDirection.sqrMagnitude < 1e-8f) return Quaternion.identity;

            var aim = (boneWorldRotation * localAim).normalized;
            var up = (boneWorldRotation * localUp).normalized;
            var right = Vector3.Cross(up, aim).normalized;
            if (aim.sqrMagnitude < 0.9f || up.sqrMagnitude < 0.9f || right.sqrMagnitude < 0.9f)
                return Quaternion.identity;
            up = Vector3.Cross(aim, right).normalized;

            var angles = ClampAimAngles(aim, right, up, targetDirection, maxYaw, maxPitch);
            var yawRadians = angles.x * Mathf.Deg2Rad;
            var pitchRadians = angles.y * Mathf.Deg2Rad;
            var cosPitch = Mathf.Cos(pitchRadians);
            var solvedDirection = (aim * (cosPitch * Mathf.Cos(yawRadians))
                                   + right * (cosPitch * Mathf.Sin(yawRadians))
                                   + up * Mathf.Sin(pitchRadians)).normalized;
            return Quaternion.FromToRotation(aim, solvedDirection);
        }

        internal static Vector2 ClampAimAngles(Vector3 aim, Vector3 right, Vector3 up,
            Vector3 targetDirection, float maxYaw, float maxPitch)
        {
            if (targetDirection.sqrMagnitude < 1e-8f) return Vector2.zero;
            var direction = targetDirection.normalized;
            var forwardAmount = Vector3.Dot(direction, aim);
            var rightAmount = Vector3.Dot(direction, right);
            var upAmount = Vector3.Dot(direction, up);
            var yaw = Mathf.Atan2(rightAmount, forwardAmount) * Mathf.Rad2Deg;
            var pitch = Mathf.Atan2(upAmount,
                Mathf.Sqrt(forwardAmount * forwardAmount + rightAmount * rightAmount)) * Mathf.Rad2Deg;
            return new Vector2(Mathf.Clamp(yaw, -Mathf.Clamp(maxYaw, 0f, 89f), Mathf.Clamp(maxYaw, 0f, 89f)),
                Mathf.Clamp(pitch, -Mathf.Clamp(maxPitch, 0f, 89f), Mathf.Clamp(maxPitch, 0f, 89f)));
        }
    }

    internal sealed class PerformerGaze : IDisposable
    {
        private const float AcquiredWeightThreshold = 0.98f;
        private const float MinimumTargetDistance = 0.001f;
        private const float TargetJitterHysteresisMultiplier = 1.5f;

        private readonly Animator _animator;
        private readonly PlayableGraph _graph;
        private PerformerAttentionLife _attentionLife;
        private readonly List<string> _diagnostics = new List<string>();
        private Transform _head;
        private Transform _leftEye;
        private Transform _rightEye;
        private PerformerGazeBoneCalibration _headCalibration;
        private PerformerGazeBoneCalibration _leftEyeCalibration;
        private PerformerGazeBoneCalibration _rightEyeCalibration;
        private AnimationScriptPlayable _gazePlayable;
        private PerformerGazeSettings _settings;
        private GazeIntention _intention;
        private EffectiveGazeInput _effectiveInput;
        private Vector3 _headAimDirection;
        private Vector3 _leftEyeAimDirection;
        private Vector3 _rightEyeAimDirection;
        private Vector3 _semanticHeadAimDirection;
        private Vector3 _semanticLeftEyeAimDirection;
        private Vector3 _semanticRightEyeAimDirection;
        private Vector3 _lastResolvedTarget;
        private float _gazeWeight;
        private bool _aimsInitialized;
        private bool _isAcquired;
        private bool _disposed;
        private int _intentionGeneration;

        public Playable OutputPlayable => _gazePlayable;
        public bool IsAvailable { get; private set; }
        public bool HasGazeTarget => _intention != null;
        public bool IsGazeAcquired => _isAcquired;
        public float GazeWeight => _gazeWeight;
        public Vector3 RawTargetPosition => _lastResolvedTarget;
        public Vector3 EffectiveHeadTargetPosition => _effectiveInput.HeadTargetWorldPosition;
        public Vector3 EffectiveEyeTargetPosition => _effectiveInput.EyeTargetWorldPosition;
        public Quaternion EffectivePreferredHeadBias => _effectiveInput.PreferredHeadBias;
        public Vector3 SmoothedHeadAimDirection => _headAimDirection;
        public Vector3 SmoothedLeftEyeAimDirection => _leftEyeAimDirection;
        public Vector3 SmoothedRightEyeAimDirection => _rightEyeAimDirection;
        public string TargetDescription => _intention == null ? "none" : _intention.Description;
        public int IntentionGeneration => _intentionGeneration;
        public int PendingWaiterCount => _intention == null ? 0 : _intention.Waiters.Count;
        public IReadOnlyList<string> Diagnostics => _diagnostics;
        public PerformerGazeBoneCalibration HeadCalibration => _headCalibration;
        public PerformerGazeBoneCalibration LeftEyeCalibration => _leftEyeCalibration;
        public PerformerGazeBoneCalibration RightEyeCalibration => _rightEyeCalibration;

        public PerformerGaze(Animator animator, PlayableGraph graph, Playable breathingOutput)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            if (!graph.IsValid()) throw new ArgumentException("A valid PlayableGraph is required.", nameof(graph));
            if (!breathingOutput.IsValid()) throw new ArgumentException("A valid breathing playable is required.", nameof(breathingOutput));

            _animator = animator;
            _graph = graph;
            _effectiveInput = new EffectiveGazeInput(Vector3.zero, Vector3.zero, Quaternion.identity);
            IsAvailable = TryResolveAndCalibrate();

            var job = new PerformerGazeJob
            {
                BindingsValid = IsAvailable,
                Head = IsAvailable ? animator.BindStreamTransform(_head) : default,
                LeftEye = IsAvailable ? animator.BindStreamTransform(_leftEye) : default,
                RightEye = IsAvailable ? animator.BindStreamTransform(_rightEye) : default,
                HeadLocalAim = _headCalibration.LocalAim,
                HeadLocalUp = _headCalibration.LocalUp,
                LeftEyeLocalAim = _leftEyeCalibration.LocalAim,
                LeftEyeLocalUp = _leftEyeCalibration.LocalUp,
                RightEyeLocalAim = _rightEyeCalibration.LocalAim,
                RightEyeLocalUp = _rightEyeCalibration.LocalUp,
                PreferredHeadBias = Quaternion.identity
            };

            _gazePlayable = AnimationScriptPlayable.Create(_graph, job, 1);
            _gazePlayable.SetProcessInputs(true);
            _gazePlayable.SetInputWeight(0, 1f);
            if (!_graph.Connect(breathingOutput, 0, _gazePlayable, 0))
                throw new InvalidOperationException("Could not connect the performer gaze layer.");
            UpdateJobData();
        }

        public void AttachAttentionLife(PerformerAttentionLife attentionLife)
        {
            _attentionLife = attentionLife ?? throw new ArgumentNullException(nameof(attentionLife));
            _attentionLife.SetGazeActive(_settings.GazeEnabled && _intention != null);
            _effectiveInput = CreateEffectiveInput(_lastResolvedTarget);
            UpdateJobData();
        }

        public void Configure(PerformerGazeSettings settings)
        {
            _settings = settings;
            _attentionLife?.SetGazeActive(_settings.GazeEnabled && _intention != null);
            _effectiveInput = CreateEffectiveInput(_lastResolvedTarget);
            UpdateJobData();
        }

        public void LookAt(Transform target, AwaitableCompletionSource<GazeCompletion> completion = null)
        {
            ThrowIfDisposed();
            if (target == null) throw new ArgumentNullException(nameof(target));
            SetIntention(GazeIntention.ForTransform(target), completion);
        }

        public void LookAt(Vector3 worldPosition,
            AwaitableCompletionSource<GazeCompletion> completion = null)
        {
            ThrowIfDisposed();
            if (!IsFinite(worldPosition.x) || !IsFinite(worldPosition.y) || !IsFinite(worldPosition.z))
                throw new ArgumentException("A fixed gaze target must contain finite world coordinates.", nameof(worldPosition));
            SetIntention(GazeIntention.ForWorldPosition(worldPosition), completion);
        }

        public void ClearGaze()
        {
            if (_disposed) return;
            var previous = _intention;
            _intention = null;
            _isAcquired = false;
            _intentionGeneration++;
            _attentionLife?.SetGazeActive(false);
            _effectiveInput = CreateEffectiveInput(_lastResolvedTarget);
            if (!_settings.GazeEnabled) _gazeWeight = 0f;
            UpdateJobData();
            CompleteWaiters(previous, GazeCompletion.Superseded);
        }

        public void Advance(float deltaTime)
        {
            if (_disposed) return;
            deltaTime = Mathf.Max(0f, deltaTime);

            if (_intention != null && !_intention.TryResolve(out _lastResolvedTarget))
            {
                var lost = _intention;
                _intention = null;
                _isAcquired = false;
                _intentionGeneration++;
                _attentionLife?.SetGazeActive(false);
                _effectiveInput = CreateEffectiveInput(_lastResolvedTarget);
                if (!_settings.GazeEnabled) _gazeWeight = 0f;
                UpdateJobData();
                CompleteWaiters(lost, GazeCompletion.TargetLost);
                return;
            }

            if (_intention == null)
            {
                _effectiveInput = CreateEffectiveInput(_lastResolvedTarget);
                if (_settings.GazeEnabled)
                    _gazeWeight = SmoothScalar(_gazeWeight, 0f, _settings.ReleaseResponse, deltaTime);
                UpdateJobData();
                return;
            }

            if (!_settings.GazeEnabled)
            {
                _isAcquired = false;
                _effectiveInput = CreateEffectiveInput(_lastResolvedTarget);
                UpdateJobData();
                return;
            }

            _effectiveInput = CreateEffectiveInput(_lastResolvedTarget);
            if (!_aimsInitialized) InitializeAimDirections();

            var headOrigin = _head.position;
            var leftEyeOrigin = _leftEye.position;
            var rightEyeOrigin = _rightEye.position;
            var rawHeadDirection = DirectionTo(_effectiveInput.HeadTargetWorldPosition, headOrigin, _headAimDirection);
            var rawLeftDirection = DirectionTo(_effectiveInput.EyeTargetWorldPosition, leftEyeOrigin, _leftEyeAimDirection);
            var rawRightDirection = DirectionTo(_effectiveInput.EyeTargetWorldPosition, rightEyeOrigin, _rightEyeAimDirection);
            var rawSemanticLeftDirection = DirectionTo(_lastResolvedTarget, leftEyeOrigin,
                _semanticLeftEyeAimDirection);
            var rawSemanticRightDirection = DirectionTo(_lastResolvedTarget, rightEyeOrigin,
                _semanticRightEyeAimDirection);
            var rawSemanticHeadDirection = DirectionTo(_lastResolvedTarget, headOrigin,
                _semanticHeadAimDirection);

            _headAimDirection = SmoothDirection(_headAimDirection, rawHeadDirection,
                _settings.HeadResponse, deltaTime, _head.up);
            _leftEyeAimDirection = SmoothDirection(_leftEyeAimDirection, rawLeftDirection,
                _settings.EyeResponse, deltaTime, _leftEye.up);
            _rightEyeAimDirection = SmoothDirection(_rightEyeAimDirection, rawRightDirection,
                _settings.EyeResponse, deltaTime, _rightEye.up);
            _semanticHeadAimDirection = SmoothDirection(_semanticHeadAimDirection,
                rawSemanticHeadDirection, _settings.HeadResponse, deltaTime, _head.up);
            _semanticLeftEyeAimDirection = SmoothDirection(_semanticLeftEyeAimDirection,
                rawSemanticLeftDirection, _settings.EyeResponse, deltaTime, _leftEye.up);
            _semanticRightEyeAimDirection = SmoothDirection(_semanticRightEyeAimDirection,
                rawSemanticRightDirection, _settings.EyeResponse, deltaTime, _rightEye.up);

            var acquireResponse = Mathf.Max(_settings.HeadResponse, _settings.EyeResponse);
            _gazeWeight = SmoothScalar(_gazeWeight, 1f, acquireResponse, deltaTime);

            var tolerance = Mathf.Max(0.1f, _settings.AcquireToleranceDegrees);
            var error = Mathf.Max(
                Vector3.Angle(_semanticHeadAimDirection, rawSemanticHeadDirection),
                Mathf.Max(Vector3.Angle(_semanticLeftEyeAimDirection, rawSemanticLeftDirection),
                    Vector3.Angle(_semanticRightEyeAimDirection, rawSemanticRightDirection)));
            var threshold = _isAcquired ? tolerance * TargetJitterHysteresisMultiplier : tolerance;
            _isAcquired = _gazeWeight >= AcquiredWeightThreshold && error <= threshold;
            if (_isAcquired && _intention.Waiters.Count > 0)
                CompleteWaiters(_intention, GazeCompletion.Acquired);

            UpdateJobData();
        }

        public Vector3 CreateValidationTarget(float yawDegrees, float pitchDegrees, float distance)
        {
            if (!IsAvailable)
                throw new InvalidOperationException("A validation target cannot be derived because performer gaze is unavailable.");
            var headAim = _head.rotation * _headCalibration.LocalAim;
            var headUp = _head.rotation * _headCalibration.LocalUp;
            var headRight = Vector3.Cross(headUp, headAim).normalized;
            headUp = Vector3.Cross(headAim, headRight).normalized;
            var yaw = yawDegrees * Mathf.Deg2Rad;
            var pitch = pitchDegrees * Mathf.Deg2Rad;
            var direction = (headAim * (Mathf.Cos(pitch) * Mathf.Cos(yaw))
                             + headRight * (Mathf.Cos(pitch) * Mathf.Sin(yaw))
                             + headUp * Mathf.Sin(pitch)).normalized;
            return _head.position + direction * Mathf.Max(0.1f, distance);
        }

        public void Dispose()
        {
            if (_disposed) return;
            var previous = _intention;
            _intention = null;
            _isAcquired = false;
            _attentionLife?.SetGazeActive(false);
            _disposed = true;
            CompleteWaiters(previous, GazeCompletion.PerformerDisabled);
            if (_gazePlayable.IsValid()) _gazePlayable.Destroy();
            _gazePlayable = default;
        }

        private void SetIntention(GazeIntention next, AwaitableCompletionSource<GazeCompletion> completion)
        {
            if (!IsAvailable)
                throw new InvalidOperationException("Performer gaze is unavailable. " + string.Join(" ", _diagnostics));

            if (_intention != null && _intention.SemanticallyMatches(next))
            {
                if (completion != null)
                {
                    if (IsCurrentlyAcquired())
                        completion.TrySetResult(GazeCompletion.Acquired);
                    else
                    {
                        _isAcquired = false;
                        _intention.Waiters.Add(completion);
                    }
                }
                return;
            }

            var previous = _intention;
            _intention = next;
            _isAcquired = false;
            _intentionGeneration++;
            _attentionLife?.SetGazeActive(_settings.GazeEnabled);
            if (completion != null) _intention.Waiters.Add(completion);
            if (_intention.TryResolve(out _lastResolvedTarget))
            {
                _effectiveInput = CreateEffectiveInput(_lastResolvedTarget);
                if (!_aimsInitialized) InitializeAimDirections();
            }
            CompleteWaiters(previous, GazeCompletion.Superseded);
            UpdateJobData();
        }

        private bool IsCurrentlyAcquired()
        {
            if (!_isAcquired || !_settings.GazeEnabled || _gazeWeight < AcquiredWeightThreshold
                || _intention == null || !_intention.TryResolve(out var rawTarget)) return false;

            var headDirection = DirectionTo(rawTarget, _head.position, _semanticHeadAimDirection);
            var leftDirection = DirectionTo(rawTarget, _leftEye.position, _semanticLeftEyeAimDirection);
            var rightDirection = DirectionTo(rawTarget, _rightEye.position, _semanticRightEyeAimDirection);
            var tolerance = Mathf.Max(0.1f, _settings.AcquireToleranceDegrees)
                            * TargetJitterHysteresisMultiplier;
            return Vector3.Angle(_semanticHeadAimDirection, headDirection) <= tolerance
                   && Vector3.Angle(_semanticLeftEyeAimDirection, leftDirection) <= tolerance
                   && Vector3.Angle(_semanticRightEyeAimDirection, rightDirection) <= tolerance;
        }

        private EffectiveGazeInput CreateEffectiveInput(Vector3 rawTarget)
        {
            var life = _attentionLife == null ? default : _attentionLife.CurrentOutput;
            var eyeTarget = rawTarget;
            if (_head != null && _leftEye != null && _rightEye != null
                && (life.EyeHorizontalOffsetDegrees != 0f || life.EyeVerticalOffsetDegrees != 0f))
            {
                var eyeOrigin = (_leftEye.position + _rightEye.position) * 0.5f;
                var rawDirection = rawTarget - eyeOrigin;
                var distance = rawDirection.magnitude;
                if (distance > MinimumTargetDistance)
                {
                    rawDirection /= distance;
                    var right = (_head.rotation * _headCalibration.LocalRight).normalized;
                    var up = (_head.rotation * _headCalibration.LocalUp).normalized;
                    var offsetDirection = Quaternion.AngleAxis(life.EyeHorizontalOffsetDegrees, up)
                                          * rawDirection;
                    offsetDirection = Quaternion.AngleAxis(-life.EyeVerticalOffsetDegrees, right)
                                      * offsetDirection;
                    eyeTarget = eyeOrigin + offsetDirection.normalized * distance;
                }
            }

            // The head continues tracking semantic intent; only the eye target receives
            // angular fixation variation. Preferred bias is consumed before the P0.6 solve.
            var preferredBias = _attentionLife == null ? Quaternion.identity : life.PreferredHeadBias;
            return new EffectiveGazeInput(rawTarget, eyeTarget, preferredBias);
        }

        private void InitializeAimDirections()
        {
            _headAimDirection = (_head.rotation * _headCalibration.LocalAim).normalized;
            _leftEyeAimDirection = (_leftEye.rotation * _leftEyeCalibration.LocalAim).normalized;
            _rightEyeAimDirection = (_rightEye.rotation * _rightEyeCalibration.LocalAim).normalized;
            _semanticHeadAimDirection = _headAimDirection;
            _semanticLeftEyeAimDirection = _leftEyeAimDirection;
            _semanticRightEyeAimDirection = _rightEyeAimDirection;
            _aimsInitialized = true;
        }

        private bool TryResolveAndCalibrate()
        {
            var transforms = _animator.GetComponentsInChildren<Transform>(true);
            if (!TryFindUnique(transforms, "head", "head", out _head)
                || !TryFindUnique(transforms, "left eye", "lEye", out _leftEye)
                || !TryFindUnique(transforms, "right eye", "rEye", out _rightEye)) return false;

            if (!_leftEye.IsChildOf(_head) || !_rightEye.IsChildOf(_head))
            {
                AddDiagnostic("Performer gaze disabled: resolved eye bones must be descendants of the resolved head bone.");
                return false;
            }

            var eyeMidpoint = (_leftEye.position + _rightEye.position) * 0.5f;
            var rootUp = _animator.transform.up.normalized;
            var faceVector = eyeMidpoint - _head.position;
            var faceForward = Vector3.ProjectOnPlane(faceVector, rootUp);
            if (!IsFinite(faceForward) || faceForward.sqrMagnitude < 1e-8f || rootUp.sqrMagnitude < 0.9f)
            {
                AddDiagnostic("Performer gaze disabled: neutral eye midpoint minus head position could not establish a facial-forward axis.");
                return false;
            }
            faceForward.Normalize();
            var faceUp = Vector3.ProjectOnPlane(rootUp, faceForward);
            if (!IsFinite(faceUp) || faceUp.sqrMagnitude < 1e-8f)
            {
                AddDiagnostic("Performer gaze disabled: performer up could not establish an anatomical face-up axis.");
                return false;
            }
            faceUp.Normalize();

            return TryBuildCalibration("head", _head, faceForward, faceUp, out _headCalibration)
                   && TryBuildCalibration("left eye", _leftEye, faceForward, faceUp, out _leftEyeCalibration)
                   && TryBuildCalibration("right eye", _rightEye, faceForward, faceUp, out _rightEyeCalibration);
        }

        private bool TryFindUnique(Transform[] transforms, string semanticName, string exactName,
            out Transform result)
        {
            var matches = transforms.Where(item => string.Equals(item.name, exactName, StringComparison.Ordinal)).ToArray();
            if (matches.Length == 1)
            {
                result = matches[0];
                return true;
            }

            result = null;
            AddDiagnostic("Performer gaze disabled: semantic bone '" + semanticName + "' expected exact transform '"
                          + exactName + "', found " + matches.Length + ".");
            return false;
        }

        private bool TryBuildCalibration(string semanticName, Transform bone, Vector3 faceForward,
            Vector3 faceUp, out PerformerGazeBoneCalibration calibration)
        {
            var inverseRotation = Quaternion.Inverse(bone.rotation);
            var localAim = (inverseRotation * faceForward).normalized;
            var localUp = (inverseRotation * faceUp).normalized;
            var localRight = Vector3.Cross(localUp, localAim).normalized;
            localUp = Vector3.Cross(localAim, localRight).normalized;
            if (!IsFinite(localAim) || !IsFinite(localUp) || !IsFinite(localRight)
                || localAim.sqrMagnitude < 0.9f || localUp.sqrMagnitude < 0.9f || localRight.sqrMagnitude < 0.9f
                || Mathf.Abs(Vector3.Dot(localAim, localUp)) > 0.001f
                || Mathf.Abs(Vector3.Dot(localAim, localRight)) > 0.001f
                || Mathf.Abs(Vector3.Dot(localUp, localRight)) > 0.001f)
            {
                calibration = default;
                AddDiagnostic("Performer gaze disabled: anatomical aim basis for '" + semanticName
                              + "' was degenerate or non-orthogonal.");
                return false;
            }

            calibration = new PerformerGazeBoneCalibration(semanticName, bone, localAim, localUp,
                localRight, HierarchyPath(_animator.transform, bone));
            return true;
        }

        private void AddDiagnostic(string message)
        {
            _diagnostics.Add(message);
            Debug.LogError(message, _animator);
        }

        private void UpdateJobData()
        {
            if (!_gazePlayable.IsValid()) return;
            var job = _gazePlayable.GetJobData<PerformerGazeJob>();
            job.BindingsValid = IsAvailable;
            job.GazeEnabled = _settings.GazeEnabled;
            job.HeadEnabled = _settings.HeadEnabled;
            job.EyesEnabled = _settings.EyesEnabled;
            job.GazeWeight = _gazeWeight;
            job.HeadWeight = Mathf.Clamp01(_settings.HeadWeight);
            job.HeadMaxYaw = Mathf.Clamp(_settings.HeadMaxYaw, 0f, 89f);
            job.HeadMaxPitch = Mathf.Clamp(_settings.HeadMaxPitch, 0f, 89f);
            job.EyeWeight = Mathf.Clamp01(_settings.EyeWeight);
            job.EyeMaxYaw = Mathf.Clamp(_settings.EyeMaxYaw, 0f, 89f);
            job.EyeMaxPitch = Mathf.Clamp(_settings.EyeMaxPitch, 0f, 89f);
            job.HeadAimDirection = _headAimDirection;
            job.LeftEyeAimDirection = _leftEyeAimDirection;
            job.RightEyeAimDirection = _rightEyeAimDirection;
            job.PreferredHeadBias = _effectiveInput.PreferredHeadBias;
            _gazePlayable.SetJobData(job);
        }

        private static Vector3 DirectionTo(Vector3 target, Vector3 origin, Vector3 fallbackDirection)
        {
            var direction = target - origin;
            if (direction.sqrMagnitude < MinimumTargetDistance * MinimumTargetDistance)
                return fallbackDirection.sqrMagnitude < 1e-8f ? Vector3.zero : fallbackDirection.normalized;
            return direction.normalized;
        }

        private static Vector3 SmoothDirection(Vector3 current, Vector3 target, float response,
            float deltaTime, Vector3 fallbackUp)
        {
            if (target.sqrMagnitude < 1e-8f) return current;
            if (current.sqrMagnitude < 1e-8f) return target.normalized;
            var alpha = ResponseAlpha(response, deltaTime);
            if (alpha >= 1f) return target.normalized;

            var from = current.normalized;
            var to = target.normalized;
            if (Vector3.Dot(from, to) < -0.9995f)
            {
                var axis = Vector3.Cross(from, fallbackUp).normalized;
                if (axis.sqrMagnitude < 1e-8f) return to;
                return (Quaternion.AngleAxis(180f * alpha, axis) * from).normalized;
            }
            return Vector3.Slerp(from, to, alpha).normalized;
        }

        private static float SmoothScalar(float current, float target, float response, float deltaTime)
        {
            return Mathf.Lerp(current, target, ResponseAlpha(response, deltaTime));
        }

        private static float ResponseAlpha(float response, float deltaTime)
        {
            if (deltaTime <= 0f) return 0f;
            response = Mathf.Max(0f, response);
            if (response <= 0f) return 1f;
            return 1f - Mathf.Exp(-response * deltaTime);
        }

        private static void CompleteWaiters(GazeIntention intention, GazeCompletion result)
        {
            if (intention == null || intention.Waiters.Count == 0) return;
            var waiters = intention.Waiters.ToArray();
            intention.Waiters.Clear();
            foreach (var waiter in waiters) waiter.TrySetResult(result);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PerformerGaze));
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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

        private sealed class GazeIntention
        {
            private GazeIntention(Transform transform, Vector3 worldPosition, bool followsTransform)
            {
                Transform = transform;
                WorldPosition = worldPosition;
                FollowsTransform = followsTransform;
            }

            public Transform Transform { get; }
            public Vector3 WorldPosition { get; }
            public bool FollowsTransform { get; }
            public List<AwaitableCompletionSource<GazeCompletion>> Waiters { get; } =
                new List<AwaitableCompletionSource<GazeCompletion>>();
            public string Description => FollowsTransform
                ? Transform == null ? "lost Transform" : Transform.name
                : "world " + WorldPosition.ToString("F2");

            public static GazeIntention ForTransform(Transform target) => new GazeIntention(target, default, true);
            public static GazeIntention ForWorldPosition(Vector3 target) => new GazeIntention(null, target, false);

            public bool TryResolve(out Vector3 position)
            {
                if (!FollowsTransform)
                {
                    position = WorldPosition;
                    return true;
                }
                if (Transform == null)
                {
                    position = default;
                    return false;
                }
                position = Transform.position;
                return true;
            }

            public bool SemanticallyMatches(GazeIntention other)
            {
                if (other == null || FollowsTransform != other.FollowsTransform) return false;
                return FollowsTransform
                    ? ReferenceEquals(Transform, other.Transform)
                    : (WorldPosition - other.WorldPosition).sqrMagnitude <= 1e-8f;
            }
        }
    }
}
