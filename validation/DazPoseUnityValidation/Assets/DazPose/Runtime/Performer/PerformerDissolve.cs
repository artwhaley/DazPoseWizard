using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer
{
    internal interface IPerformerDissolvePresentation
    {
        Vector3 BodyCenter { get; }
        float EvaluateEffectCurve(float normalizedTime);
        void Begin(PerformerDissolveProfile profile);
        void SetDissolveProgress(float progress);
        void CompleteDeparture();
        void BeginTransit(Vector3 destinationCenter);
        void SetTransitProgress(float progress);
        void BeginMaterialize();
        void SetMaterializeProgress(float progress);
        void Finish();
        void Restore();
    }

    /// <summary>Directs dissolve-out, hidden relocation, transit, and materialization.</summary>
    internal sealed class PerformerDissolve : IDisposable
    {
        private enum Phase
        {
            DissolveOut,
            HiddenRelocate,
            Transit,
            Materialize,
            Complete
        }

        private readonly Transform _actor;
        private readonly PerformerDissolveProfile _profile;
        private readonly IPerformerDissolvePresentation _presentation;
        private readonly Action<PerformerPose> _assertArrivalPose;
        private readonly Func<int> _frameCount;
        private readonly List<AwaitableCompletionSource<DissolveCompletion>> _waiters =
            new List<AwaitableCompletionSource<DissolveCompletion>>();
        private readonly List<GameObject> _audioObjects = new List<GameObject>();
        private bool _active;
        private bool _disposed;
        private Phase _phase;
        private float _phaseElapsed;
        private int _relocationFrame;
        private Vector3 _destination;
        private Quaternion? _arrivalRotation;
        private Vector3 _transitStart;
        private PerformerPose _arrivalPose;

        public bool IsDissolving => _active;

        public PerformerDissolve(Transform actor, PerformerDissolveProfile profile, PerformerDissolveRig rig,
            Action<PerformerPose> assertArrivalPose)
            : this(actor, profile, new RigPresentation(rig), assertArrivalPose, null)
        {
        }

        internal PerformerDissolve(Transform actor, PerformerDissolveProfile profile,
            IPerformerDissolvePresentation presentation, Action<PerformerPose> assertArrivalPose,
            Func<int> frameCount)
        {
            _actor = actor != null ? actor : throw new ArgumentNullException(nameof(actor));
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _assertArrivalPose = assertArrivalPose ?? throw new ArgumentNullException(nameof(assertArrivalPose));
            _frameCount = frameCount ?? (() => Time.frameCount);
        }

        public void DissolveTo(Vector3 position, PerformerPose arrivalPose,
            AwaitableCompletionSource<DissolveCompletion> waiter)
        {
            Begin(position, null, arrivalPose, waiter);
        }

        public void DissolveTo(Transform target, PerformerPose arrivalPose,
            AwaitableCompletionSource<DissolveCompletion> waiter)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                throw new ArgumentException("Dissolve target must have a nonzero planar forward vector.", nameof(target));
            Begin(target.position, Quaternion.LookRotation(forward.normalized, Vector3.up), arrivalPose, waiter);
        }

        public void Advance(float deltaTime)
        {
            _audioObjects.RemoveAll(item => item == null);
            if (!_active || _disposed) return;
            try
            {
                switch (_phase)
                {
                    case Phase.DissolveOut:
                        AdvanceDissolveOut(deltaTime);
                        break;
                    case Phase.HiddenRelocate:
                        AdvanceHiddenRelocate();
                        break;
                    case Phase.Transit:
                        AdvanceTransit(deltaTime);
                        break;
                    case Phase.Materialize:
                        AdvanceMaterialize(deltaTime);
                        break;
                }
            }
            catch (Exception exception)
            {
                try { _presentation.Restore(); }
                catch (Exception cleanupException) { Debug.LogException(cleanupException, _actor); }
                finally
                {
                    _active = false;
                    _phase = Phase.Complete;
                    Complete(DissolveCompletion.PerformerDisabled);
                    DestroyAudioObjects();
                }
                Debug.LogException(exception, _actor);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _presentation.Restore(); }
            catch (Exception exception) { Debug.LogException(exception, _actor); }
            finally
            {
                _active = false;
                _phase = Phase.Complete;
                Complete(DissolveCompletion.PerformerDisabled);
                DestroyAudioObjects();
            }
        }

        private void Begin(Vector3 destination, Quaternion? rotation, PerformerPose arrivalPose,
            AwaitableCompletionSource<DissolveCompletion> waiter)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PerformerDissolve));
            if (_active) throw new InvalidOperationException("A dissolve is already in progress; wait for it to arrive before requesting another.");
            ValidateFinite(destination);
            if (arrivalPose != null && arrivalPose.Clip == null)
                throw new ArgumentException("The arrival PerformerPose has no AnimationClip.", nameof(arrivalPose));

            Quaternion actorRotation = _actor.rotation;
            _destination = destination;
            _arrivalRotation = rotation;
            _arrivalPose = arrivalPose;
            _transitStart = _presentation.BodyCenter;
            _phaseElapsed = 0f;
            _phase = Phase.DissolveOut;
            _active = true;

            try
            {
                _presentation.Begin(_profile);
                _presentation.SetDissolveProgress(0f);
                PlaySpatialAudio(_profile.DepartureAudio, _transitStart, actorRotation,
                    _profile.DeparturePitch, "Performer Dissolve Departure Audio");
                if (waiter != null) _waiters.Add(waiter);
            }
            catch
            {
                try { _presentation.Restore(); }
                finally { _active = false; }
                throw;
            }
        }

        private void AdvanceDissolveOut(float deltaTime)
        {
            _phaseElapsed += Mathf.Max(0f, deltaTime);
            float normalized = Mathf.Clamp01(_phaseElapsed / _profile.DissolveOutDuration);
            _presentation.SetDissolveProgress(_presentation.EvaluateEffectCurve(normalized));
            if (normalized < 1f) return;

            _presentation.SetDissolveProgress(1f);
            _presentation.CompleteDeparture();

            // Relocation and any snap pose happen only after the mesh has fully dissolved.
            _actor.SetPositionAndRotation(_destination, _arrivalRotation ?? _actor.rotation);
            if (_arrivalPose != null) _assertArrivalPose(_arrivalPose);
            _relocationFrame = _frameCount();
            _phase = Phase.HiddenRelocate;
            _phaseElapsed = 0f;
        }

        private void AdvanceHiddenRelocate()
        {
            if (_frameCount() <= _relocationFrame) return;

            // Sample after a later animation evaluation so pose-dependent renderer bounds are current.
            _presentation.BeginTransit(_presentation.BodyCenter);
            _phase = Phase.Transit;
            _phaseElapsed = 0f;
        }

        private void AdvanceTransit(float deltaTime)
        {
            _phaseElapsed += Mathf.Max(0f, deltaTime);
            float normalized = Mathf.Clamp01(_phaseElapsed / _profile.TransitDuration);
            _presentation.SetTransitProgress(_presentation.EvaluateEffectCurve(normalized));
            if (normalized < 1f) return;

            _presentation.SetTransitProgress(1f);
            _presentation.BeginMaterialize();
            PlaySpatialAudio(_profile.ArrivalAudio, _destination, _actor.rotation,
                _profile.ArrivalPitch, "Performer Dissolve Arrival Audio");
            _phase = Phase.Materialize;
            _phaseElapsed = 0f;
        }

        private void AdvanceMaterialize(float deltaTime)
        {
            _phaseElapsed += Mathf.Max(0f, deltaTime);
            float normalized = Mathf.Clamp01(_phaseElapsed / _profile.MaterializeDuration);
            _presentation.SetMaterializeProgress(_presentation.EvaluateEffectCurve(normalized));
            if (normalized < 1f) return;

            _presentation.SetMaterializeProgress(1f);
            _presentation.Finish();
            _active = false;
            _phase = Phase.Complete;
            Complete(DissolveCompletion.Arrived);
        }

        private void PlaySpatialAudio(AudioClip clip, Vector3 position, Quaternion rotation,
            float pitch, string objectName)
        {
            if (clip == null) return;
            _audioObjects.RemoveAll(item => item == null);
            var audioObject = new GameObject(objectName);
            _audioObjects.Add(audioObject);
            audioObject.transform.SetPositionAndRotation(position, rotation);
            AudioSource source = audioObject.AddComponent<AudioSource>();
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

        private void Complete(DissolveCompletion result)
        {
            AwaitableCompletionSource<DissolveCompletion>[] pending = _waiters.ToArray();
            _waiters.Clear();
            foreach (AwaitableCompletionSource<DissolveCompletion> waiter in pending)
                waiter.TrySetResult(result);
        }

        private void DestroyAudioObjects()
        {
            foreach (GameObject audioObject in _audioObjects)
                if (audioObject != null) UnityEngine.Object.Destroy(audioObject);
            _audioObjects.Clear();
        }

        private static void ValidateFinite(Vector3 value)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
                throw new ArgumentOutOfRangeException(nameof(value), "Dissolve destination must contain finite world coordinates.");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class RigPresentation : IPerformerDissolvePresentation
        {
            private readonly PerformerDissolveRig _rig;

            public RigPresentation(PerformerDissolveRig rig)
            {
                _rig = rig != null ? rig : throw new ArgumentNullException(nameof(rig));
            }

            public Vector3 BodyCenter => _rig.BodyCenter;
            public float EvaluateEffectCurve(float normalizedTime) => _rig.EvaluateEffectCurve(normalizedTime);
            public void Begin(PerformerDissolveProfile profile) => _rig.Begin(profile);
            public void SetDissolveProgress(float progress) => _rig.SetDissolveProgress(progress);
            public void CompleteDeparture() => _rig.CompleteDeparture();
            public void BeginTransit(Vector3 destinationCenter) => _rig.BeginTransit(destinationCenter);
            public void SetTransitProgress(float progress) => _rig.SetTransitProgress(progress);
            public void BeginMaterialize() => _rig.BeginMaterialize();
            public void SetMaterializeProgress(float progress) => _rig.SetMaterializeProgress(progress);
            public void Finish() => _rig.Finish();
            public void Restore() => _rig.Restore();
        }
    }
}
