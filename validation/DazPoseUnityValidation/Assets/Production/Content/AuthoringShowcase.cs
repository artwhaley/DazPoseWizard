using DazPose.Motion;
using DazPose.Performer;
using UnityEngine;
namespace DazPose.Content {
 public sealed class AuthoringShowcase : PerformanceSequence {
  [SerializeField] public Transform entrance,contactMark,playerHead,actorFocus,contactView;
  [SerializeField] public PerformerPose arrivalPose,invitingPose;
  [SerializeField] public PerformerExpression expression;
  [SerializeField] public PerformerGesture greeting;
  [SerializeField] public AudioClip welcomeLine,contactLine;
  [SerializeField] public FunscriptMotionProgram motion;
  [SerializeField] public float compositionSeconds=100;
  public override async Awaitable ExecuteAsync(PerformanceRun run) {
   float began=Time.time;
   var actor=run.Performer;
   if(actor.IsSeated) await actor.StandUpAsync();
   if(actor.IsGripping) await actor.ReleaseGripAsync();
   if(actor.VisibilityState==PerformerVisibilityState.Visible) await actor.DissolveOutAsync(.7f);
   await actor.OutfitAsync("first-outfit");
   await actor.DissolveInAtAsync(entrance,1.5f,arrivalPose);
   await run.Delay(2);
   actor.LookAt(playerHead);
   actor.Expression(expression,.6f,.8f);
   var wave=actor.GestureAsync(greeting);
   await actor.SayAsync(welcomeLine);
   await wave;
   await actor.PoseAsync(invitingPose);
   await run.Delay(3);
   await actor.DissolveToAsync(contactMark,1.5f,arrivalPose);
   var view=run.Player.MoveToAsync(contactView,3);
   await run.Player.LookAtAsync(actorFocus,1);
   await view;
   await actor.OutfitAsync("second-outfit");
   await run.Delay(2);
   await actor.TryRemoveLayerAsync();
   await run.Delay(2);
   await actor.TryAddLayerAsync();
   await actor.SetHairAsync("first-outfit/EmikoHair_59875");
   await actor.SayAsync(contactLine);
   await actor.GripAsync(run.ContactTarget);
   actor.HoldGrip(.5f);
   await run.Delay(2);
   run.StartPlayback(motion,loop:true);
   actor.FollowGripMotion();
   run.Toys.Follow();
   run.Toys.RampVibration(.4f,3);
   await run.Delay(Mathf.Max(18,compositionSeconds-(Time.time-began)-8));
   run.Toys.RampVibration(0,2);
   await run.Delay(2);
   run.Toys.StopFollowing();
   run.Toys.Stop();
   run.StopPlayback();
   await actor.ReleaseGripAsync();
   actor.ClearGaze();
   await run.Delay(Mathf.Max(0,compositionSeconds-(Time.time-began)-1.5f));
   await actor.DissolveOutAsync(1.5f);
  }
 }
}