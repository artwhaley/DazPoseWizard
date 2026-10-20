using System;
using DazPose.Performer;
using DazPose.Story;
using UnityEngine;
namespace DazPose.Editor.Studio {
 public sealed partial class NativeSceneStage {
  public string Composition {get;private set;}
  public void Apply(RehearsalVisualState state,StoryLinkedContent content) {
   if(state==null||content==null)throw new ArgumentNullException();
   lease.Apply(()=>StageCumulative(state,content));
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
  void StageCumulative(RehearsalVisualState state,StoryLinkedContent content) {
   LastSeating=null;
   var c=Context;var actor=c.Lara.transform;
   bool? hidden=null,bodyHidden=null;Transform camera=null;bool track=false;
   PerformerMagicShaderProfile material=null;PerformerSeat seat=null;
   string poseName="saved baseline",locationName="saved baseline",cameraName="saved baseline";
   foreach(var effect in state.Effects) {
    try {
     var owner=content.ContentFor(effect.Source);
     Transform Mark(){var mark=c.Host.RequireLocation(effect.Symbol).placement;RequireAnchor(mark,effect.Symbol);return mark;}
     void SampleInitial(){if(c.Lara.InitialPose==null)throw new InvalidOperationException("Assign Lara's configured InitialPose.");lease.Sample(c.Lara.InitialPose.Clip);poseName=c.Lara.InitialPose.name;}
     void Seated(NativeSeatCommand command,string poseSymbol) {
      if(seat==null)throw new InvalidOperationException("PersistentSitPose needs an executed SitAt/seated destination first.");
      var pose=poseSymbol==null?null:owner.Pose(poseSymbol);
      StageSeated(new NativeSeatedProjection(seat,null,command,pose?.value,pose?.posture??PlayerPosture.Sitting,false),false);
      poseName=poseSymbol??"default "+seat.DefaultStyle;
     }
     switch(effect.Kind) {
      case VisualIntentKind.Hidden:case VisualIntentKind.DissolveIn: {
       var mark=Mark();actor.SetPositionAndRotation(mark.position,mark.rotation);hidden=effect.Kind==VisualIntentKind.Hidden;seat=null;locationName=effect.Symbol;break;
      }
      case VisualIntentKind.Idle:SampleInitial();break;
      case VisualIntentKind.Move: {
       var mark=Mark();var position=mark.position;position.y=actor.position.y;actor.SetPositionAndRotation(position,NativeStandingRules.Facing(mark.rotation,mark.forward,true));seat=null;LastSeating=null;locationName=effect.Symbol;break;
      }
      case VisualIntentKind.DissolveStanding: {
       var mark=Mark();SampleInitial();actor.SetPositionAndRotation(mark.position,NativeStandingRules.Facing(mark.rotation,mark.forward,true));hidden=false;seat=null;LastSeating=null;locationName=effect.Symbol;break;
      }
      case VisualIntentKind.Sit:case VisualIntentKind.DissolveSeated:
       seat=c.Host.RequireLocation(effect.Symbol).seat;locationName=effect.Symbol;
       Seated(effect.Kind==VisualIntentKind.Sit?NativeSeatCommand.DefaultSitAt:NativeSeatCommand.SeatedDissolve,effect.Pose);hidden=false;break;
      case VisualIntentKind.SitPose:Seated(NativeSeatCommand.PersistentSitPose,effect.Pose);break;
      case VisualIntentKind.Pose:lease.Sample(owner.Pose(effect.Pose).value.Clip);poseName=effect.Pose;break;
      case VisualIntentKind.Material:material=owner.Shader(effect.Symbol);material.ValidateConfiguration();break;
      case VisualIntentKind.BodyHidden: {
       var mark=Mark();c.Body.transform.SetPositionAndRotation(mark.position,mark.rotation);bodyHidden=true;break;
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
   Composition="Lara: "+locationName+" / "+poseName+" · camera: "+cameraName+(track?" / Track(Lara)":" / authored rotation")+" · body: "+(bodyHidden.HasValue?(bodyHidden.Value?"hidden":"visible"):"saved baseline");
  }
 }
}
