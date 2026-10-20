using System;
using System.Threading;
using System.Threading.Tasks;
using DazPose.Player;
using UnityEngine;
namespace DazPose.Performer {
 internal sealed partial class PerformancePresentationService {
  internal Action<string> Trace;
  internal Action<string> Checkpoint;
  void Guard(PerformanceOperationScope owner,CancellationToken cancellation,string phase){run.ThrowIfActive();cancellation.ThrowIfCancellationRequested();owner.Cancellation.ThrowIfCancellationRequested();Trace?.Invoke(phase);Checkpoint?.Invoke(phase);run.ThrowIfActive();cancellation.ThrowIfCancellationRequested();owner.Cancellation.ThrowIfCancellationRequested();}
  static bool KeepResource(string resource,PresentationReturnPlan plan,bool restoreGrip) {
   switch(resource) {
    case "arrival":return !plan.Has(PresentationDomain.Arrival);
    case "body-pose":return !plan.Has(PresentationDomain.Pose);
    case "seated-pose":return !plan.Has(PresentationDomain.Seat);
    case "expression":return !plan.Has(PresentationDomain.Expression);
    case "gaze":return !plan.Has(PresentationDomain.Gaze);
    case "aura":return !plan.Has(PresentationDomain.Aura);
    case "grip":return !restoreGrip;
    case "material":return !plan.Has(PresentationDomain.Material);
    case "view-orientation":return !plan.Has(PresentationDomain.View) && !plan.RelocateActor && !plan.SeatStructural;
    default:return false;
   }
  }
  public async Task RestoreAsync(PresentationReturnPlan plan,PerformanceOperationScope owner,CancellationToken cancellation) {
   var entrance=plan.Entrance;var p=entrance.Performer;var actor=p.Actor;var view=entrance.View.View;var body=entrance.Body.Body;
   bool relocate=plan.RelocateActor || plan.SeatStructural;
   bool conceal=relocate || plan.Has(PresentationDomain.Wardrobe);
   bool restoreGrip=plan.Has(PresentationDomain.Grip) || conceal && p.Grip?.Active==true;
   bool restoreView=plan.Has(PresentationDomain.View) || relocate && entrance.View.Tracking;
   Guard(owner,cancellation,"Prepared");
   // Settled control writers are replaced through their subsystem, so stale cleanup must not snap
   // their visible value to a default before its requested half-second blend starts.
   foreach(var resource in new[]{"body-pose","seated-pose","expression","gaze","aura","material","arrival"})
    if(!KeepResource(resource,plan,restoreGrip))owner.Forget(resource);
   owner.ReleaseWritersExcept(resource=>KeepResource(resource,plan,restoreGrip));
   if(restoreGrip && actor.IsGripping)actor.CancelGrip();
   if(restoreView)view.FreezePresentationOrientation();
   Guard(owner,cancellation,"WritersReleased");
   if(plan.Has(PresentationDomain.Motion)) {
    Guard(owner,cancellation,"MotionRestore");if(p.MotionVariant>=0 && actor.PresentationMotionVariant!=p.MotionVariant)actor.SetMotionVariant(p.MotionVariant,.5f);
    while(actor.IsPresentationMotionTransitioning){run.ThrowIfActive();cancellation.ThrowIfCancellationRequested();await Awaitable.NextFrameAsync(owner.Cancellation);}
   }
   if((conceal || p.Hidden) && !actor.IsHidden){Guard(owner,cancellation,"ConcealLara");await run.Performer.DissolveOutAsync(.25f);Guard(owner,cancellation,"LaraConcealed");}
   if(plan.Has(PresentationDomain.PlayerBody) && !body.IsStablyHidden){Guard(owner,cancellation,"ConcealBody");await run.Player.DissolveBodyOutAsync(.25f);Guard(owner,cancellation,"BodyConcealed");}
   if(plan.Has(PresentationDomain.Wardrobe)) {
    Guard(owner,cancellation,"WardrobeCommit");var wardrobe=actor.GetComponent<PerformerWardrobe>();
    await run.Execute("WardrobeRecall",()=>wardrobe.RecallSnapshotAsync(ToSceneSnapshot(p.Wardrobe),WardrobeTransition.Cut),result=>result!=null && (result.Status==WardrobeChangeStatus.Applied || result.Status==WardrobeChangeStatus.AlreadyEquipped || result.Status==WardrobeChangeStatus.NoChange),result=>result?.Status.ToString(),captureCancellation:()=>wardrobe.CaptureCancellation());
    Guard(owner,cancellation,"WardrobeCommitted");
   }
   if(relocate){Guard(owner,cancellation,"RelocationCommit");actor.CommitPresentationFrame(p.Frame,p.Seating,plan.SeatStructural);Guard(owner,cancellation,"Relocated");}
   if(plan.Has(PresentationDomain.PlayerBody)){Guard(owner,cancellation,"BodyRelocationCommit");body.CommitPresentationFrame(entrance.Body.Frame,entrance.Body.Frame.Parent!=null && (entrance.Body.Frame.Parent==entrance.View.Root.Object || entrance.Body.Frame.Parent.IsChildOf(entrance.View.Root.Object)));}
   if(plan.Has(PresentationDomain.Pose)) {
    Guard(owner,cancellation,"PoseBlend");await run.Execute("Pose",()=>actor.RestoreBasePresentationAsync(p.DesiredPose,owner.Cancellation),result=>result==PoseCompletion.Settled,result=>result.ToString(),captureCancellation:()=>actor.CaptureRestorePoseCancellation(),captureOwnership:()=>actor.CapturePersistentCancellation(PerformanceCommand.Pose));Guard(owner,cancellation,"PoseSettled");
   }
   if(plan.Has(PresentationDomain.Seat)) {
    Guard(owner,cancellation,"SeatedPoseBlend");
    await run.Execute("PersistentSitPose",()=>actor.RestoreSeatedPresentationAsync(p.Seating,false,owner.Cancellation),result=>result==PoseCompletion.Settled,result=>result.ToString(),captureCancellation:()=>actor.CaptureSeatedPoseCancellation(),captureOwnership:()=>actor.CaptureSeatedPoseCancellation());
    Guard(owner,cancellation,"SeatedPoseSettled");
   }
   if(plan.Has(PresentationDomain.Arrival)){Guard(owner,cancellation,"ArrivalPoseBlend");await run.Execute("ArrivalHold",()=>actor.RestoreArrivalPresentationAsync(p.Arrival,owner.Cancellation),result=>result==PoseCompletion.Settled,result=>result.ToString(),captureCancellation:()=>actor.CaptureCancellation(PerformanceCommand.Movement),captureOwnership:()=>actor.CapturePersistentCancellation(PerformanceCommand.Movement));Guard(owner,cancellation,"ArrivalPoseSettled");}
   if(plan.Has(PresentationDomain.Expression)) {Guard(owner,cancellation,"ExpressionBlend");await run.Performer.ExpressionAsync(p.Expression,p.ExpressionIntensity,.5f);Guard(owner,cancellation,"ExpressionSettled");}
   if(relocate || plan.Has(PresentationDomain.Pose) || plan.Has(PresentationDomain.Seat))actor.EvaluatePresentationEndpoint(p,relocate);
   if(plan.Has(PresentationDomain.Material)) {
    Guard(owner,cancellation,"MaterialRestore");var material=actor.GetComponent<PerformerMagicShaderPresentation>();
    if(p.Material!=null)await run.Performer.MagicShaderAsync(p.Material);else if(material!=null){material.RestoreRegular();owner.Register("material",material.CaptureRestoration());}
   }
   if(plan.Has(PresentationDomain.Gaze)){Guard(owner,cancellation,"GazeBlend");await run.Execute("LookAt",()=>actor.RestoreGazePresentationAsync(p.Gaze,owner.Cancellation),result=>result==GazeCompletion.Acquired,result=>result.ToString(),captureCancellation:()=>actor.CaptureCancellation(PerformanceCommand.Gaze),captureOwnership:()=>actor.CapturePersistentCancellation(PerformanceCommand.Gaze));Guard(owner,cancellation,"GazeSettled");}
   if(plan.Has(PresentationDomain.Aura)){Guard(owner,cancellation,"AuraRestore");if(p.Aura==null)run.Performer.ClearAura();else run.Performer.Aura(p.Aura,p.AuraTarget);}
   if(restoreView) {
    Guard(owner,cancellation,"ViewBlend");await run.Execute("Player.RestoreView",()=>view.RestorePresentationAsync(entrance.View,owner.Cancellation),result=>result==PlayerViewCompletion.Completed,result=>result.ToString(),captureCancellation:view.CapturePresentationRestoreCancellation);
    owner.Register("view-orientation",view.CaptureOrientationCancellation());Guard(owner,cancellation,"ViewSettled");
   }
   if(restoreGrip && p.Grip?.Active==true) {
    Guard(owner,cancellation,"GripRestore");await run.Performer.GripAsync(p.Grip.Target,p.Grip.Position);
    Guard(owner,cancellation,"GripAcquired");if(p.Grip.FollowMotion)run.Performer.FollowGripMotion();
   }
   if(!p.Hidden && actor.IsHidden){Guard(owner,cancellation,"RevealLara");await run.Performer.DissolveInAsync(.25f);Guard(owner,cancellation,"LaraRevealed");}

   if(!entrance.Body.Hidden && body.IsStablyHidden){Guard(owner,cancellation,"RevealBody");await run.Player.DissolveBodyInAsync(.25f);Guard(owner,cancellation,"BodyRevealed");}
   Guard(owner,cancellation,"Verify");var verified=Verify(entrance);if(!verified.Succeeded)throw new InvalidOperationException("Card presentation return verification failed: "+verified.Differences);
   if(!view.PresentationTrackAcquired(entrance.View))throw new InvalidOperationException("Card camera return did not acquire its captured tracking endpoint.");
   Guard(owner,cancellation,"Verified");
  }
 }
}
