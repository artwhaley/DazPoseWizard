using System;
using DazPose.Performer;
namespace DazPose.Player {
 public sealed partial class PlayerView {
  internal PlayerViewPresentationSnapshot CapturePresentation() {
   EnsureOperational();if(IsMoving || IsLooking || IsAcquiringTrack)throw new InvalidOperationException("View capture requires stable movement/orientation.");
   return new PlayerViewPresentationSnapshot(this,IsTracking,CurrentTrackTarget,IsTracking?activeOrientation.TrackingSettings:default);
  }
 }
}
