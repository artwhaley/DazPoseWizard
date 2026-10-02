using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Directs dissolve-out, hidden relocation, and materialization for the current standing performer.</summary>
    internal sealed class PerformerDissolve : IDisposable
    {
        private enum Phase
        {
            DissolveOut,
            Transit,
            AwaitHiddenPoseEvaluation,
            Materialize
        }

        private readonly Transform _actor;
        private readonly PerformerDissolveProfile _profile;
        private readonly PerformerDissolveRig _rig;
        private readonly Action<PerformerPose> _assertArrivalPose;
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
        private Vector3 _transitEnd;
        private PerformerPose _arrivalPose;

        public bool IsDissolving => _active;

        public PerformerDissolve(Transform actor, PerformerDissolveProfile profile, PerformerDissolveRig rig,
            Action<PerformerPose> assertArrivalPose)
        {
            _actor = actor != null ? actor : throw new ArgumentNullException(nameof(actor));
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _rig = rig != null ? rig : throw new ArgumentNullException(nameof(rig));
            _assertArrivalPose = assertArrivalPose ?? throw new ArgumentNullException(nameof(assertArrivalPose));
            if (!_profile.IsReady(out string reason)) throw new InvalidOperationException(reason);
            if (!_rig.IsReady(_profile, out reason)) throw new InvalidOperationException(reason);
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
                    case Phase.Transit:
                        AdvanceTransit(deltaTime);
                        break;
                    case Phase.AwaitHiddenPoseEvaluation:
                        if (Time.frameCount > _relocationFrame)
                        {
                            _rig.RevealBody();
                            _phase = Phase.Materialize;
                            _phaseElapsed = 0f;
                            _rig.SetDissolveProgress(1f);
                        }
                        break;
                    case Phase.Materialize:
                        AdvanceMaterialize(deltaTime);
                        break;
                }
            }
            catch (Exception exception)
            {
                try { _rig.Restore(immediateTransitCleanup: false); }
                catch (Exception cleanupException) { Debug.LogException(cleanupException, _actor); }
                finally
                {
                    _active = false;
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
            try { _rig.Restore(immediateTransitCleanup: false); }
            catch (Exception exception) { Debug.LogException(exception, _actor); }
            finally
            {
                _active = false;
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
            Vector3 centerOffsetInActorAxes = Quaternion.Inverse(actorRotation) * (_rig.BodyCenter - _actor.position);
            _destination = destination;
            _arrivalRotation = rotation;
            _arrivalPose = arrivalPose;
            _transitStart = _rig.BodyCenter;
            _transitEnd = destination + (rotation ?? actorRotation) * centerOffsetInActorAxes;
            _phaseElapsed = 0f;
            _phase = Phase.DissolveOut;
            _active = true;

            try
            {
                _rig.Begin(_profile);
                _rig.SetDissolveProgress(0f);
                _rig.StartDissolveVfx();
                PlaySpatialAudio(_profile.DepartureAudio, _transitStart, actorRotation,
                    _profile.DeparturePitch, "Performer Dissolve Departure Audio");
                if (waiter != null) _waiters.Add(waiter);
            }
            catch
            {
                try { _rig.Restore(immediateTransitCleanup: true); }
                finally { _active = false; }
                throw;
            }
        }

        private void AdvanceDissolveOut(float deltaTime)
        {
            _phaseElapsed += Mathf.Max(0f, deltaTime);
            float normalized = Mathf.Clamp01(_phaseElapsed / _profile.DissolveOutDuration);
            _rig.SetDissolveProgress(_rig.EvaluateEffectCurve(normalized));
            if (normalized < 1f) return;

            _rig.SetDissolveProgress(1f);
            _rig.ForceBodyHidden();
            _rig.StopDissolveVfx();
            _rig.StartTransit(_profile.TransitPrefab, _transitStart);
            _phase = Phase.Transit;
            _phaseElapsed = 0f;
        }

        private void AdvanceTransit(float deltaTime)
        {
            _phaseElapsed += Mathf.Max(0f, deltaTime);
            float normalized = Mathf.Clamp01(_phaseElapsed / _profile.TransitDuration);
            float eased = Mathf.SmoothStep(0f, 1f, normalized);
            Vector3 position = Vector3.LerpUnclamped(_transitStart, _transitEnd, eased)
                + Vector3.up * (_profile.TransitArcHeight * 4f * normalized * (1f - normalized));
            _rig.MoveTransit(position);
            if (normalized < 1f) return;

            _rig.MoveTransit(_transitEnd);
            _rig.StopTransit(_profile.EffectTailLifetime);
            _actor.SetPositionAndRotation(_destination, _arrivalRotation ?? _actor.rotation);
            if (_arrivalPose != null) _assertArrivalPose(_arrivalPose);
            _rig.SetDissolveProgress(1f);
            _rig.StartDissolveVfx();
            PlaySpatialAudio(_profile.ArrivalAudio, _destination, _actor.rotation,
                _profile.ArrivalPitch, "Performer Dissolve Arrival Audio");
            _relocationFrame = Time.frameCount;
            _phase = Phase.AwaitHiddenPoseEvaluation;
            _phaseElapsed = 0f;
        }

        private void AdvanceMaterialize(float deltaTime)
        {
            _phaseElapsed += Mathf.Max(0f, deltaTime);
            float normalized = Mathf.Clamp01(_phaseElapsed / _profile.MaterializeDuration);
            float eased = _rig.EvaluateEffectCurve(normalized);
            _rig.SetDissolveProgress(1f - eased);
            if (normalized < 1f) return;

            _rig.SetDissolveProgress(0f);
            _rig.Finish(_profile.EffectTailLifetime);
            _active = false;
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
    }
}
