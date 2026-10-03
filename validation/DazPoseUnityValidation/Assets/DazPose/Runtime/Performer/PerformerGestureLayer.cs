using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer
{
    /// <summary>Additive, chestLower-masked finite gestures over the current seated body source.</summary>
    internal sealed class PerformerGestureLayer : IDisposable
    {
        private sealed class ActionState
        {
            public PerformerGesture Gesture;
            public AnimationClipPlayable Playable;
            public int Slot;
            public float Time;
            public AwaitableCompletionSource<GestureCompletion> Waiter;
        }

        private readonly PlayableGraph _graph;
        private readonly AnimationLayerMixerPlayable _layers;
        private readonly AvatarMask _mask;
        private ActionState _current;
        private ActionState _outgoing;
        private float _crossfadeElapsed;
        private float _crossfadeDuration;
        private bool _disposed;

        public Playable OutputPlayable => _layers;
        public bool IsGesturing => _current != null;
        public PerformerGesture CurrentGesture => _current != null ? _current.Gesture : null;
        public float Progress => _current == null || _current.Gesture.Clip.length <= 0f
            ? 0f : Mathf.Clamp01(_current.Time / _current.Gesture.Clip.length);

        public PerformerGestureLayer(PlayableGraph graph, Playable baseSource, Animator animator, AvatarMask mask)
        {
            if (!graph.IsValid()) throw new ArgumentException("A valid performer PlayableGraph is required.", nameof(graph));
            if (!baseSource.IsValid()) throw new ArgumentException("A valid seated body source is required.", nameof(baseSource));
            if (!PerformerGestureMaskUtility.IsValidMask(animator, mask, out string reason))
                throw new InvalidOperationException(reason);

            _graph = graph;
            _mask = mask;
            _layers = AnimationLayerMixerPlayable.Create(graph, 3);
            if (!graph.Connect(baseSource, 0, _layers, 0))
                throw new InvalidOperationException("Could not connect Gesture above the seated performer body source.");
            _layers.SetInputWeight(0, 1f);
            _layers.SetInputWeight(1, 0f);
            _layers.SetInputWeight(2, 0f);
            for (uint layer = 1; layer <= 2; layer++)
            {
                _layers.SetLayerAdditive(layer, true);
                _layers.SetLayerMaskFromAvatarMask(layer, _mask);
            }
        }

        public void Request(PerformerGesture gesture, AwaitableCompletionSource<GestureCompletion> waiter)
        {
            ThrowIfDisposed();
            if (gesture == null) throw new ArgumentNullException(nameof(gesture));
            if (!gesture.IsReady(out string reason)) throw new InvalidOperationException(reason);

            int slot = _current == null ? 0 : 1 - _current.Slot;
            ActionState superseded = _current;
            AwaitableCompletionSource<GestureCompletion> supersededWaiter = superseded != null
                ? superseded.Waiter : null;

            // A request is an action, even when it repeats the same asset. Keep only the most
            // recent outgoing sample so the graph has a bounded two-clip crossfade.
            ClearAction(_outgoing);
            _outgoing = null;
            ActionState next = CreateAction(gesture, waiter, slot);
            _current = next;
            _crossfadeElapsed = 0f;
            _crossfadeDuration = superseded == null ? 0f : EffectiveBlendIn(gesture);
            if (superseded != null && _crossfadeDuration > 0f)
                _outgoing = superseded;
            else
                ClearAction(superseded);

            UpdateLayerWeights();
            // State and graph are fully installed before resolving the old waiter; its
            // continuation may synchronously issue another Gesture request.
            supersededWaiter?.TrySetResult(GestureCompletion.Superseded);
        }

        public void Advance(float deltaTime)
        {
            if (_disposed || _current == null) return;
            float dt = Mathf.Max(0f, deltaTime);
            _current.Time = Mathf.Min(_current.Gesture.Clip.length, _current.Time + dt);
            _current.Playable.SetTime(_current.Time);
            _current.Playable.SetDone(false);

            if (_outgoing != null)
            {
                _outgoing.Time = Mathf.Min(_outgoing.Gesture.Clip.length, _outgoing.Time + dt);
                _outgoing.Playable.SetTime(_outgoing.Time);
                _outgoing.Playable.SetDone(false);
                _crossfadeElapsed = Mathf.Min(_crossfadeDuration, _crossfadeElapsed + dt);
                if (_crossfadeElapsed >= _crossfadeDuration)
                {
                    ClearAction(_outgoing);
                    _outgoing = null;
                }
            }

            if (_current.Time >= _current.Gesture.Clip.length)
            {
                ActionState completed = _current;
                AwaitableCompletionSource<GestureCompletion> completedWaiter = completed.Waiter;
                ClearAction(_outgoing);
                _outgoing = null;
                _current = null;
                SetSlotWeight(completed.Slot, 0f);
                ClearAction(completed);
                _crossfadeElapsed = _crossfadeDuration = 0f;
                completedWaiter?.TrySetResult(GestureCompletion.Completed);
                return;
            }

            UpdateLayerWeights();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ActionState disabled = _current;
            AwaitableCompletionSource<GestureCompletion> disabledWaiter = disabled != null
                ? disabled.Waiter : null;
            _current = null;
            ClearAction(_outgoing);
            _outgoing = null;
            ClearAction(disabled);
            _crossfadeElapsed = _crossfadeDuration = 0f;
            if (_layers.IsValid())
            {
                _layers.SetInputWeight(0, 1f);
                _layers.SetInputWeight(1, 0f);
                _layers.SetInputWeight(2, 0f);
                _layers.Destroy();
            }
            disabledWaiter?.TrySetResult(GestureCompletion.PerformerDisabled);
        }

        private ActionState CreateAction(PerformerGesture gesture,
            AwaitableCompletionSource<GestureCompletion> waiter, int slot)
        {
            AnimationClipPlayable playable = AnimationClipPlayable.Create(_graph, gesture.Clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            playable.SetSpeed(0d);
            playable.SetTime(0d);
            playable.SetDone(false);
            int layer = slot + 1;
            if (!_graph.Connect(playable, 0, _layers, layer))
            {
                playable.Destroy();
                throw new InvalidOperationException("Could not connect Gesture clip '" + gesture.name + "' to the additive layer.");
            }
            _layers.SetLayerMaskFromAvatarMask((uint)layer, _mask);
            return new ActionState { Gesture = gesture, Playable = playable, Slot = slot, Waiter = waiter };
        }

        private void UpdateLayerWeights()
        {
            if (_current == null)
            {
                _layers.SetInputWeight(1, 0f);
                _layers.SetInputWeight(2, 0f);
                return;
            }

            float currentWeight;
            float outgoingWeight = 0f;
            if (_outgoing != null && _crossfadeDuration > 0f)
            {
                float blend = SmoothStep01(_crossfadeElapsed / _crossfadeDuration);
                currentWeight = blend;
                outgoingWeight = EvaluateEnvelope(_outgoing) * (1f - blend);
            }
            else
            {
                currentWeight = EvaluateEnvelope(_current);
            }

            SetSlotWeight(_current.Slot, currentWeight);
            if (_outgoing != null) SetSlotWeight(_outgoing.Slot, outgoingWeight);
        }

        private void SetSlotWeight(int slot, float weight)
        {
            if (_layers.IsValid()) _layers.SetInputWeight(slot + 1, Mathf.Clamp01(weight));
        }

        private static float EvaluateEnvelope(ActionState action)
        {
            float length = action.Gesture.Clip.length;
            float blendIn = EffectiveBlendIn(action.Gesture);
            float blendOut = EffectiveBlendOut(action.Gesture);
            float inWeight = blendIn <= 0f ? 1f : SmoothStep01(action.Time / blendIn);
            float outWeight = blendOut <= 0f ? 1f : SmoothStep01((length - action.Time) / blendOut);
            return Mathf.Min(inWeight, outWeight);
        }

        private static float EffectiveBlendIn(PerformerGesture gesture) =>
            Mathf.Min(gesture.BlendInSeconds, gesture.Clip.length * 0.45f);

        private static float EffectiveBlendOut(PerformerGesture gesture) =>
            Mathf.Min(gesture.BlendOutSeconds, gesture.Clip.length * 0.45f);

        private static float SmoothStep01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        private void ClearAction(ActionState action)
        {
            if (action == null) return;
            SetSlotWeight(action.Slot, 0f);
            if (action.Playable.IsValid())
            {
                if (_layers.IsValid()) _layers.DisconnectInput(action.Slot + 1);
                action.Playable.Destroy();
            }
            action.Gesture = null;
            action.Waiter = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PerformerGestureLayer));
        }
    }
}
