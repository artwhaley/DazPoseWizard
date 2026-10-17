using System;
using DazPose.Player;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Optional player-view commands bound to a PerformanceRun.</summary>
    public sealed class ScopedPlayer
    {
        private readonly PerformanceRun run;
        private readonly PlayerController player;
        internal ScopedPlayer(PerformanceRun owner, PlayerController boundPlayer) { run = owner; player = boundPlayer; }

        public Awaitable<PlayerViewCompletion> MoveToAsync(Transform target) =>
            run.Execute("Player.MoveTo", () => player.MoveToAsync(target), result => result == PlayerViewCompletion.Completed,
                result => result.ToString(), captureCancellation: () => player.View.CaptureMovementCancellation());
        public Awaitable<PlayerViewCompletion> MoveToAsync(Vector3 position) =>
            run.Execute("Player.MoveTo", () => player.MoveToAsync(position), result => result == PlayerViewCompletion.Completed,
                result => result.ToString(), captureCancellation: () => player.View.CaptureMovementCancellation());
        public Awaitable<PlayerViewCompletion> MoveToAsync(Transform target, float duration) =>
            run.Execute("Player.MoveTo", () => player.MoveToAsync(target, duration), result => result == PlayerViewCompletion.Completed,
                result => result.ToString(), captureCancellation: () => player.View.CaptureMovementCancellation());
        public Awaitable<PlayerViewCompletion> MoveToAsync(Vector3 position, float duration) =>
            run.Execute("Player.MoveTo", () => player.MoveToAsync(position, duration), result => result == PlayerViewCompletion.Completed,
                result => result.ToString(), captureCancellation: () => player.View.CaptureMovementCancellation());
        public Awaitable<PlayerViewCompletion> MoveToAsync(Transform target, ViewTransition transition) =>
            run.Execute("Player.MoveTo", () => player.MoveToAsync(target, transition), result => result == PlayerViewCompletion.Completed,
                result => result.ToString(), captureCancellation: () => player.View.CaptureMovementCancellation());
        public Awaitable<PlayerViewCompletion> MoveToAsync(Vector3 position, ViewTransition transition) =>
            run.Execute("Player.MoveTo", () => player.MoveToAsync(position, transition), result => result == PlayerViewCompletion.Completed,
                result => result.ToString(), captureCancellation: () => player.View.CaptureMovementCancellation());
        public Awaitable<PlayerViewCompletion> LookAtAsync(Transform target) =>
            run.Execute("Player.LookAt", () => player.LookAtAsync(target), result => result == PlayerViewCompletion.Completed,
                result => result.ToString(), captureCancellation: () => player.View.CaptureOrientationCancellation());
        public Awaitable<PlayerViewCompletion> LookAtAsync(Transform target, float duration) =>
            run.Execute("Player.LookAt", () => player.LookAtAsync(target, duration), result => result == PlayerViewCompletion.Completed,
                result => result.ToString(), captureCancellation: () => player.View.CaptureOrientationCancellation());
        public Awaitable<PlayerViewCompletion> LookAtAsync(Transform target, ViewTransition transition) =>
            run.Execute("Player.LookAt", () => player.LookAtAsync(target, transition), result => result == PlayerViewCompletion.Completed,
                result => result.ToString(), captureCancellation: () => player.View.CaptureOrientationCancellation());
        public Awaitable<PlayerViewCompletion> LookAtAsync(Vector3 position) =>
            run.Execute("Player.LookAt", () => player.LookAtAsync(position), result => result == PlayerViewCompletion.Completed,
                result => result.ToString(), captureCancellation: () => player.View.CaptureOrientationCancellation());
        public Awaitable<PlayerViewCompletion> LookAtAsync(Vector3 position, float duration) =>
            run.Execute("Player.LookAt", () => player.LookAtAsync(position, duration), result => result == PlayerViewCompletion.Completed,
                result => result.ToString(), captureCancellation: () => player.View.CaptureOrientationCancellation());
        public Awaitable<PlayerViewCompletion> LookAtAsync(Vector3 position, ViewTransition transition) =>
            run.Execute("Player.LookAt", () => player.LookAtAsync(position, transition), result => result == PlayerViewCompletion.Completed,
                result => result.ToString(), captureCancellation: () => player.View.CaptureOrientationCancellation());

        public void Track(Transform target)
        {
            run.ThrowIfActive();
            player.Track(target);
            run.RegisterPersistentCleanup(player.View.CaptureOrientationCancellation());
        }

        public void Track(Transform target, float acquireDuration)
        {
            run.ThrowIfActive();
            player.Track(target, acquireDuration);
            run.RegisterPersistentCleanup(player.View.CaptureOrientationCancellation());
        }

        private PlayerBodyPresence RequireBody() => player.BodyPresence != null ? player.BodyPresence
            : throw new InvalidOperationException("Assign PlayerController.bodyPresence for player body commands.");
        public PlayerBodyVisibilityState BodyVisibilityState => RequireBody().VisibilityState;
        public async Awaitable DissolveBodyInAsync(float seconds = 3f) {
            await run.Execute("Player.DissolveBodyIn", () => RequireBody().DissolveInAsync(seconds),
                result => result == VisibilityCompletion.Visible, result => result.ToString(),
                captureCancellation: () => RequireBody().CaptureCancellation());
        }
        public async Awaitable DissolveBodyOutAsync(float seconds = 3f) {
            await run.Execute("Player.DissolveBodyOut", () => RequireBody().DissolveOutAsync(seconds),
                result => result == VisibilityCompletion.Hidden, result => result.ToString(),
                captureCancellation: () => RequireBody().CaptureCancellation());
        }
        public void StopTracking()
        {
            run.ThrowIfActive();
            player.StopTracking();
        }
    }
}
