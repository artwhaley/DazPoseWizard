using System;
using System.Globalization;
namespace DazPose.Story {
 // Human-readable, event-based tracing. Never logs per frame or toy/backend activity.
 public static class StoryFlowLog {
  public static string Value(StoryValue value) => value.Type=="number" ? value.Number.ToString("0.###",CultureInfo.InvariantCulture) : value.Type=="bool" ? (value.Flag ? "yes" : "no") : value.Text;
  public static string Key(string scope,string key) => (scope+" "+key).Replace("."," ").Replace(":"," ");
  public static string Describe(StoryStep s) {
   switch(s.op) {
    case "wait.seconds": return "Wait for "+s.seconds+" seconds";
    case "lara.say": return "Lara speaks using voice clip '"+s.asset+"'";
    case "lara.pose": return "Move Lara into pose '"+s.asset+"'";
    case "lara.gesture": return "Lara performs gesture '"+s.asset+"'";
    case "lara.lookAt": return "Lara looks at '"+s.target+"'";
    case "lara.clearGaze": return "Release Lara's gaze target";
    case "lara.walkTo": return "Lara walks to '"+s.target+"'";
    case "lara.sitAt": return "Lara sits at '"+s.asset+"'";
    case "lara.stand": return "Lara stands up";
    case "lara.dissolveInAt": return "Reveal Lara at '"+s.target+"' over "+s.seconds+" seconds";
    case "lara.dissolveOut": return "Dissolve Lara out over "+s.seconds+" seconds";
    case "lara.dissolveTo": return "Dissolve Lara to '"+s.target+"' over "+s.seconds+" seconds";
    case "player.moveTo": return "Move the player's view to '"+s.target+"' over "+s.seconds+" seconds";
    case "player.lookAt": return "Turn the player's view toward '"+s.target+"'";
    case "player.track": return "Keep the player's view following '"+s.target+"'";
    case "player.stopTracking": return "Stop following the player's view target";
    case "player.body.dissolveIn": return "Reveal the prepared male player body over "+s.seconds+" seconds";
    case "player.body.dissolveOut": return "Dissolve the player body out over "+s.seconds+" seconds";
    case "event.wait": return "Wait for event '"+s.eventId+"'";
    case "event.emit": return "Send event '"+s.eventId+"'";
    case "ui.message": return "Show message: "+s.value;
    case "ui.showKinks": return "Open the interests editor and wait for confirmation";
    case "ui.showEquipment": return "Open the equipment editor and wait for confirmation";
    case "ui.showClothing": return "Open the clothing editor and wait for confirmation";
    case "session.calculateSubmissionScore": return "Calculate the submission score from saved interests";
    case "session.selectPreparedEquipment": return "Select up to "+s.number+" compatible owned equipment items";
    case "state.set": case "state.add": case "memory.set": case "memory.add": case "local.set": case "local.add":
     return (s.op.EndsWith(".add",StringComparison.Ordinal) ? "Adjust " : "Set ")+s.key.Replace("."," ").Replace(":"," ");
    case "flow.if": return "Choose the next branch";
    case "flow.repeat": return "Repeat the next block "+s.number+" times";
    case "story.checkpoint": return "Reach the authored checkpoint: "+s.note;
    default: return "Execute "+s.op;
   }
  }
  public static string Comparison(string comparison) {
   switch(comparison) {
    case "eq": return "equals"; case "neq": return "does not equal";
    case "gt": return "is greater than"; case "gte": return "is at least";
    case "lt": return "is less than"; case "lte": return "is at most";
    case "contains": return "contains"; default: return "does not contain";
   }
  }
 }
}