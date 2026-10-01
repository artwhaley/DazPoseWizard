using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer
{
    /// <summary>Full-body seating source layered above the persistent Pose/Locomotion sources.</summary>
    internal sealed class PerformerSeatingLayer : IDisposable
    {
        private const int MotionCapacity = 6;

        private readonly PlayableGraph _graph;
        private readonly AnimationMixerPlayable _sourceMixer;
        private readonly AnimationMixerPlayable _motionMixer;
        private readonly Dictionary<PerformerSeatingMotion, AnimationClipPlayable> _clips =
            new Dictionary<PerformerSeatingMotion, AnimationClipPlayable>();
        private readonly Dictionary<PerformerSeatingMotion, int> _indices =
            new Dictionary<PerformerSeatingMotion, int>();

        private PerformerSeatingProfile _profile;
        private AnimationClipPlayable _active;
        private AnimationClipPlayable _outgoing;
        private PerformerSeatingMotion _activeMotion;
        private PerformerSeatingMotion _outgoingMotion;
        private float _blendElapsed;
        private float _blendDuration;
        private float _ownershipWeight;
        private float _ownershipTarget;
        private float _ownershipStart;
        private float _ownershipElapsed;
        private float _ownershipDuration;
        private bool _disposed;

        public Playable Output => _sourceMixer;
        public PerformerSeatingMotion ActiveMotion => _activeMotion;
        public float ActiveTime => _active.IsValid() ? (float)_active.GetTime() : 0f;
        public float OwnershipWeight => _ownershipWeight;
        public float MotionBlendProgress => _blendDuration <= 0f ? 1f
            : Mathf.Clamp01(_blendElapsed / _blendDuration);

        public PerformerSeatingLayer(PlayableGraph graph, Playable baseSource)
        {
            if (!graph.IsValid()) throw new ArgumentException("A valid performer PlayableGraph is required.", nameof(graph));
            _graph = graph;
            _motionMixer = AnimationMixerPlayable.Create(graph, MotionCapacity);
            _sourceMixer = AnimationMixerPlayable.Create(graph, 2);
            if (!graph.Connect(baseSource, 0, _sourceMixer, 0)
                || !graph.Connect(_motionMixer, 0, _sourceMixer, 1))
                throw new InvalidOperationException("Could not connect the seating layer above the persistent performer body sources.");
            _sourceMixer.SetInputWeight(0, 1f);
            _sourceMixer.SetInputWeight(1, 0f);
            for (int i = 0; i < MotionCapacity; i++) _motionMixer.SetInputWeight(i, 0f);
        }

        public void PrepareProfile(PerformerSeatingProfile profile)
        {
            ThrowIfDisposed();
            if (profile == null) throw new InvalidOperationException("A baked PerformerSeatingProfile is required.");
            if (!profile.IsReady(out string reason)) throw new InvalidOperationException(reason);
            if (_profile != null)
            {
                if (_profile != profile)
                    throw new InvalidOperationException("A SuccubusPerformer uses one seating profile per runtime. Assign the same profile to its seats; different chair geometry belongs in the seat anchors.");
                return;
            }

            _profile = profile;
            PerformerSeatingMotion[] motions = GetMotions(profile);
            for (int i = 0; i < motions.Length; i++)
            {
                if (motions[i] == null) continue;
                var playable = AnimationClipPlayable.Create(_graph, motions[i].BodyClip);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                playable.SetSpeed(0d);
                playable.SetTime(0d);
                if (!_graph.Connect(playable, 0, _motionMixer, i))
                    throw new InvalidOperationException("Could not connect seating body clip " + motions[i].name + ".");
                _clips.Add(motions[i], playable);
                _indices.Add(motions[i], i);
            }
        }

        public void BeginMotion(PerformerSeatingMotion motion, float normalizedTime, float blendSeconds)
        {
            ThrowIfDisposed();
            if (motion == null || !_clips.TryGetValue(motion, out AnimationClipPlayable next))
                throw new ArgumentException("Motion is not part of the active seating profile.", nameof(motion));

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
            _active = next;
            _activeMotion = motion;
            _active.SetTime(Mathf.Clamp01(normalizedTime) * motion.DurationSeconds);
            // Times are driven explicitly by the seating state machine. During uncross
            // preparation neither the captured outgoing pose nor target frame zero advances.
            _active.SetSpeed(0d);
            if (_outgoing.IsValid()) _outgoing.SetSpeed(0d);
            _blendElapsed = 0f;
            _blendDuration = Mathf.Max(0f, blendSeconds);
            UpdateMotionBlend();
        }

        public void SetActiveTime(float seconds)
        {
            if (_active.IsValid() && _activeMotion != null)
                _active.SetTime(Mathf.Clamp(seconds, 0f, _activeMotion.DurationSeconds));
        }

        public void SetOwnership(bool seated, float blendSeconds, bool immediate = false)
        {
            float target = seated ? 1f : 0f;
            if (!immediate && target == _ownershipTarget) return;
            _ownershipStart = _ownershipWeight;
            _ownershipTarget = target;
            _ownershipElapsed = 0f;
            _ownershipDuration = Mathf.Max(0f, blendSeconds);
            if (immediate || _ownershipDuration <= 0f) _ownershipWeight = target;
            SetSourceWeights();
        }

        public void Advance(float deltaTime)
        {
            if (_disposed) return;
            float dt = Mathf.Max(0f, deltaTime);
            if (_blendElapsed < _blendDuration)
            {
                _blendElapsed = Mathf.Min(_blendDuration, _blendElapsed + dt);
                UpdateMotionBlend();
            }
            if (_ownershipWeight != _ownershipTarget)
            {
                _ownershipElapsed += dt;
                float progress = _ownershipDuration <= 0f ? 1f
                    : Mathf.Clamp01(_ownershipElapsed / _ownershipDuration);
                float smooth = progress * progress * (3f - 2f * progress);
                _ownershipWeight = Mathf.Lerp(_ownershipStart, _ownershipTarget, smooth);
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
            if (_motionMixer.IsValid()) _motionMixer.Destroy();
            if (_sourceMixer.IsValid()) _sourceMixer.Destroy();
            _active = default;
            _outgoing = default;
        }

        private void UpdateMotionBlend()
        {
            float progress = _blendDuration <= 0f ? 1f : Mathf.Clamp01(_blendElapsed / _blendDuration);
            for (int i = 0; i < _motionMixer.GetInputCount(); i++) _motionMixer.SetInputWeight(i, 0f);
            if (_outgoing.IsValid() && _outgoingMotion != null && _blendDuration > 0f && progress < 1f)
                _motionMixer.SetInputWeight(IndexOf(_outgoingMotion), 1f - progress);
            if (_active.IsValid() && _activeMotion != null)
                _motionMixer.SetInputWeight(IndexOf(_activeMotion),
                    _outgoing.IsValid() && _outgoingMotion != null && _blendDuration > 0f ? progress : 1f);
            if (progress >= 1f)
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

        private int IndexOf(PerformerSeatingMotion motion) => _indices.TryGetValue(motion, out int index)
            ? index
            : throw new InvalidOperationException("A seating motion is missing from the profile mixer.");

        private static PerformerSeatingMotion[] GetMotions(PerformerSeatingProfile profile) => new[]
        {
            profile.SitStart, profile.SitEnd, profile.CrossLegsStart,
            profile.CrossLegsLoop, profile.CrossLegsEnd, profile.BasicIdleLoopCandidate
        };

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PerformerSeatingLayer));
        }
    }
}
