using System;
using System.Threading;
using UnityEngine;
namespace DazPose.Performer {
 public sealed partial class SuccubusPerformer {
  internal PerformerArrivalSnapshot CaptureArrivalPresentation()=>_locomotion?.CapturePresentation();
  internal async Awaitable<PoseCompletion> RestoreArrivalPresentationAsync(PerformerArrivalSnapshot snapshot,CancellationToken cancellation){_locomotion?.RestorePresentation(snapshot);while(_locomotion!=null&&_locomotion.PresentationTransitioning){cancellation.ThrowIfCancellationRequested();await Awaitable.NextFrameAsync(cancellation);}return PoseCompletion.Settled;}
  internal int PresentationMotionVariant=>_motionLayer?.ActiveVariantIndex??-1;
  internal bool IsPresentationMotionTransitioning=>_motionLayer?.PresentationTransitioning??false;
  internal bool IsPresentationSeatingTransitioning=>IsSeatingTransitionActive();
  internal async Awaitable<PoseCompletion> RestoreBasePresentationAsync(PerformerPose pose,CancellationToken cancellation) {
   if(pose!=null)return await PoseAsync(pose,PoseTransition.Smooth(.5f));
   ++_poseIntentGeneration;_bodyPose.SetNeutralPose(PoseTransition.Smooth(.5f));
   while(_bodyPose.IsTransitioning){cancellation.ThrowIfCancellationRequested();await Awaitable.NextFrameAsync(cancellation);}
   cancellation.ThrowIfCancellationRequested();PublishCurrentPoseState();return PoseCompletion.Settled;
  }
  internal Action CaptureRestorePoseCancellation(){long generation=_poseIntentGeneration;return ()=>{if(generation==_poseIntentGeneration)_bodyPose?.CancelTransition();};}
  internal void CommitPresentationFrame(PresentationFrame frame,PerformerSeatSnapshot seating,bool seatStructural) {
   frame.Validate("Lara return");if(!IsHidden)throw new InvalidOperationException("Exact relocation requires concealed Lara.");
   transform.SetParent(frame.Parent,true);
   if(seatStructural)_seating?.RestorePresentation(seating,true,.5f);
   transform.SetPositionAndRotation(frame.Position,frame.Rotation);
  }
  internal async Awaitable<PoseCompletion> RestoreSeatedPresentationAsync(PerformerSeatSnapshot seating,bool structural,CancellationToken cancellation) {
   ++_storySeatGeneration;_seating?.RestorePresentation(seating,structural,.5f);
   while(_seating!=null && !_seating.PersistentPoseSettled){cancellation.ThrowIfCancellationRequested();await Awaitable.NextFrameAsync(cancellation);}
   cancellation.ThrowIfCancellationRequested();return PoseCompletion.Settled;
  }
  internal async Awaitable<GazeCompletion> RestoreGazePresentationAsync(PerformerGazeSnapshot gaze,CancellationToken cancellation) {
   if(_gaze==null){if(gaze!=null&&gaze.Active)throw new InvalidOperationException("Captured gaze runtime is unavailable.");return GazeCompletion.Acquired;}
   gaze?.Validate();
   if(gaze==null||!gaze.Active)_gaze.ClearGaze();else if(gaze.FollowsTransform)_gaze.LookAt(gaze.Target);else _gaze.LookAt(gaze.Point);
   _gaze.BeginPresentationBlend(.5f);
   float elapsed=0;while(elapsed<.5f){cancellation.ThrowIfCancellationRequested();await Awaitable.NextFrameAsync(cancellation);elapsed+=Mathf.Max(0,Time.deltaTime);}
   cancellation.ThrowIfCancellationRequested();
   return gaze==null||!gaze.Active||!GazeEnabled||_gaze.IsGazeAcquired?GazeCompletion.Acquired:GazeCompletion.TargetLost;
  }
  internal void EvaluatePresentationEndpoint(PerformerPresentationSnapshot snapshot,bool relocated) {
   _graph.Evaluate(0);_seating?.AnchorPersistentPose();
   if(relocated && snapshot.Seating?.Anchored!=true)transform.SetPositionAndRotation(snapshot.Frame.Position,snapshot.Frame.Rotation);
  }
  internal PerformerPresentationSnapshot CapturePresentation() {
   if(!IsRuntimeReady || IsTransitioning || IsLocomoting || IsTeleporting || IsDissolving || IsExpressionTransitioning || IsPerforming || IsSeatingTransitionActive() || (CurrentWardrobe?.IsChanging??false))throw new InvalidOperationException("Performer capture requires a stable presentation after the entrance barrier.");
   var wardrobe=GetComponent<PerformerWardrobe>();
   return new PerformerPresentationSnapshot(this,DesiredPose,SettledPose,_seating?.CapturePresentation(),IsHidden,wardrobe?.CaptureSelection(),GetComponent<PerformerMagicShaderPresentation>()?.CurrentProfile,_lastDesiredExpression,_lastDesiredExpressionIntensity,_gaze?.CapturePresentation(),_desiredAura,_desiredAuraTarget,_handGripController?.CapturePresentation());
  }
 }
}
