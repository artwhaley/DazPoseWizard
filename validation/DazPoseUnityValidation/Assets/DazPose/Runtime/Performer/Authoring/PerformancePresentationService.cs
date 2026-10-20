using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Story;
using UnityEngine;
namespace DazPose.Performer {
 [Flags] internal enum PresentationDomain { None=0, Placement=1, Seat=2, Pose=4, Visibility=8, Wardrobe=16, Material=32, Expression=64, Gaze=128, Aura=256, Grip=512, PlayerBody=1024, View=2048 }
 internal sealed class PresentationReturnPlan {
  public PerformancePresentationSnapshot Entrance {get;} public PresentationDomain Changed {get;}
  public PresentationReturnPlan(PerformancePresentationSnapshot entrance,PresentationDomain changed){Entrance=entrance;Changed=changed;}
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
   run.ThrowIfActive();Validate(entrance);return new PresentationReturnPlan(entrance,Differences(entrance,Capture()));
  }
  public PresentationVerification Verify(PerformancePresentationSnapshot entrance) {Validate(entrance);return new PresentationVerification(Differences(entrance,Capture()));}
  static void RequireAlive(UnityEngine.Object value,string domain){if(!ReferenceEquals(value,null)&&value==null)throw new InvalidOperationException(domain+": captured asset/target was destroyed.");}
  static void Validate(PerformancePresentationSnapshot snapshot) {
   var p=snapshot.Performer;p.Frame.Validate("Lara");snapshot.Body.Frame.Validate("Player body");snapshot.View.Root.Validate("Player root");snapshot.View.Rig.Validate("View rig");snapshot.View.Camera.Validate("Camera");
   RequireAlive(p.DesiredPose,"Pose");RequireAlive(p.SettledPose,"Pose");RequireAlive(p.Material,"Material");RequireAlive(p.Expression,"Expression");RequireAlive(p.Aura,"Aura");
   p.Gaze?.Validate();if(p.Aura!=null && p.AuraTarget==null)throw new InvalidOperationException("Aura: captured target was destroyed.");
   if(p.Seating!=null && p.Seating.State!=PerformerSeatingState.Standing) {
    if(p.Seating.Seat==null || p.Seating.Profile==null)throw new InvalidOperationException("Seating: captured seat/profile is missing.");
    if(!p.Seating.Seat.TryCaptureFrames(out _,out _,out _,out _,out var issue))throw new InvalidOperationException("Seating: "+issue);
    RequireAlive(p.Seating.Overlay,"Seated overlay");
   }
   if(snapshot.View.Tracking && snapshot.View.Target==null)throw new InvalidOperationException("Camera tracking: captured target was destroyed.");
   if(p.Grip!=null && p.Grip.Active) {
    if(p.Grip.Target==null || (p.Grip.Target is UnityEngine.Object target && target==null))throw new InvalidOperationException("Grip: captured target was destroyed.");
    if(p.Grip.FollowMotion && (p.Grip.Driver==null || !p.Grip.Driver.IsRunning))throw new InvalidOperationException("Grip follow: captured parent motion driver is unavailable; its live clock cannot be replaced or rewound.");
   }
   if(p.Wardrobe!=null) {
    var wardrobe=p.Actor.GetComponent<PerformerWardrobe>();
    if(wardrobe==null || !wardrobe.IsRuntimeInitialized || wardrobe.Catalog!=p.Catalog || wardrobe.Configuration!=p.Configuration || wardrobe.Catalog.ReleasedGenerationId!=p.Wardrobe.GenerationId)throw new InvalidOperationException("Wardrobe: captured release/configuration changed.");
    if(!wardrobe.TryPrepareSceneSnapshot(ToSceneSnapshot(p.Wardrobe),out var error))throw new InvalidOperationException("Wardrobe: "+error);
    RequireAlive(p.Fit,"Wardrobe fit");
   }
  }
  internal static SceneWardrobeSnapshot ToSceneSnapshot(WardrobeSelection selection)=>new SceneWardrobeSnapshot{presetId=selection.PresetId,layerCeiling=selection.LayerCeiling,hairAction=string.IsNullOrEmpty(selection.HairKey)?WardrobeHairAction.Clear:WardrobeHairAction.Set,resolvedHairId=selection.HairKey};
  static bool GazeEqual(PerformerGazeSnapshot a,PerformerGazeSnapshot b)=>a==null?b==null:b!=null && a.Active==b.Active && (!a.Active || a.FollowsTransform==b.FollowsTransform && a.Target==b.Target && a.Point==b.Point && a.Settings.Equals(b.Settings));
  static bool GripEqual(PerformerGripSnapshot a,PerformerGripSnapshot b)=>a==null?b==null:b!=null && a.Active==b.Active && (!a.Active || ReferenceEquals(a.Target,b.Target) && a.FollowMotion==b.FollowMotion && (!a.FollowMotion?Mathf.Abs(a.Position-b.Position)<=.0001f:a.Driver==b.Driver));
  static bool SeatEqual(PerformerSeatSnapshot a,PerformerSeatSnapshot b)=>a==null?b==null:b!=null && a.State==b.State && a.Seat==b.Seat && a.Profile==b.Profile && a.Style==b.Style && a.Overlay==b.Overlay && a.Anchored==b.Anchored && a.SeatPosition==b.SeatPosition && a.SeatRotation==b.SeatRotation && a.ApproachPosition==b.ApproachPosition && a.ApproachRotation==b.ApproachRotation;
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
   if(!GripEqual(a.Grip,b.Grip))changed|=PresentationDomain.Grip;
   if(expected.Body.Body!=actual.Body.Body || expected.Body.Hidden!=actual.Body.Hidden || !FrameEqual(expected.Body.Frame,actual.Body.Frame))changed|=PresentationDomain.PlayerBody;
   var v=expected.View;var w=actual.View;
   if(!FrameEqual(v.Root,w.Root) || !FrameEqual(v.Rig,w.Rig,!v.Tracking) || v.Tracking!=w.Tracking || v.Target!=w.Target || v.Tracking && !v.TrackingSettings.Equals(w.TrackingSettings) || v.Camera.LocalPosition!=w.Camera.LocalPosition || v.Camera.LocalRotation!=w.Camera.LocalRotation || v.Camera.Parent!=w.Camera.Parent || v.Orthographic!=w.Orthographic || v.FieldOfView!=w.FieldOfView || v.OrthographicSize!=w.OrthographicSize || v.Near!=w.Near || v.Far!=w.Far)changed|=PresentationDomain.View;
   return changed;
  }
 }
}
