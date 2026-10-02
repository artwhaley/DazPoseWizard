using System;
using UnityEngine;

namespace DazPose.Player
{
    /// <summary>Independent, latest-wins world-position and view-orientation channels.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerView : MonoBehaviour
    {
        private enum OrientationMode { FiniteLook, TrackAcquire, Tracking }

        private sealed class MoveOperation
        {
            public Vector3 Start;
            public Vector3 Destination;
            public float Elapsed;
            public ViewTransition Transition;
            public AwaitableCompletionSource<PlayerViewCompletion> Waiter;
        }

        private sealed class OrientationOperation
        {
            public OrientationMode Mode;
            public Transform Target;
            public Vector3 FixedTarget;
            public bool UsesTransform;
            public Quaternion StartRotation;
            public float Elapsed;
            public ViewTransition Transition;
            public ViewTrackingSettings TrackingSettings;
            public AwaitableCompletionSource<PlayerViewCompletion> Waiter;
        }

        [Header("Hierarchy")]
        [SerializeField] private Transform viewRig;
        [SerializeField] private Transform headPose;
        [SerializeField] private Camera mainCamera;

        [Header("Default Timing")]
        [SerializeField, Min(0f)] private float defaultMoveDuration = 2f;
        [SerializeField, Min(0f)] private float defaultLookDuration = 0.75f;
        [SerializeField] private ViewTrackingSettings defaultTrackingSettings = ViewTrackingSettings.Default;

        private MoveOperation activeMove;
        private OrientationOperation activeOrientation;

        public bool IsMoving => activeMove != null;
        public bool IsLooking => activeOrientation != null && activeOrientation.Mode == OrientationMode.FiniteLook;
        public bool IsAcquiringTrack => activeOrientation != null && activeOrientation.Mode == OrientationMode.TrackAcquire;
        public bool IsTracking => activeOrientation != null && activeOrientation.Mode == OrientationMode.Tracking;
        public float MoveProgress => activeMove == null ? 1f : Progress(activeMove.Elapsed, activeMove.Transition.Duration);
        public float LookProgress => activeOrientation == null ? 1f
            : activeOrientation.Mode == OrientationMode.Tracking ? 1f
            : Progress(activeOrientation.Elapsed, activeOrientation.Transition.Duration);
        public Transform CurrentTrackTarget => activeOrientation != null
            && activeOrientation.Mode != OrientationMode.FiniteLook ? activeOrientation.Target : null;
        public Vector3 CurrentMoveDestination => activeMove == null ? transform.position : activeMove.Destination;
        public Vector3 PlayerPosition => transform.position;
        public Vector3 CurrentForwardDirection => viewRig != null ? viewRig.forward : transform.forward;
        public Transform HeadTransform => headPose;
        public Camera MainCamera => mainCamera;

        private void Awake() => ResolveHierarchyReferences();

        private void OnValidate()
        {
            defaultMoveDuration = Mathf.Max(0f, defaultMoveDuration);
            defaultLookDuration = Mathf.Max(0f, defaultLookDuration);
        }

        private void OnDisable() => CancelActiveCommands();
        private void OnDestroy() => CancelActiveCommands();

        private void LateUpdate()
        {
            MoveOperation move = activeMove;
            OrientationOperation orientation = activeOrientation;
            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            AdvanceMove(move, deltaTime);
            AdvanceOrientation(orientation, deltaTime);
        }

        public void Configure(Transform controlledViewRig, Transform controlledHeadPose, Camera controlledCamera)
        {
            if (controlledViewRig == null) throw new ArgumentNullException(nameof(controlledViewRig));
            if (controlledHeadPose == null) throw new ArgumentNullException(nameof(controlledHeadPose));
            if (controlledCamera == null) throw new ArgumentNullException(nameof(controlledCamera));
            viewRig = controlledViewRig;
            headPose = controlledHeadPose;
            mainCamera = controlledCamera;
        }

        public void MoveTo(Transform target) => MoveTo(target, defaultMoveDuration);
        public void MoveTo(Transform target, float duration) => MoveTo(target, ViewTransition.EaseInOut(duration));

        public void MoveTo(Transform target, ViewTransition transition)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            MoveTo(target.position, transition);
        }

