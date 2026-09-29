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

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("Performer/Succubus Performer")]
    public sealed class SuccubusPerformer : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private PerformerPose initialPose = null;
        [SerializeField] private PoseTransition defaultTransition = PoseTransition.Default;

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

        private PlayableGraph _graph;
        private PerformerBodyPose _bodyPose;
        private PerformerBreathing _breathing;
        private PerformerPose _lastDesiredPose;
        private PerformerPose _lastSettledPose;
        private PerformerPoseSnapshot _neutralPoseState;
        private float _lastBreathPhase;
        private PoseRequest _activePoseRequest;
        private long _nextPoseRequestId;
        private bool _hasPoseCommand;
        private bool _isTearingDown;

        public PerformerPose SettledPose => _bodyPose != null ? _bodyPose.SettledPose : _lastSettledPose;
        public PerformerPose DesiredPose => _bodyPose != null ? _bodyPose.DesiredPose : _lastDesiredPose;
        public bool IsTransitioning => _bodyPose != null && _bodyPose.IsTransitioning;
        public float TransitionProgress => _bodyPose == null ? 0f : _bodyPose.TransitionProgress;
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

        internal PerformerBreathing BreathingRuntime => _breathing;
        internal int RuntimePlayableCount => _graph.IsValid() ? _graph.GetPlayableCount() : 0;

        private void OnEnable()
        {
            _isTearingDown = false;
            if (Application.isPlaying) CreateRuntime();
        }

        private void Update()
        {
            if (_bodyPose == null) return;

            var request = _activePoseRequest;
            _bodyPose.Advance(Time.deltaTime);
            if (_breathing != null)
            {
                _breathing.Configure(CreateBreathingSettings());
                _breathing.Advance(Time.deltaTime);
                _lastBreathPhase = _breathing.BreathPhase;
            }
            PublishCurrentPoseState();
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

            try
            {
                _graph = PlayableGraph.Create("Succubus Performer - " + name);
                _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                _bodyPose = new PerformerBodyPose(animator, _graph, _neutralPoseState);
                _neutralPoseState = _bodyPose.NeutralState;
                _breathing = new PerformerBreathing(animator, _graph, _bodyPose.OutputPlayable,
                    _bodyPose, breathingBones, _lastBreathPhase);
                _breathing.Configure(CreateBreathingSettings());
                _breathing.Advance(0f);

                var output = AnimationPlayableOutput.Create(_graph, "Performer Animation", animator);
                output.SetSourcePlayable(_breathing.OutputPlayable);
                _graph.Play();

                var poseToRestore = _hasPoseCommand ? _lastDesiredPose : initialPose;
                if (poseToRestore != null)
                    _bodyPose.SetPose(poseToRestore, PoseTransition.Snap);
                _graph.Evaluate(0f);
            }
            catch
            {
                DestroyRuntime();
                throw;
            }
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
            var request = _activePoseRequest;
            _activePoseRequest = null;
            if (request != null) CompleteRequest(request, PoseCompletion.PerformerDisabled);

            if (_graph.IsValid()) _graph.Destroy();
            _breathing?.Dispose();
            _breathing = null;
            _bodyPose?.Dispose();
            _bodyPose = null;
            _graph = default;
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
