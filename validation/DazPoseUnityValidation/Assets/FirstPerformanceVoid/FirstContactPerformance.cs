using System;
using System.Collections.Generic;
using DazPose.Performer;
using DazPose.Player;
using UnityEngine;

namespace DazPose.FirstPerformanceVoid
{
    /// <summary>The single authored First Contact scene. One run per Play Mode session.</summary>
    [DisallowMultipleComponent]
    public sealed class FirstContactPerformance : MonoBehaviour
    {
        [SerializeField] private SuccubusPerformer performer;
        [SerializeField] private PlayerController player;
        [SerializeField] private PerformerSeat loungeSeat;
        [SerializeField] private Transform laraFaceViewTarget;
        [SerializeField] private Transform laraCloseMark;
        [SerializeField] private Transform viewMarkLara;
        [SerializeField] private Transform viewMarkLounge;
        [SerializeField] private Transform viewMarkFinal;
        [SerializeField] private AudioClip speechClipA;
        [SerializeField] private AudioClip speechClipB;

        private sealed class PendingAction
        {
            public string Name;
            public bool Done;
            public string Failure;
        }

        private readonly List<PendingAction> actions = new List<PendingAction>();
        private bool hasRun;
        private bool running;
        private bool ownsTracking;
        private int runId;
        private uint positionRevision;
        private uint orientationRevision;
        private float startedAt;
        private float stoppedElapsed;
        private string status = "Idle";

        public bool IsRunning => running;
        public bool CanRun => Application.isPlaying && isActiveAndEnabled && !hasRun;
        public string Status => status;
        public float Elapsed => running ? Time.time - startedAt : stoppedElapsed;

        public void Configure(SuccubusPerformer actor, PlayerController viewer, PerformerSeat seat,
            Transform face, Transform closeMark, Transform laraView, Transform loungeView,
            Transform finalView, AudioClip lineA, AudioClip lineB)
        {
            performer = actor;
            player = viewer;
            loungeSeat = seat;
            laraFaceViewTarget = face;
            laraCloseMark = closeMark;
            viewMarkLara = laraView;
            viewMarkLounge = loungeView;
            viewMarkFinal = finalView;
            speechClipA = lineA;
            speechClipB = lineB;
        }

        public void Run()
        {
            if (!CanRun) return;
            hasRun = true;
            stoppedElapsed = 0f;
            try
            {
                ValidateStart();
                actions.Clear();
                startedAt = Time.time;
                running = true;
                ownsTracking = false;
                positionRevision = player.View.PositionCommandRevision;
                orientationRevision = player.View.OrientationCommandRevision;
                RunSequence(++runId);
            }
            catch (Exception exception)
            {
                status = "ABORTED — " + exception.Message;
                Debug.LogWarning("First Contact aborted: " + exception.Message, this);
            }
        }

