using System;
using DazPose.Motion;
using DazPose.Player;
using DazPose.Performer.HandGrip;
using UnityEngine;
namespace DazPose.Performer {
 internal sealed class PresentationFrame {
  public Transform Object {get;} public Transform Parent {get;} public bool HadParent {get;}
  public Vector3 LocalPosition {get;} public Quaternion LocalRotation {get;} public Vector3 LocalScale {get;}
  public Vector3 Position {get;} public Quaternion Rotation {get;}
  public PresentationFrame(Transform value) {
   if(value==null)throw new InvalidOperationException("Presentation transform is missing.");Object=value;Parent=value.parent;HadParent=Parent!=null;
   LocalPosition=value.localPosition;LocalRotation=value.localRotation;LocalScale=value.localScale;Position=value.position;Rotation=value.rotation;
  }
  public void Validate(string domain) {if(Object==null || (HadParent && Parent==null))throw new InvalidOperationException(domain+": captured transform/parent was destroyed.");}
  public bool Matches(bool rotation=true)=>Object!=null && Object.parent==Parent && Vector3.Distance(Object.position,Position)<=.001f && (!rotation||Quaternion.Angle(Object.rotation,Rotation)<=.1f) && Vector3.Distance(Object.localScale,LocalScale)<=.00001f;
 }
 internal sealed class PerformerGazeSnapshot {
  public bool Active {get;} public bool FollowsTransform {get;} public Transform Target {get;} public Vector3 Point {get;}
  public PerformerGazeSettings Settings {get;}
  public PerformerGazeSnapshot(bool active,bool follows,Transform target,Vector3 point,PerformerGazeSettings settings){Active=active;FollowsTransform=follows;Target=target;Point=point;Settings=settings;}
  public void Validate(){if(Active && FollowsTransform && Target==null)throw new InvalidOperationException("Gaze: captured target was destroyed.");}
 }
 internal sealed class PerformerSeatSnapshot {
  public PerformerSeat Seat {get;} public PerformerSeatingProfile Profile {get;} public PerformerSeatedStyle Style {get;}
  public PerformerSeatingState State {get;} public PerformerPose Overlay {get;}
  public Vector3 ApproachPosition {get;} public Quaternion ApproachRotation {get;} public Vector3 SeatPosition {get;} public Quaternion SeatRotation {get;}
  public bool Anchored {get;}
  public PerformerSeatSnapshot(PerformerSeat seat,PerformerSeatingProfile profile,PerformerSeatedStyle style,PerformerSeatingState state,PerformerPose overlay,Vector3 approach,Quaternion approachRotation,Vector3 position,Quaternion rotation,bool anchored) {
   Seat=seat;Profile=profile;Style=style;State=state;Overlay=overlay;ApproachPosition=approach;ApproachRotation=approachRotation;SeatPosition=position;SeatRotation=rotation;Anchored=anchored;
  }
 }
 internal sealed class PerformerGripSnapshot {
  public bool Active {get;} public IGripTarget Target {get;} public float Position {get;} public bool FollowMotion {get;} public MotionDriver Driver {get;}
  public PerformerGripSnapshot(bool active,IGripTarget target,float position,bool follow,MotionDriver driver){Active=active;Target=target;Position=position;FollowMotion=follow;Driver=driver;}
 }
 internal sealed class PerformerPresentationSnapshot {
  public SuccubusPerformer Actor {get;} public PresentationFrame Frame {get;} public PerformerPose DesiredPose {get;} public PerformerPose SettledPose {get;}
  public PerformerSeatSnapshot Seating {get;} public bool Hidden {get;} public WardrobeSelection Wardrobe {get;}
  public WardrobeCatalog Catalog {get;} public WardrobeConfiguration Configuration {get;} public WardrobeFitState Fit {get;}
  public PerformerMagicShaderProfile Material {get;} public PerformerExpression Expression {get;} public float ExpressionIntensity {get;}
  public PerformerGazeSnapshot Gaze {get;} public PerformerAura Aura {get;} public Transform AuraTarget {get;} public PerformerGripSnapshot Grip {get;}
  public PerformerPresentationSnapshot(SuccubusPerformer actor,PerformerPose desired,PerformerPose settled,PerformerSeatSnapshot seat,bool hidden,WardrobeSelection wardrobe,PerformerMagicShaderProfile material,PerformerExpression expression,float intensity,PerformerGazeSnapshot gaze,PerformerAura aura,Transform auraTarget,PerformerGripSnapshot grip) {
   Actor=actor;var owner=actor.GetComponent<PerformerWardrobe>();Catalog=owner?.Catalog;Configuration=owner?.Configuration;Fit=owner?.CurrentFit;Frame=new PresentationFrame(actor.transform);DesiredPose=desired;SettledPose=settled;Seating=seat;Hidden=hidden;Wardrobe=wardrobe;Material=material;Expression=expression;ExpressionIntensity=intensity;Gaze=gaze;Aura=aura;AuraTarget=auraTarget;Grip=grip;
  }
 }
 internal sealed class PlayerBodyPresentationSnapshot {
  public PlayerBodyPresence Body {get;} public PresentationFrame Frame {get;} public bool Hidden {get;}
  public PlayerBodyPresentationSnapshot(PlayerBodyPresence body){Body=body;Frame=new PresentationFrame(body.transform);Hidden=body.IsStablyHidden;}
 }
 internal sealed class PlayerViewPresentationSnapshot {
  public PlayerView View {get;} public PresentationFrame Root {get;} public PresentationFrame Rig {get;} public PresentationFrame Camera {get;}
  public bool Tracking {get;} public Transform Target {get;} public ViewTrackingSettings TrackingSettings {get;}
  public bool Orthographic {get;} public float FieldOfView {get;} public float OrthographicSize {get;} public float Near {get;} public float Far {get;}
  public PlayerViewPresentationSnapshot(PlayerView view,bool tracking,Transform target,ViewTrackingSettings settings){View=view;Root=new PresentationFrame(view.transform);Rig=new PresentationFrame(view.ConfiguredViewRig);Camera=new PresentationFrame(view.MainCamera.transform);Tracking=tracking;Target=target;TrackingSettings=settings;Orthographic=view.MainCamera.orthographic;FieldOfView=view.MainCamera.fieldOfView;OrthographicSize=view.MainCamera.orthographicSize;Near=view.MainCamera.nearClipPlane;Far=view.MainCamera.farClipPlane;}
 }
 internal sealed class PerformancePresentationSnapshot {
  public PerformerPresentationSnapshot Performer {get;} public PlayerBodyPresentationSnapshot Body {get;} public PlayerViewPresentationSnapshot View {get;} public DazPose.Story.PlayerPosture Posture {get;}
  public PerformancePresentationSnapshot(PerformerPresentationSnapshot performer,PlayerBodyPresentationSnapshot body,PlayerViewPresentationSnapshot view,DazPose.Story.PlayerPosture posture){Performer=performer;Body=body;View=view;Posture=posture;}
 }
}
