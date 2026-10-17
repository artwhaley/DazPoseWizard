using System;
using System.Linq;
using DazPose.Performer;
using UnityEngine;
namespace DazPose.Story {
 public static class StoryRunner {
  public static async Awaitable ExecuteAsync(StoryProgramDocument document,StoryRuntimeContext context) {
   context.Log("Story started. Session "+context.Contract.sessionId+"; equipment random seed "+context.Contract.randomSeed+".");
   try {
    await Steps(document.steps,document.id,context);
    context.CheckAuthority();
    context.Log("Story completed"+(context.Snapshot.Checkpoint==null ? "." : " at checkpoint: "+context.Snapshot.Checkpoint));
   } catch(OperationCanceledException) { context.Log("Story stopped. Pending story work and prompts were cancelled."); throw; }
   catch(Exception error) { Debug.LogError("Story failed: "+error); throw; }
  }
  static async Awaitable<bool> Steps(StoryStep[] steps,string story,StoryRuntimeContext c) {
   foreach(var s in steps ?? Array.Empty<StoryStep>()) {
    c.CheckAuthority(); c.Snapshot.SetStep(s);
    c.Log("Begin: "+StoryFlowLog.Describe(s)+". ["+s.id+"]");
    try {
     if(s.op=="flow.if") {
      var condition=s.condition;
      var actual=c.Values.Read(condition.scope,condition.key);
      var expected=condition.valueType=="number" ? new StoryValue(condition.numberValue) : condition.valueType=="bool" ? new StoryValue(condition.boolValue) : new StoryValue(condition.stringValue);
      bool selected=c.Values.Evaluate(condition);
      c.Log("Branch: "+StoryFlowLog.Key(condition.scope,condition.key)+" is "+StoryFlowLog.Value(actual)+"; checking whether it "+StoryFlowLog.Comparison(condition.comparison)+" "+StoryFlowLog.Value(expected)+". Result: "+(selected ? "yes; take the first branch." : "no; take the alternative branch."));
      if(await Steps(selected ? s.steps : s.elseSteps,story,c)) return true;
     } else if(s.op=="flow.repeat") {
      for(int i=0;i<(int)s.number;i++) {
       c.Log("Repeat iteration "+(i+1)+" of "+(int)s.number+".");
       if(await Steps(s.steps,story,c)) return true;
       // Yield even for empty or instant blocks; the frame wait also observes Stop at timeScale zero.
       await Awaitable.NextFrameAsync(c.Cancellation); c.CheckAuthority();
      }
     } else if(s.op=="story.checkpoint") {
      c.Log("Checkpoint reached: "+s.note+". No later story content will run.");
      c.Snapshot.SetCheckpoint(s.note);
      c.Bindings.prompts?.ShowCheckpoint(s.note);
      return true;
     } else await Dispatch(s,c);
     c.CheckAuthority();
     c.Log("Finished: "+StoryFlowLog.Describe(s)+". ["+s.id+"]");
    } catch(OperationCanceledException) { throw; }
    catch(StoryProgramException) { throw; }
    catch(Exception error) { throw new StoryProgramException(story,s.id,s.op,error.Message,error); }
   }
   return false;
  }
  static string Message(string value,StoryRuntimeContext c) =>
   value.Replace("{submissionScore}",c.Application.Profile.Current.submissionScore.ToString())
    .Replace("{preparedEquipment}",string.Join(", ",c.Contract.preparedEquipmentIds));
  static async Awaitable Dispatch(StoryStep s,StoryRuntimeContext c) {
   var run=c.Run; var b=c.Bindings;
   switch(s.op) {
    case "wait.seconds": await run.Delay(s.seconds); break;
    case "lara.say": await run.Performer.SayAsync(b.RequireAsset<AudioClip>(s.asset)); break;
    case "lara.pose": await run.Performer.PoseAsync(b.RequireAsset<PerformerPose>(s.asset)); break;
    case "lara.gesture": await run.Performer.GestureAsync(b.RequireAsset<PerformerGesture>(s.asset)); break;
    case "lara.lookAt": await run.Performer.LookAtAsync(b.RequireTarget(s.target)); break;
    case "lara.clearGaze": run.Performer.ClearGaze(); break;
    case "lara.walkTo": await run.Performer.WalkToAsync(b.RequireTarget(s.target)); break;
    case "lara.sitAt": await run.Performer.SitAtAsync(b.RequireAsset<PerformerSeat>(s.asset)); break;
    case "lara.stand": await run.Performer.StandUpAsync(); break;
    case "lara.dissolveInAt": await run.Performer.DissolveInAtAsync(b.RequireTarget(s.target),s.seconds); break;
    case "lara.dissolveOut": await run.Performer.DissolveOutAsync(s.seconds); break;
    case "lara.dissolveTo": await run.Performer.DissolveToAsync(b.RequireTarget(s.target),s.seconds); break;
    case "player.moveTo": await run.Player.MoveToAsync(b.RequireTarget(s.target),s.seconds); break;
    case "player.lookAt": await run.Player.LookAtAsync(b.RequireTarget(s.target),s.seconds); break;
    case "player.track": run.Player.Track(b.RequireTarget(s.target),s.seconds); break;
    case "player.stopTracking": run.Player.StopTracking(); break;
    case "player.body.dissolveIn": await run.Player.DissolveBodyInAsync(s.seconds); break;
    case "player.body.dissolveOut": await run.Player.DissolveBodyOutAsync(s.seconds); break;
    case "event.wait": await c.Events.WaitAsync(s.eventId,c.Cancellation); c.Log("Event wait satisfied: "+s.eventId+"."); break;
    case "event.emit": c.Events.Emit(s.eventId,s.value); break;
    case "state.set": case "state.add": case "memory.set": case "memory.add": case "local.set": case "local.add":
     c.Values.Mutate(s); break;
    case "ui.message": c.Log("Waiting for your "+Argument(s,"confirm","Continue")+" confirmation. Message: "+Message(s.value,c)); await b.prompts.ShowMessageAsync(s.panel ?? "First Contact",Message(s.value,c),Argument(s,"confirm","Continue"),c.Cancellation); c.Log("You confirmed the message."); break;
    case "ui.showKinks":
     await b.prompts.ShowKinkEditorAsync(c.Application.Profile,c.Catalogs.Kinks,c.Cancellation);
     c.Contract.RefreshKinks(c.Application.Profile.Current); c.Log("Interests confirmed and saved: "+string.Join(", ",c.Contract.kinks.Select(k=>k.id+" frequency "+k.frequency+", sentiment "+k.sentiment))+"."); break;
    case "ui.showEquipment": await b.prompts.ShowEquipmentEditorAsync(c.Application.Profile,c.Catalogs.Equipment,c.Cancellation); c.Log("Equipment confirmed and saved: "+string.Join(", ",c.Application.Profile.Current.equipment.Select(e=>e.id+": "+e.ownedAndWilling))+"."); break;
    case "ui.showClothing": await b.prompts.ShowClothingEditorAsync(c.Session.world,c.Cancellation); c.Log("Clothing confirmed: "+c.Session.world.ClothingCount+" items worn"+(c.Session.world.ClothingCount==0 ? "." : ": "+c.Session.world.ClothingDescription+".")); break;
    case "session.calculateSubmissionScore": c.Application.Profile.CalculateSubmissionScore(c.Catalogs.Kinks); c.Log("Submission score calculated and saved: "+c.Application.Profile.Current.submissionScore+" out of 100."); break;
    case "session.selectPreparedEquipment":
     c.Contract.preparedEquipmentIds=EquipmentCompatibility.Select(c.Catalogs.Equipment,c.Application.Profile.Current.equipment,(int)s.number,c.Random);
     if(c.Contract.preparedEquipmentIds.Length>0) {
      string names=string.Join(", ",c.Contract.preparedEquipmentIds.Select(id=>c.Catalogs.Equipment.Require(id).displayName));
      c.Log("Selected equipment: "+names+". Waiting for you to prepare it and confirm Ready.");
      await b.prompts.ShowMessageAsync("Needed for this session","Please prepare: "+names,"Ready",c.Cancellation);
      c.Log("You confirmed the prepared equipment is ready.");
     }
     else c.Log("No compatible owned equipment was selected; continue without an equipment preparation prompt.");
     break;
    default: throw new ArgumentException("Unknown or unsupported v1 opcode: "+s.op);
   }
  }
  static string Argument(StoryStep step,string name,string fallback) {
   foreach(var a in step.args ?? Array.Empty<StoryArgument>()) if(a.name==name && a.valueType=="string") return a.stringValue;
   return fallback;
  }
 }
}