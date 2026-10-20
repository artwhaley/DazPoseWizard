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
 /// <summary>Backward dependency slice for a settled shot; no interpreter or branch evaluation.</summary>
 public sealed class NativeProjectionPlan {
  readonly HashSet<int> samples=new HashSet<int>();
  public int PoseIndex {get;private set;}=-1;
  public int RootBaseIndex {get;private set;}=-1;
  public int MoveIndex {get;private set;}=-1;
  public NativeProjectionPlan(IReadOnlyList<VisualIntent> effects,IReadOnlyList<string[]> properties=null){
   for(int i=0;i<effects.Count;i++){
    var kind=effects[i].Kind;
    if(kind==VisualIntentKind.Idle||kind==VisualIntentKind.Pose||kind==VisualIntentKind.DissolveStanding||kind==VisualIntentKind.Sit||kind==VisualIntentKind.SitPose||kind==VisualIntentKind.DissolveSeated)PoseIndex=i;
    if(kind==VisualIntentKind.Hidden||kind==VisualIntentKind.DissolveIn||kind==VisualIntentKind.DissolveStanding||kind==VisualIntentKind.Sit||kind==VisualIntentKind.SitPose||kind==VisualIntentKind.DissolveSeated){RootBaseIndex=i;MoveIndex=-1;}
    else if(kind==VisualIntentKind.Move)MoveIndex=i;
   }
   var covered=new HashSet<string>(StringComparer.Ordinal);
   for(int i=effects.Count-1;i>=0;i--){
    var kind=effects[i].Kind;
    bool seat=kind==VisualIntentKind.Sit||kind==VisualIntentKind.SitPose||kind==VisualIntentKind.DissolveSeated;
    bool pose=seat||kind==VisualIntentKind.Idle||kind==VisualIntentKind.Pose||kind==VisualIntentKind.DissolveStanding;
    if(!pose)continue;
    // A seat that establishes later root height consumes its own sampled hip BEFORE later poses.
    if(seat&&i==RootBaseIndex)covered.Clear();
    var keys=properties==null?null:properties[i];
    bool needed=keys==null||i==PoseIndex||seat&&i==RootBaseIndex;
    if(keys!=null)foreach(var key in keys)if(!covered.Contains(key))needed=true;
    if(needed){samples.Add(i);if(seat)covered.Clear();}
    if(keys!=null)foreach(var key in keys)covered.Add(key);
   }
  }
  public bool SamplesPose(int index)=>samples.Contains(index);
 }
 /// <summary>Ordered executed visual intent only. No parsing, condition evaluation, card selection or RNG.</summary>
 public sealed class RehearsalVisualState {
  readonly List<VisualIntent> effects;
  public IReadOnlyList<VisualIntent> Effects=>effects.AsReadOnly();
  // Only operations that can change the settled native shot participate. Source owner matters
  // for source-local pose/material aliases; visit/line do not change their visual meaning.
  public string PresentationKey {get;private set;}="";
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
  RehearsalVisualState(RehearsalVisualState other){effects=new List<VisualIntent>(other.effects);PresentationKey=other.PresentationKey;Observation=other.Observation;CameraAnchor=other.CameraAnchor;TrackLara=other.TrackLara;ActorLocation=other.ActorLocation;PersistentPose=other.PersistentPose;Seated=other.Seated;ActorHidden=other.ActorHidden;BodyHidden=other.BodyHidden;Material=other.Material;}
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
   next.effects.Add(new VisualIntent(kind,symbol,pose,command));
   if(kind!=VisualIntentKind.Speech&&kind!=VisualIntentKind.Gesture&&kind!=VisualIntentKind.Grip&&kind!=VisualIntentKind.Motion){
    string owner=command.Source?.unit??command.Source?.file??"";
    string Part(string value)=>(value??"").Length+":"+(value??"");
    next.PresentationKey+=((int)kind)+"|"+Part(owner)+Part(symbol)+Part(pose)+";";
   }
   return next;
  }
 }
}
