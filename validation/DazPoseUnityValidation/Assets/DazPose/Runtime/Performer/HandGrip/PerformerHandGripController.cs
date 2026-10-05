using System;
using UnityEngine;
using UnityEngine.Playables;

namespace DazPose.Performer.HandGrip
{
    public enum GripInteractionState { Idle, Planning, Walking, Acquiring, Active, Releasing, Failed }
    public enum GripCompletion { Acquired, Released, Superseded, Failed, PerformerDisabled }
    public enum GripFailureReason { None, InvalidProfile, InvalidTarget, NoReachableStagingPose,
        VerticalReachImpossible, LocomotionUnavailable, LocomotionSuperseded, TargetMovedBeyondPlan,
        PerformerDisabled, AcquisitionTimeout, BasePenetration }

    [DisallowMultipleComponent]
    [AddComponentMenu("Performer/Hand Grip/Performer Hand Grip Controller")]
    public sealed class PerformerHandGripController : MonoBehaviour
    {
        [SerializeField] private HandGripRigProfile profile;
        [SerializeField] private MonoBehaviour targetSource;
        [SerializeField] private bool gripOnEnable;
        [SerializeField, Range(0f, 1f)] private float gripStrength01 = 1f;
        [SerializeField] private bool drawDiagnostics = true;
        [SerializeField] private float wristTwistDegrees;
        private PerformerHandGripLayer _layer;
        private PerformerArmIKLayer _arm;
        private SuccubusPerformer _performer;
        private DazPose.Motion.MotionDriver _driver;
        private readonly GripReachPlanner _planner = new GripReachPlanner();
        private GripReachEnvelope _envelope;
        private IGripTarget _target;
        private GripFrame _frame;
        private GripPalmTarget _desired;
        private float _weight;
        private float _elapsed;
        private float _releaseStart;
        private float _position;
        private int _generation;
        private float _motionSuppression;
        public float MotionSuppression => _motionSuppression;
        private int _replans;
        private bool _reposition;
        private bool _failAfterRelease;
        private AwaitableCompletionSource<GripCompletion> _completion;
        public bool CalibrationMode { get; set; }
        public Vector4 CalibrationCurls { get; set; }
        public float CalibrationLittleCurl { get; set; }
        public GripInteractionState State { get; private set; }
        public GripFailureReason FailureReason { get; private set; }
        public GripReachPlan ReachPlan { get; private set; }
        public bool RequiredLocomotion { get; private set; }
        public IGripTarget Target => _target;
        public HandGripRigProfile Profile => profile;
        public bool IsAvailable => _arm != null && _layer != null;
        public bool IsGripRequested => State != GripInteractionState.Idle && State != GripInteractionState.Failed && State != GripInteractionState.Releasing;
        public bool IsGripping => State == GripInteractionState.Active;
        public bool OwnsArm => CalibrationMode || State == GripInteractionState.Acquiring || State == GripInteractionState.Active || State == GripInteractionState.Releasing;
        public bool DiagnosticsVisible { get => drawDiagnostics; set => drawDiagnostics = value; }
        public float GripWeight => _weight;
        public float Position01 => _position;
        public float WristTwistDegrees { get => wristTwistDegrees; set => wristTwistDegrees = value; }
        public float GripStrength01 { get => gripStrength01; set => gripStrength01 = Mathf.Clamp01(value); }
        public float ReachFraction => _arm == null ? 0f : Vector3.Distance(_arm.Upper.position, _desired.Wrist.position) / _envelope.ArmLength;
        public HandGripSolveResult SolveResult => _layer != null ? _layer.GetSolveResult()
            : new HandGripSolveResult(HandGripStatus.InvalidProfile, 0f, 0f, 0f, 0f, 0f);
        public HandGripAlignmentDiagnostics AlignmentDiagnostics
        {
            get
            {
                if (_arm == null || !_frame.IsValid) return default;
                Vector3 actual = _arm.Hand.TransformPoint(profile.PalmAnchorLocalPosition);
                Quaternion rotation = _arm.Hand.rotation * profile.PalmAnchorLocalRotation;
                return new HandGripAlignmentDiagnostics(_desired.Palm.position, actual,
                    Vector3.Distance(actual, _desired.Palm.position), Quaternion.Angle(rotation, _desired.Palm.rotation), 0f);
            }
        }

