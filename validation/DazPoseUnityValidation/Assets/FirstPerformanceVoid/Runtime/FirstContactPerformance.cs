using System;
using DazPose.Performer;
using DazPose.Player;
using UnityEngine;
namespace DazPose.FirstPerformanceVoid {
    /// <summary>Content only. A saved PerformanceHost owns execution and cancellation.</summary>
    public sealed class FirstContactPerformance : PerformanceSequence {
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
        [SerializeField, HideInInspector] private bool runOnStart;
        [SerializeField] private PerformanceSceneBindings bindings;

        PerformanceHost Host => GetComponent<PerformanceHost>();
        public bool IsRunning => Host!=null && Host.State==PerformanceHostState.Running;
        public bool CanRun => Application.isPlaying && !IsRunning && Host!=null;
        public string Status => Host==null ? "ABORTED — Missing saved PerformanceHost" : Host.State==PerformanceHostState.Failed ? "ABORTED — "+Host.Failure?.Message : Host.State.ToString();
        public float Elapsed => 0;
        public SuccubusPerformer Performer => performer;
        public void Run() => Host?.StartPerformance();
        public void Stop() => Host?.StopPerformance();
        [Obsolete("Replay uses StartPerformance without resetting tuning or appearance.")]
        public async Awaitable ResetAsync() { Stop(); await Awaitable.NextFrameAsync(); }
        public void Configure(SuccubusPerformer actor, PlayerController viewer, PerformerSeat seat,
            Transform face, Transform closeMark, Transform laraView, Transform loungeView,
            Transform finalView, AudioClip lineA, AudioClip lineB)
        {
            performer = actor; player = viewer; loungeSeat = seat; laraFaceViewTarget = face; laraCloseMark = closeMark;
            viewMarkLara = laraView; viewMarkLounge = loungeView; viewMarkFinal = finalView;
            speechClipA = lineA; speechClipB = lineB;
        }


        public override async Awaitable ExecuteAsync(PerformanceRun run) {
            if(run.Performer.IsSeated) await run.Performer.StandUpAsync();
                run.Performer.ClearGaze(); await run.Delay(2.25f);
                
                Awaitable<PlayerViewCompletion> turn = run.Player.LookAtAsync(laraFaceViewTarget, 1.2f);
                await run.Delay(.45f); run.Performer.LookAt(player.HeadTransform); await turn;
                await run.Delay(.7f);
                await run.Performer.SayAsync(speechClipA); await run.Delay(.5f);
                run.Player.Track(laraFaceViewTarget, .75f);
                
                Awaitable<LocomotionCompletion> approach = run.Performer.WalkToAsync(laraCloseMark, LocomotionArrivalBehavior.Hold);
                await run.Delay(.3f);
                Awaitable<PlayerViewCompletion> closeMove = run.Player.MoveToAsync(viewMarkLara, 3.5f);
                await approach; await closeMove;
                await run.Delay(.9f);
                
                Awaitable<SeatingCompletion> sit = run.Performer.SitAtAsync(loungeSeat, PerformerSeatedStyle.CrossLegs);
                await run.Delay(.6f);
                Awaitable<PlayerViewCompletion> loungeMove = run.Player.MoveToAsync(viewMarkLounge, 4.5f);
                await sit; await loungeMove;
                await run.Delay(.8f);
                
                Awaitable<PlayerViewCompletion> finalMove = run.Player.MoveToAsync(viewMarkFinal, 4f);
                await run.Delay(.5f);
                
                Awaitable<SpeechCompletion> lineB = run.Performer.SayAsync(speechClipB);
                await finalMove; await lineB;
                
                run.Toys.Vibrate(.5f);
                await run.Delay(1.5f);
                run.Toys.RampVibration(0f, .5f);
                await run.Delay(.5f);
                await run.Delay(1f); run.Player.StopTracking(); await run.Delay(2f);

        }
    }
}