        public void MoveTo(Vector3 worldPosition) => MoveTo(worldPosition, defaultMoveDuration);
        public void MoveTo(Vector3 worldPosition, float duration) => MoveTo(worldPosition, ViewTransition.EaseInOut(duration));

        public void MoveTo(Vector3 worldPosition, ViewTransition transition)
        {
            BeginMove(worldPosition, transition, null);
        }

        public Awaitable<PlayerViewCompletion> MoveToAsync(Transform target) => MoveToAsync(target, defaultMoveDuration);
        public Awaitable<PlayerViewCompletion> MoveToAsync(Transform target, float duration) =>
            MoveToAsync(target, ViewTransition.EaseInOut(duration));

        public Awaitable<PlayerViewCompletion> MoveToAsync(Transform target, ViewTransition transition)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return MoveToAsync(target.position, transition);
        }

        public Awaitable<PlayerViewCompletion> MoveToAsync(Vector3 worldPosition) => MoveToAsync(worldPosition, defaultMoveDuration);
        public Awaitable<PlayerViewCompletion> MoveToAsync(Vector3 worldPosition, float duration) =>
            MoveToAsync(worldPosition, ViewTransition.EaseInOut(duration));

        public Awaitable<PlayerViewCompletion> MoveToAsync(Vector3 worldPosition, ViewTransition transition)
        {
            var waiter = new AwaitableCompletionSource<PlayerViewCompletion>();
            BeginMove(worldPosition, transition, waiter);
            return waiter.Awaitable;
        }

        public void LookAt(Transform target) => LookAt(target, defaultLookDuration);
        public void LookAt(Transform target, float duration) => LookAt(target, ViewTransition.EaseInOut(duration));

        public void LookAt(Transform target, ViewTransition transition)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            BeginLook(target, default, true, transition, null);
        }

        public void LookAt(Vector3 worldPosition) => LookAt(worldPosition, defaultLookDuration);
        public void LookAt(Vector3 worldPosition, float duration) => LookAt(worldPosition, ViewTransition.EaseInOut(duration));

        public void LookAt(Vector3 worldPosition, ViewTransition transition)
        {
            BeginLook(null, worldPosition, false, transition, null);
        }

        public Awaitable<PlayerViewCompletion> LookAtAsync(Transform target) => LookAtAsync(target, defaultLookDuration);
        public Awaitable<PlayerViewCompletion> LookAtAsync(Transform target, float duration) =>
            LookAtAsync(target, ViewTransition.EaseInOut(duration));

        public Awaitable<PlayerViewCompletion> LookAtAsync(Transform target, ViewTransition transition)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var waiter = new AwaitableCompletionSource<PlayerViewCompletion>();
            BeginLook(target, default, true, transition, waiter);
            return waiter.Awaitable;
        }

        public Awaitable<PlayerViewCompletion> LookAtAsync(Vector3 worldPosition) => LookAtAsync(worldPosition, defaultLookDuration);
        public Awaitable<PlayerViewCompletion> LookAtAsync(Vector3 worldPosition, float duration) =>
            LookAtAsync(worldPosition, ViewTransition.EaseInOut(duration));

        public Awaitable<PlayerViewCompletion> LookAtAsync(Vector3 worldPosition, ViewTransition transition)
        {
            var waiter = new AwaitableCompletionSource<PlayerViewCompletion>();
            BeginLook(null, worldPosition, false, transition, waiter);
            return waiter.Awaitable;
        }

        public void Track(Transform target) => Track(target, defaultTrackingSettings);

        public void Track(Transform target, float acquireDuration)
        {
            ViewTrackingSettings settings = defaultTrackingSettings;
            settings.Acquire = ViewTransition.EaseInOut(acquireDuration);
            Track(target, settings);
        }

        public void Track(Transform target, ViewTrackingSettings settings)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            EnsureOperational();
            settings.Validate();

            var previous = activeOrientation;
            var next = new OrientationOperation
            {
                Mode = settings.Acquire.Duration <= 0f ? OrientationMode.Tracking : OrientationMode.TrackAcquire,
                Target = target,
                UsesTransform = true,
                StartRotation = RequireViewRig().rotation,
                Transition = settings.Acquire,
                TrackingSettings = settings
            };
            activeOrientation = next;

            if (settings.Acquire.Duration <= 0f)
                RequireViewRig().rotation = DesiredRotation(target.position);

            Complete(previous?.Waiter, PlayerViewCompletion.Superseded);
        }

        /// <summary>Cancel a persistent/acquiring track and retain the exact current ViewRig rotation.</summary>
        public void StopTracking()
        {
            if (activeOrientation == null || activeOrientation.Mode == OrientationMode.FiniteLook) return;
            activeOrientation = null;
        }

        internal void CancelActiveCommands()
        {
            MoveOperation move = activeMove;
            OrientationOperation orientation = activeOrientation;
            activeMove = null;
            activeOrientation = null;
            Complete(move?.Waiter, PlayerViewCompletion.PlayerDisabled);
            Complete(orientation?.Waiter, PlayerViewCompletion.PlayerDisabled);
        }

        private void BeginMove(Vector3 worldPosition, ViewTransition transition,
            AwaitableCompletionSource<PlayerViewCompletion> waiter)
        {
            EnsureOperational();
            transition.Validate(nameof(transition));
            MoveOperation previous = activeMove;
            var next = new MoveOperation
            {
                Start = transform.position,
                Destination = worldPosition,
                Transition = transition,
                Waiter = waiter
            };

            if (transition.Duration <= 0f)
            {
                activeMove = null;
                transform.position = worldPosition;
            }
            else
            {
                activeMove = next;
            }

            // Publish the replacement before completing an old Awaitable; its continuation may re-enter.
            Complete(previous?.Waiter, PlayerViewCompletion.Superseded);
            if (transition.Duration <= 0f) Complete(waiter, PlayerViewCompletion.Completed);
        }

        private void BeginLook(Transform target, Vector3 fixedTarget, bool usesTransform,
            ViewTransition transition, AwaitableCompletionSource<PlayerViewCompletion> waiter)
        {
            EnsureOperational();
            transition.Validate(nameof(transition));
            Transform rig = RequireViewRig();
            OrientationOperation previous = activeOrientation;
            var next = new OrientationOperation
            {
                Mode = OrientationMode.FiniteLook,
                Target = target,
                FixedTarget = fixedTarget,
                UsesTransform = usesTransform,
                StartRotation = rig.rotation,
                Transition = transition,
                Waiter = waiter
            };

            if (transition.Duration <= 0f)
            {
                activeOrientation = null;
                rig.rotation = DesiredRotation(usesTransform ? target.position : fixedTarget);
            }
            else
            {
                activeOrientation = next;
            }

            Complete(previous?.Waiter, PlayerViewCompletion.Superseded);
            if (transition.Duration <= 0f) Complete(waiter, PlayerViewCompletion.Completed);
        }

        private void AdvanceMove(MoveOperation operation, float deltaTime)
        {
            if (operation == null || !ReferenceEquals(activeMove, operation) || deltaTime <= 0f) return;
            operation.Elapsed += deltaTime;
            float t = Mathf.Clamp01(operation.Elapsed / operation.Transition.Duration);
            float eased = operation.Transition.Evaluate(t);
            transform.position = Vector3.Lerp(operation.Start, operation.Destination, eased);
            if (t < 1f) return;

            transform.position = operation.Destination;
            if (!ReferenceEquals(activeMove, operation)) return;
            activeMove = null;
            Complete(operation.Waiter, PlayerViewCompletion.Completed);
        }

        private void AdvanceOrientation(OrientationOperation operation, float deltaTime)
        {
            if (operation == null || !ReferenceEquals(activeOrientation, operation)) return;
            Transform rig = RequireViewRig();

            if (operation.Mode == OrientationMode.TrackAcquire || operation.Mode == OrientationMode.Tracking)
            {
                if (operation.Target == null)
                {
                    activeOrientation = null;
                    Debug.LogWarning("PlayerView stopped tracking because its target was destroyed; the current view direction is held.", this);
                    return;
                }
            }

            if (operation.Mode == OrientationMode.Tracking)
            {
                Quaternion desired = DesiredRotation(operation.Target.position);
                float response = operation.TrackingSettings.FollowResponseSeconds;
                float alpha = response <= 0f || deltaTime <= 0f ? (response <= 0f ? 1f : 0f)
                    : 1f - Mathf.Exp(-deltaTime / response);
                rig.rotation = Quaternion.Slerp(rig.rotation, desired, Mathf.Clamp01(alpha));
                return;
            }

            if (operation.UsesTransform && operation.Target == null)
            {
                activeOrientation = null;
                Complete(operation.Waiter, PlayerViewCompletion.TargetLost);
                return;
            }

            if (deltaTime > 0f) operation.Elapsed += deltaTime;
            float t = Mathf.Clamp01(operation.Elapsed / operation.Transition.Duration);
            float eased = operation.Transition.Evaluate(t);
            Vector3 destination = operation.UsesTransform ? operation.Target.position : operation.FixedTarget;
            Quaternion desiredRotation = DesiredRotation(destination);
            rig.rotation = Quaternion.Slerp(operation.StartRotation, desiredRotation, eased);
            if (t < 1f) return;

            rig.rotation = desiredRotation;
            if (operation.Mode == OrientationMode.TrackAcquire)
            {
                operation.Mode = OrientationMode.Tracking;
                operation.Elapsed = operation.Transition.Duration;
                return;
            }

            if (!ReferenceEquals(activeOrientation, operation)) return;
            activeOrientation = null;
            Complete(operation.Waiter, PlayerViewCompletion.Completed);
        }

        private Quaternion DesiredRotation(Vector3 worldTarget)
        {
            Transform rig = RequireViewRig();
            Transform origin = mainCamera != null ? mainCamera.transform : headPose;
            if (origin == null) throw new InvalidOperationException("PlayerView needs a HeadPose or MainCamera view origin.");
            Vector3 direction = worldTarget - origin.position;
            if (direction.sqrMagnitude < 1e-8f) return rig.rotation;
            direction.Normalize();
            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(direction, up)) > 0.999f)
                up = Vector3.forward;
            return Quaternion.LookRotation(direction, up);
        }

        private void EnsureOperational()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                throw new InvalidOperationException("PlayerView accepts commands only while enabled and active.");
            ResolveHierarchyReferences();
            RequireViewRig();
            if (headPose == null || mainCamera == null)
                throw new InvalidOperationException("PlayerView needs HeadPose and MainCamera references in its hierarchy.");
        }

        private Transform RequireViewRig()
        {
            if (viewRig == null) throw new InvalidOperationException("PlayerView has no configured ViewRig transform.");
            return viewRig;
        }

        private void ResolveHierarchyReferences()
        {
            if (viewRig == null) viewRig = transform.Find("ViewRig");
            if (headPose == null && viewRig != null) headPose = viewRig.Find("HeadPose");
            if (mainCamera == null && headPose != null) mainCamera = headPose.GetComponentInChildren<Camera>(true);
        }

        private static float Progress(float elapsed, float duration) => duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);

        private static void Complete(AwaitableCompletionSource<PlayerViewCompletion> waiter,
            PlayerViewCompletion result)
        {
            waiter?.TrySetResult(result);
        }
    }
}