        private async void RunSequence(int id)
        {
            try
            {
                SetBeat("Establishing");
                performer.ClearGaze();
                await Delay(2.25f, id);

                SetBeat("Noticing Lara");
                PendingAction turn = Start(player.LookAtAsync(laraFaceViewTarget, 1.2f),
                    PlayerViewCompletion.Completed, "Player noticing turn");
                orientationRevision = player.View.OrientationCommandRevision;
                await Delay(0.45f, id);
                performer.LookAt(player.HeadTransform);
                await WaitFor(id, turn);
                SetBeat("Eye contact");
                await Delay(0.7f, id);

                SetBeat("Speech A");
                await WaitFor(id, Start(performer.SayAsync(speechClipA), SpeechCompletion.Finished, "Speech A"));
                await Delay(0.5f, id);

                player.Track(laraFaceViewTarget, new ViewTrackingSettings
                {
                    Acquire = ViewTransition.EaseInOut(0.75f),
                    FollowResponseSeconds = 0.20f
                });
                orientationRevision = player.View.OrientationCommandRevision;
                ownsTracking = true;
                performer.SetHoldLocomotionArrivalPose(true);
                SetBeat("Approaching");
                PendingAction approach = Start(performer.WalkToAsync(laraCloseMark),
                    LocomotionCompletion.Arrived, "Lara approach");
                await Delay(0.3f, id);
                PendingAction closeMove = Move(viewMarkLara, 3.5f, "Player approach");
                await WaitFor(id, approach, closeMove);
                SetBeat("Proximity hold");
                await Delay(0.9f, id);

                SetBeat("Going to lounge");
                PendingAction sit = Start(performer.SitAtAsync(loungeSeat, PerformerSeatedStyle.CrossLegs),
                    SeatingCompletion.Seated, "Lounge SitAt CrossLegs");
                await Delay(0.6f, id);
                PendingAction loungeMove = Move(viewMarkLounge, 4.5f, "Player lounge move");
                await WaitFor(id, sit, loungeMove);
                performer.SetHoldLocomotionArrivalPose(false);
                SetBeat("Seated");
                await Delay(0.8f, id);

                SetBeat("Final push");
                PendingAction finalMove = Move(viewMarkFinal, 4f, "Player final push");
                await Delay(0.5f, id);
                SetBeat("Speech B");
                PendingAction lineB = Start(performer.SayAsync(speechClipB), SpeechCompletion.Finished, "Speech B");
                await WaitFor(id, finalMove, lineB);

                SetBeat("Final hold");
                await Delay(1f, id);
                player.StopTracking();
                orientationRevision = player.View.OrientationCommandRevision;
                ownsTracking = false;
                await Delay(2f, id);
                stoppedElapsed = Time.time - startedAt;
                running = false;
                status = "Complete — re-enter Play Mode to replay";
                Debug.Log("First Contact complete in " + stoppedElapsed.ToString("F2")
                    + " gameplay seconds. Speech A: " + speechClipA.name + " (" + speechClipA.length.ToString("F2")
                    + "s); Speech B: " + speechClipB.name + " (" + speechClipB.length.ToString("F2") + "s).", this);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (this != null && running && id == runId) Abort(exception.Message);
            }
        }

        private PendingAction Move(Transform mark, float duration, string name)
        {
            PendingAction action = Start(player.MoveToAsync(mark, duration), PlayerViewCompletion.Completed, name);
            positionRevision = player.View.PositionCommandRevision;
            return action;
        }

        // Consume every Unity Awaitable exactly once, immediately after starting it.
        // Concurrent results are observed independently, so either failure aborts promptly.
        private PendingAction Start<T>(Awaitable<T> operation, T expected, string name)
        {
            var action = new PendingAction { Name = name };
            actions.Add(action);
            Observe(operation, expected, action);
            return action;
        }

        private async void Observe<T>(Awaitable<T> operation, T expected, PendingAction action)
        {
            try
            {
                T result = await operation;
                if (!EqualityComparer<T>.Default.Equals(result, expected))
                    action.Failure = action.Name + " returned " + result + " (expected " + expected + ").";
                else if (this != null && running)
                    Debug.Log("First Contact: " + action.Name + " → " + result, this);
            }
            catch (Exception exception) { action.Failure = action.Name + ": " + exception.Message; }
            action.Done = true;
        }

        private async Awaitable WaitFor(int id, params PendingAction[] required)
        {
            while (true)
            {
                CheckRun(id);
                bool complete = true;
                foreach (PendingAction action in required) complete &= action.Done;
                if (complete) return;
                await Awaitable.NextFrameAsync();
            }
        }

