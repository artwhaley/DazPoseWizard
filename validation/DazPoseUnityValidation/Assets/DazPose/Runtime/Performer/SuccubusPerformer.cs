using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer
{
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
        private bool _hasPoseCommand;

        public PerformerPose SettledPose => _bodyPose != null ? _bodyPose.SettledPose : _lastSettledPose;
        public PerformerPose DesiredPose => _bodyPose != null ? _bodyPose.DesiredPose : _lastDesiredPose;
        public bool IsTransitioning => _bodyPose != null && _bodyPose.IsTransitioning;
        public float TransitionProgress => _bodyPose == null ? 0f : _bodyPose.TransitionProgress;
        public float TrajectoryProgress => _bodyPose == null ? 0f : _bodyPose.TrajectoryProgress;
        public PoseTransition ActiveTransition => _bodyPose == null ? default : _bodyPose.ActiveTransition;
        internal int RuntimePlayableCount => _graph.IsValid() ? _graph.GetPlayableCount() : 0;

        private void OnEnable()
        {
            if (Application.isPlaying) CreateRuntime();
        }

        private void Update()
        {
            _bodyPose?.Advance(Time.deltaTime);
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
            if (_bodyPose == null)
                throw new InvalidOperationException("SuccubusPerformer can receive pose commands only while enabled in Play Mode.");

            _bodyPose.SetPose(pose, transition);
            _lastDesiredPose = _bodyPose.DesiredPose;
            _lastSettledPose = _bodyPose.SettledPose;
            _hasPoseCommand = true;
        }

        private void CreateRuntime()
        {
            DestroyRuntime();
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

            if (_graph.IsValid()) _graph.Destroy();
            _bodyPose?.Dispose();
            _bodyPose = null;
            _graph = default;
        }
    }
}
