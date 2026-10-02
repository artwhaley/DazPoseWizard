using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer
{
    public enum PoseCompletion
    {
        Settled,
        Superseded,
        PerformerDisabled
    }

    public enum ExpressionCompletion
    {
        Settled,
        Superseded,
        PerformerDisabled
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("Performer/Succubus Performer")]
    public sealed class SuccubusPerformer : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private PerformerPose initialPose = null;
        [SerializeField] private PoseTransition defaultTransition = PoseTransition.Default;

        [Header("Expression")]
        [SerializeField] private PerformerExpression initialExpression = null;
        [SerializeField, Range(0f, 1f)] private float initialExpressionIntensity = 1f;
        [SerializeField, Min(0f)] private float defaultExpressionBlendTime = 0.25f;

        [Header("Speech")]
        [SerializeField] private AudioSource speechAudioSource;

        [Header("Locomotion")]
        [SerializeField] private PerformerLocomotionProfile locomotionProfile;

        [Header("Teleport")]
        [SerializeField] private PerformerTeleportProfile teleportProfile;

        [Header("Dissolve")]
        [SerializeField] private PerformerDissolveProfile dissolveProfile;
        [SerializeField] private PerformerDissolveRig dissolveRig;

        [Header("Breathing")]
        [SerializeField] private bool breathingEnabled = true;
        [SerializeField, Range(3f, 24f)] private float breathsPerMinute = 10f;
        [SerializeField] private AnimationCurve breathingCurve = CreateDefaultBreathingCurve();

        [Header("Morph Breathing")]
        [SerializeField] private bool morphBreathingEnabled = true;
        [SerializeField, Range(0f, 3f)] private float morphBreathingStrength = 0.25f;
        [SerializeField, Range(0f, 2f)] private float breatheStrength = 1f;
        [SerializeField, Range(0f, 2f)] private float breatheBellyStrength = 0.7f;

        [Header("Bone Breathing")]
        [SerializeField] private bool boneBreathingEnabled = true;
        [SerializeField, Range(0f, 2f)] private float boneBreathingStrength = 0.3f;
        [SerializeField] private BreathingBoneChannel[] breathingBones = CreateDefaultBreathingBones();

        [Header("Gaze")]
        [SerializeField] private bool gazeEnabled = true;
        [SerializeField, Range(0.1f, 10f)] private float gazeAcquireToleranceDegrees = 2f;
        [SerializeField] private bool headGazeEnabled = true;
        [SerializeField, Range(0f, 1f)] private float headGazeWeight = 0.7f;
        [SerializeField, Range(0f, 20f)] private float headGazeResponse = 4f;
        [SerializeField, Range(0f, 89f)] private float headGazeMaxYaw = 40f;
        [SerializeField, Range(0f, 89f)] private float headGazeMaxPitch = 25f;
        [SerializeField] private bool eyeGazeEnabled = true;
        [SerializeField, Range(0f, 1f)] private float eyeGazeWeight = 1f;
        [SerializeField, Range(0f, 30f)] private float eyeGazeResponse = 12f;
        [SerializeField, Range(0f, 89f)] private float eyeGazeMaxYaw = 32f;
        [SerializeField, Range(0f, 89f)] private float eyeGazeMaxPitch = 22f;
        [SerializeField, Range(0f, 20f)] private float gazeReleaseResponse = 4f;

        [Header("Attention Life")]
        [SerializeField] private bool attentionLifeEnabled = true;
        [SerializeField] private int attentionLifeSeed = 12345;
        [SerializeField] private bool eyeFixationLifeEnabled = true;
        [SerializeField, Range(0f, 8f)] private float eyeFixationMaxHorizontalDegrees = 0.9f;
        [SerializeField, Range(0f, 6f)] private float eyeFixationMaxVerticalDegrees = 0.6f;
        [SerializeField, Min(0.05f)] private float eyeFixationMinimumHoldSeconds = 2.2f;
        [SerializeField, Min(0.05f)] private float eyeFixationMaximumHoldSeconds = 4.5f;
        [SerializeField, Range(1f, 4f)] private float eyeFixationCenterBias = 2.4f;
        [SerializeField] private bool headAttentionLifeEnabled = true;
        [SerializeField, Range(0f, 15f), InspectorName("Head Attention Max Side Tilt Degrees")]
        private float headAttentionMaxTiltDegrees = 1.25f;
        [SerializeField, Range(0f, 12f), InspectorName("Head Attention Max Chin Nod Degrees")]
        private float headAttentionMaxChinDegrees = 1f;
        [SerializeField, Min(0.1f)] private float headAttentionMinimumHoldSeconds = 8f;
        [SerializeField, Min(0.1f)] private float headAttentionMaximumHoldSeconds = 16f;
        [SerializeField, Min(0f)] private float headAttentionTransitionResponse = 0.35f;

        [Header("Autonomous Blink")]
        [SerializeField] private bool blinkEnabled = true;
        [SerializeField, Range(0f, 1f)] private float blinkStrength = 1f;
        [SerializeField, Min(0.1f)] private float blinkMinimumIntervalSeconds = 3.5f;
        [SerializeField, Min(0.1f)] private float blinkMaximumIntervalSeconds = 6.5f;
        [SerializeField, Min(0.005f)] private float blinkCloseDurationSeconds = 0.08f;
        [SerializeField, Min(0.005f)] private float blinkClosedDurationSeconds = 0.045f;
        [SerializeField, Min(0.005f)] private float blinkOpenDurationSeconds = 0.13f;

        private PlayableGraph _graph;
        private PerformerBodyPose _bodyPose;
        private PerformerBodySourceMixer _bodySourceMixer;
        private PerformerLocomotion _locomotion;
        private PerformerTeleport _teleport;
        private PerformerDissolve _dissolve;
        private PerformerSeatingLayer _seatingLayer;
        private PerformerSeating _seating;
        private PerformerBreathing _breathing;
        private PerformerGaze _gaze;
        private PerformerAttentionLife _attentionLife;
        private PerformerBlink _blink;
        private PerformerExpressionLayer _expression;
        private PerformerSpeech _speech;
        private PerformerPose _lastDesiredPose;
        private PerformerPose _lastSettledPose;
        private PerformerPoseSnapshot _neutralPoseState;
        private float _lastBreathPhase;
        private PoseRequest _activePoseRequest;
        private long _nextPoseRequestId;
        private bool _hasPoseCommand;
        private bool _isTearingDown;
        private PerformerExpression _lastDesiredExpression;
        private PerformerExpression _lastSettledExpression;
        private float _lastDesiredExpressionIntensity;
        private float _lastSettledExpressionIntensity;
        private ExpressionRequest _activeExpressionRequest;
        private bool _hasExpressionCommand;
        private bool _hasSavedApplyRootMotion;
        private bool _savedApplyRootMotion;

        public PerformerPose SettledPose => _bodyPose != null ? _bodyPose.SettledPose : _lastSettledPose;
        public PerformerPose DesiredPose => _bodyPose != null ? _bodyPose.DesiredPose : _lastDesiredPose;
        public bool IsTransitioning => _bodyPose != null && _bodyPose.IsTransitioning;
        public bool IsRuntimeReady => Application.isPlaying && isActiveAndEnabled && !_isTearingDown
            && _bodyPose != null && _speech != null && _graph.IsValid();
        public bool LocomotionAvailable => _locomotion != null;
        public bool TeleportAvailable => _teleport != null && IsRuntimeReady;
        public bool IsTeleporting => _teleport != null && _teleport.IsTeleporting;
        public bool DissolveAvailable => _dissolve != null && IsRuntimeReady;
        public bool IsDissolving => _dissolve != null && _dissolve.IsDissolving;
        public bool DissolveShaderAcceptanceAvailable => IsRuntimeReady
            && dissolveProfile != null && dissolveRig != null
            && dissolveProfile.IsShaderReady(out _) && dissolveRig.IsShaderReady(dissolveProfile, out _);
        public bool IsLocomoting => _locomotion != null && _locomotion.IsLocomoting;
        public PerformerLocomotionState LocomotionState => _locomotion == null
            ? PerformerLocomotionState.Idle : _locomotion.State;
        public string LocomotionCurrentMotion => _locomotion == null || _locomotion.CurrentMotion == null
            ? "none" : _locomotion.CurrentMotion.name;
        public float LocomotionPlaybackTime => _locomotion == null ? 0f : _locomotion.PlaybackTime;
        public float LocomotionGaitPhase => _locomotion == null ? 0f : _locomotion.GaitPhase;
        public float LocomotionRemainingDistance => _locomotion == null ? 0f : _locomotion.RemainingDistance;
        public float LocomotionHeadingError => _locomotion == null ? 0f : _locomotion.HeadingError;
        public Vector3 LocomotionCurrentTarget => _locomotion == null ? default : _locomotion.CurrentTarget;
        public string LocomotionSelectedTurn => _locomotion == null ? "none" : _locomotion.SelectedTurn;
        public string LocomotionSelectedStop => _locomotion == null ? "none" : _locomotion.SelectedStopVariant;
        public float LocomotionPredictedStopDistance => _locomotion == null ? 0f : _locomotion.PredictedStopDistance;
        public Vector3 LocomotionPredictedStopEndpoint => _locomotion == null ? default : _locomotion.PredictedStopEndpoint;
        public Vector3 LocomotionEndpointCorrection => _locomotion == null ? default : _locomotion.EndpointCorrection;
        public float LocomotionArrivalBlendProgress => _locomotion == null ? 0f : _locomotion.ArrivalBlendProgress;
        public PerformerLocomotionProfile LocomotionProfile => locomotionProfile;
        public bool SeatingAvailable => _seating != null;
        public PerformerSeatingState SeatingState => _seating == null
            ? PerformerSeatingState.Standing : _seating.State;
        public PerformerSeat CurrentSeat => _seating == null ? null : _seating.CurrentSeat;
        public PerformerSeatedStyle CurrentSeatedStyle => _seating == null
            ? PerformerSeatedStyle.Basic : _seating.CurrentStyle;
        public string SeatingCurrentMotion => _seating == null || _seating.CurrentMotion == null
            ? "none" : _seating.CurrentMotion.name;
        public float SeatingMotionTime => _seating == null ? 0f : _seating.MotionTime;
        public float SeatingCrossLegsExitBlendProgress => _seating == null ? 0f : _seating.CrossLegsExitBlendProgress;
        public float SeatingCrossLegsExitBlendDuration => _seating == null ? 0.5f : _seating.CrossLegsExitBlendDuration;
        public float SeatingOwnershipWeight => _seating == null ? 0f : _seating.OwnershipWeight;
        public Vector3 SeatingContactError => _seating == null ? default : _seating.ContactError;
        public float TransitionProgress => _bodyPose == null ? 0f : _bodyPose.TransitionProgress;
        public PerformerExpression DesiredExpression => _lastDesiredExpression;
        public PerformerExpression SettledExpression => _lastSettledExpression;
        public float DesiredExpressionIntensity => _lastDesiredExpressionIntensity;
        public float SettledExpressionIntensity => _lastSettledExpressionIntensity;
        public bool IsExpressionTransitioning => _expression != null && _expression.IsTransitioning;
        public float ExpressionTransitionProgress => _expression == null ? 0f : _expression.EvaluatedProgress;
        public float DefaultExpressionBlendTime { get => defaultExpressionBlendTime; set => defaultExpressionBlendTime = Mathf.Max(0f, value); }
        public AudioSource SpeechAudioSource => speechAudioSource;
        public bool IsSpeaking => _speech != null && _speech.IsSpeaking;
        public AudioClip CurrentSpeechClip => _speech == null ? null : _speech.CurrentSpeechClip;
        public int PendingSpeechCount => _speech == null ? 0 : _speech.PendingSpeechCount;
        internal PerformerExpressionLayer ExpressionRuntime => _expression;
        public float TrajectoryProgress => _bodyPose == null ? 0f : _bodyPose.TrajectoryProgress;
        public PoseTransition ActiveTransition => _bodyPose == null ? default : _bodyPose.ActiveTransition;
        public bool BreathingEnabled { get => breathingEnabled; set => breathingEnabled = value; }
        public float BreathsPerMinute { get => breathsPerMinute; set => breathsPerMinute = Mathf.Max(0f, value); }
        public AnimationCurve BreathingCurve
        {
            get => breathingCurve;
            set => breathingCurve = value ?? CreateDefaultBreathingCurve();
        }
        public float BreathPhase => _breathing == null ? _lastBreathPhase : _breathing.BreathPhase;
        public float BreathValue => _breathing == null ? 0f : _breathing.BreathValue;
        public bool MorphBreathingEnabled { get => morphBreathingEnabled; set => morphBreathingEnabled = value; }
        public float MorphBreathingStrength { get => morphBreathingStrength; set => morphBreathingStrength = Mathf.Max(0f, value); }
        public float BreatheStrength { get => breatheStrength; set => breatheStrength = Mathf.Max(0f, value); }
        public float BreatheBellyStrength { get => breatheBellyStrength; set => breatheBellyStrength = Mathf.Max(0f, value); }
        public bool BoneBreathingEnabled { get => boneBreathingEnabled; set => boneBreathingEnabled = value; }
        public float BoneBreathingStrength { get => boneBreathingStrength; set => boneBreathingStrength = Mathf.Max(0f, value); }
        public bool GazeEnabled
        {
            get => gazeEnabled;
            set
            {
                if (gazeEnabled == value) return;
                gazeEnabled = value;
                _gaze?.Configure(CreateGazeSettings());
            }
        }
        public bool HeadGazeEnabled { get => headGazeEnabled; set => headGazeEnabled = value; }
        public float HeadGazeWeight { get => headGazeWeight; set => headGazeWeight = Mathf.Clamp01(value); }
        public float HeadGazeResponse { get => headGazeResponse; set => headGazeResponse = Mathf.Max(0f, value); }
        public float HeadGazeMaxYaw { get => headGazeMaxYaw; set => headGazeMaxYaw = Mathf.Clamp(value, 0f, 89f); }
        public float HeadGazeMaxPitch { get => headGazeMaxPitch; set => headGazeMaxPitch = Mathf.Clamp(value, 0f, 89f); }
        public bool EyeGazeEnabled { get => eyeGazeEnabled; set => eyeGazeEnabled = value; }
        public float EyeGazeWeight { get => eyeGazeWeight; set => eyeGazeWeight = Mathf.Clamp01(value); }
        public float EyeGazeResponse { get => eyeGazeResponse; set => eyeGazeResponse = Mathf.Max(0f, value); }
        public float EyeGazeMaxYaw { get => eyeGazeMaxYaw; set => eyeGazeMaxYaw = Mathf.Clamp(value, 0f, 89f); }
        public float EyeGazeMaxPitch { get => eyeGazeMaxPitch; set => eyeGazeMaxPitch = Mathf.Clamp(value, 0f, 89f); }
        public float GazeReleaseResponse { get => gazeReleaseResponse; set => gazeReleaseResponse = Mathf.Max(0f, value); }
        public float GazeAcquireToleranceDegrees
        {
            get => gazeAcquireToleranceDegrees;
            set => gazeAcquireToleranceDegrees = Mathf.Max(0.1f, value);
        }
        public bool AttentionLifeEnabled { get => attentionLifeEnabled; set => attentionLifeEnabled = value; }
        public int AttentionLifeSeed { get => attentionLifeSeed; set => attentionLifeSeed = value; }
        public bool EyeFixationLifeEnabled { get => eyeFixationLifeEnabled; set => eyeFixationLifeEnabled = value; }
        public float EyeFixationMaxHorizontalDegrees
        {
            get => eyeFixationMaxHorizontalDegrees;
            set => eyeFixationMaxHorizontalDegrees = Mathf.Clamp(value, 0f, 8f);
        }
        public float EyeFixationMaxVerticalDegrees
        {
            get => eyeFixationMaxVerticalDegrees;
            set => eyeFixationMaxVerticalDegrees = Mathf.Clamp(value, 0f, 6f);
        }
        public float EyeFixationMinimumHoldSeconds
        {
            get => eyeFixationMinimumHoldSeconds;
            set => eyeFixationMinimumHoldSeconds = Mathf.Max(0.05f, value);
        }
        public float EyeFixationMaximumHoldSeconds
        {
            get => eyeFixationMaximumHoldSeconds;
            set => eyeFixationMaximumHoldSeconds = Mathf.Max(eyeFixationMinimumHoldSeconds, value);
        }
        public float EyeFixationCenterBias
        {
            get => eyeFixationCenterBias;
            set => eyeFixationCenterBias = Mathf.Clamp(value, 1f, 4f);
        }
        public bool HeadAttentionLifeEnabled { get => headAttentionLifeEnabled; set => headAttentionLifeEnabled = value; }
        public float HeadAttentionMaxTiltDegrees
        {
            get => headAttentionMaxTiltDegrees;
            set => headAttentionMaxTiltDegrees = Mathf.Clamp(value, 0f, 15f);
        }
        public float HeadAttentionMaxChinDegrees
        {
            get => headAttentionMaxChinDegrees;
            set => headAttentionMaxChinDegrees = Mathf.Clamp(value, 0f, 12f);
        }
        public float HeadAttentionMinimumHoldSeconds
        {
            get => headAttentionMinimumHoldSeconds;
            set => headAttentionMinimumHoldSeconds = Mathf.Max(0.1f, value);
        }
        public float HeadAttentionMaximumHoldSeconds
        {
            get => headAttentionMaximumHoldSeconds;
            set => headAttentionMaximumHoldSeconds = Mathf.Max(headAttentionMinimumHoldSeconds, value);
        }
        public float HeadAttentionTransitionResponse
        {
            get => headAttentionTransitionResponse;
            set => headAttentionTransitionResponse = Mathf.Max(0f, value);
        }
        public bool BlinkEnabled { get => blinkEnabled; set => blinkEnabled = value; }
        public float BlinkStrength { get => blinkStrength; set => blinkStrength = Mathf.Clamp01(value); }
        public float BlinkMinimumIntervalSeconds
        {
            get => blinkMinimumIntervalSeconds;
            set => blinkMinimumIntervalSeconds = Mathf.Max(0.1f, value);
        }
        public float BlinkMaximumIntervalSeconds
        {
            get => blinkMaximumIntervalSeconds;
            set => blinkMaximumIntervalSeconds = Mathf.Max(blinkMinimumIntervalSeconds, value);
        }
        public float BlinkCloseDurationSeconds
        {
            get => blinkCloseDurationSeconds;
            set => blinkCloseDurationSeconds = Mathf.Max(0.005f, value);
        }
        public float BlinkClosedDurationSeconds
        {
            get => blinkClosedDurationSeconds;
            set => blinkClosedDurationSeconds = Mathf.Max(0.005f, value);
        }
        public float BlinkOpenDurationSeconds
        {
            get => blinkOpenDurationSeconds;
            set => blinkOpenDurationSeconds = Mathf.Max(0.005f, value);
        }
        public float EyeFixationHorizontalOffset => _attentionLife == null
            ? 0f : _attentionLife.CurrentOutput.EyeHorizontalOffsetDegrees;
        public float EyeFixationVerticalOffset => _attentionLife == null
            ? 0f : _attentionLife.CurrentOutput.EyeVerticalOffsetDegrees;
        public float HeadAttentionTilt => _attentionLife == null ? 0f : _attentionLife.CurrentOutput.HeadTiltDegrees;
        public float HeadAttentionChin => _attentionLife == null ? 0f : _attentionLife.CurrentOutput.HeadChinDegrees;
        public float EyeFixationEventCountdown => _attentionLife == null ? 0f : _attentionLife.EyeEventCountdown;
        public float HeadAttentionEventCountdown => _attentionLife == null ? 0f : _attentionLife.HeadEventCountdown;
        public float BlinkClosure => _blink == null ? 0f : _blink.Closure;
        public float BlinkCountdown => _blink == null ? 0f : _blink.Countdown;
        public string BlinkState => _blink == null ? "Unavailable" : _blink.State.ToString();
        public bool BlinkResolutionAvailable => _blink != null && _blink.IsAvailable;
        public bool HasGazeTarget => _gaze != null && _gaze.HasGazeTarget;
        public bool IsGazeAcquired => _gaze != null && _gaze.IsGazeAcquired;
        public float GazeWeight => _gaze == null ? 0f : _gaze.GazeWeight;
        public string GazeTargetDescription => _gaze == null ? "none" : _gaze.TargetDescription;
        public Vector3 RawGazeTargetPosition => _gaze == null ? default : _gaze.RawTargetPosition;
        internal Vector3 EffectiveHeadTargetPosition => _gaze == null ? default : _gaze.EffectiveHeadTargetPosition;
        internal Vector3 EffectiveEyeTargetPosition => _gaze == null ? default : _gaze.EffectiveEyeTargetPosition;

        internal PerformerBreathing BreathingRuntime => _breathing;
        internal PerformerGaze GazeRuntime => _gaze;
        internal PerformerAttentionLife AttentionLifeRuntime => _attentionLife;
        internal PerformerBlink BlinkRuntime => _blink;
        internal int RuntimePlayableCount => _graph.IsValid() ? _graph.GetPlayableCount() : 0;

        private void OnEnable()
        {
            _isTearingDown = false;
            if (Application.isPlaying) CreateRuntime();
        }

        private void Update()
        {
            _speech?.Advance();
            if (_bodyPose == null) return;

            var request = _activePoseRequest;
            _bodyPose.Advance(Time.deltaTime);
            _locomotion?.Advance(Time.deltaTime);
            _seating?.Advance(Time.deltaTime);
            _teleport?.Advance(Time.deltaTime);
            _dissolve?.Advance(Time.deltaTime);
            if (_breathing != null)
            {
                _breathing.Configure(CreateBreathingSettings());
                _breathing.Advance(Time.deltaTime);
                _lastBreathPhase = _breathing.BreathPhase;
            }
            if (_attentionLife != null)
            {
                _attentionLife.Configure(CreateAttentionLifeSettings());
                _attentionLife.Advance(Time.deltaTime);
            }
            if (_gaze != null)
            {
                _gaze.Configure(CreateGazeSettings());
                _gaze.Advance(Time.deltaTime);
            }
            var expressionRequest = _activeExpressionRequest;
            if (_expression != null) _expression.Advance(Time.deltaTime);
            if (_blink != null)
            {
                _blink.Configure(CreateBlinkSettings());
                _blink.Advance(Time.deltaTime);
            }
            PublishCurrentPoseState();
            if (expressionRequest != null && ReferenceEquals(_activeExpressionRequest, expressionRequest)
                && !_expression.IsTransitioning)
            {
                _lastSettledExpression = expressionRequest.Expression;
                _lastSettledExpressionIntensity = expressionRequest.Intensity;
                _activeExpressionRequest = null;
                CompleteExpressionRequest(expressionRequest, ExpressionCompletion.Settled);
            }
            if (request == null || !ReferenceEquals(_activePoseRequest, request)
                || _bodyPose.IsTransitioning || _bodyPose.SettledPose != request.Pose) return;

            _activePoseRequest = null;
            CompleteRequest(request, PoseCompletion.Settled);
        }

        private void OnDisable()
        {
            DestroyRuntime();
        }

        private void OnDestroy()
        {
            DestroyRuntime();
        }

        public void Pose(PerformerPose pose)
        {
            Pose(pose, defaultTransition);
        }

        public void Pose(PerformerPose pose, PoseTransition transition)
        {
            RequestPose(pose, transition, null);
        }

        public Awaitable<PoseCompletion> PoseAsync(PerformerPose pose)
        {
            return PoseAsync(pose, defaultTransition);
        }

        public Awaitable<PoseCompletion> PoseAsync(PerformerPose pose, PoseTransition transition)
        {
            var completion = new AwaitableCompletionSource<PoseCompletion>();
            RequestPose(pose, transition, completion);
            return completion.Awaitable;
        }

        public void TeleportTo(Vector3 worldPosition) => TeleportTo(worldPosition, null);

        public void TeleportTo(Transform target) => TeleportTo(target, null);

        public void TeleportTo(Vector3 worldPosition, PerformerPose arrivalPose) =>
            RequireTeleportRuntime().TeleportTo(worldPosition, arrivalPose, null);

        public void TeleportTo(Transform target, PerformerPose arrivalPose) =>
            RequireTeleportRuntime().TeleportTo(target, arrivalPose, null);

        public Awaitable<TeleportCompletion> TeleportToAsync(Vector3 worldPosition) =>
            TeleportToAsync(worldPosition, null);

        public Awaitable<TeleportCompletion> TeleportToAsync(Transform target) =>
            TeleportToAsync(target, null);

        public Awaitable<TeleportCompletion> TeleportToAsync(Vector3 worldPosition, PerformerPose arrivalPose)
        {
            var completion = new AwaitableCompletionSource<TeleportCompletion>();
            RequireTeleportRuntime().TeleportTo(worldPosition, arrivalPose, completion);
            return completion.Awaitable;
        }

        public Awaitable<TeleportCompletion> TeleportToAsync(Transform target, PerformerPose arrivalPose)
        {
            var completion = new AwaitableCompletionSource<TeleportCompletion>();
            RequireTeleportRuntime().TeleportTo(target, arrivalPose, completion);
            return completion.Awaitable;
        }

        public void DissolveTo(Vector3 worldPosition) => DissolveTo(worldPosition, null);

        public void DissolveTo(Transform target) => DissolveTo(target, null);

        public void DissolveTo(Vector3 worldPosition, PerformerPose arrivalPose) =>
            RequireDissolveRuntime().DissolveTo(worldPosition, arrivalPose, null);

        public void DissolveTo(Transform target, PerformerPose arrivalPose) =>
            RequireDissolveRuntime().DissolveTo(target, arrivalPose, null);

        public Awaitable<DissolveCompletion> DissolveToAsync(Vector3 worldPosition) =>
            DissolveToAsync(worldPosition, null);

        public Awaitable<DissolveCompletion> DissolveToAsync(Transform target) =>
            DissolveToAsync(target, null);

        /// <summary>Sets the shader-only acceptance state without invoking the P0.G DissolveTo sequence.</summary>
        public void SetDissolveShaderAcceptanceState(bool enabled, float progress)
        {
            if (!IsRuntimeReady)
                throw new InvalidOperationException("Dissolve shader acceptance is available only while the performer is enabled in Play Mode.");
            if (_dissolve != null && _dissolve.IsDissolving)
                throw new InvalidOperationException("Shader acceptance controls cannot run while DissolveTo is active.");
            if (float.IsNaN(progress) || float.IsInfinity(progress))
                throw new ArgumentOutOfRangeException(nameof(progress), "Dissolve progress must be finite.");
            if (dissolveRig == null)
                throw new InvalidOperationException("The native dissolve shader rig is not assigned.");
            if (!dissolveRig.IsShaderReady(dissolveProfile, out string reason))
                throw new InvalidOperationException(reason);

            dissolveRig.SetDissolveEnabled(enabled);
            dissolveRig.SetDissolveProgress(Mathf.Clamp01(progress));
        }

        public Awaitable<DissolveCompletion> DissolveToAsync(Vector3 worldPosition, PerformerPose arrivalPose)
        {
            var completion = new AwaitableCompletionSource<DissolveCompletion>();
            RequireDissolveRuntime().DissolveTo(worldPosition, arrivalPose, completion);
            return completion.Awaitable;
        }

        public Awaitable<DissolveCompletion> DissolveToAsync(Transform target, PerformerPose arrivalPose)
        {
            var completion = new AwaitableCompletionSource<DissolveCompletion>();
            RequireDissolveRuntime().DissolveTo(target, arrivalPose, completion);
            return completion.Awaitable;
        }

        public void Expression(PerformerExpression expression) => Expression(expression, 1f, defaultExpressionBlendTime);
        public void Expression(PerformerExpression expression, float intensity) => Expression(expression, intensity, defaultExpressionBlendTime);
        public void Expression(PerformerExpression expression, float intensity, float blendTime) =>
            RequestExpression(expression, intensity, blendTime, null);

        public Awaitable<ExpressionCompletion> ExpressionAsync(PerformerExpression expression) =>
            ExpressionAsync(expression, 1f, defaultExpressionBlendTime);
        public Awaitable<ExpressionCompletion> ExpressionAsync(PerformerExpression expression, float intensity) =>
            ExpressionAsync(expression, intensity, defaultExpressionBlendTime);
        public Awaitable<ExpressionCompletion> ExpressionAsync(PerformerExpression expression, float intensity, float blendTime)
        {
            var completion = new AwaitableCompletionSource<ExpressionCompletion>();
            RequestExpression(expression, intensity, blendTime, completion);
            return completion.Awaitable;
        }

        public void ClearExpression() => ClearExpression(defaultExpressionBlendTime);
        public void ClearExpression(float blendTime) => RequestExpression(null, 0f, blendTime, null);

        public void Say(AudioClip clip)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            RequireSpeechRuntime().Say(clip);
        }

        public Awaitable<SpeechCompletion> SayAsync(AudioClip clip)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            return RequireSpeechRuntime().SayAsync(clip);
        }

        public void StopSpeaking()
        {
            RequireSpeechRuntime().StopSpeaking();
        }

        public void LookAt(Transform target)
        {
            RequestGaze(target, null);
        }

        public void LookAt(Vector3 worldPosition)
        {
            RequestGaze(worldPosition, null);
        }

        public Awaitable<GazeCompletion> LookAtAsync(Transform target)
        {
            var completion = new AwaitableCompletionSource<GazeCompletion>();
            RequestGaze(target, completion);
            return completion.Awaitable;
        }

        public Awaitable<GazeCompletion> LookAtAsync(Vector3 worldPosition)
        {
            var completion = new AwaitableCompletionSource<GazeCompletion>();
            RequestGaze(worldPosition, completion);
            return completion.Awaitable;
        }

        public void ClearGaze()
        {
            _gaze?.ClearGaze();
        }

        public void WalkTo(Vector3 worldPosition)
        {
            PerformerLocomotion runtime = RequireLocomotionRuntime();
            _seating?.PrepareForWalkRequest();
            runtime.WalkTo(worldPosition);
        }

        /// <summary>Retain the arrived body pose across an authored movement chain until released.</summary>
        public void SetHoldLocomotionArrivalPose(bool hold) => RequireLocomotionRuntime().SetHoldArrivalPose(hold);

        public void WalkTo(Transform target)
        {
            PerformerLocomotion runtime = RequireLocomotionRuntime();
            _seating?.PrepareForWalkRequest();
            runtime.WalkTo(target);
        }

        public Awaitable<LocomotionCompletion> WalkToAsync(Vector3 worldPosition)
        {
            PerformerLocomotion runtime = RequireLocomotionRuntime();
            _seating?.PrepareForWalkRequest();
            return runtime.WalkToAsync(worldPosition);
        }

        public Awaitable<LocomotionCompletion> WalkToAsync(Transform target)
        {
            PerformerLocomotion runtime = RequireLocomotionRuntime();
            _seating?.PrepareForWalkRequest();
            return runtime.WalkToAsync(target);
        }

        public void SitAt(PerformerSeat seat) => RequireSeatingRuntime().SitAt(seat, null, null);

        public void SitAt(PerformerSeat seat, PerformerSeatedStyle style) =>
            RequireSeatingRuntime().SitAt(seat, style, null);

        public Awaitable<SeatingCompletion> SitAtAsync(PerformerSeat seat)
        {
            var completion = new AwaitableCompletionSource<SeatingCompletion>();
            RequireSeatingRuntime().SitAt(seat, null, completion);
            return completion.Awaitable;
        }

        public Awaitable<SeatingCompletion> SitAtAsync(PerformerSeat seat, PerformerSeatedStyle style)
        {
            var completion = new AwaitableCompletionSource<SeatingCompletion>();
            RequireSeatingRuntime().SitAt(seat, style, completion);
            return completion.Awaitable;
        }

        public void StandUp() => RequireSeatingRuntime().StandUp(null);

        public Awaitable<SeatingCompletion> StandUpAsync()
        {
            var completion = new AwaitableCompletionSource<SeatingCompletion>();
            RequireSeatingRuntime().StandUp(completion);
            return completion.Awaitable;
        }

        internal void SetBasicIdleLoopCandidateForAcceptance(bool enabled) =>
            RequireSeatingRuntime().SetBasicIdleLoopCandidate(enabled);

        internal PerformerPoseSnapshot CaptureEvaluatedBasePoseState()
        {
            if (_bodyPose == null) throw new InvalidOperationException("The performer runtime is not active.");
            return _bodyPose.CaptureEvaluatedBaseState();
        }

        internal PerformerPoseSnapshot CaptureTransitionSourcePoseState()
        {
            if (_bodyPose == null) throw new InvalidOperationException("The performer runtime is not active.");
            return _bodyPose.CaptureTransitionSourceState();
        }

        internal void SetBreathPhaseForAcceptance(float phase)
        {
            if (_breathing == null) throw new InvalidOperationException("The performer breathing runtime is not active.");
            _breathing.Configure(CreateBreathingSettings());
            _breathing.SetPhaseForAcceptance(phase);
            _lastBreathPhase = _breathing.BreathPhase;
            if (_graph.IsValid()) _graph.Evaluate(0f);
        }

        private void RequestPose(PerformerPose pose, PoseTransition transition,
            AwaitableCompletionSource<PoseCompletion> completion)
        {
            if (_bodyPose == null || _isTearingDown || !isActiveAndEnabled)
                throw new InvalidOperationException("SuccubusPerformer can receive pose commands only while enabled in Play Mode.");
            if (pose == null) throw new ArgumentNullException(nameof(pose));

            var previousDesired = _bodyPose.DesiredPose;
            if (pose == previousDesired)
            {
                _bodyPose.SetPose(pose, transition);
                PublishCurrentPoseState();
                _hasPoseCommand = true;
                if (_bodyPose.IsTransitioning)
                {
                    if (_activePoseRequest == null || _activePoseRequest.Pose != pose)
                        _activePoseRequest = new PoseRequest(++_nextPoseRequestId, pose);
                    if (completion != null) _activePoseRequest.Waiters.Add(completion);
                    return;
                }

                var settledRequest = _activePoseRequest != null && _activePoseRequest.Pose == pose
                    ? _activePoseRequest
                    : null;
                _activePoseRequest = null;
                if (completion != null)
                {
                    if (settledRequest != null) settledRequest.Waiters.Add(completion);
                    else completion.TrySetResult(PoseCompletion.Settled);
                }
                if (settledRequest != null) CompleteRequest(settledRequest, PoseCompletion.Settled);
                return;
            }

            var supersededRequest = _activePoseRequest;
            var newRequest = new PoseRequest(++_nextPoseRequestId, pose);
            if (completion != null) newRequest.Waiters.Add(completion);

            _bodyPose.SetPose(pose, transition);
            PublishCurrentPoseState();
            _hasPoseCommand = true;
            _activePoseRequest = _bodyPose.IsTransitioning ? newRequest : null;

            if (!_bodyPose.IsTransitioning)
                CompleteRequest(newRequest, PoseCompletion.Settled);

            // Completing a Unity Awaitable can resume its caller inline. All pose state,
            // the new generation, and the published facade values are coherent first.
            if (supersededRequest != null)
                CompleteRequest(supersededRequest, PoseCompletion.Superseded);
        }

        private void RequestGaze(Transform target, AwaitableCompletionSource<GazeCompletion> completion)
        {
            if (_gaze == null || _isTearingDown || !isActiveAndEnabled)
                throw new InvalidOperationException("SuccubusPerformer can receive gaze commands only while enabled in Play Mode.");
            _gaze.LookAt(target, completion);
        }

        private void RequestExpression(PerformerExpression expression, float intensity, float blendTime,
            AwaitableCompletionSource<ExpressionCompletion> completion)
        {
            if (_expression == null || _isTearingDown || !isActiveAndEnabled)
                throw new InvalidOperationException("SuccubusPerformer can receive Expression commands only while enabled in Play Mode.");
            if (!IsFinite(blendTime))
                throw new ArgumentOutOfRangeException(nameof(blendTime), "Expression blend time must be finite.");
            if (expression != null && !IsFinite(intensity))
                throw new ArgumentOutOfRangeException(nameof(intensity), "Expression intensity must be finite.");
            intensity = Mathf.Clamp01(intensity);
            blendTime = Mathf.Max(0f, blendTime);
            var sameDesired = expression == _lastDesiredExpression && Mathf.Abs(intensity - _lastDesiredExpressionIntensity) <= 0.00001f;
            if (sameDesired)
            {
                if (_expression.IsTransitioning)
                {
                    if (completion != null && _activeExpressionRequest != null) _activeExpressionRequest.Waiters.Add(completion);
                }
                else if (completion != null) completion.TrySetResult(ExpressionCompletion.Settled);
                return;
            }

            var previous = _activeExpressionRequest;
            if (expression == null) _expression.Clear(blendTime);
            else _expression.SetExpression(expression, intensity, blendTime);
            _lastDesiredExpression = expression;
            _lastDesiredExpressionIntensity = expression == null ? 0f : intensity;
            _hasExpressionCommand = true;
            var request = new ExpressionRequest(expression, _lastDesiredExpressionIntensity);
            if (completion != null) request.Waiters.Add(completion);
            _activeExpressionRequest = _expression.IsTransitioning ? request : null;
            if (!_expression.IsTransitioning)
            {
                _lastSettledExpression = expression;
                _lastSettledExpressionIntensity = _lastDesiredExpressionIntensity;
                CompleteExpressionRequest(request, ExpressionCompletion.Settled);
            }
            if (previous != null) CompleteExpressionRequest(previous, ExpressionCompletion.Superseded);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void CompleteExpressionRequest(ExpressionRequest request, ExpressionCompletion result)
        {
            var waiters = request.Waiters.ToArray(); request.Waiters.Clear();
            foreach (var waiter in waiters) waiter.TrySetResult(result);
        }

        private sealed class ExpressionRequest
        {
            public ExpressionRequest(PerformerExpression expression, float intensity) { Expression = expression; Intensity = intensity; }
            public PerformerExpression Expression { get; }
            public float Intensity { get; }
            public List<AwaitableCompletionSource<ExpressionCompletion>> Waiters { get; } = new List<AwaitableCompletionSource<ExpressionCompletion>>();
        }

        private void RequestGaze(Vector3 worldPosition, AwaitableCompletionSource<GazeCompletion> completion)
        {
            if (_gaze == null || _isTearingDown || !isActiveAndEnabled)
                throw new InvalidOperationException("SuccubusPerformer can receive gaze commands only while enabled in Play Mode.");
            _gaze.LookAt(worldPosition, completion);
        }

        private void PublishCurrentPoseState()
        {
            if (_bodyPose == null) return;
            _lastDesiredPose = _bodyPose.DesiredPose;
            _lastSettledPose = _bodyPose.SettledPose;
        }

        private static void CompleteRequest(PoseRequest request, PoseCompletion result)
        {
            var waiters = request.Waiters.ToArray();
            request.Waiters.Clear();
            foreach (var waiter in waiters) waiter.TrySetResult(result);
        }

        private sealed class PoseRequest
        {
            public PoseRequest(long id, PerformerPose pose)
            {
                Id = id;
                Pose = pose;
            }

            public long Id { get; }
            public PerformerPose Pose { get; }
            public List<AwaitableCompletionSource<PoseCompletion>> Waiters { get; } = new List<AwaitableCompletionSource<PoseCompletion>>();
        }

        private void CreateRuntime()
        {
            DestroyRuntime();
            _isTearingDown = false;
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null)
                throw new InvalidOperationException("SuccubusPerformer needs an Animator on this GameObject or assigned in its Inspector.");
            if (locomotionProfile != null && animator.transform != transform)
                throw new InvalidOperationException("The baked KAWAII Generic locomotion profile expects the SuccubusPerformer and Animator on the same model root.");

            try
            {
                if (locomotionProfile != null)
                {
                    _savedApplyRootMotion = animator.applyRootMotion;
                    _hasSavedApplyRootMotion = true;
                    animator.applyRootMotion = false;
                }
                speechAudioSource = ResolveSpeechAudioSource();
                _speech = new PerformerSpeech(speechAudioSource);

                _graph = PlayableGraph.Create("Succubus Performer - " + name);
                _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                _bodyPose = new PerformerBodyPose(animator, _graph, _neutralPoseState,
                    animateActorRoot: locomotionProfile == null);
                _neutralPoseState = _bodyPose.NeutralState;
                Playable bodySource = _bodyPose.OutputPlayable;
                if (locomotionProfile != null)
                {
                    _bodySourceMixer = new PerformerBodySourceMixer(animator, _graph, bodySource, locomotionProfile);
                    _locomotion = new PerformerLocomotion(transform, locomotionProfile, _bodySourceMixer);
                    bodySource = _bodySourceMixer.Output;
                }
                _seatingLayer = new PerformerSeatingLayer(_graph, bodySource);
                _seating = new PerformerSeating(transform, _locomotion, _seatingLayer);
                bodySource = _seatingLayer.Output;
                _breathing = new PerformerBreathing(animator, _graph, bodySource,
                    _bodyPose, breathingBones, _lastBreathPhase);
                _breathing.Configure(CreateBreathingSettings());
                _breathing.Advance(0f);

                _gaze = new PerformerGaze(animator, _graph, _breathing.OutputPlayable);
                _attentionLife = new PerformerAttentionLife(_gaze.HeadCalibration.LocalAim,
                    _gaze.HeadCalibration.LocalRight, CreateAttentionLifeSettings());
                _gaze.AttachAttentionLife(_attentionLife);
                _gaze.Configure(CreateGazeSettings());
                _attentionLife.Advance(0f);
                _gaze.Advance(0f);

                _expression = new PerformerExpressionLayer(animator, _graph, _gaze.OutputPlayable);
                var expressionToRestore = _hasExpressionCommand ? _lastDesiredExpression : initialExpression;
                var intensityToRestore = _hasExpressionCommand ? _lastDesiredExpressionIntensity : initialExpressionIntensity;
                if (expressionToRestore != null)
                {
                    _expression.SetExpression(expressionToRestore, intensityToRestore, 0f);
                    _lastDesiredExpression = _lastSettledExpression = expressionToRestore;
                    _lastDesiredExpressionIntensity = _lastSettledExpressionIntensity = intensityToRestore;
                }
                else if (_hasExpressionCommand)
                {
                    _lastSettledExpression = null;
                    _lastSettledExpressionIntensity = 0f;
                }

                _blink = new PerformerBlink(animator, _graph, _expression.OutputPlayable,
                    _bodyPose, CreateBlinkSettings());
                _blink.Advance(0f);

                var output = AnimationPlayableOutput.Create(_graph, "Performer Animation", animator);
                output.SetSourcePlayable(_blink.OutputPlayable);
                _graph.Play();

                var poseToRestore = _hasPoseCommand ? _lastDesiredPose : initialPose;
                if (poseToRestore != null)
                    _bodyPose.SetPose(poseToRestore, PoseTransition.Snap);
                _graph.Evaluate(0f);
                if (teleportProfile != null)
                    _teleport = new PerformerTeleport(transform, teleportProfile,
                        pose => Pose(pose, PoseTransition.Snap));

                if (dissolveProfile != null && dissolveRig != null)
                {
                    if (dissolveProfile.IsReady(out string dissolveReason)
                        && dissolveRig.IsReady(dissolveProfile, out dissolveReason))
                    {
                        _dissolve = new PerformerDissolve(transform, dissolveProfile, dissolveRig,
                            pose => Pose(pose, PoseTransition.Snap));
                    }
                    else
                    {
                        Debug.LogWarning("DissolveTo is unavailable: " + dissolveReason, this);
                    }
                }
            }
            catch
            {
                DestroyRuntime();
                throw;
            }
        }

        private AudioSource ResolveSpeechAudioSource()
        {
            if (speechAudioSource == null)
            {
                var skeleton = FindUniqueChildTransform(transform, "Genesis8Female");
                var head = FindUniqueChildTransform(skeleton, "head");
                speechAudioSource = FindHeadSpeechAudioSource(head);
            }

            if (speechAudioSource == null)
                throw new InvalidOperationException("No dedicated speech AudioSource is assigned or present beneath the performer's head. In Edit Mode, select the Lara root, run Tools > DAZ Pose > Development > Setup or Refresh Performer Pose Acceptance Harness, then save PoseValidation.unity.");
            if (speechAudioSource.transform == transform || !speechAudioSource.transform.IsChildOf(transform))
                throw new InvalidOperationException("The dedicated speech AudioSource must be a child of SuccubusPerformer so it follows the performer.");

            var underHead = false;
            for (var current = speechAudioSource.transform.parent; current != null && current != transform; current = current.parent)
                if (current.name == "head") { underHead = true; break; }
            if (!underHead)
                throw new InvalidOperationException("The dedicated speech AudioSource must be on a child transform beneath the performer's head bone so spatial audio follows the mouth.");
            return speechAudioSource;
        }

        private static AudioSource FindHeadSpeechAudioSource(Transform head)
        {
            AudioSource match = null;
            foreach (var child in head.GetComponentsInChildren<Transform>(true))
            {
                if (child == head || (child.name != "SpeechAudio" && child.name != "VoiceAudio"
                    && child.name != "VoiceAnchor" && child.name != "Voice" && child.name != "MouthAudio")) continue;
                var candidate = child.GetComponent<AudioSource>();
                if (candidate == null) continue;
                if (match != null)
                    throw new InvalidOperationException("SuccubusPerformer found multiple dedicated AudioSources beneath the head. Assign the intended SpeechAudioSource explicitly.");
                match = candidate;
            }
            return match;
        }

        private static Transform FindUniqueChildTransform(Transform root, string exactName)
        {
            Transform match = null;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != exactName) continue;
                if (match != null)
                    throw new InvalidOperationException("SuccubusPerformer needs one unique '" + exactName
                        + "' transform beneath '" + root.name + "' to place its speech AudioSource.");
                match = child;
            }
            if (match == null)
                throw new InvalidOperationException("SuccubusPerformer could not find '" + exactName
                    + "' beneath '" + root.name + "' to place its speech AudioSource.");
            return match;
        }

        private PerformerSpeech RequireSpeechRuntime()
        {
            if (_speech == null || _isTearingDown || !isActiveAndEnabled)
                throw new InvalidOperationException("SuccubusPerformer can receive speech commands only while enabled in Play Mode.");
            return _speech;
        }

        private PerformerLocomotion RequireLocomotionRuntime()
        {
            if (IsTeleporting)
                throw new InvalidOperationException("WalkTo is unavailable while a teleport is in progress.");
            if (IsDissolving)
                throw new InvalidOperationException("WalkTo is unavailable while a dissolve is in progress.");
            if (_locomotion == null || _isTearingDown || !isActiveAndEnabled)
            {
                string setup = locomotionProfile == null
                    ? "Assign the generated KawaiiWalk01Profile in the Locomotion section after running Tools > DAZ Pose > Locomotion > Bake KAWAII Walk01 for Generic Lara."
                    : "SuccubusPerformer locomotion is available only while enabled in Play Mode and after its profile has initialized.";
                throw new InvalidOperationException("SuccubusPerformer cannot receive WalkTo commands. " + setup);
            }
            return _locomotion;
        }

        private PerformerSeating RequireSeatingRuntime()
        {
            if (IsTeleporting)
                throw new InvalidOperationException("SitAt/StandUp is unavailable while a teleport is in progress.");
            if (IsDissolving)
                throw new InvalidOperationException("SitAt/StandUp is unavailable while a dissolve is in progress.");
            if (_seating == null || _isTearingDown || !isActiveAndEnabled)
                throw new InvalidOperationException("SuccubusPerformer can receive SitAt/StandUp commands only while enabled in Play Mode.");
            return _seating;
        }

        private PerformerTeleport RequireTeleportRuntime()
        {
            if (!IsRuntimeReady)
                throw new InvalidOperationException("SuccubusPerformer can teleport only while its runtime is ready in Play Mode.");
            if (IsDissolving)
                throw new InvalidOperationException("TeleportTo is unavailable while a dissolve is in progress.");
            if (_teleport == null)
                throw new InvalidOperationException("Teleport is unavailable. In Edit Mode, run Tools > DAZ Pose > First Performance Void > Install Teleport Acceptance Harness.");
            if (IsLocomoting)
                throw new InvalidOperationException("Teleport requires Lara to finish locomoting first.");
            if (SeatingState != PerformerSeatingState.Standing)
                throw new InvalidOperationException("Teleport is available only while Lara is standing.");
            if (_teleport.IsTeleporting)
                throw new InvalidOperationException("A teleport is already in progress; wait for it to arrive before requesting another.");
            return _teleport;
        }

        private PerformerDissolve RequireDissolveRuntime()
        {
            if (!IsRuntimeReady)
                throw new InvalidOperationException("SuccubusPerformer can dissolve only while its runtime is ready in Play Mode.");
            if (_dissolve == null)
                throw new InvalidOperationException("DissolveTo is unavailable. In Edit Mode, run Tools > DAZ Pose > First Performance Void > Install Dissolve Acceptance Harness.");
            if (IsTeleporting)
                throw new InvalidOperationException("DissolveTo is unavailable while a teleport is in progress.");
            if (IsLocomoting)
                throw new InvalidOperationException("DissolveTo requires Lara to finish locomoting first.");
            if (SeatingState != PerformerSeatingState.Standing)
                throw new InvalidOperationException("DissolveTo is available only while Lara is standing.");
            if (_dissolve.IsDissolving)
                throw new InvalidOperationException("A dissolve is already in progress; wait for it to arrive before requesting another.");
            return _dissolve;
        }

        private void DestroyRuntime()
        {
            if (_breathing != null) _lastBreathPhase = _breathing.BreathPhase;
            if (_bodyPose != null)
            {
                if (_bodyPose.DesiredPose != null) _lastDesiredPose = _bodyPose.DesiredPose;
                if (_bodyPose.SettledPose != null) _lastSettledPose = _bodyPose.SettledPose;
            }

            _isTearingDown = true;
            _dissolve?.Dispose();
            _dissolve = null;
            _teleport?.Dispose();
            _teleport = null;
            var speech = _speech;
            _speech = null;
            speech?.Dispose();

            var request = _activePoseRequest;
            _activePoseRequest = null;
            if (request != null) CompleteRequest(request, PoseCompletion.PerformerDisabled);
            var expressionRequest = _activeExpressionRequest;
            _activeExpressionRequest = null;
            if (expressionRequest != null) CompleteExpressionRequest(expressionRequest, ExpressionCompletion.PerformerDisabled);

            _locomotion?.Dispose();
            _locomotion = null;

            _seating?.Dispose();
            _seating = null;

            _blink?.Dispose();
            _blink = null;
            _gaze?.Dispose();
            _gaze = null;
            _attentionLife = null;
            _expression?.Dispose();
            _expression = null;

            _seatingLayer?.Dispose();
            _seatingLayer = null;

            _bodySourceMixer?.Dispose();
            _bodySourceMixer = null;

            if (_graph.IsValid()) _graph.Destroy();
            _breathing?.Dispose();
            _breathing = null;
            _bodyPose?.Dispose();
            _bodyPose = null;
            _graph = default;
            if (_hasSavedApplyRootMotion && animator != null)
            {
                animator.applyRootMotion = _savedApplyRootMotion;
                _hasSavedApplyRootMotion = false;
            }
        }

        private void OnValidate()
        {
            breathsPerMinute = Mathf.Max(0f, breathsPerMinute);
            morphBreathingStrength = Mathf.Max(0f, morphBreathingStrength);
            breatheStrength = Mathf.Max(0f, breatheStrength);
            breatheBellyStrength = Mathf.Max(0f, breatheBellyStrength);
            boneBreathingStrength = Mathf.Max(0f, boneBreathingStrength);
            if (breathingCurve == null) breathingCurve = CreateDefaultBreathingCurve();
            if (breathingBones == null) breathingBones = CreateDefaultBreathingBones();
            gazeAcquireToleranceDegrees = Mathf.Max(0.1f, gazeAcquireToleranceDegrees);
            headGazeWeight = Mathf.Clamp01(headGazeWeight);
            headGazeResponse = Mathf.Max(0f, headGazeResponse);
            headGazeMaxYaw = Mathf.Clamp(headGazeMaxYaw, 0f, 89f);
            headGazeMaxPitch = Mathf.Clamp(headGazeMaxPitch, 0f, 89f);
            eyeGazeWeight = Mathf.Clamp01(eyeGazeWeight);
            eyeGazeResponse = Mathf.Max(0f, eyeGazeResponse);
            eyeGazeMaxYaw = Mathf.Clamp(eyeGazeMaxYaw, 0f, 89f);
            eyeGazeMaxPitch = Mathf.Clamp(eyeGazeMaxPitch, 0f, 89f);
            gazeReleaseResponse = Mathf.Max(0f, gazeReleaseResponse);
            eyeFixationMaxHorizontalDegrees = Mathf.Clamp(eyeFixationMaxHorizontalDegrees, 0f, 8f);
            eyeFixationMaxVerticalDegrees = Mathf.Clamp(eyeFixationMaxVerticalDegrees, 0f, 6f);
            eyeFixationMinimumHoldSeconds = Mathf.Max(0.05f, eyeFixationMinimumHoldSeconds);
            eyeFixationMaximumHoldSeconds = Mathf.Max(eyeFixationMinimumHoldSeconds,
                eyeFixationMaximumHoldSeconds);
            eyeFixationCenterBias = Mathf.Clamp(eyeFixationCenterBias, 1f, 4f);
            headAttentionMaxTiltDegrees = Mathf.Clamp(headAttentionMaxTiltDegrees, 0f, 15f);
            headAttentionMaxChinDegrees = Mathf.Clamp(headAttentionMaxChinDegrees, 0f, 12f);
            headAttentionMinimumHoldSeconds = Mathf.Max(0.1f, headAttentionMinimumHoldSeconds);
            headAttentionMaximumHoldSeconds = Mathf.Max(headAttentionMinimumHoldSeconds,
                headAttentionMaximumHoldSeconds);
            headAttentionTransitionResponse = Mathf.Max(0f, headAttentionTransitionResponse);
            blinkStrength = Mathf.Clamp01(blinkStrength);
            blinkMinimumIntervalSeconds = Mathf.Max(0.1f, blinkMinimumIntervalSeconds);
            blinkMaximumIntervalSeconds = Mathf.Max(blinkMinimumIntervalSeconds,
                blinkMaximumIntervalSeconds);
            blinkCloseDurationSeconds = Mathf.Max(0.005f, blinkCloseDurationSeconds);
            blinkClosedDurationSeconds = Mathf.Max(0.005f, blinkClosedDurationSeconds);
            blinkOpenDurationSeconds = Mathf.Max(0.005f, blinkOpenDurationSeconds);
            initialExpressionIntensity = Mathf.Clamp01(initialExpressionIntensity);
            defaultExpressionBlendTime = Mathf.Max(0f, defaultExpressionBlendTime);
        }

        private PerformerBreathingSettings CreateBreathingSettings()
        {
            return new PerformerBreathingSettings
            {
                BreathingEnabled = breathingEnabled,
                BreathsPerMinute = breathsPerMinute,
                BreathingCurve = breathingCurve,
                MorphBreathingEnabled = morphBreathingEnabled,
                MorphBreathingStrength = morphBreathingStrength,
                BreatheStrength = breatheStrength,
                BreatheBellyStrength = breatheBellyStrength,
                BoneBreathingEnabled = boneBreathingEnabled,
                BoneBreathingStrength = boneBreathingStrength
            };
        }

        private PerformerGazeSettings CreateGazeSettings()
        {
            return new PerformerGazeSettings
            {
                GazeEnabled = gazeEnabled,
                AcquireToleranceDegrees = gazeAcquireToleranceDegrees,
                HeadEnabled = headGazeEnabled,
                HeadWeight = headGazeWeight,
                HeadResponse = headGazeResponse,
                HeadMaxYaw = headGazeMaxYaw,
                HeadMaxPitch = headGazeMaxPitch,
                EyesEnabled = eyeGazeEnabled,
                EyeWeight = eyeGazeWeight,
                EyeResponse = eyeGazeResponse,
                EyeMaxYaw = eyeGazeMaxYaw,
                EyeMaxPitch = eyeGazeMaxPitch,
                ReleaseResponse = gazeReleaseResponse
            };
        }

        private PerformerAttentionLifeSettings CreateAttentionLifeSettings()
        {
            return new PerformerAttentionLifeSettings
            {
                Enabled = attentionLifeEnabled,
                Seed = attentionLifeSeed,
                EyeFixationEnabled = eyeFixationLifeEnabled,
                EyeMaxHorizontalDegrees = eyeFixationMaxHorizontalDegrees,
                EyeMaxVerticalDegrees = eyeFixationMaxVerticalDegrees,
                EyeMinimumHoldSeconds = eyeFixationMinimumHoldSeconds,
                EyeMaximumHoldSeconds = eyeFixationMaximumHoldSeconds,
                EyeCenterBias = eyeFixationCenterBias,
                HeadEnabled = headAttentionLifeEnabled,
                HeadMaxTiltDegrees = headAttentionMaxTiltDegrees,
                HeadMaxChinDegrees = headAttentionMaxChinDegrees,
                HeadMinimumHoldSeconds = headAttentionMinimumHoldSeconds,
                HeadMaximumHoldSeconds = headAttentionMaximumHoldSeconds,
                HeadTransitionResponse = headAttentionTransitionResponse
            };
        }

        private PerformerBlinkSettings CreateBlinkSettings()
        {
            return new PerformerBlinkSettings
            {
                Enabled = blinkEnabled,
                Seed = attentionLifeSeed,
                Strength = blinkStrength,
                MinimumIntervalSeconds = blinkMinimumIntervalSeconds,
                MaximumIntervalSeconds = blinkMaximumIntervalSeconds,
                CloseDurationSeconds = blinkCloseDurationSeconds,
                ClosedDurationSeconds = blinkClosedDurationSeconds,
                OpenDurationSeconds = blinkOpenDurationSeconds
            };
        }

        private static AnimationCurve CreateDefaultBreathingCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.32f, 1f, 0f, 0f),
                new Keyframe(0.44f, 1f, 0f, 0f),
                new Keyframe(1f, 0f, 0f, 0f));
        }

        private static BreathingBoneChannel[] CreateDefaultBreathingBones()
        {
            return new[]
            {
                new BreathingBoneChannel
                {
                    boneName = "abdomenLower",
                    fullInhaleLocalRotationDelta = new Vector3(-0.20f, 0f, 0f)
                },
                new BreathingBoneChannel
                {
                    boneName = "abdomenUpper",
                    fullInhaleLocalRotationDelta = new Vector3(-0.30f, 0f, 0f),
                    fullInhaleLocalPositionDelta = new Vector3(0f, 0.0003f, 0f)
                },
                new BreathingBoneChannel
                {
                    boneName = "chestLower",
                    fullInhaleLocalRotationDelta = new Vector3(-0.40f, 0f, 0f),
                    fullInhaleLocalPositionDelta = new Vector3(0f, 0.0005f, 0f)
                },
                new BreathingBoneChannel
                {
                    boneName = "chestUpper",
                    fullInhaleLocalRotationDelta = new Vector3(-0.25f, 0f, 0f),
                    fullInhaleLocalPositionDelta = new Vector3(0f, 0.0003f, 0f)
                }
            };
        }
    }
}