        // Authored pauses use scaled gameplay time; a paused game pauses the performance.
        private async Awaitable Delay(float seconds, int id)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                CheckRun(id);
                await Awaitable.NextFrameAsync();
                CheckRun(id);
                elapsed += Time.deltaTime;
            }
        }

        private void CheckRun(int id)
        {
            if (this == null || !running || id != runId) throw new OperationCanceledException();
            foreach (PendingAction action in actions)
                if (action.Done && action.Failure != null) throw new InvalidOperationException(action.Failure);
            if (performer == null || !performer.IsRuntimeReady)
                throw new InvalidOperationException("Lara became unavailable.");
            if (player == null || !player.isActiveAndEnabled || player.View == null || !player.View.isActiveAndEnabled)
                throw new InvalidOperationException("Player became unavailable.");
            if (laraFaceViewTarget == null) throw new InvalidOperationException("Lara FaceViewTarget was lost.");
            if (player.View.PositionCommandRevision != positionRevision)
                throw new InvalidOperationException("Player movement was superseded by another command.");
            if (player.View.OrientationCommandRevision != orientationRevision)
                throw new InvalidOperationException("Player orientation was superseded by another command.");
            if (ownsTracking && ((!player.View.IsTracking && !player.View.IsAcquiringTrack)
                || player.View.CurrentTrackTarget != laraFaceViewTarget))
                throw new InvalidOperationException("Player tracking of Lara stopped.");
        }

        private void ValidateStart()
        {
            if (performer == null || player == null || loungeSeat == null || laraFaceViewTarget == null
                || laraCloseMark == null || viewMarkLara == null || viewMarkLounge == null || viewMarkFinal == null
                || speechClipA == null || speechClipB == null)
                throw new InvalidOperationException("Missing references. Run Install First Contact Performance in Edit Mode.");
            if (!performer.IsRuntimeReady || !performer.LocomotionAvailable || !performer.SeatingAvailable)
                throw new InvalidOperationException("Lara's performer, locomotion and seating runtimes must be ready.");
            if (performer.IsLocomoting || performer.IsSpeaking || performer.PendingSpeechCount != 0
                || performer.SeatingState != PerformerSeatingState.Standing)
                throw new InvalidOperationException("Begin with Lara standing, not walking or speaking.");
            if (!player.isActiveAndEnabled || player.View == null || !player.View.isActiveAndEnabled
                || player.HeadTransform == null || player.MainCamera == null)
                throw new InvalidOperationException("Player View must be active and configured.");
            if (player.View.IsMoving || player.View.IsLooking || player.View.IsTracking || player.View.IsAcquiringTrack)
                throw new InvalidOperationException("Begin with Player View idle.");
            if (loungeSeat.ApproachAnchor == null || loungeSeat.SeatAnchor == null || loungeSeat.SeatingProfile == null)
                throw new InvalidOperationException("The lounge seat is not configured.");
            if (!loungeSeat.SeatingProfile.IsReady(out string reason)) throw new InvalidOperationException(reason);
        }

        private void SetBeat(string beat)
        {
            status = beat;
            Debug.Log("First Contact: " + beat + " at " + Elapsed.ToString("F2") + "s", this);
        }

        private void Abort(string reason)
        {
            stoppedElapsed = Time.time - startedAt;
            running = false;
            ++runId;
            status = "ABORTED — " + reason;
            if (performer != null && performer.IsRuntimeReady && performer.LocomotionAvailable)
                performer.SetHoldLocomotionArrivalPose(false);
            // Release only view commands still owned by this run. Never override the user's replacement.
            try
            {
                if (player != null && player.isActiveAndEnabled && player.View != null && player.View.isActiveAndEnabled)
                {
                    if (ownsTracking && player.View.OrientationCommandRevision == orientationRevision)
                        player.StopTracking();
                    if (player.View.IsMoving && player.View.PositionCommandRevision == positionRevision)
                        player.MoveTo(player.View.PlayerPosition, ViewTransition.Snap);
                }
            }
            catch (Exception exception) { Debug.LogWarning("First Contact view cleanup: " + exception.Message, this); }
            ownsTracking = false;
            Debug.LogWarning("First Contact aborted: " + reason, this);
        }

        private void OnDisable()
        {
            if (running) Abort("Performance component was disabled.");
        }

        private void OnDrawGizmosSelected()
        {
            if (laraCloseMark != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(laraCloseMark.position, 0.12f);
                Gizmos.DrawLine(laraCloseMark.position, laraCloseMark.position + laraCloseMark.forward * 0.6f);
            }
            if (viewMarkFinal != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(viewMarkFinal.position, 0.12f);
                if (viewMarkLounge != null) Gizmos.DrawLine(viewMarkLounge.position, viewMarkFinal.position);
            }
        }
    }
}
