using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Story;
using UnityEngine;
namespace DazPose.Performer {
 [Flags] internal enum PresentationDomain { None=0, Placement=1, Seat=2, Pose=4, Visibility=8, Wardrobe=16, Material=32, Expression=64, Gaze=128, Aura=256, Grip=512, PlayerBody=1024, View=2048, Motion=4096, Arrival=8192 }
 internal sealed class PresentationReturnPlan {
  public PerformancePresentationSnapshot Entrance {get;} public PresentationDomain Changed {get;}
  public bool SeatStructural {get;} public bool RelocateActor {get;}
  public PresentationReturnPlan(PerformancePresentationSnapshot entrance,PresentationDomain changed,bool seatStructural,bool relocateActor){Entrance=entrance;Changed=changed;SeatStructural=seatStructural;RelocateActor=relocateActor;}
  public bool Has(PresentationDomain domain)=>(Changed&domain)!=0;
 }
 internal sealed class PresentationVerification {
  public PresentationDomain Differences {get;} public bool Succeeded=>Differences==PresentationDomain.None;
  public PresentationVerification(PresentationDomain differences){Differences=differences;}
 }
 // Main-thread native presentation adapter. No story replay, tuning reset or hierarchy serialization.
 internal sealed partial class PerformancePresentationService {
  readonly PerformanceRun run;readonly Func<PlayerPosture> readPosture;
  public PerformancePresentationService(PerformanceRun run,Func<PlayerPosture> readPosture){this.run=run;this.readPosture=readPosture;}
  public PerformancePresentationSnapshot Capture() {
   run.ThrowIfActive();var player=run.BoundPlayer;
   if(player==null || player.View==null || player.BodyPresence==null)throw new InvalidOperationException("Protected card capture requires the bound player view and original male PlayerBodyPresence.");
   return new PerformancePresentationSnapshot(run.Actor.CapturePresentation(),player.BodyPresence.CapturePresentation(),player.View.CapturePresentation(),readPosture());
  }
  public IDisposable Pin(PerformancePresentationSnapshot snapshot) {
   var wardrobe=snapshot.Performer.Actor.GetComponent<PerformerWardrobe>();
   return wardrobe!=null && snapshot.Performer.Wardrobe!=null ? wardrobe.PinSelectionResources(snapshot.Performer.Wardrobe) : null;
  }
  public PresentationReturnPlan PrepareReturn(PerformancePresentationSnapshot entrance) {
   run.ThrowIfActive();Validate(entrance);var current=Capture();var expected=entrance.Performer;var actual=current.Performer;
   var changed=Differences(entrance,current);bool sameSeat=SameSeatStructure(expected.Seating,actual.Seating);
   bool derivedSeatPlacement=sameSeat && expected.Seating?.Seat!=null && (expected.Seating.Anchored || actual.Seating.Anchored);
   return new PresentationReturnPlan(entrance,changed,!sameSeat,(changed&PresentationDomain.Placement)!=0 && !derivedSeatPlacement);
  }
  public PresentationVerification Verify(PerformancePresentationSnapshot entrance) {Validate(entrance);return new PresentationVerification(Differences(entrance,Capture()));}
  static void RequireAlive(UnityEngine.Object value,string domain){if(!ReferenceEquals(value,null)&&value==null)throw new InvalidOperationException(domain+": captured asset/target was destroyed.");}
  static void Validate(PerformancePresentationSnapshot snapshot) {
   if(snapshot.Performer.Actor==null || snapshot.Body.Body==null || snapshot.View.View==null || snapshot.View.View.MainCamera==null)throw new InvalidOperationException("Captured presentation owner/camera was destroyed.");
   var p=snapshot.Performer;p.Frame.Validate("Lara");snapshot.Body.Frame.Validate("Player body");snapshot.View.Root.Validate("Player root");snapshot.View.Rig.Validate("View rig");snapshot.View.Camera.Validate("Camera");
   RequireAlive(p.DesiredPose,"Pose");RequireAlive(p.SettledPose,"Pose");RequireAlive(p.Material,"Material");RequireAlive(p.Expression,"Expression");RequireAlive(p.Aura,"Aura");
   if(p.DesiredPose!=null && p.DesiredPose.Clip==null)throw new InvalidOperationException("Pose: captured clip is missing.");
   if(p.Material!=null)p.Material.ValidateConfiguration();
   if(p.Aura!=null && !p.Aura.IsReady(out var auraReason))throw new InvalidOperationException("Aura: "+auraReason);
   if(p.Frame.Object.localScale!=p.Frame.LocalScale || snapshot.Body.Frame.Object.localScale!=snapshot.Body.Frame.LocalScale || snapshot.View.Root.Object.localScale!=snapshot.View.Root.LocalScale)throw new InvalidOperationException("Presentation scale/configuration changed; return does not overwrite tuning.");
   p.Gaze?.Validate();if(p.Aura!=null && p.AuraTarget==null)throw new InvalidOperationException("Aura: captured target was destroyed.");
   if(p.Seating!=null && p.Seating.State!=PerformerSeatingState.Standing) {
    if(p.Seating.Seat==null || p.Seating.Profile==null)throw new InvalidOperationException("Seating: captured seat/profile is missing.");
    if(!p.Seating.Seat.TryCaptureFrames(out _,out _,out _,out _,out var issue))throw new InvalidOperationException("Seating: "+issue);
    if(!p.Seating.Profile.IsReady(out var profileReason))throw new InvalidOperationException("Seating: "+profileReason);
    RequireAlive(p.Seating.Overlay,"Seated overlay");
    if(p.Seating.Overlay!=null && p.Seating.Overlay.Clip==null)throw new InvalidOperationException("Seated pose clip is missing.");
   }
   if(snapshot.View.Tracking && snapshot.View.Target==null)throw new InvalidOperationException("Camera tracking: captured target was destroyed.");
   if(p.Grip!=null && p.Grip.Active) {
    if(p.Grip.Target==null || (p.Grip.Target is UnityEngine.Object target && target==null))throw new InvalidOperationException("Grip: captured target was destroyed.");
    if(p.Grip.Target is HandGrip.IGripTargetReadiness ready && !ready.IsReadyForContact(true,out var gripReason))throw new InvalidOperationException("Grip: "+gripReason);
    if(p.Grip.FollowMotion && (p.Grip.Driver==null || !p.Grip.Driver.IsRunning))throw new InvalidOperationException("Grip follow: captured parent motion driver is unavailable; its live clock cannot be replaced or rewound.");
   }
   RequireAlive(p.Arrival?.Motion,"Arrival pose");
   if(p.MotionSource!=p.Actor.MotionSource || p.MotionRunning && (p.MotionSource==null || !p.MotionSource.IsRunning || p.MotionSource.CommandGeneration!=p.MotionGeneration))throw new InvalidOperationException("Motion: captured parent driver was replaced/stopped; return cannot rewind it.");
   if(p.Wardrobe!=null) {
    var wardrobe=p.Actor.GetComponent<PerformerWardrobe>();
    if(wardrobe==null || !wardrobe.IsRuntimeInitialized || wardrobe.Catalog!=p.Catalog || wardrobe.Configuration!=p.Configuration || wardrobe.CaptureSelection().GenerationId!=p.Wardrobe.GenerationId)throw new InvalidOperationException("Wardrobe: captured release/configuration changed.");
    if(!wardrobe.TryPrepareSceneSnapshot(ToSceneSnapshot(p.Wardrobe),out var error))throw new InvalidOperationException("Wardrobe: "+error);
    RequireAlive(p.Fit,"Wardrobe fit");
   }
  }
  internal static SceneWardrobeSnapshot ToSceneSnapshot(WardrobeSelection selection)=>new SceneWardrobeSnapshot{presetId=selection.PresetId,layerCeiling=selection.LayerCeiling,hairAction=string.IsNullOrEmpty(selection.HairKey)?WardrobeHairAction.Clear:WardrobeHairAction.Set,resolvedHairId=selection.HairKey};
  static bool GazeEqual(PerformerGazeSnapshot a,PerformerGazeSnapshot b)=>a==null?b==null:b!=null && a.Active==b.Active && (!a.Active || a.FollowsTransform==b.FollowsTransform && a.Target==b.Target && a.Point==b.Point);
  static bool GripEqual(PerformerGripSnapshot a,PerformerGripSnapshot b)=>a==null?b==null:b!=null && a.Active==b.Active && (!a.Active || ReferenceEquals(a.Target,b.Target) && a.FollowMotion==b.FollowMotion && (!a.FollowMotion?Mathf.Abs(a.Position-b.Position)<=.0001f:a.Driver==b.Driver));
  static bool ArrivalEqual(PerformerArrivalSnapshot a,PerformerArrivalSnapshot b)=>a==null?b==null:b!=null&&a.Arrival==b.Arrival&&a.Approach==b.Approach&&a.Motion==b.Motion&&a.Phase==b.Phase;
  static bool SameSeatStructure(PerformerSeatSnapshot a,PerformerSeatSnapshot b)=>a==null?b==null:b!=null && a.State==b.State && (a.State==PerformerSeatingState.Standing || a.Seat==b.Seat && a.Profile==b.Profile && a.Style==b.Style && a.SeatPosition==b.SeatPosition && a.SeatRotation==b.SeatRotation && a.ApproachPosition==b.ApproachPosition && a.ApproachRotation==b.ApproachRotation);
  static bool SeatEqual(PerformerSeatSnapshot a,PerformerSeatSnapshot b)=>a==null?b==null:b!=null && a.State==b.State && (a.State==PerformerSeatingState.Standing || a.Seat==b.Seat && a.Profile==b.Profile && a.Style==b.Style && a.Overlay==b.Overlay && a.Anchored==b.Anchored && a.SeatPosition==b.SeatPosition && a.SeatRotation==b.SeatRotation && a.ApproachPosition==b.ApproachPosition && a.ApproachRotation==b.ApproachRotation);
  static bool FrameEqual(PresentationFrame a,PresentationFrame b,bool rotation=true)=>a.Object==b.Object && a.Parent==b.Parent && Vector3.Distance(a.Position,b.Position)<=.001f && (!rotation || Quaternion.Angle(a.Rotation,b.Rotation)<=.1f) && a.LocalScale==b.LocalScale;
  static PresentationDomain Differences(PerformancePresentationSnapshot expected,PerformancePresentationSnapshot actual) {
   var a=expected.Performer;var b=actual.Performer;var changed=PresentationDomain.None;
   if(!FrameEqual(a.Frame,b.Frame))changed|=PresentationDomain.Placement;
   if(!SeatEqual(a.Seating,b.Seating))changed|=PresentationDomain.Seat;
   if(a.DesiredPose!=b.DesiredPose || a.SettledPose!=b.SettledPose)changed|=PresentationDomain.Pose;
   if(a.Hidden!=b.Hidden)changed|=PresentationDomain.Visibility;
   if(!Equals(a.Wardrobe,b.Wardrobe) || a.Fit!=b.Fit)changed|=PresentationDomain.Wardrobe;
   if(a.Material!=b.Material)changed|=PresentationDomain.Material;
   if(a.Expression!=b.Expression || a.ExpressionIntensity!=b.ExpressionIntensity)changed|=PresentationDomain.Expression;
   if(!GazeEqual(a.Gaze,b.Gaze))changed|=PresentationDomain.Gaze;
   if(a.Aura!=b.Aura || a.AuraTarget!=b.AuraTarget)changed|=PresentationDomain.Aura;
   if(a.MotionSource!=b.MotionSource || a.MotionVariant!=b.MotionVariant || a.MotionRunning!=b.MotionRunning)changed|=PresentationDomain.Motion;
   if(!ArrivalEqual(a.Arrival,b.Arrival))changed|=PresentationDomain.Arrival;
   if(!GripEqual(a.Grip,b.Grip))changed|=PresentationDomain.Grip;
   if(expected.Body.Body!=actual.Body.Body || expected.Body.Hidden!=actual.Body.Hidden || !FrameEqual(expected.Body.Frame,actual.Body.Frame))changed|=PresentationDomain.PlayerBody;
   var v=expected.View;var w=actual.View;
   if(!FrameEqual(v.Root,w.Root) || !FrameEqual(v.Rig,w.Rig,!v.Tracking) || v.Tracking!=w.Tracking || v.Target!=w.Target || v.Tracking && !v.TrackingSettings.Equals(w.TrackingSettings) || v.Camera.LocalPosition!=w.Camera.LocalPosition || v.Camera.LocalRotation!=w.Camera.LocalRotation || v.Camera.Parent!=w.Camera.Parent || v.Orthographic!=w.Orthographic || v.FieldOfView!=w.FieldOfView || v.OrthographicSize!=w.OrthographicSize || v.Near!=w.Near || v.Far!=w.Far)changed|=PresentationDomain.View;
   return changed;
  }
 }
}
