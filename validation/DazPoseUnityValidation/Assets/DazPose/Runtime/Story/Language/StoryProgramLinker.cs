using System;
using System.Collections.Generic;
using System.Linq;

namespace DazPose.Story.Language {
 public sealed class StoryLinkUnit {
  public string Id;
  public CompiledStoryProgram Program;
  public StoryLinkUnit(string id,CompiledStoryProgram program) {Id=id;Program=program;}
 }
 public interface IStoryCardEligibility {string ResourceIssue(CardDefinition card);}

 // Linking relocates executable data only. Unity-owned content stays associated with Source.unit.
 public static class StoryProgramLinker {
  static StorySource Source(StorySource source,string unit)=>source==null ? null : new StorySource(source.file,source.line,source.column,source.declaration) {unit=unit};
  public static CompiledStoryProgram Link(CompiledStoryProgram root,IEnumerable<StoryLinkUnit> published,string rootId="local") {
   if(root==null || root.linked)throw new InvalidOperationException("Link an imported, unlinked root program exactly once.");
   if(string.IsNullOrWhiteSpace(rootId))throw new ArgumentException("A stable root source identity is required.");
   var units=new[]{new StoryLinkUnit(rootId,root)}.Concat((published??Array.Empty<StoryLinkUnit>()).OrderBy(u=>u.Id,StringComparer.Ordinal)).ToArray();
   var seenUnits=new HashSet<string>(StringComparer.Ordinal);var publicIds=new Dictionary<string,CardDefinition>(StringComparer.Ordinal);
   for(int u=0;u<units.Length;u++) {
    var unit=units[u];
    if(unit==null || string.IsNullOrWhiteSpace(unit.Id) || unit.Id.Contains("::") || !seenUnits.Add(unit.Id) || unit.Program==null || unit.Program.linked)
     throw new InvalidOperationException("Global card sources require distinct stable identities and unlinked programs.");
    CardCatalog.Validate(unit.Program);
    if(u>0 && unit.Program.entries.Any(e=>e.kind!="card"))throw new InvalidOperationException("Published card source '"+unit.Id+"' may declare only cards.");
    if(u>0)foreach(var card in unit.Program.cards.Where(c=>c.Tags.Length>0)) {
     if(publicIds.TryGetValue(card.Id,out var previous))throw new StorySourceException(card.Source,"Duplicate public card ID '"+card.Id+"'; first declared at "+previous.Source+".");
     publicIds.Add(card.Id,card);
    }
   }
   foreach(var unit in units)foreach(var card in unit.Program.cards) {
    if(publicIds.TryGetValue(card.Id,out var exported) && !ReferenceEquals(exported,card))
     throw new StorySourceException(card.Source,"Local/public card shadowing for '"+card.Id+"'; public declaration at "+exported.Source+". Rename the local card.");
   }
   var instructions=new List<StoryInstruction>();var expressions=new List<StoryExpression>();var entries=new List<StoryEntry>();
   var cards=new List<CardDefinition>();var bindings=new List<StoryBindingUse>();var dialogue=new List<StoryDialogue>();
   int nextLoop=0;
   for(int u=0;u<units.Length;u++) {
    var unit=units[u];var program=unit.Program;int pc=instructions.Count,expr=expressions.Count;
    var loops=program.instructions.Where(i=>i.code==StoryCode.RepeatBegin || i.code==StoryCode.RepeatEnd).Select(i=>i.loop).Distinct().ToDictionary(i=>i,i=>nextLoop++);
    Func<int,int> expression=index=>index<0 ? index : index<program.expressions.Length ? index+expr : throw new InvalidOperationException("Expression index out of range in "+unit.Id+".");
    Func<int,int> address=index=>index<0 ? index : index<=program.instructions.Length ? index+pc : throw new InvalidOperationException("Instruction address out of range in "+unit.Id+".");
    Func<string,string> entry=name=>u==0 ? name : unit.Id+"::"+name;
    expressions.AddRange(program.expressions.Select(e=>new StoryExpression {atoms=e.atoms.Select(a=>new StoryAtom {code=a.code,text=a.text,number=a.number,count=a.count}).ToArray()}));
    instructions.AddRange(program.instructions.Select(i=>new StoryInstruction {
     code=i.code,name=i.name,awaited=i.awaited,completionLoop=i.completionLoop,expression=expression(i.expression),
     completionTarget=expression(i.completionTarget),jump=address(i.jump),loop=loops.TryGetValue(i.loop,out int loop) ? loop : i.loop,
     arguments=i.arguments.Select(a=>new StoryCallArgument {name=a.name,expression=expression(a.expression)}).ToArray(),source=Source(i.source,unit.Id)
    }));
    entries.AddRange(program.entries.Select(e=>new StoryEntry {name=entry(e.name),kind=e.kind,start=address(e.start),end=address(e.end)}));
    cards.AddRange(program.cards.Select(c=>new CardDefinition {
     Id=u==0 || c.Tags.Length>0 ? c.Id : unit.Id+"::"+c.Id,LocalId=c.Id,OriginUnit=unit.Id,Selectable=u>0 && c.Tags.Length>0,
     ProgramEntry=entry(c.ProgramEntry),Tags=(string[])c.Tags.Clone(),BaseWeight=c.BaseWeight,Progress=c.Progress,
     MaxUsesPerSession=c.MaxUsesPerSession,MinCardsBetweenUses=c.MinCardsBetweenUses,Source=Source(c.Source,unit.Id),
     Requirements=c.Requirements.Select(r=>new CardRequirement {Expression=expression(r.Expression),Description=r.Description,Source=Source(r.Source,unit.Id)}).ToArray()
    }));
    bindings.AddRange(program.bindings.Select(b=>new StoryBindingUse {instruction=address(b.instruction),symbol=b.symbol,kind=b.kind,source=Source(b.source,unit.Id)}));
    dialogue.AddRange(program.dialogue.Select(d=>new StoryDialogue {id=d.id,transcript=d.transcript,source=Source(d.source,unit.Id)}));
   }
   var linked=new CompiledStoryProgram {version=root.version,name=root.name,linked=true,instructions=instructions.ToArray(),expressions=expressions.ToArray(),
    entries=entries.ToArray(),cards=cards.ToArray(),bindings=bindings.ToArray(),dialogue=dialogue.ToArray()};
   CardCatalog.Validate(linked);
   foreach(var instruction in linked.instructions) {
    if(instruction.code==StoryCode.Play)ResolveCard(linked,instruction.name,instruction.source?.unit,instruction.source);
    else if(instruction.code==StoryCode.Call && instruction.name=="Director.Execute") {
     var argument=instruction.arguments.Single(a=>a.name=="card");var atoms=linked.expressions[argument.expression].atoms;
     if(atoms.Length==1 && (atoms[0].code==StoryAtomCode.String || atoms[0].code==StoryAtomCode.Symbol))ResolveCard(linked,atoms[0].text,instruction.source?.unit,instruction.source);
    }
   }
   return linked;
  }
  public static CardDefinition ResolveCard(CompiledStoryProgram program,string name,string unit=null,StorySource source=null) {
   var local=program.cards.SingleOrDefault(c=>(c.OriginUnit??"")==(unit??"") && (c.LocalId??c.Id)==name);
   var global=program.cards.SingleOrDefault(c=>c.Selectable && c.Tags.Length>0 && c.Id==name);
   if(local!=null && global!=null && !ReferenceEquals(local,global))throw new StorySourceException(source,"Ambiguous local/public card '"+name+"'.");
   return local ?? global ?? throw new StorySourceException(source,"Unknown explicit card '"+name+"' in source '"+(unit??program.name)+"'.");
  }
 }
 public static class StoryProgramReachability {
  public static CompiledStoryProgram ForCard(CompiledStoryProgram program,CardDefinition card)=>Collect(program,program.RequireEntry(card.ProgramEntry,"card"));
  public static CompiledStoryProgram ForSequence(CompiledStoryProgram program,string sequence) {
   var entry=program.RequireEntry(sequence);
   var setup=program.entries.SingleOrDefault(e=>e.name==sequence && e.kind=="setup");
   return Collect(program,new[]{entry,setup}.Where(e=>e!=null).ToArray());
  }
  static CompiledStoryProgram Collect(CompiledStoryProgram program,params StoryEntry[] roots) {
   var visited=new HashSet<string>(StringComparer.Ordinal);var reached=new HashSet<int>();var lines=new HashSet<string>(StringComparer.Ordinal);
   var pending=new Stack<StoryEntry>(roots);
   while(pending.Count>0) {
    var entry=pending.Pop();if(!visited.Add(entry.kind+":"+entry.name))continue;
    for(int pc=entry.start;pc<entry.end;pc++) {
     reached.Add(pc);var instruction=program.instructions[pc];string explicitName=null;
     if(instruction.code==StoryCode.Play)explicitName=instruction.name;
     if(instruction.code==StoryCode.Call && instruction.name=="Director.Execute") {
      var atoms=program.expressions[instruction.arguments.Single(a=>a.name=="card").expression].atoms;
      if(atoms.Length==1 && (atoms[0].code==StoryAtomCode.Symbol || atoms[0].code==StoryAtomCode.String))explicitName=atoms[0].text;
     }
     if(explicitName!=null)pending.Push(program.RequireEntry(StoryProgramLinker.ResolveCard(program,explicitName,instruction.source?.unit,instruction.source).ProgramEntry,"card"));
     if(instruction.code==StoryCode.Call && instruction.name=="Lara.Say")
      foreach(var atom in program.expressions[instruction.arguments.Single(a=>a.name=="text").expression].atoms.Where(a=>a.code==StoryAtomCode.String))
       lines.Add((instruction.source?.unit??"")+"\0"+StoryDialogue.IdFor(atom.text));
    }
   }
   return new CompiledStoryProgram {version=program.version,name=program.name,linked=program.linked,instructions=program.instructions,expressions=program.expressions,
    entries=program.entries,cards=program.cards,bindings=program.bindings.Where(b=>reached.Contains(b.instruction)).ToArray(),
    dialogue=program.dialogue.Where(d=>lines.Contains((d.source?.unit??"")+"\0"+d.id)).ToArray()};
  }
 }
}
