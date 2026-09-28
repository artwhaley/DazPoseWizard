using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.UnityValidation
{
    public enum DazPoseBlendEase
    {
        Linear,
        SmoothStep
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("DAZ Pose/Runtime Pose Blend Player")]
    public sealed class DazPoseBlendPlayer : MonoBehaviour
    {
        private const float PoseSampleTimeSeconds = 0.5f;

        [SerializeField] private DazPoseBlendEase blendEase = DazPoseBlendEase.SmoothStep;
        [SerializeField] private AnimationClip currentPoseDebug;
        [SerializeField] private AnimationClip targetPoseDebug;
        [SerializeField] private AnimationClip pendingPoseDebug;
        [SerializeField, Range(0f, 1f)] private float blendProgressDebug;

        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable[] _clipPlayables;
        private AnimationClip _currentPose;
        private AnimationClip _targetPose;
        private AnimationClip _pendingPose;
        private float _pendingBlendSeconds;
        private float _blendDurationSeconds;
        private float _blendElapsedSeconds;
        private int _currentInput = -1;
        private int _targetInput = -1;
        private bool _isBlending;

        public AnimationClip CurrentPose => _currentPose;
        public AnimationClip TargetPose => _targetPose;
        public AnimationClip PendingPose => _pendingPose;
        public bool IsBlending => _isBlending;
        public float BlendProgress => blendProgressDebug;
        public DazPoseBlendEase BlendEase
        {
            get => blendEase;
            set => blendEase = value;
        }
        public int GraphPlayableCount => _graph.IsValid() ? _graph.GetPlayableCount() : 0;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            CreateGraph();
        }

        public void SetPose(AnimationClip pose, float blendSeconds)
        {
            if (pose == null) throw new ArgumentNullException(nameof(pose));
            if (float.IsNaN(blendSeconds) || float.IsInfinity(blendSeconds))
                throw new ArgumentOutOfRangeException(nameof(blendSeconds), "Blend duration must be a finite number of seconds.");
            if (!_graph.IsValid()) throw new InvalidOperationException("DAZ Pose Blend Player is not active in Play Mode.");

            blendSeconds = Mathf.Max(0f, blendSeconds);
            if (_isBlending)
            {
                if (pose == _targetPose)
                {
                    ClearPendingPose();
                    return;
                }
                if (pose == _pendingPose) return;

                _pendingPose = pose;
                _pendingBlendSeconds = blendSeconds;
                RefreshDebugState();
                Debug.Log("Pose queued: " + pose.name + " after the current transition, duration "
                    + blendSeconds.ToString("F2") + " sec, ease " + blendEase + ".", this);
                return;
            }

            if (pose == _currentPose) return;
            if (_currentInput < 0)
            {
                var initialPlayable = CreateStaticPosePlayable(pose);
                ConnectPose(initialPlayable, 0);
                _currentInput = 0;
                _currentPose = pose;
                _mixer.SetInputWeight(_currentInput, 1f);
                _mixer.SetInputWeight(1, 0f);
                RefreshDebugState();
                Debug.Log("Pose set: <none> → " + pose.name + " | duration: 0.00 sec | ease: " + blendEase + ".", this);
                return;
            }

            BeginTransition(pose, blendSeconds);
        }

        private void Update()
        {
            if (!_isBlending || !_graph.IsValid()) return;

            _blendElapsedSeconds += Mathf.Max(0f, Time.deltaTime);
            var linearProgress = _blendDurationSeconds <= 0f ? 1f : Mathf.Clamp01(_blendElapsedSeconds / _blendDurationSeconds);
            var easedProgress = blendEase == DazPoseBlendEase.Linear
                ? linearProgress
                : linearProgress * linearProgress * (3f - 2f * linearProgress);
            _mixer.SetInputWeight(_currentInput, 1f - easedProgress);
            _mixer.SetInputWeight(_targetInput, easedProgress);
            blendProgressDebug = linearProgress;

            if (linearProgress >= 1f) CompleteTransition();
        }

        private void BeginTransition(AnimationClip pose, float blendSeconds)
        {
            _targetInput = 1 - _currentInput;
            DisconnectInput(_targetInput);
            var targetPlayable = CreateStaticPosePlayable(pose);
            ConnectPose(targetPlayable, _targetInput);
            _mixer.SetInputWeight(_currentInput, 1f);
            _mixer.SetInputWeight(_targetInput, 0f);
            _targetPose = pose;
            _blendDurationSeconds = blendSeconds;
            _blendElapsedSeconds = 0f;
            blendProgressDebug = 0f;
            _isBlending = blendSeconds > 0f;
            RefreshDebugState();

            Debug.Log("Pose transition:\n" + _currentPose.name + " → " + pose.name + "\nduration: "
                + blendSeconds.ToString("F2") + " sec\nease: " + blendEase + ".", this);
            if (!_isBlending) CompleteTransition();
        }

        private void CompleteTransition()
        {
            if (!_isBlending && _targetPose == null) return;

            var obsoleteInput = _currentInput;
            _mixer.SetInputWeight(_targetInput, 1f);
            _mixer.SetInputWeight(obsoleteInput, 0f);
            DisconnectInput(obsoleteInput);
            _currentInput = _targetInput;
            _currentPose = _targetPose;
            _targetInput = -1;
            _targetPose = null;
            _isBlending = false;
            _blendElapsedSeconds = 0f;
            _blendDurationSeconds = 0f;
            blendProgressDebug = 1f;

            var queuedPose = _pendingPose;
            var queuedDuration = _pendingBlendSeconds;
            ClearPendingPose();
            RefreshDebugState();

            if (queuedPose != null && queuedPose != _currentPose)
                BeginTransition(queuedPose, queuedDuration);
        }

        private AnimationClipPlayable CreateStaticPosePlayable(AnimationClip pose)
        {
            var playable = AnimationClipPlayable.Create(_graph, pose);
            try
            {
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                playable.SetTime(PoseSampleTimeSeconds);
                playable.SetSpeed(0d);
                return playable;
            }
            catch
            {
                if (playable.IsValid()) playable.Destroy();
                throw;
            }
        }

        private void ConnectPose(AnimationClipPlayable playable, int input)
        {
            if (!_graph.Connect(playable, 0, _mixer, input))
            {
                if (playable.IsValid()) playable.Destroy();
                throw new InvalidOperationException("Could not connect the static pose playable to mixer input " + input + ".");
            }
            _clipPlayables[input] = playable;
        }

        private void DisconnectInput(int input)
        {
            if (input < 0 || _clipPlayables == null || input >= _clipPlayables.Length) return;
            if (_graph.IsValid() && _mixer.IsValid()) _graph.Disconnect(_mixer, input);
            if (_clipPlayables[input].IsValid()) _clipPlayables[input].Destroy();
            _clipPlayables[input] = default(AnimationClipPlayable);
            _mixer.SetInputWeight(input, 0f);
        }

        private void CreateGraph()
        {
            DestroyGraph();
            _animator = GetComponent<Animator>();
            if (_animator == null) throw new InvalidOperationException("DazPoseBlendPlayer requires an Animator on the Genesis8Female animation root.");

            _graph = PlayableGraph.Create("DAZ Pose Blend Player - " + name);
            try
            {
                _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                _mixer = AnimationMixerPlayable.Create(_graph, 2);
                _clipPlayables = new AnimationClipPlayable[2];
                _mixer.SetInputWeight(0, 0f);
                _mixer.SetInputWeight(1, 0f);
                var output = AnimationPlayableOutput.Create(_graph, "DAZ Pose Blend Output", _animator);
                output.SetSourcePlayable(_mixer);
                _graph.Play();
                _currentInput = -1;
                _targetInput = -1;
                _currentPose = null;
                _targetPose = null;
                _isBlending = false;
                ClearPendingPose();
                RefreshDebugState();
            }
            catch
            {
                DestroyGraph();
                throw;
            }
        }

        private void OnDisable() => DestroyGraph();
        private void OnDestroy() => DestroyGraph();

        private void DestroyGraph()
        {
            if (_graph.IsValid()) _graph.Destroy();
            _graph = default(PlayableGraph);
            _mixer = default(AnimationMixerPlayable);
            _clipPlayables = null;
            _animator = null;
            _currentInput = -1;
            _targetInput = -1;
            _currentPose = null;
            _targetPose = null;
            _isBlending = false;
            _blendDurationSeconds = 0f;
            _blendElapsedSeconds = 0f;
            blendProgressDebug = 0f;
            ClearPendingPose();
            RefreshDebugState();
        }

        private void ClearPendingPose()
        {
            _pendingPose = null;
            _pendingBlendSeconds = 0f;
            pendingPoseDebug = null;
        }

        private void RefreshDebugState()
        {
            currentPoseDebug = _currentPose;
            targetPoseDebug = _targetPose;
            pendingPoseDebug = _pendingPose;
        }
    }
}
