using System;
using UnityEngine;

namespace DazPose.Player
{
    /// <summary>Director-facing facade for independent Player position and view direction commands.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerView))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerView view;

        public PlayerView View
        {
            get
            {
                if (view == null) view = GetComponent<PlayerView>();
                return view;
            }
        }

        public Transform HeadTransform => RequireView().HeadTransform;
        public Camera MainCamera => RequireView().MainCamera;

        public void Configure(Transform viewRig, Transform headPose, Camera mainCamera)
        {
            RequireView().Configure(viewRig, headPose, mainCamera);
        }

        public void MoveTo(Transform target) => RequireView().MoveTo(target);
        public void MoveTo(Transform target, float duration) => RequireView().MoveTo(target, duration);
        public void MoveTo(Transform target, ViewTransition transition) => RequireView().MoveTo(target, transition);
        public void MoveTo(Vector3 worldPosition) => RequireView().MoveTo(worldPosition);
        public void MoveTo(Vector3 worldPosition, float duration) => RequireView().MoveTo(worldPosition, duration);
        public void MoveTo(Vector3 worldPosition, ViewTransition transition) => RequireView().MoveTo(worldPosition, transition);

        public Awaitable<PlayerViewCompletion> MoveToAsync(Transform target) => RequireView().MoveToAsync(target);
        public Awaitable<PlayerViewCompletion> MoveToAsync(Transform target, float duration) => RequireView().MoveToAsync(target, duration);
        public Awaitable<PlayerViewCompletion> MoveToAsync(Transform target, ViewTransition transition) => RequireView().MoveToAsync(target, transition);
        public Awaitable<PlayerViewCompletion> MoveToAsync(Vector3 worldPosition) => RequireView().MoveToAsync(worldPosition);
        public Awaitable<PlayerViewCompletion> MoveToAsync(Vector3 worldPosition, float duration) => RequireView().MoveToAsync(worldPosition, duration);
        public Awaitable<PlayerViewCompletion> MoveToAsync(Vector3 worldPosition, ViewTransition transition) => RequireView().MoveToAsync(worldPosition, transition);

        public void LookAt(Transform target) => RequireView().LookAt(target);
        public void LookAt(Transform target, float duration) => RequireView().LookAt(target, duration);
        public void LookAt(Transform target, ViewTransition transition) => RequireView().LookAt(target, transition);
        public void LookAt(Vector3 worldPosition) => RequireView().LookAt(worldPosition);
        public void LookAt(Vector3 worldPosition, float duration) => RequireView().LookAt(worldPosition, duration);
        public void LookAt(Vector3 worldPosition, ViewTransition transition) => RequireView().LookAt(worldPosition, transition);

        public Awaitable<PlayerViewCompletion> LookAtAsync(Transform target) => RequireView().LookAtAsync(target);
        public Awaitable<PlayerViewCompletion> LookAtAsync(Transform target, float duration) => RequireView().LookAtAsync(target, duration);
        public Awaitable<PlayerViewCompletion> LookAtAsync(Transform target, ViewTransition transition) => RequireView().LookAtAsync(target, transition);
        public Awaitable<PlayerViewCompletion> LookAtAsync(Vector3 worldPosition) => RequireView().LookAtAsync(worldPosition);
        public Awaitable<PlayerViewCompletion> LookAtAsync(Vector3 worldPosition, float duration) => RequireView().LookAtAsync(worldPosition, duration);
        public Awaitable<PlayerViewCompletion> LookAtAsync(Vector3 worldPosition, ViewTransition transition) => RequireView().LookAtAsync(worldPosition, transition);

        public void Track(Transform target) => RequireView().Track(target);
        public void Track(Transform target, float acquireDuration) => RequireView().Track(target, acquireDuration);
        public void Track(Transform target, ViewTrackingSettings settings) => RequireView().Track(target, settings);
        public void StopTracking() => RequireView().StopTracking();

        private void Awake()
        {
            if (view == null) view = GetComponent<PlayerView>();
        }

        private void OnDisable() => view?.CancelActiveCommands();
        private void OnDestroy() => view?.CancelActiveCommands();

        private PlayerView RequireView()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                throw new InvalidOperationException("PlayerController accepts commands only while enabled and active.");
            if (view == null) view = GetComponent<PlayerView>();
            if (view == null) throw new InvalidOperationException("PlayerController requires a PlayerView component on the same GameObject.");
            return view;
        }
    }
}
