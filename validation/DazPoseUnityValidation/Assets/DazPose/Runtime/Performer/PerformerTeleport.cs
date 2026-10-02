using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>One sharp, renderer-only conceal/snap/reveal action for a standing performer.</summary>
    internal sealed class PerformerTeleport : IDisposable
    {
        private struct RendererVisibility
        {
            public Renderer Renderer;
            public bool WasForcedOff;
        }

        private readonly Transform _actor;
        private readonly PerformerTeleportProfile _profile;
        private readonly Action<PerformerPose> _assertArrivalPose;
        private readonly List<AwaitableCompletionSource<TeleportCompletion>> _waiters =
            new List<AwaitableCompletionSource<TeleportCompletion>>();
        private readonly List<GameObject> _effects = new List<GameObject>();
        private readonly List<GameObject> _audioObjects = new List<GameObject>();
        private RendererVisibility[] _visibility;
        private bool _active;
        private bool _relocated;
        private bool _revealed;
        private bool _disposed;
        private float _elapsed;
        private int _relocationFrame;
        private Vector3 _destination;
        private Quaternion? _arrivalRotation;
        private PerformerPose _arrivalPose;

        public bool IsTeleporting => _active;

        public PerformerTeleport(Transform actor, PerformerTeleportProfile profile,
            Action<PerformerPose> assertArrivalPose)
        {
            _actor = actor != null ? actor : throw new ArgumentNullException(nameof(actor));
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _assertArrivalPose = assertArrivalPose ?? throw new ArgumentNullException(nameof(assertArrivalPose));
            if (!_profile.IsReady(out string reason)) throw new InvalidOperationException(reason);
        }

        public void TeleportTo(Vector3 position, PerformerPose arrivalPose, AwaitableCompletionSource<TeleportCompletion> waiter)
        {
            Begin(position, null, arrivalPose, waiter);
        }

        public void TeleportTo(Transform target, PerformerPose arrivalPose,
            AwaitableCompletionSource<TeleportCompletion> waiter)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                throw new ArgumentException("Teleport target must have a nonzero planar forward vector.", nameof(target));
            Begin(target.position, Quaternion.LookRotation(forward.normalized, Vector3.up), arrivalPose, waiter);
        }

        private void Begin(Vector3 destination, Quaternion? rotation, PerformerPose arrivalPose,
            AwaitableCompletionSource<TeleportCompletion> waiter)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PerformerTeleport));
            if (_active) throw new InvalidOperationException("A teleport is already in progress; wait for it to arrive before requesting another.");
            ValidateFinite(destination);
            if (arrivalPose != null && arrivalPose.Clip == null)
                throw new ArgumentException("The arrival PerformerPose has no AnimationClip.", nameof(arrivalPose));
            _destination = destination;
            _arrivalRotation = rotation;
            _arrivalPose = arrivalPose;
            if (waiter != null) _waiters.Add(waiter);
            _elapsed = 0f;
            _active = true;
            _relocated = _revealed = false;
            try
            {
                SpawnEffect(_actor.position, _actor.rotation);
                PlaySpatialAudio(_profile.DepartureAudio, _actor.position, _actor.rotation,
                    _profile.DeparturePitch, "Teleport Departure Audio");
            }
            catch
            {
                _active = false;
                _waiters.Clear();
                throw;
            }
        }

        public void Advance(float deltaTime)
        {
            if (!_active || _disposed) return;
            _elapsed += Mathf.Max(0f, deltaTime);
            try
            {
                if (!_relocated && _elapsed >= _profile.HideTime) RelocateWhileHidden();
                if (_relocated && !_revealed && _elapsed >= _profile.RevealDelay
                    && Time.frameCount > _relocationFrame)
                    Reveal();
                if (_revealed && _elapsed >= _profile.CompletionTime)
                    Complete(TeleportCompletion.Arrived);
            }
            catch (Exception exception)
            {
                RestoreVisibility();
                _active = false;
                Complete(TeleportCompletion.PerformerDisabled);
                Debug.LogException(exception, _actor);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            RestoreVisibility();
            _active = false;
            Complete(TeleportCompletion.PerformerDisabled);
            foreach (GameObject effect in _effects)
                if (effect != null) UnityEngine.Object.Destroy(effect);
            _effects.Clear();
            foreach (GameObject audioObject in _audioObjects)
                if (audioObject != null) UnityEngine.Object.Destroy(audioObject);
            _audioObjects.Clear();
        }

        private void RelocateWhileHidden()
        {
            Conceal();
            _actor.SetPositionAndRotation(_destination, _arrivalRotation ?? _actor.rotation);
            if (_arrivalPose != null) _assertArrivalPose(_arrivalPose);
            SpawnEffect(_destination, _actor.rotation);
            PlaySpatialAudio(_profile.ArrivalAudio, _destination, _actor.rotation,
                _profile.ArrivalPitch, "Teleport Arrival Audio");
            _relocationFrame = Time.frameCount;
            _relocated = true;
        }

        private void Conceal()
        {
            Renderer[] renderers = _actor.GetComponentsInChildren<Renderer>(true);
            _visibility = new RendererVisibility[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                _visibility[i] = new RendererVisibility { Renderer = renderer, WasForcedOff = renderer.forceRenderingOff };
                renderer.forceRenderingOff = true;
            }
        }

        private void Reveal()
        {
            RestoreVisibility();
            _revealed = true;
        }

        private void RestoreVisibility()
        {
            if (_visibility == null) return;
            foreach (RendererVisibility entry in _visibility)
                if (entry.Renderer != null) entry.Renderer.forceRenderingOff = entry.WasForcedOff;
            _visibility = null;
        }

        private void SpawnEffect(Vector3 position, Quaternion rotation)
        {
            if (_profile.EffectPrefab == null) return;
            GameObject effect = UnityEngine.Object.Instantiate(_profile.EffectPrefab, position, rotation);
            effect.name = "Performer Teleport Flash";
            _effects.Add(effect);
            UnityEngine.Object.Destroy(effect, _profile.EffectLifetime);
        }

        private void PlaySpatialAudio(AudioClip clip, Vector3 position, Quaternion rotation,
            float pitch, string objectName)
        {
            if (clip == null) return;
            var audioObject = new GameObject(objectName);
            _audioObjects.Add(audioObject);
            audioObject.transform.SetPositionAndRotation(position, rotation);
            var source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1f;
            source.maxDistance = 22f;
            source.pitch = pitch;
            source.clip = clip;
            source.Play();
            UnityEngine.Object.Destroy(audioObject, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        private void Complete(TeleportCompletion result)
        {
            _active = false;
            AwaitableCompletionSource<TeleportCompletion>[] pending = _waiters.ToArray();
            _waiters.Clear();
            foreach (AwaitableCompletionSource<TeleportCompletion> waiter in pending)
                waiter.TrySetResult(result);
        }

        private static void ValidateFinite(Vector3 value)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x)
                || float.IsNaN(value.y) || float.IsInfinity(value.y)
                || float.IsNaN(value.z) || float.IsInfinity(value.z))
                throw new ArgumentOutOfRangeException(nameof(value), "Teleport destination must contain finite world coordinates.");
        }
    }
}
