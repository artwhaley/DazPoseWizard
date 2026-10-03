using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Snapshots one request's duration and the profile's relative phase weights.</summary>
    internal readonly struct PerformerDissolveTiming
    {
        public float Duration { get; }
        public float DepartureFraction { get; }
        public float FirstArrivalFraction { get; }
        public float FadeFraction { get; }
        public float DepartureDuration => Duration * DepartureFraction;
        public float FlightBaseDuration => Duration * FirstArrivalFraction;
        public float FadeDuration => Duration * FadeFraction;

        public PerformerDissolveTiming(PerformerDissolveProfile profile, float durationSeconds)
        {
            PerformerDissolve.ValidateDuration(durationSeconds);
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            double release = profile.DissolveOutDuration;
            double travel = profile.TransitDuration;
            double fade = profile.MaterializeDuration;
            double total = 2.0 * release + travel + fade;
            if (double.IsNaN(total) || double.IsInfinity(total) || release <= 0.0 || travel <= 0.0 || fade <= 0.0)
                throw new ArgumentException("Dissolve profile timing weights must be finite and positive.", nameof(profile));
            Duration = durationSeconds;
            DepartureFraction = (float)(release / total);
            FirstArrivalFraction = (float)((release + travel) / total);
            FadeFraction = (float)(fade / total);
        }
    }

    internal interface IPerformerDissolvePresentation
    {
        Vector3 BodyCenter { get; }
        float EvaluateEffectCurve(float normalizedTime);
        void Begin(PerformerDissolveProfile profile, PerformerDissolveTiming timing);
        void SetDissolveProgress(float progress);
        void CompleteDeparture();
        void BeginTransit(Vector3 destinationCenter);
        void RetargetTransit(Vector3 destinationCenter);
        void SetTransitProgress(float progress);
        void BeginMaterialize();
        void SetMaterializeProgress(float progress);
        void BeginVisibilityOut(PerformerDissolveProfile profile);
        void SetVisibilityOutProgress(float normalizedClock);
        void BeginVisibilityIn(PerformerDissolveProfile profile);
        void SetVisibilityInProgress(float normalizedClock);
        void Finish(bool hidden);
        void Restore(bool hidden);
    }

    /// <summary>
    /// Owns finite relocation and one-sided visibility transitions. Stable visibility is
    /// mirrored to SuccubusPerformer so it survives this runtime being disposed/recreated.
    /// </summary>
    internal sealed class PerformerDissolve : IDisposable
    {
        private enum Phase
        {
            DissolveOut,
            HiddenRelocate,
            Transit,
            Materialize,
            VisibilityOut,
            VisibilityIn,
            Complete
        }

        private enum Operation
        {
            None,
            Relocate,
            VisibilityOut,
            VisibilityIn
        }

        private readonly Transform _actor;
        private readonly PerformerDissolveProfile _profile;
        private readonly IPerformerDissolvePresentation _presentation;
        private readonly Action<PerformerPose> _assertArrivalPose;
        private readonly Func<int> _frameCount;
        private readonly Action<bool> _stableVisibilityChanged;
        private readonly List<AwaitableCompletionSource<DissolveCompletion>> _dissolveWaiters =
            new List<AwaitableCompletionSource<DissolveCompletion>>();
        private readonly List<AwaitableCompletionSource<VisibilityCompletion>> _visibilityWaiters =
            new List<AwaitableCompletionSource<VisibilityCompletion>>();
        private readonly List<GameObject> _audioObjects = new List<GameObject>();
        private bool _active;
        private bool _disposed;
        private bool _stableHiddenAtBegin;
        private bool _isStableHidden;
        private Phase _phase;
        private Operation _operation;
        private PerformerDissolveTiming _timing;
        private float _duration;
        private float _elapsed;
        private float _progress;
        private int _relocationFrame;
        private Vector3 _destination;
        private Quaternion? _arrivalRotation;
        private Vector3 _transitStart;
        private PerformerPose _arrivalPose;
        private PerformerVisibilityState _visibilityState;

        public bool IsDissolving => _active;
        public PerformerVisibilityState VisibilityState => _visibilityState;

        public PerformerDissolve(Transform actor, PerformerDissolveProfile profile, PerformerDissolveRig rig,
            Action<PerformerPose> assertArrivalPose)
            : this(actor, profile, new RigPresentation(rig), assertArrivalPose, null, false, null)
        {
        }

        internal PerformerDissolve(Transform actor, PerformerDissolveProfile profile, PerformerDissolveRig rig,
            Action<PerformerPose> assertArrivalPose, bool initiallyHidden, Action<bool> stableVisibilityChanged)
            : this(actor, profile, new RigPresentation(rig), assertArrivalPose, null,
                initiallyHidden, stableVisibilityChanged)
        {
        }

        internal PerformerDissolve(Transform actor, PerformerDissolveProfile profile,
            IPerformerDissolvePresentation presentation, Action<PerformerPose> assertArrivalPose,
            Func<int> frameCount)
            : this(actor, profile, presentation, assertArrivalPose, frameCount, false, null)
        {
        }

        internal PerformerDissolve(Transform actor, PerformerDissolveProfile profile,
            IPerformerDissolvePresentation presentation, Action<PerformerPose> assertArrivalPose,
            Func<int> frameCount, bool initiallyHidden, Action<bool> stableVisibilityChanged)
        {
            _actor = actor != null ? actor : throw new ArgumentNullException(nameof(actor));
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _assertArrivalPose = assertArrivalPose ?? throw new ArgumentNullException(nameof(assertArrivalPose));
            _frameCount = frameCount ?? (() => Time.frameCount);
            _stableVisibilityChanged = stableVisibilityChanged;
            _isStableHidden = initiallyHidden;
            _visibilityState = initiallyHidden ? PerformerVisibilityState.Hidden : PerformerVisibilityState.Visible;
        }

        public void DissolveTo(Vector3 position, PerformerPose arrivalPose,
            AwaitableCompletionSource<DissolveCompletion> waiter) =>
            DissolveTo(position, _profile.DefaultDuration, arrivalPose, waiter);

        public void DissolveTo(Transform target, PerformerPose arrivalPose,
            AwaitableCompletionSource<DissolveCompletion> waiter) =>
            DissolveTo(target, _profile.DefaultDuration, arrivalPose, waiter);

        public void DissolveTo(Vector3 position, float durationSeconds, PerformerPose arrivalPose,
            AwaitableCompletionSource<DissolveCompletion> waiter)
        {
            RequireStableState(PerformerVisibilityState.Visible, "DissolveTo");
            BeginRelocation(position, null, durationSeconds, arrivalPose, waiter);
        }

        public void DissolveTo(Transform target, float durationSeconds, PerformerPose arrivalPose,
            AwaitableCompletionSource<DissolveCompletion> waiter)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                throw new ArgumentException("Dissolve target must have a nonzero planar forward vector.", nameof(target));
            RequireStableState(PerformerVisibilityState.Visible, "DissolveTo");
            BeginRelocation(target.position, Quaternion.LookRotation(forward.normalized, Vector3.up),
                durationSeconds, arrivalPose, waiter);
        }

        public void DissolveOut(float durationSeconds,
            AwaitableCompletionSource<VisibilityCompletion> waiter)
        {
            ValidateDuration(durationSeconds);
            RequireStableState(PerformerVisibilityState.Visible, "DissolveOut");
            EnsureIdle("DissolveOut");
            BeginVisibilityTransition(Operation.VisibilityOut, durationSeconds, waiter);
        }

        public void DissolveIn(float durationSeconds,
            AwaitableCompletionSource<VisibilityCompletion> waiter)
        {
            ValidateDuration(durationSeconds);
            RequireStableState(PerformerVisibilityState.Hidden, "DissolveIn");
            EnsureIdle("DissolveIn");
            BeginVisibilityTransition(Operation.VisibilityIn, durationSeconds, waiter);
        }

        public void Advance(float deltaTime)
        {
            _audioObjects.RemoveAll(item => item == null);
            if (!_active || _disposed) return;
            try
            {
                // One captured duration drives every one-sided operation and the accepted
                // DissolveTo phase sequence. Profile changes cannot move an active clock.
                _elapsed = Mathf.Min(_duration, _elapsed + Mathf.Max(0f, deltaTime));
                _progress = Mathf.Clamp01(_elapsed / _duration);

                if (_operation == Operation.VisibilityOut)
                {
                    _presentation.SetVisibilityOutProgress(_progress);
                    if (_progress >= 1f) CompleteVisibilityTransition(hidden: true);
                    return;
                }
                if (_operation == Operation.VisibilityIn)
                {
                    _presentation.SetVisibilityInProgress(_progress);
                    if (_progress >= 1f) CompleteVisibilityTransition(hidden: false);
                    return;
                }

                _presentation.SetTransitProgress(_progress / _timing.FirstArrivalFraction);
                switch (_phase)
                {
                    case Phase.DissolveOut:
                        AdvanceDissolveOut();
                        break;
                    case Phase.HiddenRelocate:
                        AdvanceHiddenRelocate();
                        break;
                    case Phase.Transit:
                        AdvanceTransit();
                        break;
                    case Phase.Materialize:
                        AdvanceMaterialize();
                        break;
                }
            }
            catch (Exception exception)
            {
                RollbackActiveOperation();
                Debug.LogException(exception, _actor);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_active)
            {
                RollbackActiveOperation();
            }
            else
            {
                try { _presentation.Restore(_isStableHidden); }
                catch (Exception exception) { Debug.LogException(exception, _actor); }
            }
            _disposed = true;
            CompleteDissolveWaiters(DissolveCompletion.PerformerDisabled);
            CompleteVisibilityWaiters(VisibilityCompletion.PerformerDisabled);
            DestroyAudioObjects();
        }

        private void BeginRelocation(Vector3 destination, Quaternion? rotation, float durationSeconds,
            PerformerPose arrivalPose, AwaitableCompletionSource<DissolveCompletion> waiter)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PerformerDissolve));
            EnsureIdle("DissolveTo");
            ValidateFinite(destination);
            if (arrivalPose != null && arrivalPose.Clip == null)
                throw new ArgumentException("The arrival PerformerPose has no AnimationClip.", nameof(arrivalPose));

            PerformerDissolveTiming timing = new PerformerDissolveTiming(_profile, durationSeconds);
            Quaternion actorRotation = _actor.rotation;
            _destination = destination;
            _arrivalRotation = rotation;
            _arrivalPose = arrivalPose;
            _transitStart = _presentation.BodyCenter;
            _timing = timing;
            _duration = timing.Duration;
            _elapsed = 0f;
            _progress = 0f;
            _phase = Phase.DissolveOut;
            _operation = Operation.Relocate;
            _stableHiddenAtBegin = false;
            _active = true;

            try
            {
                _presentation.Begin(_profile, _timing);
                _presentation.SetDissolveProgress(0f);
                Vector3 bodyOffset = _transitStart - _actor.position;
                Quaternion destinationRotation = _arrivalRotation ?? actorRotation;
                Vector3 predictedCenter = _destination + destinationRotation * Quaternion.Inverse(actorRotation) * bodyOffset;
                _presentation.BeginTransit(predictedCenter);
                PlaySpatialAudio(_profile.DepartureAudio, _transitStart, actorRotation,
                    _profile.DeparturePitch, "Performer Dissolve Departure Audio");
                if (waiter != null) _dissolveWaiters.Add(waiter);
            }
            catch
            {
                RollbackActiveOperation();
                throw;
            }
        }

        private void BeginVisibilityTransition(Operation operation, float durationSeconds,
            AwaitableCompletionSource<VisibilityCompletion> waiter)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PerformerDissolve));
            _stableHiddenAtBegin = operation == Operation.VisibilityIn;
            _operation = operation;
            _phase = operation == Operation.VisibilityOut ? Phase.VisibilityOut : Phase.VisibilityIn;
            _duration = durationSeconds;
            _elapsed = 0f;
            _progress = 0f;
            _active = true;
            _visibilityState = operation == Operation.VisibilityOut
                ? PerformerVisibilityState.DissolvingOut : PerformerVisibilityState.DissolvingIn;

            try
            {
                Vector3 audioPosition = _presentation.BodyCenter;
                if (operation == Operation.VisibilityOut)
                {
                    _presentation.BeginVisibilityOut(_profile);
                    PlaySpatialAudio(_profile.DepartureAudio, audioPosition, _actor.rotation,
                        _profile.DeparturePitch, "Performer Dissolve Out Audio",
                        _profile.VisibilityAudioStartOffsetSeconds);
                }
                else
                {
                    _presentation.BeginVisibilityIn(_profile);
                    PlaySpatialAudio(_profile.ArrivalAudio, audioPosition, _actor.rotation,
                        _profile.ArrivalPitch, "Performer Dissolve In Audio",
                        _profile.VisibilityAudioStartOffsetSeconds);
                }
                if (waiter != null) _visibilityWaiters.Add(waiter);
            }
            catch
            {
                RollbackActiveOperation();
                throw;
            }
        }

        private void AdvanceDissolveOut()
        {
            float normalized = Mathf.Clamp01(_progress / _timing.DepartureFraction);
            _presentation.SetDissolveProgress(normalized);
            if (normalized < 1f) return;

            _presentation.SetDissolveProgress(1f);
            _presentation.CompleteDeparture();

            // Relocation and any snap pose happen only after the mesh has fully dissolved.
            _actor.SetPositionAndRotation(_destination, _arrivalRotation ?? _actor.rotation);
            if (_arrivalPose != null) _assertArrivalPose(_arrivalPose);
            _relocationFrame = _frameCount();
            _phase = Phase.HiddenRelocate;
        }

        private void AdvanceHiddenRelocate()
        {
            if (_frameCount() <= _relocationFrame) return;

            // Sample after a later animation evaluation so pose-dependent renderer bounds are current.
            _presentation.RetargetTransit(_presentation.BodyCenter);
            _phase = Phase.Transit;
        }

        private void AdvanceTransit()
        {
            if (_progress < _timing.FirstArrivalFraction) return;

            _presentation.BeginMaterialize();
            PlaySpatialAudio(_profile.ArrivalAudio, _destination, _actor.rotation,
                _profile.ArrivalPitch, "Performer Dissolve Arrival Audio");
            _phase = Phase.Materialize;
            UpdateMaterialize();
        }

        private void AdvanceMaterialize() => UpdateMaterialize();

        private void UpdateMaterialize()
        {
            float normalized = Mathf.Clamp01((_progress - _timing.FirstArrivalFraction) / (1f - _timing.FirstArrivalFraction));
            _presentation.SetMaterializeProgress(normalized);
            if (normalized < 1f) return;

            _presentation.Finish(hidden: false);
            _isStableHidden = false;
            _visibilityState = PerformerVisibilityState.Visible;
            _stableVisibilityChanged?.Invoke(false);
            _active = false;
            _operation = Operation.None;
            _phase = Phase.Complete;
            CompleteDissolveWaiters(DissolveCompletion.Arrived);
        }

        private void CompleteVisibilityTransition(bool hidden)
        {
            _presentation.Finish(hidden);
            _isStableHidden = hidden;
            _visibilityState = hidden ? PerformerVisibilityState.Hidden : PerformerVisibilityState.Visible;
            _stableVisibilityChanged?.Invoke(hidden);
            _active = false;
            _operation = Operation.None;
            _phase = Phase.Complete;
            CompleteVisibilityWaiters(hidden ? VisibilityCompletion.Hidden : VisibilityCompletion.Visible);
        }

        private void RollbackActiveOperation()
        {
            bool stableHidden = _active ? _stableHiddenAtBegin : _isStableHidden;
            try { _presentation.Restore(stableHidden); }
            catch (Exception exception) { Debug.LogException(exception, _actor); }
            finally
            {
                _isStableHidden = stableHidden;
                _visibilityState = stableHidden ? PerformerVisibilityState.Hidden : PerformerVisibilityState.Visible;
                _stableVisibilityChanged?.Invoke(stableHidden);
                _active = false;
                _operation = Operation.None;
                _phase = Phase.Complete;
                CompleteDissolveWaiters(DissolveCompletion.PerformerDisabled);
                CompleteVisibilityWaiters(VisibilityCompletion.PerformerDisabled);
                DestroyAudioObjects();
            }
        }

        private void PlaySpatialAudio(AudioClip clip, Vector3 position, Quaternion rotation,
            float pitch, string objectName, float startOffsetSeconds = 0f)
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
            float appliedOffset = 0f;
            if (startOffsetSeconds > 0f)
            {
                appliedOffset = Mathf.Clamp(startOffsetSeconds, 0f, Mathf.Max(0f, clip.length - 0.01f));
                source.time = appliedOffset;
            }
            source.Play();
            UnityEngine.Object.Destroy(audioObject, Mathf.Max(0.01f, clip.length - appliedOffset) / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        private void CompleteDissolveWaiters(DissolveCompletion result)
        {
            AwaitableCompletionSource<DissolveCompletion>[] pending = _dissolveWaiters.ToArray();
            _dissolveWaiters.Clear();
            foreach (AwaitableCompletionSource<DissolveCompletion> waiter in pending)
                waiter.TrySetResult(result);
        }

        private void CompleteVisibilityWaiters(VisibilityCompletion result)
        {
            AwaitableCompletionSource<VisibilityCompletion>[] pending = _visibilityWaiters.ToArray();
            _visibilityWaiters.Clear();
            foreach (AwaitableCompletionSource<VisibilityCompletion> waiter in pending)
                waiter.TrySetResult(result);
        }

        private void DestroyAudioObjects()
        {
            foreach (GameObject audioObject in _audioObjects)
                if (audioObject != null) UnityEngine.Object.Destroy(audioObject);
            _audioObjects.Clear();
        }

        private void EnsureIdle(string command)
        {
            if (_active)
                throw new InvalidOperationException(command + " is unavailable while another dissolve/visibility transition is in progress.");
        }

        private void RequireStableState(PerformerVisibilityState required, string command)
        {
            if (_visibilityState == required) return;
            string expected = required == PerformerVisibilityState.Visible ? "stable Visible" : "stable Hidden";
            throw new InvalidOperationException(command + " requires Lara to be in " + expected
                + " visibility state; current state is " + _visibilityState + ".");
        }

        internal static void ValidateDuration(float durationSeconds)
        {
            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Visibility/dissolve duration must be finite and greater than zero.");
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
            public void Begin(PerformerDissolveProfile profile, PerformerDissolveTiming timing) => _rig.Begin(profile, timing);
            public void SetDissolveProgress(float progress) => _rig.SetDissolveProgress(progress);
            public void CompleteDeparture() => _rig.CompleteDeparture();
            public void BeginTransit(Vector3 destinationCenter) => _rig.BeginTransit(destinationCenter);
            public void RetargetTransit(Vector3 destinationCenter) => _rig.RetargetTransit(destinationCenter);
            public void SetTransitProgress(float progress) => _rig.SetTransitProgress(progress);
            public void BeginMaterialize() => _rig.BeginMaterialize();
            public void SetMaterializeProgress(float progress) => _rig.SetMaterializeProgress(progress);
            public void BeginVisibilityOut(PerformerDissolveProfile profile) => _rig.BeginVisibilityOut(profile);
            public void SetVisibilityOutProgress(float normalizedClock) => _rig.SetVisibilityOutProgress(normalizedClock);
            public void BeginVisibilityIn(PerformerDissolveProfile profile) => _rig.BeginVisibilityIn(profile);
            public void SetVisibilityInProgress(float normalizedClock) => _rig.SetVisibilityInProgress(normalizedClock);
            public void Finish(bool hidden) => _rig.Finish(hidden);
            public void Restore(bool hidden) => _rig.Restore(hidden);
        }
    }
}
