using System;
using System.Collections.Generic;
using System.Linq;
namespace DazPose.Story.Language {
 // Read-only validation view for a saved authoring preview. Execution still uses the full original program.
 public static class StoryPlaybackWindow {
  public static CompiledStoryProgram BeforePhase(CompiledStoryProgram program,string sequence,string phase) {
   var entry=program.RequireEntry(sequence);
   var boundaries=Enumerable.Range(entry.start,entry.end-entry.start).Where(i=>program.instructions[i].code==StoryCode.EnterPhase && program.instructions[i].name==phase).ToArray();
   if(boundaries.Length!=1)throw new InvalidOperationException("Authoring preview requires one phase named '"+phase+"' in sequence '"+sequence+"'.");
   int boundary=boundaries[0];var reached=new HashSet<int>();var cards=new HashSet<string>(StringComparer.Ordinal);var dialogue=new HashSet<string>(StringComparer.Ordinal);
   void Collect(StoryEntry part,int end) {
    var pending=new Stack<int>();pending.Push(part.start);
    while(pending.Count>0) {
     int pc=pending.Pop();if(pc<part.start || pc>=end || !reached.Add(pc))continue;
     var instruction=program.instructions[pc];
     if(instruction.code==StoryCode.Call && instruction.name=="Lara.Say") {
      var text=instruction.arguments.Single(a=>a.name=="text");
      foreach(var atom in program.expressions[text.expression].atoms.Where(a=>a.code==StoryAtomCode.String))dialogue.Add(StoryDialogue.IdFor(atom.text));
     }
     if(instruction.code==StoryCode.Play && cards.Add(instruction.name)) {var card=program.RequireEntry(instruction.name,"card");Collect(card,card.end);}
     if(instruction.code==StoryCode.Return)continue;
     if(instruction.code==StoryCode.Jump) {pending.Push(instruction.jump);continue;}
     if(instruction.code==StoryCode.JumpFalse || instruction.code==StoryCode.RepeatBegin || instruction.code==StoryCode.RepeatEnd)pending.Push(instruction.jump);
     pending.Push(pc+1);
    }
   }
   var setup=program.entries.SingleOrDefault(e=>e.kind=="setup" && e.name==sequence);if(setup!=null)Collect(setup,setup.end);
   Collect(entry,boundary);
   return new CompiledStoryProgram {version=program.version,name=program.name,instructions=program.instructions,expressions=program.expressions,entries=program.entries,cards=program.cards,bindings=program.bindings.Where(b=>reached.Contains(b.instruction)).ToArray(),dialogue=program.dialogue.Where(d=>dialogue.Contains(d.id)).ToArray()};
  }
 }
}
