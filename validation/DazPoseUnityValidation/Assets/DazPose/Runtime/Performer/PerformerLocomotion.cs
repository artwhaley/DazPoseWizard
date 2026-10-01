using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer
{
    public enum PerformerLocomotionState { Idle, Turning, Starting, Walking, Stopping, Settling }

    /// <summary>Finite KAWAII Walk01 locomotion. Actor motion follows the profile's baked trajectory.</summary>
    internal sealed class PerformerLocomotion : IDisposable
    {
        private sealed class Request
        {
            public Vector3 Position;
            public Vector3 Facing;
            public bool HasFacing;
            public readonly List<AwaitableCompletionSource<LocomotionCompletion>> Waiters =
                new List<AwaitableCompletionSource<LocomotionCompletion>>();
        }

        private readonly Transform _actor;
        private readonly PerformerLocomotionProfile _profile;
        private readonly PerformerBodySourceMixer _body;
        private Request _request;
        private PerformerLocomotionState _state;
        private PerformerLocomotionMotion _motion;
        private float _motionTime;
        private float _turnYawScale = 1f;
        private int _startParity;
        private bool _facingOnlyTurn;
        private bool _reversalStopPending;
        private bool _shortWalk;
        private bool _stoppingForReversal;
        private bool _arrivalBlendStarted;
        private float _arrivalBlendProgress;
        private bool _settlingTransformActive;
        private float _settlingElapsed;
        private float _settlingDuration;
        private Vector3 _settlingStartPosition;
        private Vector3 _settlingTargetPosition;
        private Quaternion _settlingStartRotation;
        private Quaternion _settlingTargetRotation;
        private Vector3 _facingTurnOriginPosition;
        private Quaternion _facingTurnOriginRotation;
        private Vector3 _stopOrigin;
        private Vector3 _stopForward;
        private Vector3 _stopTarget;
        private float _stopPlanStartPhase;
        private float _stopPlanStartDistance;
        private string _selectedStop = "none";
        private string _selectedTurn = "none";
        private float _predictedStopDistance;
        private bool _disposed;

        public bool IsLocomoting => _request != null;
        public PerformerLocomotionState State => _state;
        public PerformerLocomotionMotion CurrentMotion => _motion;
        public float PlaybackTime => _motionTime;
        public float GaitPhase => _state == PerformerLocomotionState.Walking && _profile.WalkLoop.DurationSeconds > 0f
            ? Mathf.Repeat(_motionTime / _profile.WalkLoop.DurationSeconds, 1f) : 0f;
        public float RemainingDistance => _request == null ? 0f : Planar(_request.Position - _actor.position).magnitude;
        public float HeadingError => _request == null ? 0f : SignedHeadingToGoal();
        public float PredictedStopDistance => _predictedStopDistance;
        public string SelectedStopVariant => _selectedStop;
        public string SelectedTurn => _selectedTurn;
        public Vector3 CurrentTarget => _request == null ? default : _request.Position;
        public Vector3 PredictedStopEndpoint { get; private set; }
        public Vector3 EndpointCorrection { get; private set; }
        public float ArrivalBlendProgress => _arrivalBlendProgress;

        public PerformerLocomotion(Transform actor, PerformerLocomotionProfile profile, PerformerBodySourceMixer body)
        {
            _actor = actor != null ? actor : throw new ArgumentNullException(nameof(actor));
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _body = body != null ? body : throw new ArgumentNullException(nameof(body));
        }

        public void WalkTo(Vector3 position) => RequestTarget(position, default, false, null);

        public void WalkTo(Transform target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            RequestTarget(target.position, target.forward, true, null);
        }

        public Awaitable<LocomotionCompletion> WalkToAsync(Vector3 position)
        {
            var source = new AwaitableCompletionSource<LocomotionCompletion>();
            RequestTarget(position, default, false, source);
            return source.Awaitable;
        }

        public Awaitable<LocomotionCompletion> WalkToAsync(Transform target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var source = new AwaitableCompletionSource<LocomotionCompletion>();
            RequestTarget(target.position, target.forward, true, source);
            return source.Awaitable;
        }

        internal Awaitable<LocomotionCompletion> WalkToAsync(Vector3 position, Vector3 facing)
        {
            var source = new AwaitableCompletionSource<LocomotionCompletion>();
            RequestTarget(position, facing, true, source, true);
            return source.Awaitable;
        }

        public void Advance(float deltaTime)
        {
            if (_disposed) return;
            float dt = Mathf.Max(0f, deltaTime);
            _body.Advance(dt);

            if (_state == PerformerLocomotionState.Settling)
            {
                AdvanceFinalAlignment(dt);
                CompleteSettlingIfReady();
                return;
            }
            if (_request == null || _motion == null || dt <= 0f) return;

            float secondsToAdvance = dt * _profile.PlaybackSpeed;
            if (_state == PerformerLocomotionState.Stopping && !_stoppingForReversal && !_shortWalk)
            {
                AdvancePlannedStop(secondsToAdvance);
                return;
            }
            float secondsConsumed = 0f;
            if (_state == PerformerLocomotionState.Walking && !_shortWalk)
            {
                if (_reversalStopPending
                    && TryChooseReversalStop(secondsToAdvance, out PerformerLocomotionMotion reversalStop,
                        out secondsConsumed))
                {
                    EndpointCorrection = Vector3.zero;
                    Vector3 predictedEndpoint = _actor.position
                        + PredictPlanarDisplacement(reversalStop, _actor.rotation);
                    predictedEndpoint.y = _actor.position.y;
                    PredictedStopEndpoint = predictedEndpoint;
                    _predictedStopDistance = reversalStop.NominalPlanarDisplacement.magnitude;
                    _reversalStopPending = false;
                    _stoppingForReversal = true;
                    BeginMotion(reversalStop, 0f, PerformerLocomotionState.Stopping, true);
                    return;
                }

                if (!_reversalStopPending && TryChooseStop(out PerformerLocomotionMotion stop,
                        out int cyclesToEntry, out float predictedDistance))
                {
                    _predictedStopDistance = predictedDistance;
                    if (cyclesToEntry == 0
                        && TryEnterLoopPhase(stop.EntryGaitPhase, secondsToAdvance, out secondsConsumed))
                    {
                        EndpointCorrection = Vector3.zero;
                        BeginMotion(stop, 0f, PerformerLocomotionState.Stopping, true);
                        return;
                    }
                }
            }

            if (!_reversalStopPending && !_stoppingForReversal
                && (_state == PerformerLocomotionState.Starting
                    || _state == PerformerLocomotionState.Walking || _state == PerformerLocomotionState.Stopping))
            {
                AdvanceTravelMotion(secondsToAdvance);
                return;
            }

            float previous = _motionTime;
            _motionTime = Mathf.Min(_motion.DurationSeconds, _motionTime + secondsToAdvance);
            _body.SetActiveTime(_motionTime);
            float phase = _motion.DurationSeconds <= 0f ? 1f : Mathf.Clamp01(_motionTime / _motion.DurationSeconds);
            ApplyTrajectoryDelta(previous, _motionTime, phase,
                secondsToAdvance / Mathf.Max(0.0001f, _profile.PlaybackSpeed));
            if (_state == PerformerLocomotionState.Turning && _facingOnlyTurn)
                ApplyFinalFacingConvergence(previous, _motionTime);

            if (_motionTime + 0.00001f >= _motion.DurationSeconds)
                CompleteCurrentMotion();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Request pending = _request;
            _request = null;
            _motion = null;
            _state = PerformerLocomotionState.Idle;
            _body.SetOwnership(false, true);
            Complete(pending, LocomotionCompletion.PerformerDisabled);
        }

        private void RequestTarget(Vector3 position, Vector3 facing, bool hasFacing,
            AwaitableCompletionSource<LocomotionCompletion> waiter, bool allowNearReplacement = false)
        {
            ThrowIfDisposed();
            ValidateFinite(position, nameof(position));
            if (hasFacing)
            {
                ValidateFinite(facing, nameof(facing));
                facing = Planar(facing);
                if (facing.sqrMagnitude < 0.0001f)
                    throw new ArgumentException("A Transform locomotion target must have a nonzero planar forward vector.", nameof(facing));
                facing.Normalize();
            }

            bool sameGoal = _request != null && Planar(position - _request.Position).magnitude <= 0.05f
                && hasFacing == _request.HasFacing
                && (!hasFacing || Vector3.Angle(facing, _request.Facing) <= 4f);
            if (sameGoal)
            {
                if (waiter != null) _request.Waiters.Add(waiter);
                return;
            }

            float requestedDistance = Planar(position - _actor.position).magnitude;
            bool needsFacingTurn = hasFacing
                && Vector3.Angle(Planar(_actor.forward), facing) > _profile.ArrivalHeadingTolerance;
            if (_request != null && _state != PerformerLocomotionState.Settling
                && requestedDistance <= _profile.ArrivalPositionTolerance && !allowNearReplacement)
                throw new InvalidOperationException("A moving locomotion request cannot be replaced with the performer's current position. Request a destination far enough away to use an authored Stop; the active goal remains in effect.");
            if (_request == null)
            {
                if (requestedDistance <= 0.001f && !needsFacingTurn)
                {
                    waiter?.TrySetResult(LocomotionCompletion.Arrived);
                    return;
                }
            }

            var next = new Request { Position = position, Facing = facing, HasFacing = hasFacing };
            if (waiter != null) next.Waiters.Add(waiter);
            Request superseded = _request;
            if (_arrivalBlendStarted && superseded != null)
            {
                _body.SetOwnership(true);
                _arrivalBlendStarted = false;
                _arrivalBlendProgress = 0f;
            }
            bool priorReversalStopPending = _reversalStopPending;
            bool priorShortWalk = _shortWalk;
            _request = next;
            bool finishingCurrentStop = _state == PerformerLocomotionState.Stopping;
            if (!finishingCurrentStop) _selectedStop = "none";
            if (superseded == null || _state != PerformerLocomotionState.Turning)
                _selectedTurn = "none";
            if (!finishingCurrentStop)
            {
                EndpointCorrection = Vector3.zero;
                PredictedStopEndpoint = _actor.position;
            }
            try
            {
                if (requestedDistance <= _profile.ArrivalPositionTolerance
                    && (!needsFacingTurn || allowNearReplacement))
                {
                    BeginFinalAlignment();
                }
                else if (superseded == null || _state == PerformerLocomotionState.Idle || _state == PerformerLocomotionState.Settling)
                {
                    _reversalStopPending = false;
                    PlanFromCurrentPose();
                }
                else if (_state == PerformerLocomotionState.Walking)
                {
                    float heading = SignedHeadingToGoal();
                    _shortWalk = requestedDistance < _profile.MinimumWalkDistance;
                    _reversalStopPending = !_shortWalk && Mathf.Abs(heading) > 135f;
                    if (_shortWalk && Mathf.Abs(heading) > 135f) BeginTurn(heading, false);
                }
                else if (_state == PerformerLocomotionState.Starting)
                {
                    _shortWalk = requestedDistance < _profile.MinimumWalkDistance;
                }
                else if (finishingCurrentStop && !_stoppingForReversal && !_shortWalk)
                {
                    PlanStopEndpoint();
                }
            }
            catch
            {
                _request = superseded;
                _reversalStopPending = priorReversalStopPending;
                _shortWalk = priorShortWalk;
                throw;
            }
            Complete(superseded, LocomotionCompletion.Superseded);
        }

        private void PlanFromCurrentPose()
        {
            Vector3 toTarget = Planar(_request.Position - _actor.position);
            if (toTarget.sqrMagnitude <= _profile.ArrivalPositionTolerance * _profile.ArrivalPositionTolerance)
            {
                if (_request.HasFacing && Vector3.Angle(Planar(_actor.forward), _request.Facing) > _profile.ArrivalHeadingTolerance)
                    BeginTurn(SignedAngleTo(_actor.forward, _request.Facing), true);
                else BeginFinalAlignment();
                return;
            }

            float error = SignedHeadingToGoal();
            if (Mathf.Abs(error) > _profile.SmallTurnThreshold) BeginTurn(error, false);
            else BeginStart();
        }

        private void BeginTurn(float desiredYaw, bool facingOnly)
        {
            float magnitude = Mathf.Abs(desiredYaw);
            PerformerLocomotionMotion turn = magnitude <= 135f
                ? (desiredYaw < 0f ? _profile.TurnLeft90 : _profile.TurnRight90)
                : (desiredYaw < 0f ? _profile.TurnLeft180 : _profile.TurnRight180);
            if (Mathf.Abs(turn.NominalYawDegrees) < 1f)
                throw new InvalidOperationException(turn.name + " has no usable baked yaw trajectory; refusing to rotate Lara by a transform snap.");
            if (Mathf.Sign(turn.NominalYawDegrees) != Mathf.Sign(desiredYaw))
                throw new InvalidOperationException(turn.name + " baked yaw turns opposite the requested direction. Check the KAWAII source clip/import root-motion settings.");

            float difference = magnitude - Mathf.Abs(turn.NominalYawDegrees);
            if (difference > _profile.MaximumTurnWarpDegrees)
                throw new InvalidOperationException(turn.name + " requires " + difference.ToString("0")
                    + "° of angular warping, above the configured safe bound.");
            _turnYawScale = magnitude / Mathf.Abs(turn.NominalYawDegrees);
            _facingOnlyTurn = facingOnly;
            if (facingOnly)
            {
                _facingTurnOriginPosition = _actor.position;
                _facingTurnOriginRotation = _actor.rotation;
                PredictedStopEndpoint = _request.Position;
                EndpointCorrection = _request.Position - _actor.position;
            }
            _selectedTurn = turn.name;
            BeginMotion(turn, 0f, PerformerLocomotionState.Turning, true);
        }

        private void BeginStart()
        {
            _shortWalk = RemainingDistance < _profile.MinimumWalkDistance;
            _reversalStopPending = false;
            PerformerLocomotionMotion start = (_startParity++ & 1) == 0 ? _profile.StartA : _profile.StartB;
            BeginMotion(start, 0f, PerformerLocomotionState.Starting, true);
        }

        private void AdvanceTravelMotion(float secondsToAdvance)
        {
            float duration = _motion.DurationSeconds;
            float from = Mathf.Clamp01(_motionTime / duration);
            float to = Mathf.Min(1f, (_motionTime + secondsToAdvance) / duration);
            Vector3 direction = Planar(_request.Position - _actor.position);
            float remaining = direction.magnitude;

            bool arrivalTimeKnown = TryFindTimeToCoverDistance(_motion, from, remaining,
                out float timeToArrival);
            if (!arrivalTimeKnown && _state == PerformerLocomotionState.Starting && _shortWalk)
                arrivalTimeKnown = TryFindShortStartStopArrivalTime(from, remaining, out timeToArrival);
            if (arrivalTimeKnown && _profile.LocomotionToIdleBlendSeconds > 0f)
            {
                float arrivalWindowPlaybackSeconds = _profile.LocomotionToIdleBlendSeconds * _profile.PlaybackSpeed;
                if (!_arrivalBlendStarted && timeToArrival <= arrivalWindowPlaybackSeconds + 0.00001f)
                {
                    float remainingRealSeconds = timeToArrival / Mathf.Max(0.0001f, _profile.PlaybackSpeed);
                    _body.SetOwnership(false, Mathf.Min(_profile.LocomotionToIdleBlendSeconds, remainingRealSeconds));
                    _arrivalBlendStarted = true;
                }
                _arrivalBlendProgress = arrivalWindowPlaybackSeconds <= 0f ? 1f
                    : Smooth01(1f - timeToArrival / arrivalWindowPlaybackSeconds);
            }

            float travel = _motion.DistanceBetweenPhases(from, to);
            bool arrived = travel >= remaining;
            if (arrived && remaining > 0f)
            {
                // Find the exact clip time at the endpoint instead of playing beyond it
                // or stretching the whole gait to fit the requested distance.
                float low = from;
                float high = to;
                for (int i = 0; i < 20; i++)
                {
                    float middle = (low + high) * 0.5f;
                    if (_motion.DistanceBetweenPhases(from, middle) < remaining) low = middle;
                    else high = middle;
                }
                to = high;
            }

            float elapsed = (to - from) * duration / _profile.PlaybackSpeed;
            float steering = Mathf.Clamp(SignedHeadingToGoal(),
                -_profile.MaxSteeringDegreesPerSecond * elapsed,
                _profile.MaxSteeringDegreesPerSecond * elapsed);
            float yaw = _motion.SampleYawDelta(from, to);
            // Use authored distance timing along the target line. Yaw controls facing,
            // not the path, so a missed stopping phase cannot create an orbit.
            _actor.position += direction.normalized * Mathf.Min(travel, remaining);
            _actor.rotation = Quaternion.AngleAxis(yaw + steering, Vector3.up) * _actor.rotation;
            _motionTime = to * duration;
            _body.SetActiveTime(_motionTime);
            if (arrived)
            {
                _shortWalk = false;
                if (_request.HasFacing)
                {
                    float facingError = SignedAngleTo(_actor.forward, _request.Facing);
                    if (Mathf.Abs(facingError) > _profile.ArrivalHeadingTolerance)
                    {
                        BeginTurn(facingError, true);
                        return;
                    }
                }
                BeginSettling();
            }
            else if (_motionTime + 0.00001f >= duration)
            {
                if (_state == PerformerLocomotionState.Starting && !_shortWalk)
                {
                    CompleteCurrentMotion();
                }
                else if (_state == PerformerLocomotionState.Walking && !_shortWalk)
                {
                    _motionTime = 0f;
                    _body.SetActiveTime(0f);
                }
                else if (_state == PerformerLocomotionState.Starting || _state == PerformerLocomotionState.Walking)
                {
                    float entryPhase = _state == PerformerLocomotionState.Starting
                        ? _motion.LoopEntryPhase : GaitPhase;
                    float phaseA = Mathf.Abs(Mathf.DeltaAngle(entryPhase * 360f, _profile.StopA.EntryGaitPhase * 360f));
                    float phaseB = Mathf.Abs(Mathf.DeltaAngle(entryPhase * 360f, _profile.StopB.EntryGaitPhase * 360f));
                    PerformerLocomotionMotion stop = phaseA <= phaseB ? _profile.StopA : _profile.StopB;
                    EndpointCorrection = Vector3.zero;
                    _predictedStopDistance = Mathf.Min(RemainingDistance, stop.PlanarDistanceAt(1f));
                    BeginMotion(stop, 0f, PerformerLocomotionState.Stopping, true);
                }
                else
                {
                    // A destination replaced during Stop may now lie beyond that clip.
                    // Replan from the completed stop rather than repeating its frames.
                    _shortWalk = false;
                    PlanFromCurrentPose();
                }
            }
        }

        private bool TryChooseReversalStop(float secondsToAdvance,
            out PerformerLocomotionMotion selected, out float secondsConsumed)
        {
            selected = null;
            secondsConsumed = 0f;
            float duration = _profile.WalkLoop.DurationSeconds;
            float currentPhase = duration <= 0f ? 0f : Mathf.Clamp01(_motionTime / duration);
            float earliestSeconds = float.PositiveInfinity;
            float selectedPhase = 0f;

            foreach (PerformerLocomotionMotion stop in new[] { _profile.StopA, _profile.StopB })
            {
                float phase = stop.EntryGaitPhase;
                // If both phase-matched entries are behind the current phase, finish this
                // loop cycle first. The next cycle starts at phase zero and will select one.
                if (phase + 0.00001f < currentPhase) continue;
                float secondsToEntry = Mathf.Max(0f, phase - currentPhase) * duration;
                if (secondsToEntry >= earliestSeconds) continue;
                earliestSeconds = secondsToEntry;
                selected = stop;
                selectedPhase = phase;
            }

            if (selected == null || !TryEnterLoopPhase(selectedPhase, secondsToAdvance, out secondsConsumed))
            {
                selected = null;
                secondsConsumed = 0f;
                return false;
            }
            return true;
        }

        private void BeginMotion(PerformerLocomotionMotion motion, float phase,
            PerformerLocomotionState state, bool transitionBody)
        {
            if (motion == null || motion.BodyClip == null)
                throw new InvalidOperationException("A required baked locomotion motion is missing.");
            bool preserveShortArrivalFade = _arrivalBlendStarted && _shortWalk
                && state == PerformerLocomotionState.Stopping
                && (_state == PerformerLocomotionState.Starting || _state == PerformerLocomotionState.Walking);
            _motion = motion;
            _motionTime = Mathf.Clamp01(phase) * motion.DurationSeconds;
            _state = state;
            if (!preserveShortArrivalFade)
            {
                _arrivalBlendStarted = false;
                _arrivalBlendProgress = 0f;
            }
            if (state != PerformerLocomotionState.Stopping) _stoppingForReversal = false;
            _body.BeginMotion(motion, Mathf.Clamp01(phase), transitionBody ? _profile.BodyBlendSeconds : 0f);
            if (preserveShortArrivalFade) _body.SetOwnership(false);
            else _body.SetOwnership(true);
            _body.SetActiveTime(_motionTime);
            if (state == PerformerLocomotionState.Stopping)
            {
                _selectedStop = motion.name;
                Vector3 predictedEndpoint = _stoppingForReversal
                    ? _actor.position + PredictPlanarDisplacement(motion, _actor.rotation)
                    : _request.Position;
                predictedEndpoint.y = _actor.position.y;
                PredictedStopEndpoint = predictedEndpoint;
                if (!_stoppingForReversal && !_shortWalk) PlanStopEndpoint();
                else if (_shortWalk)
                {
                    EndpointCorrection = Vector3.zero;
                    _predictedStopDistance = RemainingDistance;
                }
            }
        }

        private void PlanStopEndpoint()
        {
            _stopOrigin = _actor.position;
            _stopTarget = _request.Position;
            _stopTarget.y = _stopOrigin.y;
            Vector3 direction = Planar(_stopTarget - _stopOrigin);
            _stopForward = direction.sqrMagnitude > 0.000001f
                ? direction.normalized : Planar(_actor.forward).normalized;
            _stopPlanStartPhase = Mathf.Clamp01(_motionTime / _motion.DurationSeconds);
            _stopPlanStartDistance = _motion.PlanarDistanceAt(_stopPlanStartPhase);
            float authoredRemaining = _motion.PlanarDistanceAt(1f) - _stopPlanStartDistance;
            PredictedStopEndpoint = _stopOrigin + _stopForward * authoredRemaining;
            EndpointCorrection = _stopTarget - PredictedStopEndpoint;
            _predictedStopDistance = authoredRemaining;
        }

        private void AdvancePlannedStop(float secondsToAdvance)
        {
            float previousPhase = Mathf.Clamp01(_motionTime / _motion.DurationSeconds);
            _motionTime = Mathf.Min(_motion.DurationSeconds, _motionTime + secondsToAdvance);
            _body.SetActiveTime(_motionTime);
            float phase = Mathf.Clamp01(_motionTime / _motion.DurationSeconds);
            // Use the profile's real-time arrival window, independent of clip speed.
            float blendPlaybackSeconds = _profile.LocomotionToIdleBlendSeconds * _profile.PlaybackSpeed;
            float correctionStart = Mathf.Max(_stopPlanStartPhase,
                1f - blendPlaybackSeconds / _motion.DurationSeconds);
            float correctionRange = 1f - correctionStart;
            float correctionPhase = correctionRange <= 0.00001f
                ? (phase + 0.00001f >= 1f ? 1f : 0f)
                : Mathf.InverseLerp(correctionStart, 1f, phase);
            if (!_arrivalBlendStarted && phase + 0.00001f >= correctionStart)
            {
                float remainingRealSeconds = (1f - previousPhase) * _motion.DurationSeconds
                    / Mathf.Max(0.0001f, _profile.PlaybackSpeed);
                _body.SetOwnership(false, Mathf.Min(_profile.LocomotionToIdleBlendSeconds, remainingRealSeconds));
                _arrivalBlendStarted = true;
            }
            _arrivalBlendProgress = Smooth01(correctionPhase);
            float authoredDistance = _motion.PlanarDistanceAt(phase) - _stopPlanStartDistance;
            _actor.position = _stopOrigin + _stopForward * authoredDistance
                + EndpointCorrection * Smooth01(correctionPhase);
            float steering = Mathf.Clamp(SignedAngleTo(_actor.forward, _stopForward),
                -_profile.MaxSteeringDegreesPerSecond * secondsToAdvance / _profile.PlaybackSpeed,
                _profile.MaxSteeringDegreesPerSecond * secondsToAdvance / _profile.PlaybackSpeed);
            _actor.rotation = Quaternion.AngleAxis(steering, Vector3.up) * _actor.rotation;
            if (_motionTime + 0.00001f >= _motion.DurationSeconds)
            {
                // Assign the planned endpoint explicitly to eliminate accumulated float error.
                _actor.position = _stopTarget;
                CompleteCurrentMotion();
            }
        }

        private bool TryEnterLoopPhase(float normalizedPhase, float secondsToAdvance, out float secondsConsumed)
        {
            secondsConsumed = 0f;
            float duration = _profile.WalkLoop.DurationSeconds;
            float currentPhase = duration <= 0f ? 0f : Mathf.Clamp01(_motionTime / duration);
            float targetPhase = Mathf.Clamp01(normalizedPhase);
            if (targetPhase + 0.00001f < currentPhase) return false;
            float secondsToEntry = (targetPhase - currentPhase) * duration;
            if (secondsToEntry > secondsToAdvance + 0.00001f) return false;

            float previous = _motionTime;
            _motionTime = targetPhase * duration;
            _body.SetActiveTime(_motionTime);
            secondsConsumed = secondsToEntry;
            ApplyTrajectoryDelta(previous, _motionTime, targetPhase,
                secondsConsumed / Mathf.Max(0.0001f, _profile.PlaybackSpeed));
            return true;
        }

        private bool TryChooseStop(out PerformerLocomotionMotion selected, out int cyclesToEntry,
            out float predictedDistance)
        {
            PerformerLocomotionMotion loop = _profile.WalkLoop;
            float cycleDistance = loop.CycleDistance;
            if (cycleDistance <= 0.001f)
                throw new InvalidOperationException("The baked Walk01 loop has no planar trajectory; production WalkTo cannot safely use it.");

            float targetAhead = RemainingDistance;
            float phase = GaitPhase;
            selected = null;
            cyclesToEntry = 0;
            predictedDistance = 0f;
            float bestError = float.PositiveInfinity;

            PerformerLocomotionMotion[] stops = { _profile.StopA, _profile.StopB };
            foreach (PerformerLocomotionMotion stop in stops)
            {
                float distanceToEntry = loop.DistanceBetweenPhases(phase, stop.EntryGaitPhase);
                float stopDistance = stop.PlanarDistanceAt(1f);
                float immediateDistance = distanceToEntry + stopDistance;
                int extraCycles = Mathf.Max(0, Mathf.RoundToInt((targetAhead - immediateDistance) / cycleDistance));
                float estimate = immediateDistance + extraCycles * cycleDistance;
                float error = Mathf.Abs(targetAhead - estimate);
                if (error >= bestError) continue;

                selected = stop;
                cyclesToEntry = extraCycles;
                predictedDistance = estimate;
                bestError = error;
            }

            if (selected == null) return false;
            _selectedStop = selected.name;
            Vector3 predictedEndpoint = _actor.position
                + Planar(_request.Position - _actor.position).normalized * Mathf.Min(predictedDistance, targetAhead);
            predictedEndpoint.y = _actor.position.y;
            PredictedStopEndpoint = predictedEndpoint;
            EndpointCorrection = Vector3.zero;
            _predictedStopDistance = predictedDistance;
            return true;
        }

        private static Vector3 PredictPlanarDisplacement(PerformerLocomotionMotion motion, Quaternion startRotation)
        {
            const int steps = 60;
            Vector3 displacement = Vector3.zero;
            Quaternion rotation = startRotation;
            float previous = 0f;
            for (int i = 1; i <= steps; i++)
            {
                float phase = (float)i / steps;
                displacement += Planar(rotation * motion.SamplePositionDelta(previous, phase));
                float yawDelta = motion.SampleYawDelta(previous, phase);
                rotation = Quaternion.AngleAxis(yawDelta, Vector3.up) * rotation;
                previous = phase;
            }
            return displacement;
        }

        private void ApplyTrajectoryDelta(float previousSeconds, float currentSeconds, float phase, float deltaTime)
        {
            float duration = Mathf.Max(0.0001f, _motion.DurationSeconds);
            float from = Mathf.Clamp01(previousSeconds / duration);
            float to = Mathf.Clamp01(phase);
            Vector3 localDelta = _motion.SamplePositionDelta(from, to);
            float yawDelta = _motion.SampleYawDelta(from, to);
            if (_state == PerformerLocomotionState.Turning)
                yawDelta *= _turnYawScale;

            float steering = 0f;
            if ((_state == PerformerLocomotionState.Starting || _state == PerformerLocomotionState.Walking)
                && !_reversalStopPending && _request != null)
            {
                steering = Mathf.Clamp(SignedHeadingToGoal(), -_profile.MaxSteeringDegreesPerSecond * deltaTime,
                    _profile.MaxSteeringDegreesPerSecond * deltaTime);
            }

            Vector3 worldDelta = Planar(_actor.rotation * localDelta);
            if (_state == PerformerLocomotionState.Turning && _facingOnlyTurn)
                worldDelta = Vector3.zero;
            if (!_reversalStopPending && !_stoppingForReversal
                && (_state == PerformerLocomotionState.Starting
                    || _state == PerformerLocomotionState.Walking || _state == PerformerLocomotionState.Stopping))
            {
                Vector3 direction = Planar(_request.Position - _actor.position);
                worldDelta = direction.normalized * Mathf.Min(direction.magnitude,
                    _motion.DistanceBetweenPhases(from, to));
            }
            float correctionPhase = _state == PerformerLocomotionState.Stopping ? Smooth01(to) : 0f;
            Vector3 correctionDelta = _state == PerformerLocomotionState.Stopping
                ? EndpointCorrection * (correctionPhase - Smooth01(from))
                : Vector3.zero;
            _actor.position += worldDelta + correctionDelta;
            _actor.rotation = Quaternion.AngleAxis(yawDelta + steering, Vector3.up) * _actor.rotation;
        }

        private void ApplyFinalFacingConvergence(float previousSeconds, float currentSeconds)
        {
            float duration = Mathf.Max(0.0001f, _motion.DurationSeconds);
            float from = Mathf.Clamp01(previousSeconds / duration);
            float to = Mathf.Clamp01(currentSeconds / duration);
            float blendSeconds = _profile.LocomotionToIdleBlendSeconds;
            float blendPhase = Mathf.Max(0f, 1f - blendSeconds * _profile.PlaybackSpeed / duration);
            if (!_arrivalBlendStarted && to + 0.00001f >= blendPhase)
            {
                float remainingRealSeconds = (1f - from) * duration / Mathf.Max(0.0001f, _profile.PlaybackSpeed);
                _body.SetOwnership(false, Mathf.Min(blendSeconds, remainingRealSeconds));
                _arrivalBlendStarted = true;
            }

            float blendRange = 1f - blendPhase;
            float progress = blendRange <= 0.00001f
                ? (to + 0.00001f >= 1f ? 1f : 0f)
                : Smooth01(Mathf.InverseLerp(blendPhase, 1f, to));
            _arrivalBlendProgress = progress;
            Vector3 targetPosition = _request.Position;
            targetPosition.y = _facingTurnOriginPosition.y;
            _actor.position = Vector3.Lerp(_facingTurnOriginPosition, targetPosition, progress);

            Quaternion authoredEnd = _facingTurnOriginRotation
                * Quaternion.AngleAxis(_motion.YawAt(1f) * _turnYawScale, Vector3.up);
            float yawCorrection = Vector3.SignedAngle(authoredEnd * Vector3.forward,
                _request.Facing, Vector3.up);
            Quaternion authoredNow = _facingTurnOriginRotation
                * Quaternion.AngleAxis(_motion.YawAt(to) * _turnYawScale, Vector3.up);
            _actor.rotation = Quaternion.AngleAxis(yawCorrection * progress, Vector3.up) * authoredNow;
        }

        private static bool TryFindTimeToCoverDistance(PerformerLocomotionMotion motion,
            float phase, float distance, out float playbackSeconds)
        {
            playbackSeconds = 0f;
            if (motion == null || motion.DurationSeconds <= 0f || distance < 0f) return false;

            float cycleDistance = motion.CycleDistance;
            if (motion.BodyClip.isLooping && cycleDistance > 0.0001f)
            {
                float toEnd = motion.DistanceBetweenPhases(phase, 1f);
                if (distance > toEnd)
                {
                    distance -= toEnd;
                    playbackSeconds += (1f - phase) * motion.DurationSeconds;
                    phase = 0f;
                    float wholeCycles = Mathf.Floor(distance / cycleDistance);
                    if (wholeCycles > 0f)
                    {
                        distance -= wholeCycles * cycleDistance;
                        playbackSeconds += wholeCycles * motion.DurationSeconds;
                    }
                }
            }
            else if (distance > motion.DistanceBetweenPhases(phase, 1f) + 0.00001f)
            {
                return false;
            }

            float low = phase;
            float high = 1f;
            for (int i = 0; i < 24; i++)
            {
                float middle = (low + high) * 0.5f;
                if (motion.DistanceBetweenPhases(phase, middle) < distance) low = middle;
                else high = middle;
            }
            playbackSeconds += (high - phase) * motion.DurationSeconds;
            return true;
        }

        private bool TryFindShortStartStopArrivalTime(float startPhase, float distance,
            out float playbackSeconds)
        {
            playbackSeconds = 0f;
            float remainingStartDistance = _motion.DistanceBetweenPhases(startPhase, 1f);
            if (distance <= remainingStartDistance) return false;
            distance -= remainingStartDistance;

            float loopEntry = _motion.LoopEntryPhase;
            PerformerLocomotionMotion stop =
                Mathf.Abs(Mathf.DeltaAngle(loopEntry * 360f, _profile.StopA.EntryGaitPhase * 360f))
                <= Mathf.Abs(Mathf.DeltaAngle(loopEntry * 360f, _profile.StopB.EntryGaitPhase * 360f))
                    ? _profile.StopA : _profile.StopB;
            float stopDistance = stop.PlanarDistanceAt(1f);
            if (distance > stopDistance + 0.00001f) return false;

            float low = 0f;
            float high = 1f;
            for (int i = 0; i < 24; i++)
            {
                float middle = (low + high) * 0.5f;
                if (stop.PlanarDistanceAt(middle) < distance) low = middle;
                else high = middle;
            }
            playbackSeconds = (1f - startPhase) * _motion.DurationSeconds
                + high * stop.DurationSeconds;
            return true;
        }

        private void CompleteCurrentMotion()
        {
            PerformerLocomotionState finished = _state;
            if (finished == PerformerLocomotionState.Turning)
            {
                _turnYawScale = 1f;
                if (_facingOnlyTurn)
                {
                    float facingError = _request.HasFacing ? SignedAngleTo(_actor.forward, _request.Facing) : 0f;
                    if (Mathf.Abs(facingError) > _profile.ArrivalHeadingTolerance) BeginTurn(facingError, true);
                    else BeginSettling();
                }
                else
                {
                    if (RemainingDistance <= _profile.ArrivalPositionTolerance)
                    {
                        float facingError = _request.HasFacing ? SignedAngleTo(_actor.forward, _request.Facing) : 0f;
                        if (Mathf.Abs(facingError) > _profile.ArrivalHeadingTolerance) BeginTurn(facingError, true);
                        else BeginSettling();
                    }
                    else
                    {
                        float heading = SignedHeadingToGoal();
                        if (Mathf.Abs(heading) > _profile.SmallTurnThreshold) BeginTurn(heading, false);
                        else BeginStart();
                    }
                }
                return;
            }
            if (finished == PerformerLocomotionState.Starting)
            {
                float loopEntry = _motion.LoopEntryPhase;
                BeginMotion(_profile.WalkLoop, loopEntry, PerformerLocomotionState.Walking, true);
                float heading = SignedHeadingToGoal();
                _reversalStopPending = Mathf.Abs(heading) > 135f;
                return;
            }
            if (finished == PerformerLocomotionState.Stopping)
            {
                _stoppingForReversal = false;
                if (RemainingDistance > _profile.ArrivalPositionTolerance)
                {
                    float heading = SignedHeadingToGoal();
                    if (Mathf.Abs(heading) > _profile.SmallTurnThreshold) BeginTurn(heading, false);
                    else BeginStart();
                    return;
                }
                if (_request.HasFacing)
                {
                    float facingError = SignedAngleTo(_actor.forward, _request.Facing);
                    if (Mathf.Abs(facingError) > _profile.ArrivalHeadingTolerance)
                    {
                        BeginTurn(facingError, true);
                        return;
                    }
                }
                BeginSettling();
                return;
            }
            if (finished == PerformerLocomotionState.Walking)
            {
                _motionTime = 0f;
                _body.SetActiveTime(0f);
            }
        }

        private void BeginSettling()
        {
            Vector3 endpoint = _request.Position;
            endpoint.y = _actor.position.y;
            _actor.position = endpoint;
            _settlingTransformActive = false;
            _arrivalBlendProgress = 1f;
            _state = PerformerLocomotionState.Settling;
            _motion = null;
            _reversalStopPending = false;
            _shortWalk = false;
            _stoppingForReversal = false;
            _body.SetOwnership(false);
            if (_profile.LocomotionToIdleBlendSeconds <= 0f) CompleteSettlingIfReady();
        }

        private void BeginFinalAlignment()
        {
            if (_request == null) return;
            _motion = null;
            _reversalStopPending = false;
            _stoppingForReversal = false;
            _shortWalk = false;
            _settlingStartPosition = _actor.position;
            _settlingTargetPosition = _request.Position;
            _settlingTargetPosition.y = _actor.position.y;
            _settlingStartRotation = _actor.rotation;
            _settlingTargetRotation = _request.HasFacing
                ? Quaternion.LookRotation(_request.Facing, Vector3.up) : _actor.rotation;
            _settlingElapsed = 0f;
            _settlingDuration = Mathf.Max(0.08f, _profile.LocomotionToIdleBlendSeconds);
            _settlingTransformActive = Vector3.Distance(_settlingStartPosition, _settlingTargetPosition) > 0.0005f
                || Quaternion.Angle(_settlingStartRotation, _settlingTargetRotation) > 0.05f;
            _state = PerformerLocomotionState.Settling;
            _body.SetOwnership(false, _profile.LocomotionToIdleBlendSeconds);
            EndpointCorrection = _settlingTargetPosition - _settlingStartPosition;
            PredictedStopEndpoint = _settlingTargetPosition;
            if (!_settlingTransformActive) _arrivalBlendProgress = 1f;
        }

        private void AdvanceFinalAlignment(float deltaTime)
        {
            if (!_settlingTransformActive) return;
            _settlingElapsed += deltaTime;
            float progress = Mathf.Clamp01(_settlingElapsed / _settlingDuration);
            float smooth = Smooth01(progress);
            _actor.position = Vector3.Lerp(_settlingStartPosition, _settlingTargetPosition, smooth);
            _actor.rotation = Quaternion.Slerp(_settlingStartRotation, _settlingTargetRotation, smooth);
            _arrivalBlendProgress = smooth;
            if (progress < 1f) return;
            _actor.SetPositionAndRotation(_settlingTargetPosition, _settlingTargetRotation);
            _settlingTransformActive = false;
        }

        private void CompleteSettlingIfReady()
        {
            if (_settlingTransformActive || _body.LocomotionWeight > 0.001f) return;
            Request arrived = _request;
            _request = null;
            _state = PerformerLocomotionState.Idle;
            if (arrived != null)
                Debug.Log("[PerformerLocomotion] Arrived. Target=" + arrived.Position.ToString("F3")
                    + ", actual=" + _actor.position.ToString("F3")
                    + ", endpoint correction=" + EndpointCorrection.magnitude.ToString("F3") + " m"
                    + ", arrival blend=" + _arrivalBlendProgress.ToString("F2") + ".", _actor);
            Complete(arrived, LocomotionCompletion.Arrived);
        }

        private float SignedHeadingToGoal()
        {
            Vector3 direction = Planar(_request.Position - _actor.position);
            if (direction.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.SignedAngle(Planar(_actor.forward).normalized, direction.normalized, Vector3.up);
        }

        private static float SignedAngleTo(Vector3 from, Vector3 to)
        {
            from = Planar(from).normalized;
            to = Planar(to).normalized;
            if (from.sqrMagnitude < 0.0001f || to.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.SignedAngle(from, to, Vector3.up);
        }

        private static Vector3 Planar(Vector3 value) { value.y = 0f; return value; }
        private static float Smooth01(float value) { value = Mathf.Clamp01(value); return value * value * (3f - 2f * value); }

        private static void ValidateFinite(Vector3 value, string name)
        {
            if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z)
                || float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z))
                throw new ArgumentOutOfRangeException(name, "Locomotion destination components must be finite.");
        }

        private static void Complete(Request request, LocomotionCompletion result)
        {
            if (request == null) return;
            foreach (var waiter in request.Waiters) waiter.TrySetResult(result);
            request.Waiters.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PerformerLocomotion));
        }
    }
}
