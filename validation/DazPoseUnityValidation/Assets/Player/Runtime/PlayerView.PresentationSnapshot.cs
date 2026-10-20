using System;
using System.Threading;
using UnityEngine;
using DazPose.Performer;
namespace DazPose.Player {
 public sealed partial class PlayerView {
  internal void FreezePresentationOrientation(){CaptureOrientationCancellation()();}
  internal async Awaitable<PlayerViewCompletion> RestorePresentationAsync(PlayerViewPresentationSnapshot snapshot,CancellationToken cancellation) {
   EnsureOperational();snapshot.Root.Validate("Player root");snapshot.Rig.Validate("View rig");snapshot.Camera.Validate("Camera");
   CaptureMovementCancellation()();CaptureOrientationCancellation()();uint move=++PositionCommandRevision,orientation=++OrientationCommandRevision;
   transform.SetParent(snapshot.Root.Parent,true);viewRig.SetParent(snapshot.Rig.Parent,true);
   var camera=mainCamera.transform;camera.SetParent(snapshot.Camera.Parent,true);
   Vector3 start=transform.position,startCamera=camera.localPosition,startRigLocal=viewRig.localPosition;Quaternion startRoot=transform.rotation,startRig=viewRig.rotation,startCameraRotation=camera.localRotation;
   float elapsed=0;
   while(true) {
    cancellation.ThrowIfCancellationRequested();if(move!=PositionCommandRevision||orientation!=OrientationCommandRevision)throw new InvalidOperationException("Presentation view restore was superseded.");
    snapshot.Root.Validate("Player root");snapshot.Rig.Validate("View rig");snapshot.Camera.Validate("Camera");
    float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.5f));
    transform.SetPositionAndRotation(Vector3.Lerp(start,snapshot.Root.Position,t),Quaternion.Slerp(startRoot,snapshot.Root.Rotation,t));
    camera.localPosition=Vector3.Lerp(startCamera,snapshot.Camera.LocalPosition,t);camera.localRotation=Quaternion.Slerp(startCameraRotation,snapshot.Camera.LocalRotation,t);
    viewRig.localPosition=Vector3.Lerp(startRigLocal,snapshot.Rig.LocalPosition,t);
    if(snapshot.Tracking && snapshot.Target==null)throw new InvalidOperationException("Captured camera tracking target was destroyed.");
    Quaternion destination=snapshot.Tracking?DesiredRotation(snapshot.Target.position):snapshot.Rig.Rotation;
    viewRig.rotation=Quaternion.Slerp(startRig,destination,t);
    if(elapsed>=.5f)break;
    await Awaitable.NextFrameAsync(cancellation);elapsed+=Mathf.Max(0,Time.deltaTime);
   }
   cancellation.ThrowIfCancellationRequested();mainCamera.orthographic=snapshot.Orthographic;mainCamera.fieldOfView=snapshot.FieldOfView;mainCamera.orthographicSize=snapshot.OrthographicSize;mainCamera.nearClipPlane=snapshot.Near;mainCamera.farClipPlane=snapshot.Far;
   if(snapshot.Tracking)activeOrientation=new OrientationOperation{Mode=OrientationMode.Tracking,Target=snapshot.Target,UsesTransform=true,TrackingSettings=snapshot.TrackingSettings,Transition=snapshot.TrackingSettings.Acquire,Elapsed=snapshot.TrackingSettings.Acquire.Duration};
   return PlayerViewCompletion.Completed;
  }
  internal Action CapturePresentationRestoreCancellation() {uint position=PositionCommandRevision,orientation=OrientationCommandRevision;return ()=>{if(position==PositionCommandRevision)++PositionCommandRevision;if(orientation==OrientationCommandRevision){++OrientationCommandRevision;CaptureOrientationCancellation()();}};}
  internal bool PresentationTrackAcquired(PlayerViewPresentationSnapshot snapshot)=>!snapshot.Tracking || IsTracking && CurrentTrackTarget==snapshot.Target && Quaternion.Angle(viewRig.rotation,DesiredRotation(snapshot.Target.position))<=.1f;
  internal PlayerViewPresentationSnapshot CapturePresentation() {
   EnsureOperational();if(IsMoving || IsLooking || IsAcquiringTrack)throw new InvalidOperationException("View capture requires stable movement/orientation.");
   return new PlayerViewPresentationSnapshot(this,IsTracking,CurrentTrackTarget,IsTracking?activeOrientation.TrackingSettings:default);
  }
 }
}
