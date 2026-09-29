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

        private PlayableGraph _graph;
        private PerformerBodyPose _bodyPose;
        private PerformerPose _lastDesiredPose;
        private PerformerPose _lastSettledPose;
        private PerformerPoseSnapshot _neutralPoseState;
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

                var output = AnimationPlayableOutput.Create(_graph, "Performer Animation", animator);
                output.SetSourcePlayable(_bodyPose.OutputPlayable);
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
            _bodyPose?.Dispose();
            _bodyPose = null;
            _graph = default;
        }
    }
}
