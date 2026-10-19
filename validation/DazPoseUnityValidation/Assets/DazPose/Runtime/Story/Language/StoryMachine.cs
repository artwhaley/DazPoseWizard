using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace DazPose.Story.Language {
 public interface IStoryPhaseObserver {void LeavePhase(string phase);}
 public interface IStoryExecutionHost {
  StoryData Read(string key);
  void Write(string key,StoryData value);
  Task<StoryData> InvokeAsync(StoryCommand command,CancellationToken cancellation);
  Task<StoryData> WaitAnyAsync(StoryData[] events,CancellationToken cancellation);
  StoryData Function(string name,StoryData[] arguments);
  Task YieldAsync(CancellationToken cancellation);
  void CancelOperations();
  void Trace(StorySource source,string message);
 }
 // Executes domain instructions. The host adapts typed commands to PerformanceRun, never renderer/bone hacks.
 public sealed class StoryMachine {
  sealed class Lease { public StorySource source; public string verb; }
  readonly CompiledStoryProgram program;
  readonly IStoryExecutionHost host;
  readonly Dictionary<string,Lease> channels=new Dictionary<string,Lease>(StringComparer.Ordinal);
  readonly List<Task> children=new List<Task>();
  readonly TaskCompletionSource<Exception> failure=new TaskCompletionSource<Exception>();
  readonly TaskCompletionSource<bool> cancelled=new TaskCompletionSource<bool>();
  CancellationTokenSource lifetime;
  int executed,cardDepth;
  double? completionTarget;
  readonly StorySessionRandom random;
  readonly IStoryRehearsalObserver rehearsalObserver;
  readonly List<StoryCallFrameSnapshot> entryFrames=new List<StoryCallFrameSnapshot>();
  StoryInstructionSnapshot lastPausedSnapshot; StoryInstruction lastPausedInstruction; Dictionary<string,StoryData> lastPausedLocals;
  public StoryDirector Director {get;}
  public CardExecutionContext CardContext {get;private set;}
  string stopBeforePhase;
  public string ReachedCheckpoint {get;private set;}
  sealed class CheckpointReached : Exception { }
  public StoryMachine(CompiledStoryProgram program,IStoryExecutionHost host,StorySessionRandom random=null,IStoryRehearsalObserver rehearsalObserver=null) {
   this.program=program;this.host=host;this.random=random ?? new StorySessionRandom(0);this.rehearsalObserver=rehearsalObserver;
   Director=new StoryDirector(program,index=>Evaluate(index,new Dictionary<string,StoryData>(StringComparer.Ordinal)),Read,this.random,message=>host.Trace(null,message),host is IStoryCardEligibility eligibility ? new Func<CardDefinition,string>(eligibility.ResourceIssue) : null);
  }
  public async Task RunAsync(string sequence,CancellationToken cancellation=default,string stopBeforePhase=null) {
   if(!string.IsNullOrEmpty(stopBeforePhase)) StoryPlaybackWindow.BeforePhase(program,sequence,stopBeforePhase);
   this.stopBeforePhase=stopBeforePhase;
   if(lifetime!=null) throw new InvalidOperationException("A story machine runs once.");
   lifetime=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
   using(lifetime.Token.Register(()=> { cancelled.TrySetResult(true); host.CancelOperations(); })) {
    try {
     host.Trace(null,"Story started: "+sequence+".");
     var setup=program.entries.SingleOrDefault(e=>e.kind=="setup" && e.name==sequence);
     if(setup!=null) await Entry(setup,0);
     try { await Entry(program.RequireEntry(sequence),0); }
     catch(CheckpointReached) { ReachedCheckpoint=stopBeforePhase; }
     await AwaitOwned(Task.WhenAll(children.ToArray())); Check();
     host.Trace(null,ReachedCheckpoint==null ? "Story completed: "+sequence+"." : "Authoring preview complete. Stopped before phase "+ReachedCheckpoint+"; later phases did not run.");
    } catch(Exception error) {
     bool failed=failure.Task.IsCompleted;
     lifetime.Cancel();host.CancelOperations();
     host.Trace(null,failed || !(error is OperationCanceledException) ? "Story failed: "+(failed ? failure.Task.Result.Message : error.Message) : "Story stopped. Pending actions and waits were cancelled.");
     if(failed) throw failure.Task.Result;
     throw;
    } finally {
     // Every accepted background result is consumed; the host cancellation settles its scoped operations.
     try { await Task.WhenAll(children.ToArray()); } catch { }
     lifetime.Dispose();
    }
   }
  }
  void Check() { if(failure.Task.IsCompleted) throw failure.Task.Result; lifetime.Token.ThrowIfCancellationRequested(); }
  async Task AwaitOwned(Task task) {
   var winner=await Task.WhenAny(task,failure.Task,cancelled.Task);
   if(failure.Task.IsCompleted) throw failure.Task.Result;
   if(winner==cancelled.Task) throw new OperationCanceledException(lifetime.Token);
   await task;
  }
  async Task Entry(StoryEntry entry,int depth) {
   if(depth>128) throw new InvalidOperationException("Recursive card calls exceeded 128 active entries.");
   var locals=new Dictionary<string,StoryData>(StringComparer.Ordinal); var repeats=new Dictionary<int,int>();
   var scopeChildren=new List<Task>();var loops=new Dictionary<int,double>();var outerTarget=completionTarget;
   entryFrames.Add(new StoryCallFrameSnapshot(entry.name,entry.kind,CardContext?.CardId,CardContext?.InvocationNumber ?? 0));
   try {
   for(int pc=entry.start;pc<entry.end;pc++) {
    Check(); if(++executed>1000000) throw new InvalidOperationException("Story instruction limit reached; check a non-advancing loop.");
    if(executed%256==0) await host.YieldAsync(lifetime.Token);
    var instruction=program.instructions[pc];
    try {
     int instructionPc=pc;var rehearsalSnapshot=rehearsalObserver!=null ? RehearsalSnapshot(entry,instructionPc,instruction,locals,repeats) : null;
     if(rehearsalObserver!=null) {lastPausedSnapshot=rehearsalSnapshot;lastPausedInstruction=instruction;lastPausedLocals=new Dictionary<string,StoryData>(locals,StringComparer.Ordinal);await rehearsalObserver.BeforeInstructionAsync(rehearsalSnapshot,lifetime.Token);Check();}
     switch(instruction.code) {
      case StoryCode.Call: {
       var command=new StoryCommand {Verb=instruction.name,Awaited=instruction.awaited,Source=instruction.source};
       foreach(var argument in instruction.arguments) command.Arguments.Add(argument.name,Evaluate(argument.expression,locals));
       var task=Start(command,scopeChildren); if(instruction.awaited || StoryVocabulary.Require(command.Verb,command.Source).instant) await AwaitOwned(task); break;
      }
      case StoryCode.Store: {
       var value=Evaluate(instruction.expression,locals);
       if(instruction.name.Contains(".")) host.Write(instruction.name,value); else locals[instruction.name]=value;
       host.Trace(instruction.source,"Set "+instruction.name+" to "+value+"."); break;
      }
      case StoryCode.Jump:pc=instruction.jump-1;break;
      case StoryCode.JumpFalse: {
       bool evaluated=Evaluate(instruction.expression,locals).Boolean;bool condition=evaluated;bool forced=false;
       if(rehearsalObserver!=null) {var decision=new StoryBranchSnapshot(entry.name,pc,instruction.source,ExpressionText(instruction.expression),evaluated,evaluated,false);if(rehearsalObserver.TryOverrideBranch(decision,out var overridden)){condition=overridden;forced=true;}rehearsalObserver.ObserveBranch(new StoryBranchSnapshot(entry.name,pc,instruction.source,ExpressionText(instruction.expression),evaluated,condition,forced));}
       host.Trace(instruction.source,"Condition is "+(condition ? "true; enter this branch." : "false; skip this branch."));
       if(instruction.completionLoop) {
        if(condition)loops[pc]=instruction.completionTarget>=0 ? Evaluate(instruction.completionTarget,locals).Numeric : 100;else loops.Remove(pc);
        completionTarget=loops.Count>0 ? (double?)loops.Values.Last() : outerTarget;
       }
       if(!condition) pc=instruction.jump-1; break;
      }
      case StoryCode.RepeatBegin: {
       double count=Evaluate(instruction.expression,locals).Numeric;
       if(count<0 || count>100000 || count!=Math.Floor(count)) throw new InvalidOperationException("repeat requires a whole count from 0 to 100000.");
       repeats[instruction.loop]=(int)count; if(count==0) pc=instruction.jump-1; break;
      }
      case StoryCode.RepeatEnd: if(--repeats[instruction.loop]>0) pc=instruction.jump-1; else repeats.Remove(instruction.loop);break;
      case StoryCode.Play:host.Trace(instruction.source,"Play card "+instruction.name+"."); await ExecuteCard(StoryProgramLinker.ResolveCard(program,instruction.name,instruction.source?.unit,instruction.source),ReadOptional("Session.IntensityMultiplier",new StoryData(1d)).Numeric,false,instruction.source);break;
      case StoryCode.EnterPhase:if(instruction.name==stopBeforePhase)throw new CheckpointReached();host.Write("Session.Phase",new StoryData(instruction.name)); host.Write("Session.PhaseCompletion",new StoryData(0d)); host.Trace(instruction.source,"Enter phase "+instruction.name+" at zero completion.");break;
      case StoryCode.LeavePhase:if(host is IStoryPhaseObserver phaseObserver)phaseObserver.LeavePhase(instruction.name);host.Trace(instruction.source,"Leave phase "+instruction.name+".");break;
      case StoryCode.WaitAny: {
       var events=instruction.arguments.Select(a=>Evaluate(a.expression,locals)).ToArray();
       var waitSnapshot=new StoryWaitSnapshot(entry.name,pc,instruction.source,events);
       rehearsalObserver?.ObserveWaitAny(waitSnapshot);
       if(rehearsalObserver!=null && rehearsalObserver.TryInjectEvent(waitSnapshot,out var response)) {
        if(response.Kind!=StoryDataKind.Text || !events.Any(e=>e.Kind==StoryDataKind.Text && e.String==response.String))
         throw new StorySourceException(instruction.source,"Rehearsal event '"+response+"' is not one of the selected WaitAny inputs ["+string.Join(", ",events.Select(e=>e.ToString()))+"].");
        locals[instruction.name]=response;
       }
       else {var wait=host.WaitAnyAsync(events,lifetime.Token);await AwaitOwned(wait);locals[instruction.name]=await wait;} break;
      }
      case StoryCode.Return:pc=entry.end;break;
      default:throw new InvalidOperationException("Unknown compiled instruction "+instruction.code+".");
     }
     if(rehearsalObserver!=null)rehearsalObserver.AfterInstruction(RehearsalSnapshot(entry,instructionPc,instruction,locals,repeats));
    } catch(CheckpointReached) { throw; }
    catch(OperationCanceledException) { throw; }
    catch(StorySourceException) { throw; }
    catch(Exception error) { throw new StorySourceException(instruction.source,error.Message,error); }
   }
   await AwaitOwned(Task.WhenAll(scopeChildren.ToArray()));completionTarget=outerTarget;
   } finally {entryFrames.RemoveAt(entryFrames.Count-1);}
  }
  Task Start(StoryCommand command,List<Task> scopeChildren) {
   var contract=StoryVocabulary.Require(command.Verb,command.Source);
   if(!command.Awaited && !contract.instant && !contract.canRunUnawaited)throw new StorySourceException(command.Source,command.Verb+" requires await; this runtime contract also applies to programs created by other frontends.");
   string channel=contract.channel;
   // Dissolve also needs normal material ownership. Speech and an accepted pose/gesture may overlap.
   var required=command.Verb.StartsWith("Lara.Dissolve",StringComparison.Ordinal) ? new[]{"Body","Visual"} : channel==null ? Array.Empty<string>() : new[]{channel};
   foreach(string key in required) if(channels.TryGetValue(key,out var active))
    throw new StorySourceException(command.Source,"Runtime conflict: "+command.Verb+" needs "+key+" while "+active.verb+" from "+active.source+" is still running. Await it before starting this command.");
   var lease=new Lease {source=command.Source,verb=command.Verb}; foreach(string key in required) channels.Add(key,lease);
   host.Trace(command.Source,"Begin: "+StoryCommandDescription.Describe(command)+(command.Awaited ? "; wait for completion." : contract.instant ? "." : "; continue concurrently."));
   var task=Observe(command,required,lease); children.Add(task);scopeChildren.Add(task);return task;
  }
  async Task Observe(StoryCommand command,string[] required,Lease lease) {
   try { if(command.Verb.StartsWith("Director.",StringComparison.Ordinal))await Direct(command);else await host.InvokeAsync(command,lifetime.Token); host.Trace(command.Source,"Finished: "+StoryCommandDescription.Describe(command)+"."); }
   catch(OperationCanceledException) when(lifetime.IsCancellationRequested) { throw; }
   catch(Exception error) {
    var contextual=error as StorySourceException ?? new StorySourceException(command.Source,error.Message,error);
    failure.TrySetResult(contextual); lifetime.Cancel(); throw contextual;
   } finally { foreach(string key in required) if(channels.TryGetValue(key,out var owner) && ReferenceEquals(owner,lease)) channels.Remove(key); }
  }

  StoryInstructionSnapshot RehearsalSnapshot(StoryEntry entry,int pc,StoryInstruction instruction,Dictionary<string,StoryData> locals,Dictionary<int,int> repeats) {
   var state=host is IStoryRehearsalStateReader reader ? reader.SnapshotState() : new Dictionary<string,StoryData>(StringComparer.Ordinal);
   return new StoryInstructionSnapshot(entry.name,entry.kind,pc,instruction,instruction.expression>=0 ? ExpressionText(instruction.expression) : null,entryFrames,locals,state,repeats);
  }
  string ExpressionText(int index) => string.Join(" ",program.expressions[index].atoms.Select(a=>a.code==StoryAtomCode.Number ? a.number.ToString(System.Globalization.CultureInfo.InvariantCulture) : a.text ?? a.code.ToString()));
  StoryData ReadOptional(string key,StoryData fallback) {try{return host.Read(key);}catch(KeyNotFoundException){return fallback;}}
  StoryData Read(string key) {
   if(key=="Card.Intensity")return new StoryData(CardContext?.IntensityMultiplier ?? throw new InvalidOperationException("Card.Intensity is available only inside a card."));
   if(key=="Card.Invocation")return new StoryData((double)(CardContext?.InvocationNumber ?? throw new InvalidOperationException("Card.Invocation is available only inside a card.")));
   if(key=="Card.Id")return new StoryData(CardContext?.CardId ?? throw new InvalidOperationException("Card.Id is available only inside a card."));
   return host.Read(key);
  }
  StoryData Function(string name,StoryData[] args) {
   if(name=="random") {
    if(args.Length!=1 || args[0].Kind!=StoryDataKind.Range)throw new InvalidOperationException("Use random(min..max).");
    double min=args[0].Number,max=args[0].Upper;if(min!=Math.Floor(min) || max!=Math.Floor(max) || min>max || min<int.MinValue || max>=int.MaxValue)throw new InvalidOperationException("random requires ordered integer bounds.");
    return new StoryData((double)random.CardLocal.Next((int)min,(int)max+1));
   }
   return host.Function(name,args);
  }
  void PublishDirector() {if(host is IStoryDirectorObserver observer)observer.ObserveDirector(Director.Snapshot,CardContext);}
  CardSelectionRequest BuildDirectorRequest(StoryCommand command) {
   var request=new CardSelectionRequest {Phase=ReadOptional("Session.Phase",new StoryData("Setup")).String,Intensity=command.Number("intensity",ReadOptional("Session.IntensityMultiplier",new StoryData(1d)).Numeric),Completion=ReadOptional("Session.PhaseCompletion",new StoryData(0d)).Numeric,CompletionTarget=completionTarget ?? 100,RequireProgress=completionTarget.HasValue};
   if(command.Verb=="Director.Execute")request.ExplicitCardId=StoryProgramLinker.ResolveCard(program,command.Require("card").String,command.Source?.unit,command.Source).Id;
   if(command.Arguments.TryGetValue("tags",out var tags))request.RequiredTags=tags.Items ?? throw new InvalidOperationException("Director tags require a list.");
   if(command.Arguments.TryGetValue("equipment",out var equipment)) {
    request.EquipmentRequested=true;request.EquipmentIds=equipment.Items ?? throw new InvalidOperationException("Director equipment requires a list.");
    request.EquipmentTags=request.EquipmentIds.SelectMany(id=>host.Function("Equipment.Tags",new[]{new StoryData(id)}).Items ?? throw new InvalidOperationException("Equipment tags must be a list.")).Distinct(StringComparer.Ordinal).ToArray();
   }
   return request;
  }
  public DirectorSnapshot InspectDirector(StoryInstructionSnapshot snapshot) {
   if(snapshot==null||!ReferenceEquals(snapshot,lastPausedSnapshot)||lastPausedInstruction==null||lastPausedInstruction.code!=StoryCode.Call||lastPausedInstruction.name==null||!lastPausedInstruction.name.StartsWith("Director.",StringComparison.Ordinal))
    throw new InvalidOperationException("Director inspection requires the current paused Director call instruction.");
   var command=new StoryCommand {Verb=lastPausedInstruction.name,Awaited=lastPausedInstruction.awaited,Source=lastPausedInstruction.source};
   foreach(var argument in lastPausedInstruction.arguments) {
    var atoms=program.expressions[argument.expression].atoms;
    if(atoms.Any(a=>a.code==StoryAtomCode.Function&&a.text=="random"))throw new InvalidOperationException("Director inspection is unavailable for a request containing random(); inspection must not advance the session RNG.");
    command.Arguments.Add(argument.name,Evaluate(argument.expression,lastPausedLocals));
   }
   return Director.Inspect(BuildDirectorRequest(command));
  }
  async Task Direct(StoryCommand command) {
   var request=BuildDirectorRequest(command);
   CardDefinition selected;
   string forcedCardId=null;
   if(rehearsalObserver!=null)forcedCardId=rehearsalObserver.ForcedDirectorCardId(new StoryDirectorCallSnapshot(command.Source,StoryRehearsalController.DirectorKey(command.Source),request));
   try {selected=Director.Select(request,forcedCardId);rehearsalObserver?.ObserveDirectorSelection(new StoryDirectorSelectionSnapshot(selected?.Id,forcedCardId!=null,forcedCardId,Director.Snapshot.Candidates));}
   catch {if(forcedCardId!=null)rehearsalObserver?.ObserveDirectorSelection(new StoryDirectorSelectionSnapshot(null,true,forcedCardId,Director.Snapshot.Candidates));throw;}
   finally {PublishDirector();}
   if(selected==null)return;
   try {
    await ExecuteCard(selected,request.Intensity,true,command.Source);
    double progress=Director.Complete(selected,ReadOptional("Contract.PacingMultiplier",new StoryData(1d)).Numeric);
    host.Write("Session.PhaseCompletion",new StoryData(ReadOptional("Session.PhaseCompletion",new StoryData(0d)).Numeric+progress));
    PublishDirector();
   }catch {Director.Snapshot.Outcome="Failed or cancelled; no progress credit.";PublishDirector();throw;}
  }
  async Task ExecuteCard(CardDefinition card,double intensity,bool selected,StorySource callSite=null) {
   if(host is IStoryCardEligibility eligibility) {string issue=eligibility.ResourceIssue(card);if(issue!=null)throw new StorySourceException(callSite ?? card.Source,issue);}
   if(++cardDepth>128)throw new InvalidOperationException("Card recursion exceeded 128 active entries.");
   var previous=CardContext;
   CardContext=new CardExecutionContext {CardId=card.Id,Phase=ReadOptional("Session.Phase",new StoryData("Setup")).String,SessionSeed=random.Seed,Random=random,IntensityMultiplier=intensity,InvocationNumber=Director.History.For(card.Id).TimesUsed+1,PreparedEquipment=ReadOptional("Session.PreparedEquipment",new StoryData(Array.Empty<string>())).Items,ActiveEquipment=ReadOptional("Session.ActiveEquipment",new StoryData(Array.Empty<string>())).Items};
   PublishDirector();host.Trace(card.Source,"Enter card "+card.Id+"; invocation "+CardContext.InvocationNumber+"; intensity "+intensity+".");
   try {await Entry(program.RequireEntry(card.ProgramEntry,"card"),cardDepth);Check();host.Trace(card.Source,"Card body completed: "+card.Id+".");}
   finally {CardContext=previous;cardDepth--;PublishDirector();}
  }
  StoryData Evaluate(int index,Dictionary<string,StoryData> locals) {
   var stack=new Stack<StoryData>(); var atoms=program.expressions[index].atoms;
   for(int pc=0;pc<atoms.Length;pc++) {
    var atom=atoms[pc]; StoryData a,b;
    switch(atom.code) {
     case StoryAtomCode.Number:stack.Push(new StoryData(atom.number));break;
     case StoryAtomCode.String:case StoryAtomCode.Symbol:stack.Push(new StoryData(atom.text));break;
     case StoryAtomCode.Bool:stack.Push(new StoryData(atom.text=="true"));break;
     case StoryAtomCode.Read:stack.Push(locals.TryGetValue(atom.text,out var local) ? local : Read(atom.text));break;
     case StoryAtomCode.AndSkip:case StoryAtomCode.OrSkip:
      bool value=stack.Peek().Boolean;
      if(atom.code==StoryAtomCode.AndSkip && !value || atom.code==StoryAtomCode.OrSkip && value) pc=atom.count-1; else stack.Pop();break;
     case StoryAtomCode.Negate:stack.Push(new StoryData(-stack.Pop().Numeric));break;
     case StoryAtomCode.Not:stack.Push(new StoryData(!stack.Pop().Boolean));break;
     case StoryAtomCode.Function:case StoryAtomCode.List: {
      var args=new StoryData[atom.count]; for(int i=args.Length-1;i>=0;i--) args[i]=stack.Pop();
      stack.Push(atom.code==StoryAtomCode.List ? new StoryData(args.Select(v=>v.String).ToArray()) : Function(atom.text,args)); break;
     }
     default:
      b=stack.Pop(); a=stack.Pop();
      switch(atom.code) {
       case StoryAtomCode.Add:stack.Push(new StoryData(a.Numeric+b.Numeric));break;
       case StoryAtomCode.Subtract:stack.Push(new StoryData(a.Numeric-b.Numeric));break;
       case StoryAtomCode.Multiply:stack.Push(new StoryData(a.Numeric*b.Numeric));break;
       case StoryAtomCode.Divide:stack.Push(new StoryData(a.Numeric/b.Numeric));break;
       case StoryAtomCode.Range:stack.Push(new StoryData(a.Numeric,b.Numeric));break;
       case StoryAtomCode.Equal:case StoryAtomCode.NotEqual:
        bool equal=a.Kind==b.Kind && (a.Kind==StoryDataKind.Text ? a.Text==b.Text : a.Kind==StoryDataKind.Number || a.Kind==StoryDataKind.Bool ? a.Number==b.Number : false);
        stack.Push(new StoryData(atom.code==StoryAtomCode.Equal ? equal : !equal));break;
       case StoryAtomCode.Less:stack.Push(new StoryData(a.Numeric<b.Numeric));break;
       case StoryAtomCode.LessEqual:stack.Push(new StoryData(a.Numeric<=b.Numeric));break;
       case StoryAtomCode.Greater:stack.Push(new StoryData(a.Numeric>b.Numeric));break;
       case StoryAtomCode.GreaterEqual:stack.Push(new StoryData(a.Numeric>=b.Numeric));break;
       default:throw new InvalidOperationException("Invalid expression instruction "+atom.code);
      }
      break;
    }
   }
   if(stack.Count!=1) throw new InvalidOperationException("Malformed compiled expression stack."); return stack.Pop();
  }
 }
}