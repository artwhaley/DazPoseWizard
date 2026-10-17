using System;
using System.Collections.Generic;
using System.Linq;
namespace DazPose.Story.Language {
 public static class DazStoryCompiler {
  public static CompiledStoryProgram Compile(string source,string file,IReadOnlyDictionary<string,StoryBindingKind> symbols=null) => new Builder(file,symbols).Compile(new StoryParser(source,file).Parse());
  sealed class Builder {
   readonly string file;
   readonly IReadOnlyDictionary<string,StoryBindingKind> symbols;
   readonly List<StoryInstruction> instructions=new List<StoryInstruction>();
   readonly List<StoryExpression> expressions=new List<StoryExpression>();
   readonly List<CardDefinition> cards=new List<CardDefinition>();
   readonly List<StoryBindingUse> bindings=new List<StoryBindingUse>();
   readonly Dictionary<string,StoryDialogue> dialogue=new Dictionary<string,StoryDialogue>(StringComparer.Ordinal);
   readonly HashSet<string> locals=new HashSet<string>(StringComparer.Ordinal);
   StoryAstDeclaration current;
   int loopId;
   static readonly HashSet<string> readable=new HashSet<string>(new[]{
    "Completion","Session.Phase","Session.PhaseCompletion","Session.Mood","Session.IntensityMultiplier",
    "Session.PreparedEquipment","Session.PreparedEquipment.Count","Contract.PacingMultiplier","Profile.SubmissionScore","Profile.Favor",
    "World.Posture","World.Hands","World.Ankles","World.Oral","World.Anal","World.Genital","World.Clothing.Count",
    "World.Clothing.Shoes","World.Clothing.Socks","World.Clothing.Shirt","World.Clothing.Pants","World.Clothing.Bra","World.Clothing.Underwear",
    "Card.Intensity","Card.Invocation","Card.Id","Session.ActiveEquipment","Session.ActiveEquipmentTags","Contract.SubmissionScore","Contract.Favor" },StringComparer.Ordinal);
   static readonly HashSet<string> functions=new HashSet<string>(new[]{"random","round","World.Clothing.LeastRisqueWorn","UI.Pressed","Equipment.ActiveHasTag","Equipment.PreparedHasTag","Equipment.IsPrepared","Contract.KinkEnabled"},StringComparer.Ordinal);
   public Builder(string file,IReadOnlyDictionary<string,StoryBindingKind> symbols) { this.file=file; this.symbols=symbols; }
   StorySource At(StoryToken token)=>new StorySource(file,token.line,token.column,current.kind+" "+current.name);
   StorySourceException Error(StoryToken token,string message)=>new StorySourceException(At(token),message);
   public CompiledStoryProgram Compile(List<StoryAstDeclaration> declarations) {
    var entries=new List<StoryEntry>(); var ids=new HashSet<string>(StringComparer.Ordinal);
    foreach(var declaration in declarations) {
     current=declaration; locals.Clear();
     if(!ids.Add(declaration.kind+":"+declaration.name)) throw Error(declaration.token,"Duplicate "+declaration.kind+" '"+declaration.name+"'.");
     var entry=new StoryEntry {name=declaration.name,kind=declaration.kind,start=instructions.Count};
     if(declaration.kind=="card") {
      var card=new CardDefinition {Id=declaration.name,Tags=declaration.tags.ToArray(),BaseWeight=declaration.weight,Progress=declaration.progress,MaxUsesPerSession=declaration.maxUses,MinCardsBetweenUses=declaration.cooldown,ProgramEntry=declaration.name,Source=At(declaration.token)};
      card.Requirements=declaration.requirements.Select(r=> {
       if(!IsPredicate(r))throw Error(r.token,"A card requirement must be a boolean predicate.");
       return new CardRequirement {Expression=Expression(r),Description=Describe(r),Source=At(r.token)};
      }).ToArray();
      CardCatalog.Validate(card);cards.Add(card);
     }
     Emit(declaration.body); Add(StoryCode.Return,declaration.token); entry.end=instructions.Count; entries.Add(entry);
    }
    foreach(var instruction in instructions.Where(i=>i.code==StoryCode.Play))
     if(!entries.Any(e=>e.kind=="card" && e.name==instruction.name)) throw new StorySourceException(instruction.source,"Unknown card declaration '"+instruction.name+"'.");
    foreach(var instruction in instructions.Where(i=>i.code==StoryCode.Call && i.name=="Director.Execute")) {
     var argument=instruction.arguments.Single(a=>a.name=="card");var atoms=expressions[argument.expression].atoms;
     if(atoms.Length==1 && (atoms[0].code==StoryAtomCode.Symbol || atoms[0].code==StoryAtomCode.String) && !cards.Any(c=>c.Id==atoms[0].text))throw new StorySourceException(instruction.source,"Unknown explicit card '"+atoms[0].text+"'.");
    }
    return new CompiledStoryProgram {name=System.IO.Path.GetFileNameWithoutExtension(file),instructions=instructions.ToArray(),expressions=expressions.ToArray(),entries=entries.ToArray(),cards=cards.ToArray(),bindings=bindings.ToArray(),dialogue=dialogue.Values.ToArray()};
   }
   StoryInstruction Add(StoryCode code,StoryToken token,string name=null) {
    var instruction=new StoryInstruction {code=code,name=name,source=At(token)}; instructions.Add(instruction); return instruction;
   }
   void Emit(IEnumerable<StoryAstNode> nodes) { foreach(var node in nodes) Emit(node); }
   void Emit(StoryAstNode node) {
    switch(node.kind) {
     case "call":Call(node);break;
     case "let":case "set": {
      if(node.kind=="set" && !locals.Contains(node.name) && !Writable(node.name)) throw Error(node.token,"Unknown or read-only state '"+node.name+"'.");
      if(node.kind=="let" && readable.Contains(node.name)) throw Error(node.token,"A local cannot replace state '"+node.name+"'.");
      var expression=node.expression;
      if(node.add) {
       var plus=new StoryAstExpression {op="+",token=node.token}; plus.args.Add(new StoryAstExpression {op="name",token=new StoryToken("id",node.name,node.token.line,node.token.column)}); plus.args.Add(expression); expression=plus;
      }
      var instruction=Add(StoryCode.Store,node.token,node.name); instruction.expression=Expression(expression);
      if(node.kind=="let") locals.Add(node.name); break;
     }
     case "if": {
      var branch=Add(StoryCode.JumpFalse,node.token); branch.expression=Expression(node.expression); Emit(node.body);
      var end=Add(StoryCode.Jump,node.token); branch.jump=instructions.Count; Emit(node.otherwise); end.jump=instructions.Count; break;
     }
     case "while": {
      int start=instructions.Count; var branch=Add(StoryCode.JumpFalse,node.token); branch.expression=Expression(node.expression);
      branch.completionLoop=ContainsCompletion(node.expression);if(branch.completionLoop && node.expression.args.Count==2)branch.completionTarget=Expression(node.expression.args[1]);
      Emit(node.body); Add(StoryCode.Jump,node.token).jump=start; branch.jump=instructions.Count; break;
     }
     case "repeat": {
      int start=instructions.Count,id=loopId++; var begin=Add(StoryCode.RepeatBegin,node.token); begin.expression=Expression(node.expression); begin.loop=id;
      Emit(node.body); var end=Add(StoryCode.RepeatEnd,node.token); end.jump=start+1; end.loop=id; begin.jump=instructions.Count; break;
     }
     case "play":Add(StoryCode.Play,node.token,node.name);break;
     case "phase":Add(StoryCode.EnterPhase,node.token,node.name); Emit(node.body); Add(StoryCode.LeavePhase,node.token,node.name);break;
     case "race": {
      var race=Add(StoryCode.WaitAny,node.token,node.name); race.arguments=node.arguments.Select(a=>new StoryCallArgument {expression=Expression(a.expression)}).ToArray(); locals.Add(node.name); break;
     }
     default:throw Error(node.token,"Unknown source statement '"+node.kind+"'.");
    }
   }
   static bool ContainsCompletion(StoryAstExpression node)=>node.op=="name" && (node.token.text=="Completion" || node.token.text=="Session.PhaseCompletion") || node.args.Any(ContainsCompletion);
   static bool IsPredicate(StoryAstExpression node) {
    if(node.op=="function")return new[]{"Equipment.ActiveHasTag","Equipment.PreparedHasTag","Equipment.IsPrepared","Contract.KinkEnabled"}.Contains(node.token.text);
    if(node.op=="name")return node.token.text.StartsWith("World.Clothing.",StringComparison.Ordinal) && node.token.text!="World.Clothing.Count";
    if(node.op=="&&" || node.op=="||" || node.op=="not")return node.args.All(IsPredicate);
    if(new[]{"==","!=","<","<=",">",">=","bool"}.Contains(node.op))return !ContainsRandom(node);
    return false;
   }
   static bool ContainsRandom(StoryAstExpression node)=>node.op=="function" && (node.token.text=="random" || node.token.text=="UI.Pressed") || node.args.Any(ContainsRandom);
   static string Describe(StoryAstExpression node) {
    if(node.op=="function")return node.token.text+"("+string.Join(", ",node.args.Select(Describe))+")";
    if(node.op=="string")return "'"+node.token.text+"'";
    if(node.args.Count==2)return Describe(node.args[0])+" "+node.op+" "+Describe(node.args[1]);
    if(node.args.Count==1)return node.op+" "+Describe(node.args[0]);return node.token.text;
   }
   static bool Writable(string name) => new[]{"Session.Mood","Session.IntensityMultiplier","Session.PhaseCompletion","World.Posture","World.Hands","World.Ankles","World.Oral","World.Anal","World.Genital"}.Contains(name) || readable.Contains(name) && name.StartsWith("World.Clothing.",StringComparison.Ordinal) && name!="World.Clothing.Count";
   void Call(StoryAstNode node) {
    var spec=StoryVocabulary.Require(node.name,At(node.token));
    if(spec.setupOnly && current.kind!="setup") throw Error(node.token,node.name+" belongs in the setup block.");
    if(!node.awaited && !spec.instant && !spec.canRunUnawaited) throw Error(node.token,node.name+" requires await; its contract does not allow a background operation.");
    if(node.name.StartsWith("Lara.",StringComparison.Ordinal))bindings.Add(new StoryBindingUse {instruction=instructions.Count,symbol="Lara",kind=StoryBindingKind.Performer,source=At(node.token)});
    if(node.name.StartsWith("PlayerBody.",StringComparison.Ordinal))bindings.Add(new StoryBindingUse {instruction=instructions.Count,symbol="PlayerBody",kind=StoryBindingKind.PlayerBody,source=At(node.token)});
    var output=new List<StoryCallArgument>(); var assigned=new HashSet<string>(StringComparer.Ordinal); int positional=0; bool named=false;
    foreach(var argument in node.arguments) {
     string name=argument.name;
     if(name==null) { if(named) throw Error(node.token,"Positional arguments must precede named arguments."); if(positional>=spec.parameters.Length) throw Error(node.token,"Too many arguments for "+node.name+"."); name=spec.parameters[positional++]; }
     else named=true;
     if(!spec.parameters.Contains(name)) throw Error(node.token,"Unknown argument '"+name+"' for "+node.name+".");
     if(!assigned.Add(name)) throw Error(node.token,"Duplicate argument '"+name+"'.");
     if(spec.bindingParameters.TryGetValue(name,out var kind)) {
      if(argument.expression.op!="name") throw Error(argument.expression.token,"Expected a symbolic "+kind+" binding for '"+name+"'.");
      string symbol=argument.expression.token.text;
      if(symbols!=null && (!symbols.TryGetValue(symbol,out var actual) || actual!=kind))
       throw Error(argument.expression.token,"Unknown or wrong-type "+kind+" symbol '"+symbol+"'; expected "+kind+" binding.");
      bindings.Add(new StoryBindingUse {instruction=instructions.Count,symbol=symbol,kind=kind,source=At(argument.expression.token)});
     }
     if(node.name=="Popup.Show" && (argument.expression.op!="name" || !new[]{"Kinks","Equipment","Clothing"}.Contains(argument.expression.token.text))) throw Error(argument.expression.token,"Popup.Show expects Kinks, Equipment or Clothing.");
     if(node.name=="Lara.Say") {
      if(argument.expression.op!="string") throw Error(argument.expression.token,"Lara.Say requires a readable literal transcript.");
      string transcript=argument.expression.token.text,id=StoryDialogue.IdFor(transcript);
      if(!dialogue.ContainsKey(id)) dialogue.Add(id,new StoryDialogue {id=id,transcript=transcript,source=At(argument.expression.token)});
     }
     output.Add(new StoryCallArgument {name=name,expression=Expression(argument.expression)});
    }
    foreach(var required in spec.parameters.Take(spec.required)) if(!assigned.Contains(required)) throw Error(node.token,"Missing argument '"+required+"' for "+node.name+".");
    var instruction=Add(StoryCode.Call,node.token,node.name); instruction.awaited=node.awaited; instruction.arguments=output.ToArray();
   }
   int Expression(StoryAstExpression source) {
    var atoms=new List<StoryAtom>(); CompileExpression(source,atoms); expressions.Add(new StoryExpression {atoms=atoms.ToArray()}); return expressions.Count-1;
   }
   void CompileExpression(StoryAstExpression node,List<StoryAtom> atoms) {
    if(node.op=="&&" || node.op=="||") {
     CompileExpression(node.args[0],atoms); var skip=new StoryAtom {code=node.op=="&&" ? StoryAtomCode.AndSkip : StoryAtomCode.OrSkip}; atoms.Add(skip); CompileExpression(node.args[1],atoms); skip.count=atoms.Count; return;
    }
    foreach(var child in node.args) CompileExpression(child,atoms);
    StoryAtomCode code;
    switch(node.op) {
     case "number":code=StoryAtomCode.Number;break; case "string":code=StoryAtomCode.String;break; case "bool":code=StoryAtomCode.Bool;break;
     case "name":
      string name=node.token.text;
      if((name.StartsWith("World.",StringComparison.Ordinal) || name.StartsWith("Session.",StringComparison.Ordinal) || name.StartsWith("Contract.",StringComparison.Ordinal) || name.StartsWith("Profile.",StringComparison.Ordinal) || name.StartsWith("Card.",StringComparison.Ordinal)) && !readable.Contains(name)) throw Error(node.token,"Unknown state value '"+name+"'.");
      code=readable.Contains(name) || locals.Contains(name) ? StoryAtomCode.Read : StoryAtomCode.Symbol; break;
     case "function":
      if(!functions.Contains(node.token.text))throw Error(node.token,"Unknown value function '"+node.token.text+"'.");
      int arity=node.token.text=="World.Clothing.LeastRisqueWorn" ? 0 : 1;
      if(node.args.Count!=arity)throw Error(node.token,node.token.text+" requires "+arity+" arguments.");
      code=StoryAtomCode.Function;break;
     case "list":code=StoryAtomCode.List;break; case "+":code=StoryAtomCode.Add;break; case "-":code=StoryAtomCode.Subtract;break;
     case "*":code=StoryAtomCode.Multiply;break; case "/":code=StoryAtomCode.Divide;break; case "..":code=StoryAtomCode.Range;break;
     case "not":code=StoryAtomCode.Not;break; case "negate":code=StoryAtomCode.Negate;break;
     case "==":code=StoryAtomCode.Equal;break; case "!=":code=StoryAtomCode.NotEqual;break;
     case "<":code=StoryAtomCode.Less;break; case "<=":code=StoryAtomCode.LessEqual;break; case ">":code=StoryAtomCode.Greater;break; case ">=":code=StoryAtomCode.GreaterEqual;break;
     default:throw Error(node.token,"Unknown expression operation '"+node.op+"'.");
    }
    atoms.Add(new StoryAtom {code=code,text=node.token.text,number=node.number,count=node.args.Count});
   }
  }
 }
}
