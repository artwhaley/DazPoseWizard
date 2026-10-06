using System;
using DazPose.Performer;
using DazPose.Player;
using DazPose.Toys;
using UnityEngine;

namespace DazPose.FirstPerformanceVoid
{
    /// <summary>Authored First Contact with per-run ownership and explicit reset/replay.</summary>
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
        [SerializeField] private bool runOnStart;
        PerformanceExecutionScope scope;
        bool running, resetting, capturedInitial;
        int generation;
        float startedAt, stoppedElapsed;
        string status = "Idle";
        Vector3 actorPosition, playerPosition;
        Quaternion actorRotation, viewRotation;
        SceneWardrobeSnapshot initialWardrobe;
        Action resetCancellation;
        public bool IsRunning => running;
        public bool CanRun => Application.isPlaying && isActiveAndEnabled && !running && !resetting;
        public string Status => status;
        public float Elapsed => running ? Time.time - startedAt : stoppedElapsed;
        public SuccubusPerformer Performer => performer;
        void Start() { CaptureInitial(); if (runOnStart) Run(); }
        public void Configure(SuccubusPerformer actor, PlayerController viewer, PerformerSeat seat,
            Transform face, Transform closeMark, Transform laraView, Transform loungeView,
            Transform finalView, AudioClip lineA, AudioClip lineB)
        {
            performer = actor; player = viewer; loungeSeat = seat; laraFaceViewTarget = face;
            laraCloseMark = closeMark; viewMarkLara = laraView; viewMarkLounge = loungeView;
            viewMarkFinal = finalView; speechClipA = lineA; speechClipB = lineB;
        }
        void CaptureInitial()
        {
            if (capturedInitial || performer == null || player == null || player.View == null) return;
            actorPosition = performer.transform.position; actorRotation = performer.transform.rotation;
            playerPosition = player.View.PlayerPosition; viewRotation = player.View.ViewRotation;
            var wardrobe = performer.GetComponent<PerformerWardrobe>();
            if (wardrobe != null) { var state = wardrobe.CurrentWardrobe;
                initialWardrobe = new SceneWardrobeSnapshot { presetId = state.OutfitId, layerCeiling = state.LayerCeiling,
                    hairAction = state.EffectiveHairId == "none" ? WardrobeHairAction.Clear : WardrobeHairAction.Set,
                    resolvedHairId = state.EffectiveHairId, variantId = "default" }; }
            capturedInitial = true;
        }
        public void Run()
        {
            if (!CanRun) return;
            CaptureInitial();
            running = true; startedAt = Time.time; stoppedElapsed = 0f;
            var id = ++generation;
            RunSequence(id);
        }
        async void RunSequence(int id)
        {
            PerformanceExecutionScope current = null;
            try
            {
                await ResetInitialAsync();
                if (id != generation || !running) return;
                ValidateStart();
                current = scope = new PerformanceExecutionScope(name + "/First Contact", () =>
                    performer != null && performer.IsRuntimeReady && player != null && player.isActiveAndEnabled &&
                    player.View != null && player.View.isActiveAndEnabled && laraFaceViewTarget != null);
                SetBeat("Establishing"); performer.ClearGaze(); await current.Delay(2.25f);
                SetBeat("Noticing Lara");
                var turnAwaitable = player.LookAtAsync(laraFaceViewTarget, 1.2f);
                var turn = current.Observe(turnAwaitable, PlayerViewCompletion.Completed, "Player noticing turn", player.View.CaptureOrientationCancellation());
                await current.Delay(.45f); performer.LookAt(player.HeadTransform);
                current.Own("Lara gaze", performer.CaptureCancellation(PerformanceCommand.Gaze));
                await current.Wait(turn);
                SetBeat("Eye contact"); await current.Delay(.7f);
                SetBeat("Speech A"); await current.Wait(current.Observe(performer, PerformanceCommand.Speech,
                    () => performer.SayAsync(speechClipA), SpeechCompletion.Finished, "Speech A"));
                await current.Delay(.5f);
                player.Track(laraFaceViewTarget, new ViewTrackingSettings { Acquire = ViewTransition.EaseInOut(.75f), FollowResponseSeconds = .20f });
                var cancelTracking = player.View.CaptureOrientationCancellation(); current.Own("Player tracking", cancelTracking);
                SetBeat("Approaching");
                var approach = current.Observe(performer, PerformanceCommand.Movement,
                    () => performer.WalkToAsync(laraCloseMark, LocomotionArrivalBehavior.Hold), LocomotionCompletion.Arrived, "Lara approach", persistent: true);
                await current.Delay(.3f); var closeMove = Move(current, viewMarkLara, 3.5f, "Player approach");
                await current.Wait(approach, closeMove); SetBeat("Proximity hold"); await current.Delay(.9f);
                SetBeat("Going to lounge");
                var sit = current.Observe(performer, PerformanceCommand.Seating,
                    () => performer.SitAtAsync(loungeSeat, PerformerSeatedStyle.CrossLegs), SeatingCompletion.Seated, "Lounge SitAt CrossLegs");
                await current.Delay(.6f); var loungeMove = Move(current, viewMarkLounge, 4.5f, "Player lounge move");
                await current.Wait(sit, loungeMove); SetBeat("Seated"); await current.Delay(.8f);
                SetBeat("Final push"); var finalMove = Move(current, viewMarkFinal, 4f, "Player final push");
                await current.Delay(.5f); SetBeat("Speech B");
                var lineB = current.Observe(performer, PerformanceCommand.Speech,
                    () => performer.SayAsync(speechClipB), SpeechCompletion.Finished, "Speech B");
                await current.Wait(finalMove, lineB);
                var toys = player.BoundToyService;
                if (toys != null && toys.CanVibrate)
                {
                    SetBeat("Vibration response"); toys.Vibrate(.5f);
                    var stopOutput = toys.CaptureOutputCancellation(ToyOutputCapability.Vibrate);
                    current.Own("Owned vibration", stopOutput);
                    await current.Delay(1.5f);
                    var ramp = toys.RampVibrationAsync(0f, .5f);
                    current.Own("Owned vibration ramp", toys.CaptureOutputCancellation(ToyOutputCapability.Vibrate));
                    await ramp;
                    current.Check();
                }
                else { SetBeat("Toy alternate — continue without vibration"); await current.Delay(2f); }
                SetBeat("Final hold"); await current.Delay(1f); cancelTracking(); await current.Delay(2f);
                current.Check(); stoppedElapsed = Time.time - startedAt; running = false; status = "Complete — ready to replay";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (id == generation) { status = "ABORTED — " + ex.Message; Debug.LogWarning(status, this); running = false; } }
            finally { current?.Dispose(); if (id == generation) { running = false; scope = null; } }
        }
        PerformanceExecutionScope.Operation Move(PerformanceExecutionScope current, Transform mark, float seconds, string label)
        { var command = player.MoveToAsync(mark, seconds); return current.Observe(command, PlayerViewCompletion.Completed, label, player.View.CaptureMovementCancellation()); }
        public void Stop()
        {
            ++generation; stoppedElapsed = running ? Time.time - startedAt : stoppedElapsed; running = false;
            scope?.Cancel(); scope = null; status = "Stopped — ready to reset/replay";
            resetCancellation?.Invoke(); resetCancellation = null;
        }
        public async Awaitable ResetAsync()
        { Stop(); await ResetInitialAsync(); status = "Reset — ready to run"; }
        async Awaitable ResetInitialAsync()
        {
            if (resetting) throw new InvalidOperationException("First Contact reset is already active.");
            resetting = true;
            try
            {
                CaptureInitial();
                if (!capturedInitial || performer == null || !performer.IsRuntimeReady || player == null)
                    throw new InvalidOperationException("First Contact actor/view must be bound and ready for reset.");
                performer.ResetPerformance(actorPosition, actorRotation);
                player.View.ResetPerformanceView(playerPosition, viewRotation);
                var wardrobe = performer.GetComponent<PerformerWardrobe>();
                if (initialWardrobe != null && wardrobe != null) {
                    var recall = wardrobe.RecallSnapshotAsync(initialWardrobe);
                    resetCancellation = wardrobe.CaptureCancellation();
                    var result = await recall;
                    resetCancellation = null;
                    if (result.Status != WardrobeChangeStatus.Applied && result.Status != WardrobeChangeStatus.NoChange)
                        throw new InvalidOperationException("Initial wardrobe reset failed: " + result.Status); }
            }
            finally { resetting = false; }
        }
        void ValidateStart()
        {
            if (performer == null || player == null || loungeSeat == null || laraFaceViewTarget == null || laraCloseMark == null ||
                viewMarkLara == null || viewMarkLounge == null || viewMarkFinal == null || speechClipA == null || speechClipB == null)
                throw new InvalidOperationException("First Contact is missing an actor, view, seat, named target or speech clip. Assign the scene references.");
            if (!performer.IsRuntimeReady || !performer.LocomotionAvailable || !performer.SeatingAvailable)
                throw new InvalidOperationException("First Contact actor locomotion/seating runtimes are unavailable.");
            if (!loungeSeat.SeatingProfile.IsReady(out var reason)) throw new InvalidOperationException(reason);
        }
        void SetBeat(string beat) { status = beat; Debug.Log(name + "/First Contact: " + beat, this); }
        void OnDisable() { Stop(); }
    }
}
