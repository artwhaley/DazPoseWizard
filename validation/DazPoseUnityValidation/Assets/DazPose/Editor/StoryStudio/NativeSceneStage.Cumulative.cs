using System;
using System.Collections.Generic;
using DazPose.Player;
using DazPose.Performer;
using DazPose.Story;
using UnityEngine;
namespace DazPose.Editor.Studio {
 public sealed partial class NativeSceneStage {
  readonly List<Action> spatialUpdates=new List<Action>();
  public int FullProjectionCount {get;private set;}
  public int SpatialProjectionCount {get;private set;}
  public int PoseSampleCount=>lease.SampleCount;
  public double LastProjectionMilliseconds {get;private set;}
  public void Reanchor(){
   if(spatialUpdates.Count==0)return;
   var timer=System.Diagnostics.Stopwatch.StartNew();
   try{lease.ApplySpatial(()=>{foreach(var update in spatialUpdates)update();});SpatialProjectionCount++;}
   finally{LastProjectionMilliseconds=timer.Elapsed.TotalMilliseconds;}
  }
  public string Composition {get;private set;}
  public void Apply(RehearsalVisualState state,StoryLinkedContent content) {
   if(state==null||content==null)throw new ArgumentNullException();
   var timer=System.Diagnostics.Stopwatch.StartNew();
   try{lease.Apply(()=>StageCumulative(state,content));lease.SpatialUndo=Reanchor;}
   finally{LastProjectionMilliseconds=timer.Elapsed.TotalMilliseconds;}
  }
  // Read-only validation is acknowledged for every executed command even during fast navigation.
  // The actual native projection is applied once at the selected endpoint.
  public void ValidateIntent(RehearsalVisualState state,StoryLinkedContent content) {
   if(state.Effects.Count==0)return;
   var effect=state.Effects[state.Effects.Count-1];var c=Context;
   try {
    var owner=content.ContentFor(effect.Source);
    switch(effect.Kind){
     case VisualIntentKind.Hidden:case VisualIntentKind.DissolveIn:case VisualIntentKind.Move:case VisualIntentKind.BodyHidden:case VisualIntentKind.CameraStart:
      RequireAnchor(c.Host.RequireLocation(effect.Symbol).placement,effect.Symbol);break;
     case VisualIntentKind.DissolveStanding:
      RequireAnchor(c.Host.RequireLocation(effect.Symbol).placement,effect.Symbol);
      lease.ValidateClip(c.Lara.InitialPose?.Clip);break;
     case VisualIntentKind.Idle:lease.ValidateClip(c.Lara.InitialPose?.Clip);break;
     case VisualIntentKind.Pose:lease.ValidateClip(owner.Pose(effect.Pose).value.Clip);break;
     case VisualIntentKind.Sit:case VisualIntentKind.SitPose:case VisualIntentKind.DissolveSeated:
      var seat=c.Host.RequireLocation(state.ActorLocation).seat;
      if(seat==null)throw new InvalidOperationException("Assign the seat for "+state.ActorLocation+".");
      RequireAnchor(seat.SeatAnchor,"Seat position");RequireAnchor(seat.ApproachAnchor,"Approach position");
      var pose=effect.Pose==null?null:owner.Pose(effect.Pose);
      var command=effect.Kind==VisualIntentKind.Sit?NativeSeatCommand.DefaultSitAt:effect.Kind==VisualIntentKind.SitPose?NativeSeatCommand.PersistentSitPose:NativeSeatCommand.SeatedDissolve;
      var frame=NativeSeatEndpoint.Resolve(c,new NativeSeatedProjection(seat,null,command,pose?.value,pose?.posture??PlayerPosture.Sitting,false));
      lease.ValidateClip(frame.Clip);break;
     case VisualIntentKind.Material:owner.Shader(effect.Symbol).ValidateConfiguration();break;
     case VisualIntentKind.Gesture:owner.Gesture(effect.Symbol);break;
     case VisualIntentKind.Speech:owner.Dialogue(effect.Symbol);break;
    }
   }catch(Exception error){throw new InvalidOperationException(effect.Source+" · "+effect.Kind+": "+error.Message,error);}
  }
  string[][] SampleProperties(RehearsalVisualState state,StoryLinkedContent content){
   var properties=new string[state.Effects.Count][];PerformerSeat seat=null;
   var cache=new Dictionary<AnimationClip,string[]>();
   for(int i=0;i<state.Effects.Count;i++){
    var effect=state.Effects[i];var owner=content.ContentFor(effect.Source);AnimationClip clip=null;
    switch(effect.Kind){
     case VisualIntentKind.Idle:case VisualIntentKind.DissolveStanding:clip=Context.Lara.InitialPose?.Clip;break;
     case VisualIntentKind.Pose:clip=owner.Pose(effect.Pose).value.Clip;break;
     case VisualIntentKind.Sit:case VisualIntentKind.DissolveSeated:
      seat=Context.Host.RequireLocation(effect.Symbol).seat;goto case VisualIntentKind.SitPose;
     case VisualIntentKind.SitPose:
      var pose=effect.Pose==null?null:owner.Pose(effect.Pose);
      var command=effect.Kind==VisualIntentKind.Sit?NativeSeatCommand.DefaultSitAt:effect.Kind==VisualIntentKind.SitPose?NativeSeatCommand.PersistentSitPose:NativeSeatCommand.SeatedDissolve;
      clip=NativeSeatEndpoint.Resolve(Context,new NativeSeatedProjection(seat,null,command,pose?.value,pose?.posture??PlayerPosture.Sitting,false)).Clip;break;
    }
    if(clip!=null){if(!cache.TryGetValue(clip,out var keys)){keys=lease.ClipProperties(clip);cache.Add(clip,keys);}properties[i]=keys;}
   }
   return properties;
  }
  void StageCumulative(RehearsalVisualState state,StoryLinkedContent content) {
   LastSeating=null;FullProjectionCount++;spatialUpdates.Clear();
   var plan=new NativeProjectionPlan(state.Effects,SampleProperties(state,content));
   var c=Context;var actor=c.Lara.transform;
   var baselinePosition=actor.localPosition;var baselineRotation=actor.localRotation;
   spatialUpdates.Add(()=>{actor.localPosition=baselinePosition;actor.localRotation=baselineRotation;});
   void Place(Action action){action();spatialUpdates.Add(action);}
   bool? hidden=null,bodyHidden=null;Transform camera=null;bool track=false;
   PerformerMagicShaderProfile material=null;PerformerSeat seat=null;
   string poseName="saved baseline",locationName="saved baseline",cameraName="saved baseline";
   for(int index=0;index<state.Effects.Count;index++) {
    var effect=state.Effects[index];
    try {
     var owner=content.ContentFor(effect.Source);
     Transform Mark(){var mark=c.Host.RequireLocation(effect.Symbol).placement;RequireAnchor(mark,effect.Symbol);return mark;}
     void SampleInitial(){if(c.Lara.InitialPose==null)throw new InvalidOperationException("Assign Lara's configured InitialPose.");lease.Sample(c.Lara.InitialPose.Clip);poseName=c.Lara.InitialPose.name;}
     void Seated(NativeSeatCommand command,string poseSymbol) {
      if(seat==null)throw new InvalidOperationException("PersistentSitPose needs an executed SitAt/seated destination first.");
      var pose=poseSymbol==null?null:owner.Pose(poseSymbol);
      var projection=new NativeSeatedProjection(seat,null,command,pose?.value,pose?.posture??PlayerPosture.Sitting,false);
      StageSeated(projection,false);
      // Retain the local hip of this sampled endpoint even if a later cue replaces the pose.
      var frame=NativeSeatEndpoint.Resolve(c,projection);var retainedSeat=seat;
      var hipLocal=actor.InverseTransformPoint(frame.Hip.position);
      var bakedOffset=frame.Profile.SitStart.PelvisOffsetAt(1);bool align=frame.AlignHip;
      var observation=LastSeating;
      var defaultOffset=align?Vector3.zero:frame.DefaultMotion.PelvisOffsetAt(seat.DefaultStyle==PerformerSeatedStyle.Basic?1:0);
      spatialUpdates.Add(()=>{
       RequireAnchor(retainedSeat.SeatAnchor,"Seat position");
       if(!retainedSeat.TryCaptureFrames(out var approach,out var approachYaw,out var position,out var yaw,out var reason))throw new InvalidOperationException(reason);
       NativeSeatingMath.RequireUsableScale(actor.lossyScale,"Lara");
       actor.rotation=yaw;
       actor.position=align?position-actor.TransformVector(hipLocal):NativeSeatingMath.BakedRoot(position,actor.rotation,actor.lossyScale,bakedOffset);
       observation.actorRoot=actor.position;observation.actorRotation=actor.rotation;observation.actorWorldScale=actor.lossyScale;
       observation.seat=position;observation.approach=approach;observation.hip=actor.TransformPoint(hipLocal);
       observation.expectedHip=align?position:actor.TransformPoint(defaultOffset);
       observation.hipToSeatMeters=NativeSeatingMath.Error(position,observation.hip);
       observation.expectedHipResidualMeters=NativeSeatingMath.Error(observation.expectedHip,observation.hip);
       observation.facingDisagreementDegrees=Quaternion.Angle(approachYaw,yaw);
       NativeSeatingMath.RequireContact(observation.expectedHipResidualMeters,observation.toleranceMeters,observation.command);
      });
      poseName=poseSymbol??"default "+seat.DefaultStyle;
     }
     switch(effect.Kind) {
      case VisualIntentKind.Hidden:case VisualIntentKind.DissolveIn: {
       var mark=Mark();if(index==plan.RootBaseIndex)Place(()=>actor.SetPositionAndRotation(mark.position,mark.rotation));hidden=effect.Kind==VisualIntentKind.Hidden;seat=null;locationName=effect.Symbol;break;
      }
      case VisualIntentKind.Idle:if(plan.SamplesPose(index))SampleInitial();break;
      case VisualIntentKind.Move: {
       var mark=Mark();if(index==plan.MoveIndex)Place(()=>{var position=mark.position;position.y=actor.position.y;actor.SetPositionAndRotation(position,NativeStandingRules.Facing(mark.rotation,mark.forward,true));});seat=null;LastSeating=null;locationName=effect.Symbol;break;
      }
      case VisualIntentKind.DissolveStanding: {
       var mark=Mark();if(plan.SamplesPose(index))SampleInitial();if(index==plan.RootBaseIndex)Place(()=>actor.SetPositionAndRotation(mark.position,NativeStandingRules.Facing(mark.rotation,mark.forward,true)));hidden=false;seat=null;LastSeating=null;locationName=effect.Symbol;break;
      }
      case VisualIntentKind.Sit:case VisualIntentKind.DissolveSeated:
       seat=c.Host.RequireLocation(effect.Symbol).seat;locationName=effect.Symbol;
       if(plan.SamplesPose(index))Seated(effect.Kind==VisualIntentKind.Sit?NativeSeatCommand.DefaultSitAt:NativeSeatCommand.SeatedDissolve,effect.Pose);hidden=false;break;
      case VisualIntentKind.SitPose:if(plan.SamplesPose(index))Seated(NativeSeatCommand.PersistentSitPose,effect.Pose);break;
      case VisualIntentKind.Pose:if(plan.SamplesPose(index)){lease.Sample(owner.Pose(effect.Pose).value.Clip);poseName=effect.Pose;}break;
      case VisualIntentKind.Material:material=owner.Shader(effect.Symbol);material.ValidateConfiguration();break;
      case VisualIntentKind.BodyHidden: {
       var mark=Mark();Place(()=>c.Body.transform.SetPositionAndRotation(mark.position,mark.rotation));bodyHidden=true;break;
      }
      case VisualIntentKind.BodyShow:bodyHidden=false;break;
      case VisualIntentKind.BodyHide:bodyHidden=true;break;
      case VisualIntentKind.CameraStart:camera=Mark();cameraName=effect.Symbol;track=false;break;
      case VisualIntentKind.CameraTrack:track=true;break;
      case VisualIntentKind.Gesture:owner.Gesture(effect.Symbol);break; // settled overlay weight is zero; never replace base pose
      case VisualIntentKind.Speech:owner.Dialogue(effect.Symbol);break;
      case VisualIntentKind.Motion:break;
      case VisualIntentKind.Grip:break; // explicitly disclosed dynamic solver observation
      default:throw new InvalidOperationException("Unsupported visual intent "+effect.Kind);
     }
    }catch(Exception error){throw new InvalidOperationException(effect.Source+" · PC "+effect.Visit?.ProgramCounter+" · visit "+effect.Visit?.Encounter+" · "+effect.Kind+": "+error.Message,error);}
   }
   // Aim once, after ALL pose/root operations. No diagnostic framing defaults enter this path.
   Present(hidden,bodyHidden,material,camera,track);
   var cameraMark=camera;bool trackLara=track;
   spatialUpdates.Add(()=>{
    if(cameraMark!=null){RequireAnchor(cameraMark,"Camera placement");c.PlayerRoot.position=cameraMark.position;c.ViewRig.rotation=cameraMark.rotation;}
    if(trackLara)c.ViewRig.rotation=PlayerViewEndpoint.ProjectionRotation(true,c.ViewRig.rotation,c.Host.laraFocus.position,c.Camera.transform.position);
   });
   Composition="Lara: "+locationName+" / "+poseName+" · camera: "+cameraName+(track?" / Track(Lara)":" / authored rotation")+" · body: "+(bodyHidden.HasValue?(bodyHidden.Value?"hidden":"visible"):"saved baseline");
  }
 }
}
