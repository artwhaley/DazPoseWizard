using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer
{
    /// <summary>Non-additive full-body action override below Gesture and above seating.</summary>
    internal sealed class PerformerActionLayer : IDisposable
    {
        private readonly PlayableGraph _graph;
        private readonly AnimationLayerMixerPlayable _layers;
        private AnimationClipPlayable _active;
        private bool _disposed;

        public Playable OutputPlayable => _layers;

        public PerformerActionLayer(PlayableGraph graph, Playable baseSource)
        {
            if (!graph.IsValid()) throw new ArgumentException("A valid performer PlayableGraph is required.", nameof(graph));
            if (!baseSource.IsValid()) throw new ArgumentException("A valid seated body source is required.", nameof(baseSource));
            _graph = graph;
            _layers = AnimationLayerMixerPlayable.Create(graph, 2);
            if (!graph.Connect(baseSource, 0, _layers, 0))
                throw new InvalidOperationException("Could not connect Action below Gesture to the seated body source.");
            _layers.SetInputWeight(0, 1f);
            _layers.SetInputWeight(1, 0f);
        }

        public void Begin(AnimationClip clip)
        {
            ThrowIfDisposed();
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            if (_active.IsValid()) throw new InvalidOperationException("An Action clip is already connected to the full-body layer.");
            AnimationClipPlayable playable = AnimationClipPlayable.Create(_graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            playable.SetSpeed(0d);
            playable.SetTime(0d);
            playable.SetDone(false);
            if (!_graph.Connect(playable, 0, _layers, 1))
            {
                playable.Destroy();
                throw new InvalidOperationException("Could not connect the PerformerAction clip to the full-body override layer.");
            }
            _active = playable;
            _layers.SetInputWeight(1, 0f);
        }

        public void SetFrame(float timeSeconds, float weight)
        {
            if (_disposed || !_active.IsValid()) return;
            _active.SetTime(Mathf.Max(0f, timeSeconds));
            _active.SetDone(false);
            _layers.SetInputWeight(1, Mathf.Clamp01(weight));
        }

        public void Release()
        {
            if (_layers.IsValid()) _layers.SetInputWeight(1, 0f);
            if (_active.IsValid())
            {
                if (_layers.IsValid()) _layers.DisconnectInput(1);
                _active.Destroy();
            }
            _active = default;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Release();
            if (_layers.IsValid())
            {
                _layers.SetInputWeight(0, 1f);
                _layers.Destroy();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PerformerActionLayer));
        }
    }
}
