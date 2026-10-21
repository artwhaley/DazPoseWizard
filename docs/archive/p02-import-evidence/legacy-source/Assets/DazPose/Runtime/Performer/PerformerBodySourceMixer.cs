using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer
{
    /// <summary>Mixes the persistent Pose source with reusable baked Generic locomotion clips.</summary>
    internal sealed class PerformerBodySourceMixer : IDisposable
    {
        private readonly PerformerLocomotionProfile _profile;
        private readonly AnimationMixerPlayable _sourceMixer;
        private readonly AnimationMixerPlayable _locomotionMixer;
        private readonly Dictionary<PerformerLocomotionMotion, AnimationClipPlayable> _clips =
            new Dictionary<PerformerLocomotionMotion, AnimationClipPlayable>();
        private readonly Dictionary<PerformerLocomotionMotion, int> _motionIndices =
            new Dictionary<PerformerLocomotionMotion, int>();
        private AnimationClipPlayable _active;
        private AnimationClipPlayable _outgoing;
        private PerformerLocomotionMotion _activeMotion;
        private PerformerLocomotionMotion _outgoingMotion;
        private float _blendElapsed;
        private float _blendDuration;
        private float _ownershipWeight;
        private float _ownershipTarget;
        private float _ownershipStart;
        private float _ownershipElapsed;
        private float _ownershipDuration;
        private bool _disposed;

        public Playable Output => _sourceMixer;
        public PerformerLocomotionMotion ActiveMotion => _activeMotion;
        public float ActiveTime => _active.IsValid() ? (float)_active.GetTime() : 0f;
        public float ActivePhase => _activeMotion == null || _activeMotion.DurationSeconds <= 0f
            ? 0f : Mathf.Repeat(ActiveTime / _activeMotion.DurationSeconds, 1f);
        public float LocomotionWeight => _ownershipWeight;
        public bool HoldArrivalPose { get; set; }

        public PerformerBodySourceMixer(Animator animator, PlayableGraph graph, Playable poseSource,
            PerformerLocomotionProfile profile)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            if (!graph.IsValid()) throw new ArgumentException("A valid performer graph is required.", nameof(graph));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!profile.IsReady(out string reason)) throw new InvalidOperationException(reason);
            _profile = profile;
            PerformerLocomotionMotion[] motions = GetProfileMotions(profile);
            _locomotionMixer = AnimationMixerPlayable.Create(graph, motions.Length);
            for (int i = 0; i < motions.Length; i++)
            {
                var playable = AnimationClipPlayable.Create(graph, motions[i].BodyClip);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                playable.SetSpeed(0d);
                playable.SetTime(0d);
                if (!graph.Connect(playable, 0, _locomotionMixer, i))
                    throw new InvalidOperationException("Could not connect baked locomotion motion " + motions[i].name + ".");
                _locomotionMixer.SetInputWeight(i, 0f);
                _clips.Add(motions[i], playable);
                _motionIndices.Add(motions[i], i);
            }

            _sourceMixer = AnimationMixerPlayable.Create(graph, 2);
            if (!graph.Connect(poseSource, 0, _sourceMixer, 0)
                || !graph.Connect(_locomotionMixer, 0, _sourceMixer, 1))
                throw new InvalidOperationException("Could not connect the persistent pose and locomotion body sources.");
            _sourceMixer.SetInputWeight(0, 1f);
            _sourceMixer.SetInputWeight(1, 0f);
        }

        public void BeginMotion(PerformerLocomotionMotion motion, float normalizedTime, float blendSeconds)
        {
            ThrowIfDisposed();
            if (motion == null || !_clips.TryGetValue(motion, out AnimationClipPlayable next))
                throw new ArgumentException("Motion is not part of the configured locomotion profile.", nameof(motion));

            // A repeated clip is one Playable, not two independent blend inputs.
            // Blending it against itself would overwrite its weight with zero.
            if (_active.IsValid() && _activeMotion == motion)
            {
                _active.SetTime(Mathf.Clamp01(normalizedTime) * motion.DurationSeconds);
                _outgoing = default;
                _outgoingMotion = null;
                _blendElapsed = _blendDuration = 0f;
                UpdateMotionBlend();
                return;
            }

            _outgoing = _active;
            _outgoingMotion = _activeMotion;
            if (_ownershipWeight <= 0.0001f && _ownershipTarget == 0f)
            {
                _outgoing = default;
                _outgoingMotion = null;
            }
            _active = next;
            _activeMotion = motion;
            _active.SetTime(Mathf.Clamp01(normalizedTime) * motion.DurationSeconds);
            _active.SetSpeed(0d);
            _blendElapsed = 0f;
            _blendDuration = Mathf.Max(0f, blendSeconds);
            if (_outgoing.IsValid()) _outgoing.SetSpeed(0d);
            UpdateMotionBlend();
        }

        public void SetActiveTime(float seconds)
        {
            if (_active.IsValid()) _active.SetTime(Mathf.Clamp(seconds, 0f, _activeMotion.DurationSeconds));
        }

        public void SetOwnership(bool locomotion, bool immediate = false)
        {
            SetOwnership(locomotion, locomotion
                ? _profile.IdleToLocomotionBlendSeconds
                : _profile.LocomotionToIdleBlendSeconds, immediate);
        }

        public void SetOwnership(bool locomotion, float blendSeconds)
        {
            SetOwnership(locomotion, blendSeconds, false);
        }

        private void SetOwnership(bool locomotion, float blendSeconds, bool immediate)
        {
            if (!locomotion && HoldArrivalPose && !immediate) return;
            float target = locomotion ? 1f : 0f;
            if (!immediate && target == _ownershipTarget) return;
            _ownershipStart = _ownershipWeight;
            _ownershipTarget = target;
            _ownershipElapsed = 0f;
            _ownershipDuration = immediate ? 0f : Mathf.Max(locomotion ? 0.5f : 1f, blendSeconds);
            if (immediate || _ownershipDuration <= 0f) _ownershipWeight = _ownershipTarget;
            SetSourceWeights();
        }

        public void Advance(float deltaTime)
        {
            if (_disposed) return;
            if (_blendElapsed < _blendDuration)
            {
                _blendElapsed = Mathf.Min(_blendDuration, _blendElapsed + Mathf.Max(0f, deltaTime));
                UpdateMotionBlend();
            }
            if (_ownershipWeight != _ownershipTarget)
            {
                _ownershipElapsed += Mathf.Max(0f, deltaTime);
                float progress = _ownershipDuration <= 0f ? 1f
                    : Mathf.Clamp01(_ownershipElapsed / _ownershipDuration);
                float weight = progress * progress * (3f - 2f * progress);
                _ownershipWeight = Mathf.Lerp(_ownershipStart, _ownershipTarget, weight);
                SetSourceWeights();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (AnimationClipPlayable clip in _clips.Values)
                if (clip.IsValid()) clip.Destroy();
            _clips.Clear();
            if (_locomotionMixer.IsValid()) _locomotionMixer.Destroy();
            if (_sourceMixer.IsValid()) _sourceMixer.Destroy();
            _active = default;
            _outgoing = default;
        }

        private void UpdateMotionBlend()
        {
            float blend = _blendDuration <= 0f ? 1f : Mathf.Clamp01(_blendElapsed / _blendDuration);
            for (int i = 0; i < _locomotionMixer.GetInputCount(); i++) _locomotionMixer.SetInputWeight(i, 0f);
            if (_outgoing.IsValid() && _outgoingMotion != null && _blendDuration > 0f && blend < 1f)
                _locomotionMixer.SetInputWeight(IndexOf(_outgoingMotion), 1f - blend);
            if (_active.IsValid() && _activeMotion != null)
                // Entry from idle is blended once by the source mixer. The incoming
                // clip keeps full internal weight and advances throughout that blend.
                _locomotionMixer.SetInputWeight(IndexOf(_activeMotion),
                    _outgoing.IsValid() && _outgoingMotion != null ? blend : 1f);
            if (blend >= 1f)
            {
                _outgoing = default;
                _outgoingMotion = null;
            }
            SetSourceWeights();
        }

        private void SetSourceWeights()
        {
            if (!_sourceMixer.IsValid()) return;
            _sourceMixer.SetInputWeight(0, 1f - _ownershipWeight);
            _sourceMixer.SetInputWeight(1, _ownershipWeight);
        }

        private int IndexOf(PerformerLocomotionMotion motion)
        {
            return _motionIndices.TryGetValue(motion, out int index)
                ? index
                : throw new InvalidOperationException("A locomotion motion is missing from its profile mixer.");
        }

        private static PerformerLocomotionMotion[] GetProfileMotions(PerformerLocomotionProfile profile) => new[]
        {
            profile.StartA, profile.StartB, profile.WalkLoop, profile.StopA, profile.StopB,
            profile.TurnLeft90, profile.TurnRight90, profile.TurnLeft180, profile.TurnRight180
        };

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PerformerBodySourceMixer));
        }
    }
}
