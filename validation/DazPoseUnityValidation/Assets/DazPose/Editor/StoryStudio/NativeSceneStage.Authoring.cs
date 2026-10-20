using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Performer;
using DazPose.Player;
using DazPose.Story;
using UnityEngine;
using UnityEditor;
namespace DazPose.Editor.Studio {
 public sealed partial class NativeSceneStage {
  public string Composition {get;private set;}
  public int FullProjectionCount {get;private set;}
  public int SpatialProjectionCount {get;private set;}
  public int PoseSampleCount=>lease.SampleCount;
  public double LastProjectionMilliseconds {get;private set;}
  Vector3 authorActorPosition,authorBodyPosition,authorPlayerPosition;Quaternion authorActorRotation,authorBodyRotation,authorViewRotation;
  string authorPoseKey,authorPresentationKey,authorShotKey;Action authorSpatial;
  readonly Dictionary<string,Vector3> authorHipCache=new Dictionary<string,Vector3>();
  public string[] AuthoringWarnings {get;private set;}=Array.Empty<string>();
  public UnityEngine.Object[] AuthoringDependencies {get;private set;}=Array.Empty<UnityEngine.Object>();
  int validatedPoseRevision;
  public int AuthoringPoseUpdates {get;private set;}
  void CaptureAuthoringBaseline(){var c=Context;authorActorPosition=c.Lara.transform.localPosition;authorActorRotation=c.Lara.transform.localRotation;authorBodyPosition=c.Body.transform.localPosition;authorBodyRotation=c.Body.transform.localRotation;authorPlayerPosition=c.PlayerRoot.localPosition;authorViewRotation=c.ViewRig.localRotation;}
  public void ApplyAuthoringState(AuthoringVisualState state,Func<AuthoringValue,StoryContentBindings> content,int poseRevision=0,int presentationRevision=0){
   if(validatedPoseRevision!=poseRevision){lease.InvalidateClipValidation();validatedPoseRevision=poseRevision;}
   var watch=System.Diagnostics.Stopwatch.StartNew();var c=Context;var actor=c.Lara.transform;
   AnimationClip sampledClip=null;float sampledTime=-1;
   void Sample(AnimationClip clip,float time){if(sampledClip==clip&&sampledTime==time)return;lease.ResetPoseToBaseline();lease.Sample(clip,time);sampledClip=clip;sampledTime=time;}
   var warnings=new List<string>(state.Diagnostics);var dependencies=new HashSet<UnityEngine.Object>();
   void Dependency(UnityEngine.Object obj){if(obj!=null)dependencies.Add(obj);}
   Transform Mark(AuthoringValue v){var mark=c.Host.RequireLocation(v.Symbol).placement;RequireAnchor(mark,v.Symbol);Dependency(mark);return mark;}
   void Error(AuthoringField field,Exception e){warnings.Add(field+": "+e.Message+". Saved baseline shown.");}
   T Resolve<T>(AuthoringField field,Func<T> resolve,T fallback=default){try{return resolve();}catch(Exception e){Error(field,e);return fallback;}}
   AnimationClip PoseClip(AuthoringValue value,out float time){
    time=value.Op=="standing"||value.Op=="initial"?PoseSamplingConventions.StandingSeconds:0;
    if(value.Op=="initial"){var initial=c.Lara.InitialPose;Dependency(initial);return initial?.Clip;}
    var owner=content(value);Dependency(owner);var pose=owner.Pose(value.Symbol).value;Dependency(pose);return pose.Clip;
   }
   var pv=state[AuthoringField.Pose];AnimationClip finalClip=null;float finalTime=0;
   bool usableSeat=state[AuthoringField.Placement].IsKnown&&(state[AuthoringField.Placement].Op=="sit"||state[AuthoringField.Placement].Op=="seatpose");
   if(pv.IsKnown&&pv.Op!="defaultseat"){
    if(pv.Op=="seated"&&!usableSeat)warnings.Add("Seated pose needs a known seat; saved pose shown.");
    else if(pv.Op=="standing"&&usableSeat)warnings.Add("PersistentPose changes the base layer beneath runtime seating. This mixed-layer endpoint is unresolved here; saved pose shown. Select a PersistentSitPose endpoint for seat posing.");
    else finalClip=Resolve(AuthoringField.Pose,()=>{var clip=PoseClip(pv,out finalTime);lease.ValidateClip(clip);Dependency(clip);return clip;});
   }
   bool validPlacement=true;
   // Prepare a bounded recipe. Hip samples occur only on pose/geometry changes, never on mark movement.
   Func<bool,Action> Placement(AuthoringValue v){
    if(v==null||!v.IsKnown){if(v?.Knowledge==AuthoringKnowledge.Unknown)validPlacement=false;return _=>()=>{actor.localPosition=authorActorPosition;actor.localRotation=authorActorRotation;};}
    if(v.Op=="full"||v.Op=="yaw"){
     var mark=Mark(v);return _=>()=>actor.SetPositionAndRotation(mark.position,v.Op=="full"?mark.rotation:NativeStandingRules.Facing(mark.rotation,mark.forward,true));
    }
    if(v.Op=="move"){
     var mark=Mark(v);var previous=Placement(v.Parent);if(v.Parent?.Knowledge==AuthoringKnowledge.Unknown)throw new InvalidOperationException("Locomotion height is unknown after skipped logic");
     return sample=>{var height=previous(sample);return ()=>{height();var p=mark.position;p.y=actor.position.y;actor.SetPositionAndRotation(p,NativeStandingRules.Facing(mark.rotation,mark.forward,true));};};
    }
    if(v.Op=="sit"||v.Op=="seatpose"){
     var seat=c.Host.RequireLocation(v.Symbol).seat;if(seat==null)throw new InvalidOperationException("Assign seat "+v.Symbol);
     Dependency(seat);Dependency(seat.SeatAnchor);Dependency(seat.ApproachAnchor);Dependency(seat.SeatingProfile);
     RequireAnchor(seat.SeatAnchor,"Seat position");RequireAnchor(seat.ApproachAnchor,"Approach position");
     var custom=v.Pose==null?null:content(v.Pose).Pose(v.Pose.Symbol);Dependency(custom?.value);
     var command=v.Op=="sit"?NativeSeatCommand.DefaultSitAt:NativeSeatCommand.PersistentSitPose;
     var projection=new NativeSeatedProjection(seat,null,command,custom?.value,custom?.posture??PlayerPosture.Sitting,false);
     var frame=NativeSeatEndpoint.Resolve(c,projection);lease.ValidateClip(frame.Clip);Dependency(frame.Clip);
     if(pv.Op=="defaultseat"){finalClip=frame.Clip;finalTime=frame.Time;}
     Vector3 hipLocal=Vector3.zero;string hipKey=v.Key+"/"+frame.Clip.GetEntityId()+"/"+EditorUtility.GetDirtyCount(frame.Clip)+"/"+poseRevision;bool prepared=authorHipCache.TryGetValue(hipKey,out hipLocal);
     return sample=>{
      if(!prepared){Sample(frame.Clip,frame.Time);hipLocal=actor.InverseTransformPoint(frame.Hip.position);prepared=true;if(authorHipCache.Count>=64)authorHipCache.Clear();authorHipCache[hipKey]=hipLocal;}
      return ()=>{
       if(!seat.TryCaptureFrames(out var approach,out var approachYaw,out var position,out var yaw,out var reason))throw new InvalidOperationException(reason);
       NativeSeatingMath.RequireUsableScale(actor.lossyScale,"Lara");NativeSeatingMath.RequireUsableScale(seat.SeatAnchor.lossyScale,"SeatAnchor");
       actor.rotation=yaw;actor.position=frame.AlignHip?position-actor.TransformVector(hipLocal):NativeSeatingMath.BakedRoot(position,yaw,actor.lossyScale,frame.Profile.SitStart.PelvisOffsetAt(1));
       var hip=actor.TransformPoint(hipLocal);var expected=frame.AlignHip?position:actor.TransformPoint(frame.DefaultMotion.PelvisOffsetAt(seat.DefaultStyle==PerformerSeatedStyle.Basic?1:0));LastSeating=new NativeSeatObservation{command=command.ToString(),style=seat.DefaultStyle.ToString(),clip=frame.Clip.name,sampleSeconds=frame.Time,actorRoot=actor.position,actorRotation=actor.rotation,actorWorldScale=actor.lossyScale,seat=position,approach=approach,hip=hip,expectedHip=expected,expectedHipResidualMeters=NativeSeatingMath.Error(expected,hip),toleranceMeters=frame.AlignHip?NativeSeatingMath.PersistentContactTolerance:NativeSeatingMath.BakedResidualTolerance,hipPath=NativeStageContext.ObjectPath(frame.Hip),seatPath=NativeStageContext.ObjectPath(seat.SeatAnchor),warning=frame.Warning,hipToSeatMeters=NativeSeatingMath.Error(position,hip),liveHipAligned=frame.AlignHip,rule=frame.AlignHip?"sampled hip alignment":"baked default landing root",facingDisagreementDegrees=Quaternion.Angle(approachYaw,yaw)};
      };
     };
    }
    throw new InvalidOperationException("Placement requires explicit known seat/location");
   }
   var preparePlacement=Resolve(AuthoringField.Placement,()=>Placement(state[AuthoringField.Placement]));
   if(usableSeat&&pv.IsKnown&&pv.Op!="defaultseat"&&finalClip==null){preparePlacement=null;warnings.Add("Seated placement depends on the unresolved pose; saved position shown.");}
   if(preparePlacement==null){validPlacement=false;preparePlacement=Placement(AuthoringValue.Baseline());}
   var bv=state[AuthoringField.BodyPlacement];var bodyMark=bv.IsKnown?Resolve(AuthoringField.BodyPlacement,()=>Mark(bv)):null;
   var cv=state[AuthoringField.Camera];var cameraMark=cv.IsKnown?Resolve(AuthoringField.Camera,()=>Mark(cv)):null;
   bool track=state[AuthoringField.Tracking].Op=="true";
   if(track&&(!validPlacement||state[AuthoringField.Placement].Knowledge==AuthoringKnowledge.Unknown||state[AuthoringField.Pose].Knowledge==AuthoringKnowledge.Unknown||pv.IsKnown&&finalClip==null)){
    track=false;warnings.Add("Camera tracking unresolved because Lara's position/pose is unresolved; authored camera rotation shown.");
   }
   if(track&&c.Host.laraFocus==null){track=false;warnings.Add("Camera tracking target is missing; assign Lara focus on the scene host. Authored camera rotation shown.");}
   if(track)Dependency(c.Host.laraFocus);
   var mv=state[AuthoringField.Material];var material=mv.IsKnown?Resolve(AuthoringField.Material,()=>{var owner=content(mv);Dependency(owner);var m=owner.Shader(mv.Symbol);m.ValidateConfiguration();Dependency(m);Dependency(m.wireframeMaterial);return m;}):null;
   bool? Hidden(AuthoringField f)=>state[f].IsKnown?state[f].Op=="true":(bool?)null;
   string ClipKey(AnimationClip clip)=>clip==null?"baseline":clip.GetEntityId()+"/"+EditorUtility.GetDirtyCount(clip);
   string poseKey=pv.Key+"/"+ClipKey(finalClip)+"/"+finalTime+"/"+poseRevision;
   // A seat height dependency requires a cached sample even if the displayed standing pose is unchanged.
   var placementValue=state[AuthoringField.Placement];bool seatGeometry=placementValue.Op=="sit"||placementValue.Op=="seatpose"||placementValue.Parent?.Op=="sit"||placementValue.Parent?.Op=="seatpose";
   if(seatGeometry)poseKey+="/"+placementValue.Key+"/"+string.Join(";",dependencies.Where(o=>o is PerformerSeat||o!=null&&o.GetType().Name.Contains("SeatingProfile")).Select(o=>o.GetEntityId()+":"+EditorUtility.GetDirtyCount(o)));
   string presentationKey=state[AuthoringField.Hidden].Key+state[AuthoringField.BodyHidden].Key+mv.Key+"/"+(material!=null?material.GetEntityId().ToString():"")+"/"+presentationRevision;
   bool poseChanged=poseKey!=authorPoseKey,presentationChanged=presentationKey!=authorPresentationKey;
   string shotKey=state.Key+poseKey+presentationKey+string.Join(";",dependencies.Select(o=>o.GetEntityId()+":"+EditorUtility.GetDirtyCount(o)));
   AuthoringDependencies=dependencies.ToArray();AuthoringWarnings=warnings.Distinct().ToArray();
   if(lease.IsStaged&&shotKey==authorShotKey){LastProjectionMilliseconds=watch.Elapsed.TotalMilliseconds;return;}
   // Retain prepared seat geometry when only source provenance or a mark changes.
   Action rootAction=null;
   void Spatial(){rootAction?.Invoke();if(bodyMark!=null)c.Body.transform.SetPositionAndRotation(bodyMark.position,bodyMark.rotation);else{c.Body.transform.localPosition=authorBodyPosition;c.Body.transform.localRotation=authorBodyRotation;}
    c.PlayerRoot.localPosition=authorPlayerPosition;c.ViewRig.localRotation=authorViewRotation;
    if(cameraMark!=null){c.PlayerRoot.position=cameraMark.position;c.ViewRig.rotation=cameraMark.rotation;}
    if(track)c.ViewRig.rotation=PlayerViewEndpoint.ProjectionRotation(true,c.ViewRig.rotation,c.Host.laraFocus.position,c.Camera.transform.position);
   }
   void Presentation()=>Present(Hidden(AuthoringField.Hidden),Hidden(AuthoringField.BodyHidden),material,null,false);
   void SampleAndSpatial(){rootAction=preparePlacement(true);if(finalClip!=null)Sample(finalClip,finalTime);else lease.ResetPoseToBaseline();Spatial();}
   void Complete(){sampledClip=null;sampledTime=-1;LastSeating=null;SampleAndSpatial();Presentation();}
   // For unchanged poses retain prior root action (including sampled hip) while using current live transforms.
   void Delta(){if(poseChanged){AuthoringPoseUpdates++;SampleAndSpatial();}else{rootAction=preparePlacement(false);Spatial();}if(presentationChanged)Presentation();}
   authorSpatial=Spatial;
   try{if(!lease.IsStaged){FullProjectionCount++;lease.Apply(Complete);}else lease.ApplyAuthoringDelta(Complete,Delta,poseChanged,presentationChanged);}
   finally{LastProjectionMilliseconds=watch.Elapsed.TotalMilliseconds;}
   lease.SpatialUndo=ReanchorAuthoring;authorPoseKey=poseKey;authorPresentationKey=presentationKey;authorShotKey=shotKey;
   AuthoringDependencies=dependencies.ToArray();AuthoringWarnings=warnings.Distinct().ToArray();
   Composition="Lara: "+(placementValue.Symbol??"saved baseline")+" / "+(pv.Symbol??pv.Op??"saved pose")+" · camera: "+(cv.Symbol??"saved baseline")+(track?" / Track(Lara)":" / authored rotation");
  }
  public void ReanchorAuthoring(){if(authorSpatial==null)return;var watch=System.Diagnostics.Stopwatch.StartNew();lease.ApplySpatial(authorSpatial);SpatialProjectionCount++;LastProjectionMilliseconds=watch.Elapsed.TotalMilliseconds;}
 }
}