        internal Playable AttachToGraph(PlayableGraph graph, Playable input, Animator animator, DazPose.Motion.MotionDriver driver)
        {
            Detach();
            _performer = GetComponent<SuccubusPerformer>(); _driver = driver;
            if (profile == null || !profile.IsReady(animator, out _)) { FailureReason = GripFailureReason.InvalidProfile; return input; }
            try
            {
                _arm = new PerformerArmIKLayer(graph, input, animator);
                _envelope = new GripReachEnvelope(_arm.UpperLength, _arm.LowerLength,
                    profile.MinimumReachFraction, profile.MaximumReachFraction, profile.PreferredReachFraction);
                _layer = new PerformerHandGripLayer(graph, _arm.Output, animator, profile);
                if (gripOnEnable && targetSource is IGripTarget target) Grip(target);
                return _layer.OutputPlayable;
            }
            catch (Exception error)
            {
                Detach(); FailureReason = GripFailureReason.InvalidProfile;
                Debug.LogException(error, this); return input;
            }
        }

        public Awaitable<GripCompletion> GripAsync(IGripTarget target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            _generation++; Complete(GripCompletion.Superseded);
            var source = new AwaitableCompletionSource<GripCompletion>(); _completion = source;
            _target = target; targetSource = target as MonoBehaviour;
            _replans = 0; _failAfterRelease = false; RequiredLocomotion = false; FailureReason = GripFailureReason.None;
            _reposition = true;
            if (!IsAvailable) Fail(GripFailureReason.InvalidProfile);
            else if (_weight > 0f) BeginRelease(true);
            else { State = GripInteractionState.Planning; _elapsed = 0f; }
            return source.Awaitable;
        }
        public void Grip(IGripTarget target) { GripAsync(target); }
        public void Grip(float strength01 = 1f)
        {
            GripStrength01 = strength01;
            if (targetSource is IGripTarget target) Grip(target);
            else Fail(GripFailureReason.InvalidTarget);
        }
        public void Grip(MonoBehaviour target, float strength01 = 1f)
        {
            GripStrength01 = strength01;
            if (!(target is IGripTarget spatial)) throw new ArgumentException("Target must implement IGripTarget.");
            Grip(spatial);
        }
        public void ReleaseGrip()
        {
            bool wasWalking = State == GripInteractionState.Walking;
            _generation++; _failAfterRelease = false; Complete(GripCompletion.Released);
            if (wasWalking && _performer != null)
            {
                // Supersede only our walking request through the existing locomotion
                // alignment path; never move the root from the grip controller.
                _performer.WalkToGripAsync(transform.position, transform.forward);
            }
            if (IsAvailable) BeginRelease(false);
            else State = GripInteractionState.Idle;
        }
        private void BeginRelease(bool reposition)
        { _reposition = reposition; _releaseStart = _weight; _elapsed = 0f; State = GripInteractionState.Releasing; }

