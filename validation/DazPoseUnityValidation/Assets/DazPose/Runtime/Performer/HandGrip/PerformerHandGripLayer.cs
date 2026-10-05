using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer.HandGrip
{
    /// <summary>AnimationScriptPlayable that owns only calibrated digit transforms while blended in.</summary>
    internal sealed class PerformerHandGripLayer : IDisposable
    {
        private readonly PlayableGraph _graph;
        private readonly Animator _animator;
        private readonly Transform _hand;
        private readonly HandGripRigProfile _profile;
        private AnimationScriptPlayable _playable;
        private NativeArray<HandGripJobDigit> _digits;
        private NativeArray<HandGripJobJoint> _joints;
        private NativeArray<HandGripJobProbe> _probes;
        private NativeArray<Quaternion> _incomingRotations;
        private NativeArray<float> _solvedCurls;
        private NativeArray<HandGripStatus> _digitStatuses;
        private NativeArray<HandGripProbeDiagnostic> _probeDiagnostics;
        private bool _disposed;
        private HandGripStatus _immediateStatus = HandGripStatus.InvalidTarget;

        public Playable OutputPlayable => _playable;
        public int ProbeCount => _probeDiagnostics.IsCreated ? _probeDiagnostics.Length : 0;
        public HandGripRigProfile Profile => _profile;

        public PerformerHandGripLayer(PlayableGraph graph, Playable baseSource, Animator animator,
            HandGripRigProfile profile)
        {
            if (!graph.IsValid()) throw new ArgumentException("A valid performer PlayableGraph is required.", nameof(graph));
            if (!baseSource.IsValid()) throw new ArgumentException("A valid incoming body source is required.", nameof(baseSource));
            _animator = animator != null ? animator : throw new ArgumentNullException(nameof(animator));
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            if (!_profile.TryValidate(_animator, out Transform hand, out string reason))
                throw new InvalidOperationException(reason);
            _hand = hand;
            _graph = graph;

            try
            {
                BuildBindings();
                var job = new HandGripAnimationJob
                {
                    Digits = _digits,
                    Joints = _joints,
                    Probes = _probes,
                    IncomingRotations = _incomingRotations,
                    SolvedCurls = _solvedCurls,
                    DigitStatuses = _digitStatuses,
                    ProbeDiagnostics = _probeDiagnostics,
                    HandHandle = _animator.BindStreamTransform(_hand),
                    ContactClearance = _profile.ContactClearance,
                    NearContactDistance = _profile.NearContactDistance,
                    BinarySearchIterations = _profile.BinarySearchIterations
                };
                _playable = AnimationScriptPlayable.Create(_graph, job, 1);
                _playable.SetProcessInputs(true);
                _playable.SetInputWeight(0, 1f);
                if (!_graph.Connect(baseSource, 0, _playable, 0))
                    throw new InvalidOperationException("Could not connect the HandGrip animation job above the current performer body source.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void SetState(bool enabled, GripFrame frame, float weight, float strength,
            HandGripStatus invalidStatus)
        {
            if (_disposed || !_playable.IsValid()) return;
            HandGripAnimationJob job = _playable.GetJobData<HandGripAnimationJob>();
            job.Enabled = enabled;
            job.Frame = frame;
            job.GripWeight = Mathf.Clamp01(weight);
            job.GripStrength = Mathf.Clamp01(strength);
            _playable.SetJobData(job);
            _immediateStatus = enabled ? HandGripStatus.Clear : invalidStatus;
            if (!enabled)
            {
                for (int index = 0; index < _digitStatuses.Length; index++)
                {
                    _digitStatuses[index] = invalidStatus;
                    _solvedCurls[index] = 0f;
                }
                for (int index = 0; index < _probeDiagnostics.Length; index++)
                    _probeDiagnostics[index] = default;
            }
        }

        public HandGripSolveResult GetSolveResult()
        {
            if (_disposed || !_solvedCurls.IsCreated)
                return new HandGripSolveResult(HandGripStatus.InvalidProfile, 0f, 0f, 0f, 0f, 0f);
            if (_immediateStatus != HandGripStatus.Clear)
                return new HandGripSolveResult(_immediateStatus, 0f, 0f, 0f, 0f, 0f);

            HandGripStatus overall = HandGripStatus.Clear;
            for (int index = 0; index < _digitStatuses.Length; index++)
            {
                HandGripStatus status = _digitStatuses[index];
                if (status == HandGripStatus.BasePenetration) overall = status;
                else if (status == HandGripStatus.InvalidProfile && overall != HandGripStatus.BasePenetration)
                    overall = status;
                else if (status == HandGripStatus.Contact && overall == HandGripStatus.Clear)
                    overall = status;
            }
            return new HandGripSolveResult(overall, Curl(0), Curl(1), Curl(2), Curl(3), Curl(4));
        }

        public HandGripStatus GetDigitStatus(int index)
        {
            if (!_digitStatuses.IsCreated || index < 0 || index >= _digitStatuses.Length)
                return HandGripStatus.InvalidProfile;
            return _digitStatuses[index];
        }

        public bool TryGetProbeDiagnostic(int index, out Vector3 worldPosition,
            out HandGripProbeState state)
        {
            worldPosition = default;
            state = HandGripProbeState.Clear;
            if (!_probeDiagnostics.IsCreated || index < 0 || index >= _probeDiagnostics.Length) return false;
            HandGripProbeDiagnostic diagnostic = _probeDiagnostics[index];
            worldPosition = diagnostic.WorldPosition;
            state = diagnostic.State;
            return true;
        }

        public float GetProbeRadius(int index)
        {
            return _probes.IsCreated && index >= 0 && index < _probes.Length
                ? _probes[index].Radius : 0f;
        }

        public HandGripAlignmentDiagnostics GetAlignmentDiagnostics(GripFrame targetFrame)
        {
            Vector3 actual = _hand.TransformPoint(_profile.GripCenterLocalPosition);
            Vector3 expectedForward = targetFrame.Tangent;
            Vector3 expectedUp = targetFrame.Normal;
            Vector3 calibratedForward = _hand.TransformDirection(
                _profile.GripCenterLocalRotation * Vector3.forward);
            Vector3 calibratedUp = _hand.TransformDirection(
                _profile.GripCenterLocalRotation * Vector3.up);
            float angular = Vector3.Angle(calibratedForward, expectedForward);
            Vector3 actualUpProjected = Vector3.ProjectOnPlane(calibratedUp, expectedForward);
            Vector3 expectedUpProjected = Vector3.ProjectOnPlane(expectedUp, expectedForward);
            float roll = actualUpProjected.sqrMagnitude < 1e-8f || expectedUpProjected.sqrMagnitude < 1e-8f
                ? 0f : Vector3.SignedAngle(expectedUpProjected, actualUpProjected, expectedForward);
            return new HandGripAlignmentDiagnostics(targetFrame.Center, actual,
                Vector3.Distance(actual, targetFrame.Center), angular, roll);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_playable.IsValid()) _playable.Destroy();
            DisposeIfCreated(ref _digitStatuses);
            DisposeIfCreated(ref _solvedCurls);
            DisposeIfCreated(ref _incomingRotations);
            DisposeIfCreated(ref _probeDiagnostics);
            DisposeIfCreated(ref _probes);
            DisposeIfCreated(ref _joints);
            DisposeIfCreated(ref _digits);
            _playable = default;
        }

        private float Curl(int index) => index >= 0 && index < _solvedCurls.Length ? _solvedCurls[index] : 0f;

        private void BuildBindings()
        {
            HandGripDigitProfile[] profileDigits = _profile.Digits;
            var orderedDigits = new HandGripDigitProfile[5];
            int jointCount = 0;
            int probeCount = 0;
            foreach (HandGripDigitProfile digit in profileDigits)
            {
                orderedDigits[(int)digit.Digit] = digit;
                jointCount += digit.JointPaths.Length;
                probeCount += digit.Probes.Length;
            }

            _digits = new NativeArray<HandGripJobDigit>(5, Allocator.Persistent);
            _joints = new NativeArray<HandGripJobJoint>(jointCount, Allocator.Persistent);
            _probes = new NativeArray<HandGripJobProbe>(probeCount, Allocator.Persistent);
            _incomingRotations = new NativeArray<Quaternion>(jointCount, Allocator.Persistent);
            _solvedCurls = new NativeArray<float>(5, Allocator.Persistent);
            _digitStatuses = new NativeArray<HandGripStatus>(5, Allocator.Persistent);
            _probeDiagnostics = new NativeArray<HandGripProbeDiagnostic>(probeCount, Allocator.Persistent);

            int nextJoint = 0;
            int nextProbe = 0;
            for (int digitIndex = 0; digitIndex < orderedDigits.Length; digitIndex++)
            {
                HandGripDigitProfile digit = orderedDigits[digitIndex];
                int digitJointStart = nextJoint;
                int digitProbeStart = nextProbe;
                for (int localIndex = 0; localIndex < digit.JointPaths.Length; localIndex++)
                {
                    Transform bone = FindByPath(_animator.transform, digit.JointPaths[localIndex]);
                    _joints[nextJoint] = new HandGripJobJoint
                    {
                        Handle = _animator.BindStreamTransform(bone),
                        DigitIndex = digitIndex,
                        LocalPosition = digit.JointLocalPositions[localIndex],
                        OpenLocalRotation = digit.OpenLocalRotations[localIndex],
                        ClosedLocalRotation = digit.ClosedLocalRotations[localIndex]
                    };
                    nextJoint++;
                }
                foreach (HandGripProbeDefinition definition in digit.Probes)
                {
                    _probes[nextProbe] = new HandGripJobProbe
                    {
                        DigitIndex = digitIndex,
                        JointIndex = digitJointStart + definition.jointIndex,
                        LocalPosition = definition.localPosition,
                        Radius = definition.radius
                    };
                    nextProbe++;
                }
                _digits[digitIndex] = new HandGripJobDigit
                {
                    JointStart = digitJointStart,
                    JointCount = digit.JointPaths.Length,
                    ProbeStart = digitProbeStart,
                    ProbeCount = digit.Probes.Length,
                    CurlBias = digit.CurlBias
                };
            }
        }

        private static Transform FindByPath(Transform root, string path)
        {
            Transform current = root;
            foreach (string segment in path.Split('/'))
            {
                current = current.Find(segment);
                if (current == null) return null;
            }
            return current;
        }

        private static void DisposeIfCreated<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated) array.Dispose();
            array = default;
        }
    }
}
