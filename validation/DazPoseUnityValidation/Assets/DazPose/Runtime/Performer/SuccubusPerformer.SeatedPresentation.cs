using System;
using UnityEngine;
namespace DazPose.Performer {
 public sealed partial class SuccubusPerformer {
  public bool HasPersistentSeatedPose=>_seating!=null && _seating.HasPersistentPose;
  int _storySeatGeneration,_storyRelocateGeneration;
  void LateUpdate() {_seating?.AnchorPersistentPose();}
  public async Awaitable<PoseCompletion> PersistentSitPoseAsync(PerformerPose pose) {
   RequireVisibleForWorldCommands("PersistentSitPose");
   if(!IsRuntimeReady || _seating==null)throw new InvalidOperationException("The prepared seating runtime is required.");
   int generation=++_storySeatGeneration;_seating.SetPersistentPose(pose,defaultTransition);
   while(!_seating.PersistentPoseSettled) {
    await Awaitable.NextFrameAsync();
    if(!IsRuntimeReady || generation!=_storySeatGeneration)return PoseCompletion.Cancelled;
   }
   return PoseCompletion.Settled;
  }
  internal Action CaptureSeatedPoseCancellation() {
   int generation=_storySeatGeneration;return ()=> {if(generation!=_storySeatGeneration)return;++_storySeatGeneration;_seating?.ClearPersistentPose();};
  }
  public async Awaitable<DissolveCompletion> RelocatePresentationAsync(Transform location,PerformerSeat seated,PerformerPose pose,float seconds) {
   if(location==null)throw new ArgumentNullException(nameof(location));
   PerformerDissolve.ValidateDuration(seconds);
   if(seated!=null && pose==null)throw new ArgumentException("A seated destination requires its authored pose.");
   int generation=++_storyRelocateGeneration;
   var departure=await DissolveOutAsync(seconds*.5f);
   if(departure!=VisibilityCompletion.Hidden || generation!=_storyRelocateGeneration)return DissolveCompletion.Cancelled;
   _seating?.Cancel();
   if(seated!=null) {
    _seating.ArriveHiddenAt(seated,pose);_graph.Evaluate(0);_seating.AnchorPersistentPose();
   } else {
    transform.SetPositionAndRotation(location.position,PerformerSeat.YawRotation(location.forward));
    Pose(initialPose,PoseTransition.Snap);_graph.Evaluate(0);
   }
   // Establish graph, seat ownership and anchoring while every renderer is still hidden.
   await Awaitable.NextFrameAsync();
   if(!IsRuntimeReady || generation!=_storyRelocateGeneration)return DissolveCompletion.Cancelled;
   _seating?.AnchorPersistentPose();
   var arrival=await DissolveInAsync(seconds*.5f);
   return arrival==VisibilityCompletion.Visible && generation==_storyRelocateGeneration ? DissolveCompletion.Arrived : DissolveCompletion.Cancelled;
  }
  internal Action CapturePresentationRelocationCancellation() {
   int generation=_storyRelocateGeneration;return ()=> {if(generation!=_storyRelocateGeneration)return;++_storyRelocateGeneration;CancelAppearance();};
  }
 }
}