        private GripReachPlan Plan(bool search = true)
        {
            Pose root = new Pose(transform.position, transform.rotation);
            Vector3 shoulder = Quaternion.Inverse(root.rotation) * (_arm.Upper.position - root.position);
            return _planner.Plan(root, shoulder, _envelope, _target, profile.PalmAnchorLocalPosition,
                profile.PalmAnchorLocalRotation, profile.GripFrameCalibration, profile.PalmClearance, wristTwistDegrees, search);
        }
        internal void Advance(float delta)
        {
            if (!IsAvailable) return;
            _motionSuppression = Mathf.MoveTowards(_motionSuppression, OwnsArm ? 1f : 0f,
                Mathf.Max(0f, delta) / Mathf.Max(0.001f, profile.ReleaseSeconds));
            _layer.SetCalibration(CalibrationMode, CalibrationCurls, CalibrationLittleCurl);
            if (CalibrationMode) { SetArmWeight(0f); return; }
            float dt = Mathf.Max(0f, delta); _elapsed += dt;
            if (State == GripInteractionState.Planning)
            {
                ReachPlan = Plan();
                if (!ReachPlan.Valid)
                {
                    Fail(ReachPlan.Failure == GripPlanFailure.VerticalReachImpossible ? GripFailureReason.VerticalReachImpossible
                        : ReachPlan.Failure == GripPlanFailure.InvalidTarget ? GripFailureReason.InvalidTarget : GripFailureReason.NoReachableStagingPose);
                    return;
                }
                if (ReachPlan.RequiresWalking)
                {
                    RequiredLocomotion = true; State = GripInteractionState.Walking;
                    Walk(_generation, ReachPlan.Root); SetInactive(); return;
                }
                State = GripInteractionState.Acquiring; _elapsed = 0f;
            }
            if (State == GripInteractionState.Walking || State == GripInteractionState.Idle || State == GripInteractionState.Failed)
            { SetInactive(); return; }
            if (State == GripInteractionState.Releasing)
            {
                float t = profile.ReleaseSeconds <= 0f ? 1f : Mathf.Clamp01(_elapsed / profile.ReleaseSeconds);
                _weight = _releaseStart * (1f - Smooth(t));
                SetArmWeight(_weight);
                _layer.SetState(_frame.IsValid, _frame, _weight * Mathf.Clamp01(1f - t * 2f), gripStrength01, HandGripStatus.Clear);
                if (t >= 1f) { State = _failAfterRelease ? GripInteractionState.Failed
                    : _reposition ? GripInteractionState.Planning : GripInteractionState.Idle; _elapsed = 0f; }
                return;
            }
            ReachPlan = Plan(false);
            if (!ReachPlan.Valid || ReachPlan.RequiresWalking)
            {
                if (++_replans > 3) { Fail(GripFailureReason.TargetMovedBeyondPlan); return; }
                BeginRelease(true); return;
            }
            try
            {
                _position = _driver != null ? _driver.CurrentSample.Position01 : 0f;
                _frame = _target.Evaluate(_position); _desired = profile.EvaluatePalm(_frame, wristTwistDegrees);
                if (targetSource is GripContactRod rod) rod.CurrentPosition01 = _position;
            }
            catch (Exception error) { Debug.LogWarning(error.Message, this); Fail(GripFailureReason.InvalidTarget); return; }
            _weight = State == GripInteractionState.Active ? 1f
                : Smooth(profile.GripInSeconds <= 0f ? 1f : _elapsed / profile.GripInSeconds);
            SetArmWeight(_weight);
            float fingers = Mathf.Clamp01((_weight - 0.7f) / 0.3f);
            _layer.SetState(true, _frame, fingers, gripStrength01, HandGripStatus.Clear);
            if (State == GripInteractionState.Acquiring && _elapsed > profile.GripInSeconds)
            {
                var alignment = AlignmentDiagnostics;
                if (alignment.PositionErrorMeters <= 0.005f && alignment.OrientationErrorDegrees <= 5f
                    && SolveResult.Status != HandGripStatus.BasePenetration && SolveResult.Status != HandGripStatus.InvalidProfile)
                { State = GripInteractionState.Active; Complete(GripCompletion.Acquired); }
                else if (_elapsed > profile.GripInSeconds + 2f)
                    Fail(SolveResult.Status == HandGripStatus.BasePenetration ? GripFailureReason.BasePenetration : GripFailureReason.AcquisitionTimeout);
            }
        }
        private async void Walk(int generation, Pose root)
        {
            try
            {
                LocomotionCompletion result = await _performer.WalkToGripAsync(root.position, root.rotation * Vector3.forward);
                if (generation != _generation || State != GripInteractionState.Walking) return;
                if (result != LocomotionCompletion.Arrived)
                { Fail(result == LocomotionCompletion.Superseded ? GripFailureReason.LocomotionSuperseded : GripFailureReason.PerformerDisabled); return; }
                if (++_replans > 3) { Fail(GripFailureReason.TargetMovedBeyondPlan); return; }
                State = GripInteractionState.Planning; _elapsed = 0f;
            }
            catch (Exception error)
            { if (generation == _generation) { Debug.LogWarning(error.Message, this); Fail(GripFailureReason.LocomotionUnavailable); } }
        }
        private void SetArmWeight(float weight)
        {
            Vector3 hint = _arm.Upper.position + transform.rotation * profile.ElbowHintLocalDirection.normalized * _envelope.ArmLength;
            _arm.SetTarget(_frame.IsValid ? _desired.Wrist : new Pose(_arm.Hand.position, _arm.Hand.rotation), hint, weight);
        }
        private void SetInactive() { _weight = 0f; SetArmWeight(0f); _layer.SetState(false, default, 0f, gripStrength01, HandGripStatus.Clear); }
        private void Fail(GripFailureReason reason)
        {
            FailureReason = reason;
            if (IsAvailable && _weight > 0f)
            { BeginRelease(false); _failAfterRelease = true; }
            else { State = GripInteractionState.Failed; _reposition = false; if (IsAvailable) SetInactive(); }
            Complete(GripCompletion.Failed);
        }
        private void Complete(GripCompletion result) { var source = _completion; _completion = null; source?.TrySetResult(result); }
        internal void Detach()
        {
            _generation++; Complete(GripCompletion.PerformerDisabled);
            _layer?.Dispose(); _layer = null; _arm?.Dispose(); _arm = null;
            _weight = 0f; State = GripInteractionState.Idle;
        }
        public HandGripStatus GetDigitStatus(HandGripDigit digit) => _layer != null ? _layer.GetDigitStatus((int)digit) : HandGripStatus.InvalidProfile;
        public bool TryGetProbe(int index, out Vector3 worldPosition, out float radius, out HandGripProbeState state)
        {
            worldPosition = default; state = default; radius = _layer != null ? _layer.GetProbeRadius(index) : 0f;
            return _layer != null && _layer.TryGetProbeDiagnostic(index, out worldPosition, out state);
        }
        private void OnDrawGizmos()
        {
            if (!drawDiagnostics || !IsAvailable) return;
            Gizmos.color = ReachPlan.Valid ? Color.green : Color.red;
            Gizmos.DrawWireSphere(_arm.Upper.position, _envelope.Maximum);
            Gizmos.DrawLine(_arm.Upper.position, _arm.Lower.position); Gizmos.DrawLine(_arm.Lower.position, _arm.Hand.position);
            Gizmos.DrawSphere(_arm.HintPosition, 0.01f);
            if (_frame.IsValid)
            {
                Gizmos.DrawWireSphere(_desired.Palm.position, 0.01f);
                Gizmos.DrawLine(_desired.Palm.position, AlignmentDiagnostics.ActualGripCenter);
            }
            for (int index = 0; index < _layer.ProbeCount; index++)
                if (TryGetProbe(index, out Vector3 point, out float radius, out HandGripProbeState status))
                { Gizmos.color = status == HandGripProbeState.Penetrating ? Color.red : status == HandGripProbeState.NearContact ? Color.yellow : Color.green; Gizmos.DrawWireSphere(point, radius); }
            Gizmos.color = Color.cyan; Gizmos.DrawWireCube(ReachPlan.Root.position, new Vector3(0.2f, 0.02f, 0.2f));
            for (int index = 0; index < GripReachPlanner.SampleCount; index++) Gizmos.DrawSphere(_planner.GetSampleWrist(index), 0.005f);
        }
        private static float Smooth(float value) { float t = Mathf.Clamp01(value); return t * t * (3f - 2f * t); }
    }
}
