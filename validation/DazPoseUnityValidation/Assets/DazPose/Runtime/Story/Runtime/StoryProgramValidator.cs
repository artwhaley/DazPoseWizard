using System;
using DazPose.Performer;
using UnityEngine;
namespace DazPose.Story {
 public static class StoryProgramValidator {
  public static void Validate(StoryProgramDocument doc, StorySceneBindings bindings) {
   StoryProgramSerializer.Validate(doc);
   if(bindings==null) throw new StoryProgramException(doc.id,null,null,"Assign StorySceneBindings.");
   try { bindings.ValidateBindings(); }
   catch(Exception e) { throw new StoryProgramException(doc.id,null,null,e.Message,e); }
   ValidateSteps(doc.id,doc.steps,bindings);
  }
  static void Nonblank(string value,string label) { if(string.IsNullOrWhiteSpace(value)) throw new ArgumentException(label+" is required."); }
  static void Count(float count) {
   if(!float.IsFinite(count) || count<0 || count>1000 || count!=Mathf.Floor(count))
    throw new ArgumentException("Count must be an integer from 0 through 1000.");
  }
  static void ValidateSteps(string story,StoryStep[] steps,StorySceneBindings b) {
   foreach(var s in steps ?? Array.Empty<StoryStep>()) {
    try {
     if(!float.IsFinite(s.seconds) || s.seconds<0) throw new ArgumentException("seconds must be finite and nonnegative.");
     if(!float.IsFinite(s.number)) throw new ArgumentException("number must be finite.");
     if((s.op.StartsWith("lara.dissolve",StringComparison.Ordinal) || s.op.StartsWith("player.body.dissolve",StringComparison.Ordinal)) && s.seconds<=0) throw new ArgumentException("Dissolve seconds must be greater than zero.");
     var names=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
     foreach(var a in s.args ?? Array.Empty<StoryArgument>()) {
      if(a==null || string.IsNullOrWhiteSpace(a.name) || !names.Add(a.name)) throw new ArgumentException("Arguments require unique nonblank names.");
      StoryValue.ValidateType(a.valueType);
      if(!float.IsFinite(a.numberValue)) throw new ArgumentException("Argument number must be finite.");
     }
     switch(s.op) {
      case "story.checkpoint": Nonblank(s.note,"Checkpoint note"); break;
      case "wait.seconds": break;
      case "flow.if": StoryValueAccessor.ValidateCondition(s.condition); break;
      case "flow.repeat": Count(s.number); break;
      case "event.wait": case "event.emit": Nonblank(s.eventId,"eventId"); break;
      case "lara.say": b.RequireAsset<AudioClip>(s.asset); break;
      case "lara.pose": b.RequireAsset<PerformerPose>(s.asset); break;
      case "lara.gesture": b.RequireAsset<PerformerGesture>(s.asset); break;
      case "lara.sitAt": b.RequireAsset<PerformerSeat>(s.asset); break;
      case "lara.lookAt": case "lara.walkTo": case "lara.dissolveInAt": case "lara.dissolveTo":
      case "player.moveTo": case "player.lookAt": case "player.track": b.RequireTarget(s.target); break;
      case "lara.clearGaze": case "lara.stand": case "lara.dissolveOut": case "player.stopTracking": break;
      case "player.body.dissolveIn": case "player.body.dissolveOut":
       if(b.playerBody==null) throw new ArgumentException("Assign player body presence.");
       b.playerBody.ValidateConfiguration(); break;
      case "state.set": case "state.add": case "memory.set": case "memory.add": case "local.set": case "local.add":
       StoryValueAccessor.ValidateMutation(s); break;
      case "ui.message": case "ui.showKinks": case "ui.showEquipment": case "ui.showClothing":
       if(b.prompts==null) throw new ArgumentException("Assign StoryPromptService.");
       if(s.op=="ui.message") Nonblank(s.value,"Message value");
       break;
      case "session.calculateSubmissionScore": break;
      case "session.selectPreparedEquipment": Count(s.number);
       if(b.prompts==null) throw new ArgumentException("Assign StoryPromptService for prepared equipment message.");
       break;
      default: throw new ArgumentException("Unknown or unsupported v1 opcode.");
     }
     if(s.op!="flow.if" && s.elseSteps!=null && s.elseSteps.Length>0) throw new ArgumentException("elseSteps requires flow.if.");
     if(s.op!="flow.if" && s.op!="flow.repeat" && s.steps!=null && s.steps.Length>0) throw new ArgumentException("Nested steps require a flow block.");
     ValidateSteps(story,s.steps,b); ValidateSteps(story,s.elseSteps,b);
    } catch(StoryProgramException) { throw; }
    catch(Exception e) { throw new StoryProgramException(story,s.id,s.op,e.Message,e); }
   }
  }
 }
}