using System;
using DazPose.Performer;
using DazPose.Player;
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
        [SerializeField] private PerformanceSceneBindings bindings;
        private PerformanceRun run;
        private bool running, resetting, capturedInitial;
        private int generation;
        private float startedAt, stoppedElapsed;
        private string status = "Idle";
        private Vector3 actorPosition, playerPosition;
        private Quaternion actorRotation, viewRotation;
        private SceneWardrobeSnapshot initialWardrobe;

        public bool IsRunning => running;
        public bool CanRun => Application.isPlaying && isActiveAndEnabled && !running && !resetting;
        public string Status => status;
        public float Elapsed => running ? Time.time - startedAt : stoppedElapsed;
        public SuccubusPerformer Performer => performer;

        private void Start() { CaptureInitial(); if (runOnStart) Run(); }

        public void Configure(SuccubusPerformer actor, PlayerController viewer, PerformerSeat seat,
            Transform face, Transform closeMark, Transform laraView, Transform loungeView,
            Transform finalView, AudioClip lineA, AudioClip lineB)
        {
            performer = actor; player = viewer; loungeSeat = seat; laraFaceViewTarget = face; laraCloseMark = closeMark;
            viewMarkLara = laraView; viewMarkLounge = loungeView; viewMarkFinal = finalView;
            speechClipA = lineA; speechClipB = lineB;
        }

        private void CaptureInitial()
        {
            if (capturedInitial || performer == null || player == null || player.View == null) return;
            actorPosition = performer.transform.position; actorRotation = performer.transform.rotation;
            playerPosition = player.View.PlayerPosition; viewRotation = player.View.ViewRotation;
            var wardrobe = performer.GetComponent<PerformerWardrobe>();
            if (wardrobe != null)
            {
                var state = wardrobe.CurrentWardrobe;
                initialWardrobe = new SceneWardrobeSnapshot
                {
                    presetId = state.OutfitId, layerCeiling = state.LayerCeiling,
                    hairAction = state.EffectiveHairId == "none" ? WardrobeHairAction.Clear : WardrobeHairAction.Set,
                    resolvedHairId = state.EffectiveHairId, variantId = "default"
                };
            }
            capturedInitial = true;
        }

        public void Run()
        {
            if (!CanRun) return;
            CaptureInitial();
            running = true; startedAt = Time.time; stoppedElapsed = 0f;
            _ = RunSequence(++generation);
        }

        private async Awaitable RunSequence(int id)
        {
            try
            {
                await ResetInitialAsync();
                if (id != generation || !running) return;
                ValidateStart();
                EnsureBindings();
                run = PerformanceRun.Begin(bindings);
                SetBeat("Establishing"); run.Performer.ClearGaze(); await run.Delay(2.25f);
                SetBeat("Noticing Lara");
                Awaitable<PlayerViewCompletion> turn = run.Player.LookAtAsync(laraFaceViewTarget, 1.2f);
                await run.Delay(.45f); run.Performer.LookAt(player.HeadTransform); await turn;
                SetBeat("Eye contact"); await run.Delay(.7f);
                SetBeat("Speech A"); await run.Performer.SayAsync(speechClipA); await run.Delay(.5f);
                run.Player.Track(laraFaceViewTarget, .75f);
                SetBeat("Approaching");
                Awaitable<LocomotionCompletion> approach = run.Performer.WalkToAsync(laraCloseMark, LocomotionArrivalBehavior.Hold);
                await run.Delay(.3f);
                Awaitable<PlayerViewCompletion> closeMove = run.Player.MoveToAsync(viewMarkLara, 3.5f);
                await approach; await closeMove;
                SetBeat("Proximity hold"); await run.Delay(.9f);
                SetBeat("Going to lounge");
                Awaitable<SeatingCompletion> sit = run.Performer.SitAtAsync(loungeSeat, PerformerSeatedStyle.CrossLegs);
                await run.Delay(.6f);
                Awaitable<PlayerViewCompletion> loungeMove = run.Player.MoveToAsync(viewMarkLounge, 4.5f);
                await sit; await loungeMove;
                SetBeat("Seated"); await run.Delay(.8f);
                SetBeat("Final push");
                Awaitable<PlayerViewCompletion> finalMove = run.Player.MoveToAsync(viewMarkFinal, 4f);
                await run.Delay(.5f);
                SetBeat("Speech B");
                Awaitable<SpeechCompletion> lineB = run.Performer.SayAsync(speechClipB);
                await finalMove; await lineB;
                if (run.HasToys && run.Toys.CanOutput(DazPose.Toys.ToyOutputCapability.Vibrate))
                {
                    SetBeat("Vibration response"); await run.Toys.VibrateAsync(.5f); await run.Delay(1.5f);
                    await run.Toys.RampVibrationAsync(0f, .5f);
                }
                else { SetBeat("Toy alternate — continue without vibration"); await run.Delay(2f); }
                SetBeat("Final hold"); await run.Delay(1f); run.Player.StopTracking(); await run.Delay(2f);
                stoppedElapsed = Time.time - startedAt; status = "Complete — ready to replay"; running = false;
            }
            catch (OperationCanceledException exception)
            {
                if (id == generation && run != null && run.Failure != null)
                {
                    status = "ABORTED — " + run.Failure.Message;
                    Debug.LogWarning(status, this);
                    running = false;
                }
                else if (id == generation && running)
                {
                    Debug.LogWarning(name + "/First Contact cancelled: " + exception.Message, this);
                }
            }
            catch (Exception exception)
            {
                if (id == generation)
                {
                    status = "ABORTED — " + exception.Message; Debug.LogWarning(status, this); running = false;
                }
            }
            finally
            {
                if (id == generation)
                {
                    run?.Dispose(); run = null; running = false;
                }
            }
        }

        private void EnsureBindings()
        {
            if (bindings == null) bindings = GetComponent<PerformanceSceneBindings>();
            if (bindings == null) bindings = gameObject.AddComponent<PerformanceSceneBindings>();
            bindings.Configure(performer, player, null, player.BoundToyService,
                PerformerCapability.Pose | PerformerCapability.Speech | PerformerCapability.Movement | PerformerCapability.Seating);
            bindings.DeclareAssets(null, null, new[] { speechClipA, speechClipB });
            bindings.DeclareSeats(new[] { loungeSeat });
        }

        public void Stop()
        {
            ++generation; stoppedElapsed = running ? Time.time - startedAt : stoppedElapsed; running = false;
            run?.Stop(); run = null; status = "Stopped — ready to reset/replay";
        }

        public async Awaitable ResetAsync()
        {
            Stop(); await ResetInitialAsync(); status = "Reset — ready to run";
        }

        private async Awaitable ResetInitialAsync()
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
                if (initialWardrobe != null && wardrobe != null)
                {
                    var result = await wardrobe.RecallSnapshotAsync(initialWardrobe);
                    if (result.Status != WardrobeChangeStatus.Applied && result.Status != WardrobeChangeStatus.NoChange)
                        throw new InvalidOperationException("Initial wardrobe reset failed: " + result.Status);
                }
            }
            finally { resetting = false; }
        }

        private void ValidateStart()
        {
            if (performer == null || player == null || loungeSeat == null || laraFaceViewTarget == null || laraCloseMark == null
                || viewMarkLara == null || viewMarkLounge == null || viewMarkFinal == null || speechClipA == null || speechClipB == null)
                throw new InvalidOperationException("First Contact is missing an actor, view, seat, named target or speech clip. Assign the scene references.");
            if (!performer.IsRuntimeReady || !performer.LocomotionAvailable || !performer.SeatingAvailable)
                throw new InvalidOperationException("First Contact actor locomotion/seating runtimes are unavailable.");
            if (!loungeSeat.SeatingProfile.IsReady(out var reason)) throw new InvalidOperationException(reason);
        }

        private void SetBeat(string beat) { status = beat; Debug.Log(name + "/First Contact: " + beat, this); }
        private void OnDisable() { Stop(); }
    }
}
