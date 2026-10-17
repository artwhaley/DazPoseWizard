using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DazPose.Performer;
using DazPose.Story.Language;
using UnityEngine;
namespace DazPose.Story {
 // Unity adapter only. All choreography and branches come from the compiled authored source.
 public sealed class PerformanceStoryHost : IStoryExecutionHost,IStoryDirectorObserver,IStoryPhaseObserver {
  readonly StoryRuntimeContext context;
  readonly StoryContentBindings content;
  StorySignalHub signals=new StorySignalHub();
  CancellationTokenSource phaseLifetime;
  int phaseInvocation;
  long playbackGeneration=-1;
  Action<long> motionEnded;
  public PerformanceStoryHost(StoryRuntimeContext context,StoryContentBindings content) {
   this.context=context;this.content=content;phaseLifetime=CancellationTokenSource.CreateLinkedTokenSource(context.Cancellation);
   context.Run.RegisterPersistentCleanup(()=> {phaseLifetime.Cancel();context.Bindings.prompts.ClearActions();if(motionEnded!=null && context.Run.PlaybackDriver!=null)context.Run.PlaybackDriver.Ended-=motionEnded;});
  }
  public void ObserveDirector(DirectorSnapshot snapshot,CardExecutionContext card) {context.Snapshot.SetDirector(snapshot,card);}
  public void LeavePhase(string phase) {context.Bindings.prompts.ClearActions();phaseLifetime.Cancel();Trace(null,"Phase "+phase+" ended; its actions and event subscriptions were removed.");}
  void BeginPhase() {
   phaseLifetime.Cancel();phaseLifetime.Dispose();phaseLifetime=CancellationTokenSource.CreateLinkedTokenSource(context.Cancellation);signals=new StorySignalHub();
   context.Bindings.prompts.BeginActionScope(++phaseInvocation);playbackGeneration=-1;
  }
  public StoryData Read(string key) {
   if(key=="Session.PreparedEquipment") return new StoryData(context.Contract.preparedEquipmentIds);
   if(key=="Session.PreparedEquipment.Count") return new StoryData((double)context.Contract.preparedEquipmentIds.Length);
   if(key=="Session.ActiveEquipment")return new StoryData(context.Session.activeEquipmentIds);
   if(key=="Session.ActiveEquipmentTags")return new StoryData(context.Session.activeEquipmentIds.SelectMany(id=>Tags(id)).Distinct(StringComparer.Ordinal).ToArray());
   if(key=="Profile.SubmissionScore" || key=="Contract.SubmissionScore")return new StoryData((double)context.Contract.submissionScore);
   if(key=="Profile.Favor" || key=="Contract.Favor")return new StoryData((double)context.Contract.favor);
   if(key=="Completion") return new StoryData((double)context.Session.phaseCompletion);
   if(key=="Contract.PacingMultiplier") return new StoryData((double)context.Contract.pacingMultiplier);
   Split(key,out string scope,out string field);
   var value=context.Values.Read(scope,field);
   return value.Type=="number" ? new StoryData((double)value.Number) : value.Type=="bool" ? new StoryData(value.Flag) : new StoryData(value.Text);
  }
  static void Split(string key,out string scope,out string field) {
   int dot=key.IndexOf('.'); if(dot<=0) throw new ArgumentException("Unknown story state '"+key+"'.");
   scope=key.Substring(0,dot).ToLowerInvariant(); field=key.Substring(dot+1);
   field=char.ToLowerInvariant(field[0])+field.Substring(1);
   if(field.StartsWith("clothing.",StringComparison.Ordinal)) field="clothing."+field.Substring(9).ToLowerInvariant();
  }
  public void Write(string key,StoryData value) {
   context.CheckAuthority(); var session=context.Session; var world=session.world;
   switch(key) {
    case "Session.Phase":BeginPhase();session.phase=value.String;return;
    case "Session.PhaseCompletion":session.phaseCompletion=Finite(value);return;
    case "Session.Mood":session.mood=Finite(value);return;
    case "Session.IntensityMultiplier":if(value.Numeric<=0) throw new ArgumentException("Intensity multiplier must be positive.");session.intensityMultiplier=Finite(value);return;
    case "World.Posture":world.posture=StateEnum<PlayerPosture>(value.String);return;
    case "World.Hands":world.hands=StateEnum<PlayerHands>(value.String);return;
    case "World.Ankles":world.ankles=StateEnum<PlayerAnkles>(value.String);return;
    case "World.Oral":world.oral=StateEnum<PlayerOccupancy>(value.String);return;
    case "World.Anal":world.anal=StateEnum<PlayerOccupancy>(value.String);return;
    case "World.Genital":world.genital=StateEnum<PlayerOccupancy>(value.String);return;
   }
   if(key.StartsWith("World.Clothing.",StringComparison.Ordinal)) {world.SetClothing(key.Substring(15).ToLowerInvariant(),value.Boolean);return;}
   throw new ArgumentException("Unknown or read-only story state '"+key+"'.");
  }
  static T StateEnum<T>(string text) where T:struct,Enum {
   if(!Enum.TryParse<T>(text,false,out var value) || !Enum.IsDefined(typeof(T),value) || value.ToString()!=text)throw new ArgumentException("Invalid "+typeof(T).Name+": "+text);
   return value;
  }
  static float Finite(StoryData value) { float number=(float)value.Numeric; if(!float.IsFinite(number)) throw new ArgumentException("State value must be finite.");return number; }
  public StoryData Function(string name,StoryData[] arguments) {
   switch(name) {
    case "Equipment.Tags":return new StoryData(Tags(arguments.Single().String));
    case "Equipment.ActiveHasTag":return new StoryData(context.Session.activeEquipmentIds.Any(id=>Tags(id).Contains(arguments.Single().String)));
    case "Equipment.PreparedHasTag":return new StoryData(context.Contract.preparedEquipmentIds.Any(id=>Tags(id).Contains(arguments.Single().String)));
    case "Equipment.IsPrepared":return new StoryData(context.Contract.preparedEquipmentIds.Contains(arguments.Single().String));
    case "Contract.KinkEnabled":return new StoryData(context.Contract.kinks.Any(k=>k.id==arguments.Single().String && k.frequency!=KinkFrequency.No));
    case "World.Clothing.LeastRisqueWorn":
     if(arguments.Length!=0) throw new ArgumentException("LeastRisqueWorn takes no arguments.");
     return new StoryData(PlayerWorldState.ClothingSlots.FirstOrDefault(context.Session.world.GetClothing) ?? throw new InvalidOperationException("No clothing item is worn."));
    case "round":if(arguments.Length!=1) throw new ArgumentException("round takes one number.");return new StoryData(Math.Round(arguments[0].Numeric,MidpointRounding.AwayFromZero));
    case "random":
     if(arguments.Length!=1 || arguments[0].Kind!=StoryDataKind.Range) throw new ArgumentException("Use random(min..max).");
     double min=arguments[0].Number,max=arguments[0].Upper;
     if(min!=Math.Floor(min) || max!=Math.Floor(max) || min>max || min<int.MinValue || max>=int.MaxValue) throw new ArgumentException("random requires ordered integer bounds.");
     return new StoryData((double)context.Random.Next((int)min,(int)max+1));
    case "UI.Pressed":if(arguments.Length!=1) throw new ArgumentException("UI.Pressed takes an action name.");return new StoryData("UI.Pressed:"+arguments[0].String);
   }
   throw new ArgumentException("Unknown value function '"+name+"'.");
  }
  string[] Tags(string id) {var item=context.Catalogs.Equipment.Require(id);return item.identityTags.Concat(item.themeTags).Distinct(StringComparer.Ordinal).ToArray();}
  public async Task<StoryData> InvokeAsync(StoryCommand command,CancellationToken cancellation) {
   cancellation.ThrowIfCancellationRequested(); context.CheckAuthority();
   var run=context.Run; var scene=context.Bindings; var world=context.Session.world;
   string Arg(string name)=>command.Require(name).String;
   StoryLocationBinding Location()=>scene.RequireLocation(Arg("location"));
   switch(command.Verb) {
    case "Lara.StartHiddenAt":run.Performer.StartHiddenAt(Location().placement);break;
    case "PlayerBody.StartHiddenAt":run.Player.StartBodyHiddenAt(Location().placement);break;
    case "Camera.StartAt":run.Player.StartAt(Location().placement);break;
    case "Lara.StartIdle":run.Performer.StartConfiguredIdle();world.posture=PlayerPosture.Standing;break;
    case "Lara.DissolveIn":await run.Performer.DissolveInAtAsync(Location().placement,(float)command.Number("seconds",scene.lara.DissolveProfile.DefaultDuration));break;
    case "Camera.Track":if(Arg("performer")!="Lara" || scene.laraFocus==null) throw new InvalidOperationException("Assign Lara's authored focus target.");run.Player.Track(scene.laraFocus);break;
    case "Lara.LocomoteTo":await run.Performer.WalkToAsync(Location().placement);break;
    case "Lara.PersistentPose": {
     var pose=content.Pose(Arg("pose"));
     if(pose.value==null) throw new InvalidOperationException("Assign the selected PerformerPose wrapper for "+pose.id+".");
     await run.Performer.PoseAsync(pose.value); world.posture=pose.posture; Trace(command.Source,"World posture is now "+pose.posture+" from pose metadata.");break;
    }
    case "Lara.Say":await run.Performer.SayAsync(content.Dialogue(Arg("text")));break;
    case "Lara.MagicShader":await run.Performer.MagicShaderAsync(content.Shader(Arg("profile")));break;
    case "Wait":await run.Delay((float)command.Require("seconds").Numeric);break;
    case "Lara.PersistentSitPose": {
     var pose=content.Pose(Arg("pose"));if(pose.posture!=PlayerPosture.Sitting)throw new InvalidOperationException("PersistentSitPose requires Sitting metadata.");
     await run.Performer.PersistentSitPoseAsync(pose.value);world.posture=PlayerPosture.Sitting;break;
    }
    case "Lara.DissolveTo": {
     var destination=Location();PerformerSeat seat=null;PerformerPose pose=null;
     if(command.Arguments.ContainsKey("seated")) {seat=destination.seat ?? throw new InvalidOperationException("The seated location requires its PerformerSeat.");var chosen=content.Pose(Arg("seated"));if(chosen.posture!=PlayerPosture.Sitting)throw new InvalidOperationException("Seated arrival requires Sitting pose metadata.");pose=chosen.value;}
     await run.Performer.StoryDissolveToAsync(destination.placement,seat,pose,(float)command.Number("seconds",scene.lara.DissolveProfile.DefaultDuration));
     world.posture=seat!=null ? PlayerPosture.Sitting : PlayerPosture.Standing;break;
    }
    case "Lara.SitAt": {
     var seat=Location().seat ?? throw new InvalidOperationException("Location '"+Arg("location")+"' needs a PerformerSeat binding.");
     await run.Performer.SitAtAsync(seat);world.posture=PlayerPosture.Sitting;break;
    }
    case "Lara.PlayOneShotGesture":await run.Performer.GestureAsync(content.Gesture(Arg("gesture")));break;
    case "Popup.Show":
     Trace(command.Source,"Waiting for your "+Arg("panel")+" confirmation.");
     switch(Arg("panel")) {
      case "Kinks":await scene.prompts.ShowKinkEditorAsync(context.Application.Profile,context.Catalogs.Kinks,cancellation);context.Contract.RefreshKinks(context.Application.Profile.Current);break;
      case "Equipment":await scene.prompts.ShowEquipmentEditorAsync(context.Application.Profile,context.Catalogs.Equipment,cancellation);break;
      case "Clothing":await scene.prompts.ShowClothingEditorAsync(world,cancellation);Trace(command.Source,"Clothing confirmed: "+world.ClothingCount+" items; "+world.ClothingDescription+".");break;
      default:throw new ArgumentException("Unknown popup panel.");
     }
     Trace(command.Source,"You confirmed "+Arg("panel")+".");break;
    case "SubmissionScore.Calculate":context.Application.Profile.CalculateSubmissionScore(context.Catalogs.Kinks);context.Contract.RefreshKinks(context.Application.Profile.Current);Trace(command.Source,"Submission score is "+context.Application.Profile.Current.submissionScore+".");break;
    case "Session.SelectPreparedEquipment":
     double count=command.Require("count").Numeric;
     if(count<0 || count>3 || count!=Math.Floor(count)) throw new ArgumentException("Prepared equipment count must be a whole number from 0 to 3.");
     context.Contract.preparedEquipmentIds=EquipmentCompatibility.Select(context.Catalogs.Equipment,context.Application.Profile.Current.equipment,(int)count,context.Random);
     Trace(command.Source,"Selected equipment: "+(context.Contract.preparedEquipmentIds.Length==0 ? "none." : string.Join(", ",context.Contract.preparedEquipmentIds)+"."));break;
    case "Popup.ShowNeededEquipment":
     var items=command.Require("items").Items ?? throw new ArgumentException("Expected the prepared equipment list.");
     if(items.Length>0) await scene.prompts.ShowMessageAsync("Needed for this session","Please prepare: "+string.Join(", ",items.Select(id=>context.Catalogs.Equipment.Require(id).displayName)),"Ready",cancellation);break;
    case "Popup.RemoveClothingItem":await scene.prompts.ShowMessageAsync("Remove clothing","Please remove your "+Arg("item")+", then select Ready.","Ready",cancellation);break;
    case "World.Clothing.MarkRemoved":world.SetClothing(Arg("item").ToLowerInvariant(),false);Trace(command.Source,"Marked "+Arg("item")+" as removed.");break;
    case "Popup.Ready":await scene.prompts.ShowMessageAsync("Ready",Arg("label"),"Ready",cancellation);break;
    case "World.Clothing.Clear":foreach(string slot in PlayerWorldState.ClothingSlots) world.SetClothing(slot,false);break;
    case "PlayerBody.DissolveIn":await run.Player.DissolveBodyInAsync((float)command.Number("seconds",3));break;
    case "PlayerBody.DissolveOut":await run.Player.DissolveBodyOutAsync((float)command.Number("seconds",3));break;
    case "Lara.Grip":await run.Performer.GripAsync(run.ContactTarget);break;
    case "MotionDriver.PlayFunscript": {
     var driver=run.PlaybackDriver;if(driver==null)throw new InvalidOperationException("Assign the session MotionDriver.");
     if(motionEnded!=null)driver.Ended-=motionEnded;
     int invocation=phaseInvocation;var ownedSignals=signals;
     motionEnded=generation=> {if(invocation!=phaseInvocation || generation!=playbackGeneration || context.Cancellation.IsCancellationRequested)return;Trace(command.Source,"Motion program reached its natural end.");context.Events.Emit("MotionDriver.Ended");ownedSignals.Emit("MotionDriver.Ended");};
     driver.Ended+=motionEnded;
     bool loop=command.Arguments.TryGetValue("loop",out var value) && value.Boolean;
     run.StartPlayback(content.Motion(Arg("program")),loop);playbackGeneration=driver.CommandGeneration;
     run.Performer.FollowGripMotion();break;
    }
    case "MotionDriver.Stop":run.StopPlayback();break;
    case "UI.ShowAction": {
     string action=Arg("action");int invocation=phaseInvocation;var ownedSignals=signals;
     scene.prompts.ShowAction(action,Arg("label"),()=> {if(invocation!=phaseInvocation || context.Cancellation.IsCancellationRequested)return;Trace(command.Source,"You pressed "+action+".");context.Events.Emit(action);ownedSignals.Emit("UI.Pressed:"+action);});break;
    }
    case "UI.HideAction":scene.prompts.HideAction(Arg("action"));break;
    case "UI.WaitFor":await WaitAnyAsync(new[]{new StoryData("UI.Pressed:"+Arg("action"))},cancellation);break;
    case "Equipment.Activate": {
     string id=Arg("equipment");context.Catalogs.Equipment.Require(id);
     if(!context.Contract.preparedEquipmentIds.Contains(id))throw new InvalidOperationException("Only prepared equipment may be activated: "+id);
     context.Session.activeEquipmentIds=context.Session.activeEquipmentIds.Concat(new[]{id}).Distinct(StringComparer.Ordinal).ToArray();
     Trace(command.Source,"Activated "+id+" with tags ["+string.Join(", ",Tags(id))+"]. Physical state effects come from the authored card.");break;
    }
    case "Toy.Vibrate":try {run.Toys.Vibrate(Math.Clamp((float)command.Require("intensity").Numeric,0,1));}catch {}break;
    default:throw new StorySourceException(command.Source,"Unsupported typed performance command "+command.Verb+".");
   }
   cancellation.ThrowIfCancellationRequested();context.CheckAuthority(); return default;
  }
  public async Task<StoryData> WaitAnyAsync(StoryData[] events,CancellationToken cancellation) {
   string[] ids=events.Select(e=>e.String).ToArray();
   foreach(string id in ids)if(id!="MotionDriver.Ended" && !id.StartsWith("UI.Pressed:",StringComparison.Ordinal))throw new ArgumentException("Unknown story event "+id);
   using(var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,phaseLifetime.Token)) {
    var hub=signals;var wait=hub.WaitAnyAsync(ids,linked.Token);
    // Latch only a press from this current action scope, never an older phase.
    foreach(string id in ids)if(id.StartsWith("UI.Pressed:",StringComparison.Ordinal) && context.Bindings.prompts.ActionWasPressed(id.Substring(11)))hub.Emit(id);
    Trace(null,"Waiting for the first of ["+string.Join(", ",ids)+"].");
    string winner=await wait;Trace(null,"Event race won by "+winner+"; losing subscriptions removed.");return new StoryData(winner);
   }
  }
  public async Task YieldAsync(CancellationToken cancellation) {await Awaitable.NextFrameAsync(cancellation);}
  public void CancelOperations() { if(!context.Run.IsStopped) context.Run.Stop(); }
  public void Trace(StorySource source,string message) {context.Snapshot.SetSource(source,message);context.Log((source==null ? "" : source+" — ")+message);}
 }
}