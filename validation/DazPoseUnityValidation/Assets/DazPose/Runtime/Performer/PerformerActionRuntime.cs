using System;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Runs one finite Action trajectory and hands displaced recovery back to locomotion.</summary>
    internal sealed class PerformerActionRuntime : IDisposable
    {
        private readonly Transform _actor;
        private readonly PerformerActionLayer _layer;
        private readonly PerformerLocomotion _locomotion;
        private readonly PerformerLocomotionProfile _locomotionProfile;
        private PerformerAction _current;
        private PerformerActionState _state;
        private AwaitableCompletionSource<ActionCompletion> _waiter;
        private Vector3 _anchorPosition;
        private Vector3 _anchorForward;
        private Quaternion _anchorRotation;
        private Quaternion _trajectoryBasis;
        private Vector3 _previousTrajectoryPosition;
        private float _previousTrajectoryYaw;
        private float _elapsed;
        private bool _recoveryRequested;
        private bool _hasAnchor;
        private bool _disposed;

        public bool IsPerforming => _state != PerformerActionState.Idle;
        public PerformerAction CurrentAction => _current;
        public PerformerActionState State => _state;
        public float Progress => _state == PerformerActionState.Idle || _current == null
            ? 0f : _state == PerformerActionState.Recovering ? 1f
                : Mathf.Clamp01(_elapsed / _current.DurationSeconds);
        public Vector3 AnchorPosition => _anchorPosition;
        public Vector3 AnchorForward => _anchorForward;
        public bool HasAnchor => _hasAnchor;
        public float PositionError => Vector3.Distance(_actor.position, _anchorPosition);
        public float HeadingError => SignedAngleTo(_actor.forward, _anchorForward);

        public PerformerActionRuntime(Transform actor, PerformerActionLayer layer,
            PerformerLocomotion locomotion, PerformerLocomotionProfile locomotionProfile)
        {
            _actor = actor != null ? actor : throw new ArgumentNullException(nameof(actor));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _locomotion = locomotion ?? throw new ArgumentNullException(nameof(locomotion));
            _locomotionProfile = locomotionProfile != null
                ? locomotionProfile : throw new ArgumentNullException(nameof(locomotionProfile));
            if (!_locomotionProfile.IsReady(out string reason))
                throw new InvalidOperationException("PerformerAction requires a ready locomotion profile for return-home recovery. " + reason);
        }

        public void Request(PerformerAction action, AwaitableCompletionSource<ActionCompletion> waiter)
        {
            ThrowIfDisposed();
            if (_state != PerformerActionState.Idle)
                throw new InvalidOperationException("A PerformerAction is already active. Wait for its return-home recovery before starting another.");
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!action.IsReady(out string reason)) throw new InvalidOperationException(reason);

            Vector3 forward = Planar(_actor.forward);
            if (forward.sqrMagnitude < 0.000001f)
                throw new InvalidOperationException("PerformerAction requires a nonzero planar forward direction at its anchor.");

            _anchorPosition = _actor.position;
            _anchorRotation = _actor.rotation;
            _anchorForward = forward.normalized;
            _hasAnchor = true;
            _locomotion.ReleaseArrivalPoseHoldForAction();
            _trajectoryBasis = Quaternion.LookRotation(_anchorForward, Vector3.up);
            Vector3 initial = TrajectoryPositionAt(action, 0f);
            float initialYaw = action.RootYaw.Evaluate(0f);
            _previousTrajectoryPosition = initial;
            _previousTrajectoryYaw = initialYaw;
            _elapsed = 0f;
            _recoveryRequested = false;

            _layer.Begin(action.BodyClip);
            _layer.SetFrame(0f, 0f);
            _current = action;
            _waiter = waiter;
            _state = PerformerActionState.Performing;
        }

        public void Advance(float deltaTime)
        {
            if (_disposed || _state == PerformerActionState.Idle) return;
            if (_state == PerformerActionState.Recovering)
            {
                if (IsRecoverySettled()) Complete(ActionCompletion.Completed);
                return;
            }

            float duration = _current.DurationSeconds;
            _elapsed = Mathf.Min(duration, _elapsed + Mathf.Max(0f, deltaTime));
            float normalized = Mathf.Clamp01(_elapsed / duration);
            float envelope = EvaluateEnvelope(_current, _elapsed);
            _layer.SetFrame(_elapsed, envelope);
            ApplyTrajectory(normalized, normalized >= 1f);
            if (normalized >= 1f) BeginRecovery();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            AwaitableCompletionSource<ActionCompletion> disabledWaiter = IsPerforming ? _waiter : null;
            _waiter = null;
            _current = null;
            _state = PerformerActionState.Idle;
            _recoveryRequested = false;
            _layer.Release();
            disabledWaiter?.TrySetResult(ActionCompletion.PerformerDisabled);
        }

        private void ApplyTrajectory(float normalizedTime, bool finalSample)
        {
            Vector3 position = TrajectoryPositionAt(_current, normalizedTime);
            float yaw = _current.RootYaw.Evaluate(normalizedTime);
            Vector3 delta = position - _previousTrajectoryPosition;
            Vector3 worldDelta = _trajectoryBasis * new Vector3(delta.x, 0f, delta.z)
                + Vector3.up * delta.y;
            _actor.position += worldDelta;
            _actor.rotation = Quaternion.AngleAxis(yaw - _previousTrajectoryYaw, Vector3.up) * _actor.rotation;
            _previousTrajectoryPosition = position;
            _previousTrajectoryYaw = yaw;

            if (!finalSample) return;
            Vector3 finalPlanar = _trajectoryBasis * new Vector3(position.x, 0f, position.z);
            _actor.position = _anchorPosition + finalPlanar + Vector3.up * position.y;
            _actor.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * _anchorRotation;
        }

        private void BeginRecovery()
        {
            if (_state != PerformerActionState.Performing) return;
            _state = PerformerActionState.Recovering;
            _layer.Release();
            _recoveryRequested = true;
            if (PositionError <= _locomotionProfile.ArrivalPositionTolerance
                && HeadingError <= _locomotionProfile.ArrivalHeadingTolerance)
            {
                if (IsRecoverySettled()) Complete(ActionCompletion.Completed);
                return;
            }

            try
            {
                _locomotion.WalkTo(_anchorPosition, _anchorForward);
                if (IsRecoverySettled()) Complete(ActionCompletion.Completed);
            }
            catch (Exception exception)
            {
                CompleteExceptionally(exception);
            }
        }

        private bool IsRecoverySettled() => _recoveryRequested && !_locomotion.IsLocomoting
            && _locomotion.PersistentPoseOwnsBody;

        private void Complete(ActionCompletion result)
        {
            AwaitableCompletionSource<ActionCompletion> waiter = _waiter;
            _waiter = null;
            _current = null;
            _state = PerformerActionState.Idle;
            _recoveryRequested = false;
            _elapsed = 0f;
            waiter?.TrySetResult(result);
        }

        private void CompleteExceptionally(Exception exception)
        {
            AwaitableCompletionSource<ActionCompletion> waiter = _waiter;
            _waiter = null;
            _current = null;
            _state = PerformerActionState.Idle;
            _recoveryRequested = false;
            _elapsed = 0f;
            if (waiter != null) waiter.TrySetException(exception);
            else Debug.LogException(exception);
        }

        private static Vector3 TrajectoryPositionAt(PerformerAction action, float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            float x = action.RootX.Evaluate(t);
            float y = action.RootY.Evaluate(t);
            float z = action.RootZ.Evaluate(t);
            float duration = action.DurationSeconds;
            float correctionWindow = Mathf.Min(duration * 0.45f,
                Mathf.Max(0.10f, Mathf.Min(action.BlendOutSeconds, duration * 0.45f)));
            float correctionStart = 1f - correctionWindow / duration;
            float correction = SmoothStep01((t - correctionStart) / Mathf.Max(0.0001f, 1f - correctionStart));
            y -= action.RootY.Evaluate(1f) * correction;
            return new Vector3(x, y, z);
        }

        private static float EvaluateEnvelope(PerformerAction action, float elapsed)
        {
            float duration = action.DurationSeconds;
            float blendIn = Mathf.Min(action.BlendInSeconds, duration * 0.45f);
            float blendOut = Mathf.Min(action.BlendOutSeconds, duration * 0.45f);
            float inWeight = blendIn <= 0f ? 1f : SmoothStep01(elapsed / blendIn);
            float outWeight = blendOut <= 0f ? 1f : SmoothStep01((duration - elapsed) / blendOut);
            return Mathf.Min(inWeight, outWeight);
        }

        private static float SignedAngleTo(Vector3 from, Vector3 to)
        {
            from = Planar(from).normalized;
            to = Planar(to).normalized;
            return Mathf.Abs(Vector3.SignedAngle(from, to, Vector3.up));
        }

        private static Vector3 Planar(Vector3 value) => new Vector3(value.x, 0f, value.z);

        private static float SmoothStep01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PerformerActionRuntime));
        }
    }
}
