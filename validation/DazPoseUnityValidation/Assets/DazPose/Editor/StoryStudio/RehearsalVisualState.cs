using System;
using System.Collections.Generic;
using DazPose.Story.Language;
namespace DazPose.Editor.Studio {
 public enum VisualIntentKind { Hidden, Idle, DissolveIn, Move, DissolveStanding, Sit, SitPose, DissolveSeated, Pose, Material, BodyHidden, BodyShow, BodyHide, CameraStart, CameraTrack, Gesture, Speech, Grip, Motion }
 public sealed class VisualIntent {
  public VisualIntentKind Kind {get;}
  public string Symbol {get;}
  public string Pose {get;}
  public StorySource Source {get;}
  public StoryInstructionVisit Visit {get;}
  public bool Awaited {get;}
  public VisualIntent(VisualIntentKind kind,string symbol,string pose,StoryCommand command){Kind=kind;Symbol=symbol;Pose=pose;Source=command.Source;Visit=command.Visit;Awaited=command.Awaited;}
 }
 /// <summary>Ordered executed visual intent only. No parsing, condition evaluation, card selection or RNG.</summary>
 public sealed class RehearsalVisualState {
  readonly List<VisualIntent> effects;
  public IReadOnlyList<VisualIntent> Effects=>effects.AsReadOnly();
  public string Observation {get;private set;}
  public string CameraAnchor {get;private set;}
  public bool TrackLara {get;private set;}
  public string ActorLocation {get;private set;}
  public string PersistentPose {get;private set;}
  public bool Seated {get;private set;}
  public bool? ActorHidden {get;private set;}
  public bool? BodyHidden {get;private set;}
  public string Material {get;private set;}
  public RehearsalVisualState(){effects=new List<VisualIntent>();}
  RehearsalVisualState(RehearsalVisualState other){effects=new List<VisualIntent>(other.effects);Observation=other.Observation;CameraAnchor=other.CameraAnchor;TrackLara=other.TrackLara;ActorLocation=other.ActorLocation;PersistentPose=other.PersistentPose;Seated=other.Seated;ActorHidden=other.ActorHidden;BodyHidden=other.BodyHidden;Material=other.Material;}
  public RehearsalVisualState After(StoryCommand command) {
   var next=new RehearsalVisualState(this);string symbol=null,pose=null;VisualIntentKind kind;
   string Arg(string name)=>command.Require(name).String;
   switch(command.Verb) {
    case "Lara.StartHiddenAt":kind=VisualIntentKind.Hidden;symbol=Arg("location");break;
    case "Lara.StartIdle":kind=VisualIntentKind.Idle;break;
    case "Lara.DissolveIn":kind=VisualIntentKind.DissolveIn;symbol=Arg("location");break;
    case "Lara.LocomoteTo":kind=VisualIntentKind.Move;symbol=Arg("location");break;
    case "Lara.DissolveTo":symbol=Arg("location");if(command.Arguments.ContainsKey("seated")){kind=VisualIntentKind.DissolveSeated;pose=Arg("seated");}else kind=VisualIntentKind.DissolveStanding;break;
    case "Lara.SitAt":kind=VisualIntentKind.Sit;symbol=Arg("location");break;
    case "Lara.PersistentSitPose":kind=VisualIntentKind.SitPose;pose=Arg("pose");break;
    case "Lara.PersistentPose":kind=VisualIntentKind.Pose;pose=Arg("pose");break;
    case "Lara.MagicShader":kind=VisualIntentKind.Material;symbol=Arg("profile");break;
    case "PlayerBody.StartHiddenAt":kind=VisualIntentKind.BodyHidden;symbol=Arg("location");break;
    case "PlayerBody.DissolveIn":kind=VisualIntentKind.BodyShow;break;
    case "PlayerBody.DissolveOut":kind=VisualIntentKind.BodyHide;break;
    case "Camera.StartAt":kind=VisualIntentKind.CameraStart;symbol=Arg("location");break;
    case "Camera.Track":kind=VisualIntentKind.CameraTrack;symbol=Arg("performer");if(symbol!="Lara")throw new StorySourceException(command.Source,"Camera.Track requires Lara.");break;
    case "Lara.PlayOneShotGesture":kind=VisualIntentKind.Gesture;symbol=Arg("gesture");next.Observation=command.Awaited?"Gesture completed: additive overlay faded out; persistent base pose shown.":"Non-awaited gesture launch: persistent base pose shown; in-progress additive overlay is not auditioned.";break;
    case "Lara.Say":kind=VisualIntentKind.Speech;symbol=Arg("text");break;
    case "Lara.Grip":kind=VisualIntentKind.Grip;symbol=Arg("target");next.Observation="Grip intent only: dynamic contact solver requires Art's later Play check.";break;
    case "MotionDriver.PlayFunscript":kind=VisualIntentKind.Motion;symbol=Arg("program");next.Observation="Virtual motion program only; no device output or invented actor motion.";break;
    default:throw new StorySourceException(command.Source,"INCOMPLETE: no native visual adapter for "+command.Verb);
   }
   switch(kind) {
    case VisualIntentKind.CameraStart:next.CameraAnchor=symbol;next.TrackLara=false;break;
    case VisualIntentKind.CameraTrack:next.TrackLara=true;break;
    case VisualIntentKind.Hidden:next.ActorHidden=true;next.ActorLocation=symbol;next.Seated=false;break;
    case VisualIntentKind.DissolveIn:next.ActorHidden=false;next.ActorLocation=symbol;next.Seated=false;break;
    case VisualIntentKind.Move:next.ActorLocation=symbol;next.Seated=false;break;
    case VisualIntentKind.DissolveStanding:next.ActorLocation=symbol;next.ActorHidden=false;next.Seated=false;next.PersistentPose="configured InitialPose";break;
    case VisualIntentKind.Idle:next.PersistentPose="configured InitialPose";break;
    case VisualIntentKind.Pose:next.PersistentPose=pose;break;
    case VisualIntentKind.Sit:next.ActorLocation=symbol;next.Seated=true;next.PersistentPose="seat default rest";break;
    case VisualIntentKind.SitPose:next.Seated=true;next.PersistentPose=pose;break;
    case VisualIntentKind.DissolveSeated:next.ActorLocation=symbol;next.Seated=true;next.ActorHidden=false;next.PersistentPose=pose;break;
    case VisualIntentKind.BodyHidden:case VisualIntentKind.BodyHide:next.BodyHidden=true;break;
    case VisualIntentKind.BodyShow:next.BodyHidden=false;break;
    case VisualIntentKind.Material:next.Material=symbol;break;
   }
   next.effects.Add(new VisualIntent(kind,symbol,pose,command));return next;
  }
 }
}
