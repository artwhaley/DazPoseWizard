using System;
using DazPose.Motion;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer
{
    /// <summary>Masked non-additive arm ownership with phase-aligned, externally-clocked variants.</summary>
    internal sealed class PerformerMotionLayer : IDisposable
    {
        private readonly AnimationLayerMixerPlayable _layers;
        private readonly AnimationMixerPlayable _variantsMixer;
        private readonly AnimationClipPlayable[] _variantPlayables;
        private readonly PerformerMotionVariant[] _variants;
        private readonly float[] _variantWeights;
        private readonly float[] _variantStartWeights;
        private readonly float[] _variantTargetWeights;
        private readonly float _ownershipBlendSeconds;
        private float _motionWeight;
        private float _ownershipStartWeight;
        private float _ownershipTargetWeight;
        private float _ownershipBlendElapsed;
        private float _ownershipBlendDuration;
        private float _variantBlendElapsed;
        private float _variantBlendDuration;
        private int _activeVariantIndex;
        private bool _disposed;
        private float _spatialSuppression;
        public void SetSpatialSuppression(float weight)
        {
            _spatialSuppression = Mathf.Clamp01(weight);
            if (_layers.IsValid()) _layers.SetInputWeight(1, _motionWeight * (1f - _spatialSuppression));
        }

        public Playable OutputPlayable => _layers;
        public float MotionWeight => _motionWeight;
        public int ActiveVariantIndex => _activeVariantIndex;
        public string ActiveVariantName => _variants[_activeVariantIndex].DisplayName;
        public float SampledPosition01 { get; private set; }
        public MotionSample LastSample { get; private set; }
        public int VariantCount => _variants.Length;

        public PerformerMotionLayer(PlayableGraph graph, Playable baseSource, Animator animator,
            PerformerMotionSet set, AvatarMask mask, float ownershipBlendSeconds, bool validateMask = true)
        {
            if (!graph.IsValid()) throw new ArgumentException("A valid performer PlayableGraph is required.", nameof(graph));
            if (!baseSource.IsValid()) throw new ArgumentException("A valid underlying body source is required.", nameof(baseSource));
            if (validateMask && !PerformerMotionMaskUtility.IsValidMask(animator, mask, out string maskReason))
                throw new InvalidOperationException(maskReason);
            if (mask == null) throw new ArgumentNullException(nameof(mask));
            if (set == null) throw new ArgumentNullException(nameof(set));
            if (!set.IsReady(out string setReason)) throw new InvalidOperationException(setReason);

            _variants = set.Variants;
            _variantPlayables = new AnimationClipPlayable[_variants.Length];
            _variantWeights = new float[_variants.Length];
            _variantStartWeights = new float[_variants.Length];
            _variantTargetWeights = new float[_variants.Length];
            _ownershipBlendSeconds = Mathf.Max(0f, ownershipBlendSeconds);

            _layers = AnimationLayerMixerPlayable.Create(graph, 2);
            _variantsMixer = AnimationMixerPlayable.Create(graph, _variants.Length);
            if (!graph.Connect(baseSource, 0, _layers, 0))
                throw new InvalidOperationException("Could not connect Motion below Gesture to the current performer body source.");
            if (!graph.Connect(_variantsMixer, 0, _layers, 1))
                throw new InvalidOperationException("Could not connect the variant mixer to the masked Motion layer.");
            _layers.SetInputWeight(0, 1f);
            _layers.SetInputWeight(1, 0f);
            _layers.SetLayerAdditive(1, false);
            _layers.SetLayerMaskFromAvatarMask(1, mask);

            try
            {
                for (int i = 0; i < _variants.Length; i++)
                {
                    AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, _variants[i].Clip);
                    playable.SetApplyFootIK(false);
                    playable.SetApplyPlayableIK(false);
                    playable.SetSpeed(0d);
                    playable.SetTime(0d);
                    playable.SetDone(false);
                    if (!graph.Connect(playable, 0, _variantsMixer, i))
                    {
                        playable.Destroy();
                        throw new InvalidOperationException("Could not connect motion variant '" + _variants[i].DisplayName + "'.");
                    }
                    _variantPlayables[i] = playable;
                    float initialWeight = i == 0 ? 1f : 0f;
                    _variantWeights[i] = _variantStartWeights[i] = _variantTargetWeights[i] = initialWeight;
                    _variantsMixer.SetInputWeight(i, initialWeight);
                }
                _activeVariantIndex = 0;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void SetSample(MotionSample sample)
        {
            if (_disposed) return;
            float position = Mathf.Clamp01(sample.Position01);
            SampledPosition01 = position;
            LastSample = sample;
            for (int i = 0; i < _variantPlayables.Length; i++)
            {
                AnimationClipPlayable playable = _variantPlayables[i];
                if (!playable.IsValid()) continue;
                playable.SetSpeed(0d);
                playable.SetTime(position * _variants[i].Clip.length);
                playable.SetDone(false);
            }
        }

        public void SetEngaged(bool engaged, float blendSeconds = -1f)
        {
            if (_disposed) return;
            float target = engaged ? 1f : 0f;
            if (Mathf.Approximately(_ownershipTargetWeight, target)) return;
            _ownershipStartWeight = _motionWeight;
            _ownershipTargetWeight = target;
            _ownershipBlendElapsed = 0f;
            _ownershipBlendDuration = blendSeconds < 0f ? _ownershipBlendSeconds : Mathf.Max(0f, blendSeconds);
            if (_ownershipBlendDuration <= 0f)
            {
                _motionWeight = target;
                _layers.SetInputWeight(1, target * (1f - _spatialSuppression));
            }
        }

        public void SetVariant(int index, float blendSeconds)
        {
            ThrowIfDisposed();
            if (index < 0 || index >= _variants.Length)
                throw new ArgumentOutOfRangeException(nameof(index), "Variant index is outside the configured MotionSet.");

            _activeVariantIndex = index;
            for (int i = 0; i < _variantWeights.Length; i++)
            {
                _variantStartWeights[i] = _variantWeights[i];
                _variantTargetWeights[i] = i == index ? 1f : 0f;
            }
            _variantBlendElapsed = 0f;
            _variantBlendDuration = Mathf.Max(0f, blendSeconds);
            if (_variantBlendDuration <= 0f) ApplyVariantBlend(1f);
        }

        public void Advance(float deltaSeconds)
        {
            if (_disposed) return;
            float dt = Mathf.Max(0f, deltaSeconds);
            if (!Mathf.Approximately(_motionWeight, _ownershipTargetWeight))
            {
                if (_ownershipBlendDuration <= 0f) _motionWeight = _ownershipTargetWeight;
                else
                {
                    _ownershipBlendElapsed = Mathf.Min(_ownershipBlendDuration, _ownershipBlendElapsed + dt);
                    float t = SmoothStep01(_ownershipBlendElapsed / _ownershipBlendDuration);
                    _motionWeight = Mathf.LerpUnclamped(_ownershipStartWeight, _ownershipTargetWeight, t);
                }
                _layers.SetInputWeight(1, Mathf.Clamp01(_motionWeight) * (1f - _spatialSuppression));
            }
            if (_variantBlendElapsed < _variantBlendDuration)
            {
                _variantBlendElapsed = Mathf.Min(_variantBlendDuration, _variantBlendElapsed + dt);
                ApplyVariantBlend(SmoothStep01(_variantBlendElapsed / _variantBlendDuration));
            }
        }

        public float GetVariantSampleTime(int index)
        {
            ValidateIndex(index);
            return _variantPlayables[index].IsValid() ? (float)_variantPlayables[index].GetTime() : 0f;
        }

        public float GetVariantWeight(int index)
        {
            ValidateIndex(index);
            return _variantWeights[index];
        }

        public double GetVariantSpeed(int index)
        {
            ValidateIndex(index);
            return _variantPlayables[index].IsValid() ? _variantPlayables[index].GetSpeed() : 0d;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _motionWeight = _ownershipTargetWeight = 0f;
            if (_layers.IsValid())
            {
                _layers.SetInputWeight(0, 1f);
                _layers.SetInputWeight(1, 0f);
                _layers.DisconnectInput(1);
            }
            for (int i = 0; i < _variantPlayables.Length; i++)
            {
                if (!_variantPlayables[i].IsValid()) continue;
                if (_variantsMixer.IsValid()) _variantsMixer.DisconnectInput(i);
                _variantPlayables[i].Destroy();
            }
            if (_variantsMixer.IsValid()) _variantsMixer.Destroy();
            if (_layers.IsValid()) _layers.Destroy();
        }

        private void ApplyVariantBlend(float t)
        {
            for (int i = 0; i < _variantWeights.Length; i++)
            {
                float weight = Mathf.Clamp01(Mathf.LerpUnclamped(_variantStartWeights[i], _variantTargetWeights[i], t));
                _variantWeights[i] = weight;
                if (_variantsMixer.IsValid()) _variantsMixer.SetInputWeight(i, weight);
            }
        }

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= _variantPlayables.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PerformerMotionLayer));
        }

        private static float SmoothStep01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
