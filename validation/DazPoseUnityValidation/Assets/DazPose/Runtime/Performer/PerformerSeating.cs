using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer
{
    public enum PerformerSeatedStyle { Basic, CrossLegs }

    public enum SeatingCompletion
    {
        Seated,
        Standing,
        Superseded,
        PerformerDisabled,
        Failed
    }

    public enum PerformerSeatingState
    {
        Standing,
        Approaching,
        Aligning,
        SittingDown,
        BasicSeated,
        CrossingLegs,
        CrossLegsSeated,
        PreparingUncross,
        UncrossingLegs,
        StandingUp
    }

    /// <summary>Chair-specific finite seating state machine. It does not own navigation or downstream life layers.</summary>
    internal sealed class PerformerSeating : IDisposable
    {
        private sealed class SitRequest
        {
            public PerformerSeat Seat;
            public PerformerSeatingProfile Profile;
            public PerformerSeatedStyle Style;
            public Vector3 ApproachPosition;
            public Quaternion ApproachRotation;
            public Vector3 SeatPosition;
            public Quaternion SeatRotation;
            public readonly List<AwaitableCompletionSource<SeatingCompletion>> Waiters =
                new List<AwaitableCompletionSource<SeatingCompletion>>();
        }

        private readonly Transform _actor;
        private readonly PerformerLocomotion _locomotion;
        private readonly PerformerSeatingLayer _layer;
        private readonly List<AwaitableCompletionSource<SeatingCompletion>> _standWaiters =
            new List<AwaitableCompletionSource<SeatingCompletion>>();

        private PerformerSeatingState _state;
        private SitRequest _sitRequest;
        private PerformerSeat _currentSeat;
        private PerformerSeatingProfile _currentProfile;
        private PerformerSeatedStyle _currentStyle;
        private PerformerSeatingMotion _motion;
        private float _motionTime;
        private float _motionEntryTime;
        private Vector3 _motionOriginPosition;
        private Quaternion _motionOriginRotation;
        private Vector3 _motionTargetPosition;
        private Quaternion _motionTargetRotation;
        private bool _exitAfterUncross;
        private float _uncrossBlendProgress;
        private float _uncrossBlendDuration = 0.5f;
        private bool _basicIdleCandidateActive;
        private float _alignElapsed;
        private Vector3 _alignStartPosition;
        private Quaternion _alignStartRotation;
        private Vector3 _lastContactError;
        private bool _disposed;

        public PerformerSeatingState State => _state;
        public PerformerSeat CurrentSeat => _currentSeat;
        public PerformerSeatedStyle CurrentStyle => _currentStyle;
        public PerformerSeatingMotion CurrentMotion => _motion;
        public float MotionTime => _motionTime;
        public float OwnershipWeight => _layer.OwnershipWeight;
        public float CrossLegsExitBlendProgress => _uncrossBlendProgress;
        public float CrossLegsExitBlendDuration => _state == PerformerSeatingState.PreparingUncross
            || _currentProfile == null ? _uncrossBlendDuration : _currentProfile.CrossLegsExitBlendSeconds;
        public Vector3 ContactError => _lastContactError;
        public bool BlocksLocomotion => _state == PerformerSeatingState.SittingDown
            || _state == PerformerSeatingState.BasicSeated
            || _state == PerformerSeatingState.CrossingLegs
            || _state == PerformerSeatingState.CrossLegsSeated
            || _state == PerformerSeatingState.PreparingUncross
            || _state == PerformerSeatingState.UncrossingLegs
            || _state == PerformerSeatingState.StandingUp;

        public PerformerSeating(Transform actor, PerformerLocomotion locomotion, PerformerSeatingLayer layer)
        {
            _actor = actor != null ? actor : throw new ArgumentNullException(nameof(actor));
            _locomotion = locomotion;
            _layer = layer != null ? layer : throw new ArgumentNullException(nameof(layer));
            _state = PerformerSeatingState.Standing;
        }

        public void SitAt(PerformerSeat seat, PerformerSeatedStyle? requestedStyle,
            AwaitableCompletionSource<SeatingCompletion> waiter)
        {
            ThrowIfDisposed();
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            if (_locomotion == null)
                throw new InvalidOperationException("SitAt requires a SuccubusPerformer with a configured, baked locomotion profile so it can walk to ApproachAnchor.");
            if (!seat.TryCaptureFrames(out Vector3 approach, out Quaternion approachRotation,
                    out Vector3 seatPosition, out Quaternion seatRotation, out string reason))
                throw new InvalidOperationException(reason);

            PerformerSeatedStyle style = requestedStyle ?? seat.DefaultStyle;
            if (!Enum.IsDefined(typeof(PerformerSeatedStyle), style))
                throw new ArgumentOutOfRangeException(nameof(requestedStyle), style, "Unknown seated style.");
            if (Vector3.Angle(approachRotation * Vector3.forward, seatRotation * Vector3.forward)
                > seat.FacingDisagreementWarningDegrees)
                Debug.LogWarning("PerformerSeat '" + seat.name + "' has ApproachAnchor and SeatAnchor forward axes that differ by "
                    + Vector3.Angle(approachRotation * Vector3.forward, seatRotation * Vector3.forward).ToString("0.0")
                    + "°. SitAt will honor both authored directions; inspect the anchors if this is unintended.", seat);

            if ((_state == PerformerSeatingState.Approaching || _state == PerformerSeatingState.Aligning)
                && _sitRequest != null)
            {
                if (_sitRequest.Seat == seat && _sitRequest.Style == style)
                {
                    if (waiter != null) _sitRequest.Waiters.Add(waiter);
                    return;
                }
                CompleteSitRequest(_sitRequest, SeatingCompletion.Superseded);
                _sitRequest = null;
                StartApproach(CreateRequest(seat, style, approach, approachRotation, seatPosition, seatRotation, waiter));
                return;
            }

            if (_state == PerformerSeatingState.BasicSeated || _state == PerformerSeatingState.CrossLegsSeated)
            {
                if (_currentSeat != seat)
                    throw new InvalidOperationException("The performer is seated at a different seat. Call StandUpAsync() before SitAt() with another seat.");
                if (_currentProfile != seat.SeatingProfile)
                    throw new InvalidOperationException("The current chair uses a different seating profile. StandUpAsync() before changing the performer's seating profile.");
                if (_currentStyle == style)
                {
                    waiter?.TrySetResult(SeatingCompletion.Seated);
                    return;
                }
                _sitRequest = CreateRequest(seat, style, _approachPosition, _approachRotation,
                    _seatPosition, _seatRotation, waiter);
                _currentProfile = _sitRequest.Profile;
                _layer.PrepareProfile(_currentProfile);
                if (style == PerformerSeatedStyle.CrossLegs) BeginCrossingLegs();
                else BeginPreparingUncross(false);
                return;
            }

            if (_state == PerformerSeatingState.SittingDown
                || _state == PerformerSeatingState.CrossingLegs
                || _state == PerformerSeatingState.PreparingUncross
                || _state == PerformerSeatingState.UncrossingLegs
                || _state == PerformerSeatingState.StandingUp)
            {
                if (_sitRequest != null && _sitRequest.Seat == seat && _sitRequest.Style == style)
                {
                    if (waiter != null) _sitRequest.Waiters.Add(waiter);
                    return;
                }
                throw new InvalidOperationException("A seating body transition is already in progress. Wait for it to reach a stable seated or standing state before requesting another seat/style.");
            }

            StartApproach(CreateRequest(seat, style, approach, approachRotation, seatPosition, seatRotation, waiter));
        }

        public void StandUp(AwaitableCompletionSource<SeatingCompletion> waiter)
        {
            ThrowIfDisposed();
            if (_state == PerformerSeatingState.Standing)
            {
                waiter?.TrySetResult(SeatingCompletion.Standing);
                return;
            }
            if (_state == PerformerSeatingState.StandingUp)
            {
                if (waiter != null) _standWaiters.Add(waiter);
                return;
            }
            if ((_state == PerformerSeatingState.PreparingUncross
                    || _state == PerformerSeatingState.UncrossingLegs) && _exitAfterUncross)
            {
                if (waiter != null) _standWaiters.Add(waiter);
                return;
            }
            if (_state != PerformerSeatingState.BasicSeated && _state != PerformerSeatingState.CrossLegsSeated)
                throw new InvalidOperationException("StandUp can begin only after the current sit/style transition reaches a stable seated state.");

            if (waiter != null) _standWaiters.Add(waiter);
            if (_currentStyle == PerformerSeatedStyle.CrossLegs) BeginPreparingUncross(true);
            else BeginSitEnd();
        }

        public void Advance(float deltaTime)
        {
            if (_disposed) return;
            float dt = Mathf.Max(0f, deltaTime);
            _layer.Advance(dt);

            switch (_state)
            {
                case PerformerSeatingState.Aligning:
                    AdvanceAlignment(dt);
                    break;
                case PerformerSeatingState.SittingDown:
                    AdvanceRootMotion(dt, true, false);
                    break;
                case PerformerSeatingState.CrossingLegs:
                    // Seated style changes only sample body clips. The actor root stays at the seat.
                    AdvanceBodyMotion(dt, true);
                    break;
                case PerformerSeatingState.CrossLegsSeated:
                    AdvanceSeatedLoop(dt);
                    break;
                case PerformerSeatingState.BasicSeated:
                    if (_basicIdleCandidateActive) AdvanceSeatedLoop(dt);
                    break;
                case PerformerSeatingState.PreparingUncross:
                    AdvancePreparingUncross();
                    break;
                case PerformerSeatingState.UncrossingLegs:
                    AdvanceBodyMotion(dt, false);
                    break;
                case PerformerSeatingState.StandingUp:
                    AdvanceRootMotion(dt, false, true);
                    break;
            }
        }

        public void PrepareForWalkRequest()
        {
            if (_state == PerformerSeatingState.Approaching || _state == PerformerSeatingState.Aligning)
            {
                SitRequest interrupted = _sitRequest;
                _sitRequest = null;
                _state = PerformerSeatingState.Standing;
                CompleteSitRequest(interrupted, SeatingCompletion.Superseded);
                return;
            }
            if (BlocksLocomotion)
                throw new InvalidOperationException("WalkTo is unavailable while the performer is sitting down, seated, changing seated style, or standing up. Await StandUpAsync() first.");
        }

        public void SetBasicIdleLoopCandidate(bool enabled)
        {
            ThrowIfDisposed();
            if (_state != PerformerSeatingState.BasicSeated || _currentProfile == null)
                throw new InvalidOperationException("The Basic seated loop can be auditioned only while Lara is stably seated in Basic style.");
            if (enabled)
            {
                PerformerSeatingMotion candidate = _currentProfile.BasicIdleLoopCandidate;
                if (candidate == null)
                    throw new InvalidOperationException("This seating profile has no baked KA_Idle10_Sit_Loop audition candidate.");
                if (_basicIdleCandidateActive) return;
                _motion = candidate;
                _motionTime = 0f;
                _layer.BeginMotion(candidate, 0f, _currentProfile.BodyBlendSeconds);
                _layer.SetActiveTime(0f);
                _basicIdleCandidateActive = true;
            }
            else
            {
                if (!_basicIdleCandidateActive) return;
                HoldBasicSeatedPose();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _state = PerformerSeatingState.Standing;
            SitRequest request = _sitRequest;
            _sitRequest = null;
            CompleteSitRequest(request, SeatingCompletion.PerformerDisabled);
            foreach (var waiter in _standWaiters) waiter.TrySetResult(SeatingCompletion.PerformerDisabled);
            _standWaiters.Clear();
            _layer.SetOwnership(false, 0f, true);
        }

        private Vector3 _approachPosition;
        private Quaternion _approachRotation;
        private Vector3 _seatPosition;
        private Quaternion _seatRotation;

        private SitRequest CreateRequest(PerformerSeat seat, PerformerSeatedStyle style,
            Vector3 approach, Quaternion approachRotation, Vector3 seatPosition,
            Quaternion seatRotation, AwaitableCompletionSource<SeatingCompletion> waiter)
        {
            var request = new SitRequest
            {
                Seat = seat,
                Profile = seat.SeatingProfile,
                Style = style,
                ApproachPosition = approach,
                ApproachRotation = approachRotation,
                SeatPosition = seatPosition,
                SeatRotation = seatRotation
            };
            if (waiter != null) request.Waiters.Add(waiter);
            return request;
        }

        private void StartApproach(SitRequest request)
        {
            _layer.PrepareProfile(request.Profile);
            _sitRequest = request;
            _currentProfile = request.Profile;
            _approachPosition = request.ApproachPosition;
            _approachRotation = request.ApproachRotation;
            _seatPosition = request.SeatPosition;
            _seatRotation = request.SeatRotation;
            _state = PerformerSeatingState.Approaching;
            AwaitApproach(request);
        }

        private async void AwaitApproach(SitRequest request)
        {
            try
            {
                LocomotionCompletion result = await _locomotion.WalkToAsync(
                    request.ApproachPosition, request.ApproachRotation * Vector3.forward);
                if (_disposed || !ReferenceEquals(request, _sitRequest)) return;
                if (result != LocomotionCompletion.Arrived)
                {
                    _sitRequest = null;
                    _state = PerformerSeatingState.Standing;
                    CompleteSitRequest(request, result == LocomotionCompletion.PerformerDisabled
                        ? SeatingCompletion.PerformerDisabled : SeatingCompletion.Superseded);
                    return;
                }
                BeginAlignment(request);
            }
            catch (Exception exception)
            {
                if (_disposed || !ReferenceEquals(request, _sitRequest)) return;
                Debug.LogException(exception, _actor);
                _sitRequest = null;
                _state = PerformerSeatingState.Standing;
                CompleteSitRequest(request, SeatingCompletion.Failed);
            }
        }

        private void BeginAlignment(SitRequest request)
        {
            _approachPosition = request.ApproachPosition;
            _approachRotation = request.ApproachRotation;
            _seatPosition = request.SeatPosition;
            _seatRotation = request.SeatRotation;
            _alignStartPosition = _actor.position;
            _alignStartRotation = _actor.rotation;
            float positionError = Vector3.Distance(_alignStartPosition, _approachPosition);
            float facingError = Quaternion.Angle(_alignStartRotation, _approachRotation);
            if (positionError > _currentProfile.MaximumApproachPositionErrorMeters
                || facingError > _currentProfile.MaximumApproachFacingErrorDegrees)
                throw new InvalidOperationException("WalkTo reached the chair approach with "
                    + positionError.ToString("F3") + " m position error and "
                    + facingError.ToString("F1") + "° facing error. Sit_Start was not played because the actor is outside the seat profile's alignment tolerance.");
            _alignElapsed = 0f;
            if (Vector3.Distance(_alignStartPosition, _approachPosition) <= 0.002f
                && Quaternion.Angle(_alignStartRotation, _approachRotation) <= 0.25f)
            {
                _actor.SetPositionAndRotation(_approachPosition, _approachRotation);
                BeginSitStart();
                return;
            }
            _state = PerformerSeatingState.Aligning;
        }

        private void AdvanceAlignment(float deltaTime)
        {
            SitRequest request = _sitRequest;
            if (request == null) return;
            _alignElapsed += deltaTime;
            float duration = _currentProfile.ApproachAlignmentSeconds;
            float progress = duration <= 0f ? 1f : Mathf.Clamp01(_alignElapsed / duration);
            float smooth = Smooth01(progress);
            _actor.position = Vector3.Lerp(_alignStartPosition, _approachPosition, smooth);
            _actor.rotation = Quaternion.Slerp(_alignStartRotation, _approachRotation, smooth);
            if (progress >= 1f)
            {
                _actor.SetPositionAndRotation(_approachPosition, _approachRotation);
                BeginSitStart();
            }
        }

        private void BeginSitStart()
        {
            _currentSeat = _sitRequest.Seat;
            _currentStyle = _sitRequest.Style;
            StartRootMotion(_currentProfile.SitStart, 0f,
                PerformerSeatingState.SittingDown, _seatPosition - ScaleOffsetForRotation(
                    _seatRotation, _currentProfile.SitStart.PelvisOffsetAt(1f)),
                _seatRotation, true);
            _layer.SetOwnership(true, _currentProfile.BodyBlendSeconds);
        }

        private void AdvanceRootMotion(float deltaTime, bool seatingDown, bool fadingOut)
        {
            if (_motion == null || _currentProfile == null || deltaTime <= 0f) return;
            _motionTime = Mathf.Min(_motion.DurationSeconds,
                _motionTime + deltaTime * _currentProfile.PlaybackSpeed);
            _layer.SetActiveTime(_motionTime);
            float phase = Mathf.Clamp01(_motionTime / _motion.DurationSeconds);
            _motion.EvaluateAnchoredRoot(_motionTime, _motionEntryTime,
                _motionOriginPosition, _motionOriginRotation, _motionTargetPosition, _motionTargetRotation,
                _currentProfile.FinalBlendSeconds, _currentProfile.PlaybackSpeed,
                out Vector3 position, out Quaternion rotation);
            _actor.SetPositionAndRotation(position, rotation);

            float finalWindowStart = Mathf.Max(_motionEntryTime,
                _motion.DurationSeconds - _currentProfile.FinalBlendSeconds * _currentProfile.PlaybackSpeed);

            // Sit_End may begin at a measured entry phase already inside the final blend
            // window. Testing the current time (rather than requiring a crossed boundary)
            // starts its ownership fade on the first update in that case.
            if (fadingOut && _motionTime + 0.00001f >= finalWindowStart)
            {
                float remainingSeconds = (_motion.DurationSeconds - _motionTime)
                    / Mathf.Max(0.0001f, _currentProfile.PlaybackSpeed);
                _layer.SetOwnership(false, Mathf.Min(_currentProfile.FinalBlendSeconds, remainingSeconds));
            }
            _lastContactError = _seatPosition - (_actor.TransformPoint(
                (seatingDown ? _currentProfile.SitStart : _motion).PelvisOffsetAt(phase)));

            if (_motionTime + 0.00001f < _motion.DurationSeconds) return;
            _actor.SetPositionAndRotation(_motionTargetPosition, _motionTargetRotation);
            if (seatingDown)
            {
                HoldBasicSeatedPose();
                if (_sitRequest != null && _sitRequest.Style == PerformerSeatedStyle.CrossLegs)
                    BeginCrossingLegs();
                else
                    CompleteSeatedRequest(PerformerSeatedStyle.Basic);
            }
            else
            {
                _layer.SetOwnership(false, 0f, true);
                _lastContactError = Vector3.zero;
                _currentSeat = null;
                _currentProfile = null;
                _state = PerformerSeatingState.Standing;
                _currentStyle = PerformerSeatedStyle.Basic;
                _motion = null;
                foreach (var waiter in _standWaiters) waiter.TrySetResult(SeatingCompletion.Standing);
                _standWaiters.Clear();
            }
        }

        private void StartRootMotion(PerformerSeatingMotion motion, float normalizedStart,
            PerformerSeatingState state, Vector3 targetPosition, Quaternion targetRotation, bool isSitStart)
        {
            _motion = motion;
            _motionEntryTime = Mathf.Clamp01(normalizedStart) * motion.DurationSeconds;
            _motionTime = _motionEntryTime;
            _motionOriginPosition = _actor.position;
            _motionOriginRotation = PerformerSeat.YawRotation(_actor.forward);
            _motionTargetPosition = targetPosition;
            _motionTargetRotation = targetRotation;
            _state = state;
            _layer.BeginMotion(motion, normalizedStart, _currentProfile.BodyBlendSeconds);
            _layer.SetActiveTime(_motionTime);
            _lastContactError = isSitStart ? _seatPosition - _actor.TransformPoint(motion.PelvisOffsetAt(normalizedStart)) : Vector3.zero;
        }

        private void BeginCrossingLegs()
        {
            if (_currentProfile == null) return;
            _basicIdleCandidateActive = false;
            _uncrossBlendProgress = 0f;
            _currentStyle = PerformerSeatedStyle.CrossLegs;
            StartBodyMotion(_currentProfile.CrossLegsStart, 0f, PerformerSeatingState.CrossingLegs);
        }

        private void BeginPreparingUncross(bool standAfter)
        {
            _exitAfterUncross = standAfter;
            _uncrossBlendDuration = Mathf.Max(0f, _currentProfile.CrossLegsExitBlendSeconds);
            _motion = _currentProfile.CrossLegsEnd;
            _motionEntryTime = 0f;
            _motionTime = 0f;
            // BeginMotion captures the current loop Playable at its existing time.
            // Only mixer weights advance until preparation completes; the actor root
            // and both clip poses remain frozen. Duration is gameplay time, not clip time.
            _layer.BeginMotion(_motion, 0f, _uncrossBlendDuration);
            _layer.SetActiveTime(0f);
            _uncrossBlendProgress = _layer.MotionBlendProgress;
            _state = PerformerSeatingState.PreparingUncross;
            if (_uncrossBlendProgress >= 1f) BeginUncrossing();
        }

        private void AdvancePreparingUncross()
        {
            // _layer.Advance has advanced only the weights using gameplay dt,
            // independent of PlaybackSpeed.
            // Do not change _motionTime or either Playable's time during preparation.
            _uncrossBlendProgress = _layer.MotionBlendProgress;
            if (_uncrossBlendProgress >= 1f) BeginUncrossing();
        }

        private void BeginUncrossing()
        {
            if (_currentProfile == null) return;
            _currentStyle = PerformerSeatedStyle.Basic;
            // The End clip already owns the seating mixer at frame zero. Switching state
            // starts its clock on the next update without initiating another crossfade.
            _state = PerformerSeatingState.UncrossingLegs;
        }

        private void AdvanceBodyMotion(float deltaTime, bool crossing)
        {
            if (_motion == null || _currentProfile == null || deltaTime <= 0f) return;
            _motionTime = Mathf.Min(_motion.DurationSeconds,
                _motionTime + deltaTime * _currentProfile.PlaybackSpeed);
            _layer.SetActiveTime(_motionTime);
            if (_motionTime + 0.00001f < _motion.DurationSeconds) return;

            if (crossing)
            {
                StartLoop();
                CompleteSeatedRequest(PerformerSeatedStyle.CrossLegs);
                return;
            }

            HoldBasicSeatedPose();
            if (_exitAfterUncross)
            {
                _exitAfterUncross = false;
                BeginSitEnd();
            }
            else
            {
                _currentStyle = PerformerSeatedStyle.Basic;
                _state = PerformerSeatingState.BasicSeated;
                CompleteSeatedRequest(PerformerSeatedStyle.Basic);
            }
        }

        private void StartLoop()
        {
            _motion = _currentProfile.CrossLegsLoop;
            _motionEntryTime = 0f;
            _motionTime = 0f;
            _layer.BeginMotion(_motion, 0f, _currentProfile.BodyBlendSeconds);
            _layer.SetActiveTime(0f);
            _state = PerformerSeatingState.CrossLegsSeated;
            _currentStyle = PerformerSeatedStyle.CrossLegs;
        }

        private void AdvanceSeatedLoop(float deltaTime)
        {
            if (_motion == null || _currentProfile == null || _motion.DurationSeconds <= 0f) return;
            _motionTime = Mathf.Repeat(_motionTime + deltaTime * _currentProfile.PlaybackSpeed,
                _motion.DurationSeconds);
            _layer.SetActiveTime(_motionTime);
            _lastContactError = _seatPosition - _actor.TransformPoint(_motion.PelvisOffsetAt(
                _motionTime / _motion.DurationSeconds));
        }

        private void HoldBasicSeatedPose()
        {
            _basicIdleCandidateActive = false;
            _motion = _currentProfile.SitStart;
            _motionEntryTime = _motion.DurationSeconds;
            _motionTime = _motion.DurationSeconds;
            _layer.BeginMotion(_motion, 1f, _currentProfile.BodyBlendSeconds);
            _layer.SetActiveTime(_motionTime);
            _state = PerformerSeatingState.BasicSeated;
            _currentStyle = PerformerSeatedStyle.Basic;
            _lastContactError = _seatPosition - _actor.TransformPoint(_motion.PelvisOffsetAt(1f));
        }

        private void BeginSitEnd()
        {
            if (_currentProfile == null)
                throw new InvalidOperationException("The seated motion profile was lost before StandUp could begin.");
            _basicIdleCandidateActive = false;
            StartRootMotion(_currentProfile.SitEnd, _currentProfile.SitEndEntryPhase,
                PerformerSeatingState.StandingUp, _approachPosition, _approachRotation, false);
        }

        private void StartBodyMotion(PerformerSeatingMotion motion, float normalizedStart,
            PerformerSeatingState state)
        {
            // RootPositionAt/RootYawAt are deliberately not applied for seated body transitions.
            _motion = motion;
            _motionEntryTime = Mathf.Clamp01(normalizedStart) * motion.DurationSeconds;
            _motionTime = _motionEntryTime;
            _state = state;
            _layer.BeginMotion(motion, normalizedStart, _currentProfile.BodyBlendSeconds);
            _layer.SetActiveTime(_motionTime);
        }

        private void CompleteSeatedRequest(PerformerSeatedStyle style)
        {
            _currentStyle = style;
            _state = style == PerformerSeatedStyle.CrossLegs
                ? PerformerSeatingState.CrossLegsSeated : PerformerSeatingState.BasicSeated;
            SitRequest completed = _sitRequest;
            _sitRequest = null;
            CompleteSitRequest(completed, SeatingCompletion.Seated);
        }

        private void CompleteSitRequest(SitRequest request, SeatingCompletion result)
        {
            if (request == null) return;
            foreach (var waiter in request.Waiters) waiter.TrySetResult(result);
            request.Waiters.Clear();
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private Vector3 ScaleOffsetForRotation(Quaternion rotation, Vector3 localOffset)
        {
            // PelvisOffsetAt is stored in performer-root local units. Convert it to
            // world units before solving actor-root position so scaled scene instances
            // still place the contact point on the SeatAnchor.
            return rotation * Vector3.Scale(_actor.lossyScale, localOffset);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PerformerSeating));
        }
    }
}
