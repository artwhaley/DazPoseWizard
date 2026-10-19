using DazPose.Story.Language;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;

namespace DazPose.Story.Tests;

public sealed class RehearsalTests {
 sealed class Host : IStoryExecutionHost,IStoryRehearsalStateReader {
  public readonly Dictionary<string,StoryData> State=new() { ["Session.Mood"]=new(50d),["Session.Phase"]=new("Setup"),["Session.PhaseCompletion"]=new(0d),["Session.IntensityMultiplier"]=new(1d) };
  public readonly List<StoryCommand> Commands=new(); public readonly List<string> TraceLines=new(); public int WaitCalls,SnapshotCalls;
  public StoryData Read(string key)=>State[key]; public void Write(string key,StoryData value)=>State[key]=value;
  public Task<StoryData> InvokeAsync(StoryCommand command,CancellationToken cancellation){Commands.Add(command);return Task.FromResult(default(StoryData));}
  public Task<StoryData> WaitAnyAsync(StoryData[] events,CancellationToken cancellation){WaitCalls++;return Task.FromResult(new StoryData("UI.Pressed:Ready"));}
  public StoryData Function(string name,StoryData[] arguments)=>name=="UI.Pressed" ? new StoryData("UI.Pressed:"+arguments[0].String) : throw new InvalidOperationException(name);
  public Task YieldAsync(CancellationToken cancellation)=>Task.CompletedTask; public void CancelOperations(){}
  public void Trace(StorySource source,string message)=>TraceLines.Add(message);
  public IDictionary<string,StoryData> SnapshotState(){SnapshotCalls++;return new Dictionary<string,StoryData>(State,StringComparer.Ordinal);}
 }
 static CompiledStoryProgram Compile(string source)=>DazStoryCompiler.Compile(source,"rehearsal.dazstory");
 [Fact] public async Task DefaultAndContinuousObserverKeepCanonicalOutputIdentical() {
  const string source="sequence Test {\nlet count = 2\nrepeat count {\nSession.Mood += 1\n}\n}";
  var ordinaryHost=new Host();var ordinary=new StoryMachine(Compile(source),ordinaryHost,new StorySessionRandom(2026));await ordinary.RunAsync("Test");
  var rehearsalHost=new Host();var controller=new StoryRehearsalController();controller.Continue();
  var rehearsal=new StoryMachine(Compile(source),rehearsalHost,new StorySessionRandom(2026),controller);await rehearsal.RunAsync("Test");
  Assert.Equal(ordinaryHost.TraceLines,rehearsalHost.TraceLines);Assert.Equal(0,ordinaryHost.SnapshotCalls);Assert.True(rehearsalHost.SnapshotCalls>0);Assert.Equal(ordinaryHost.Commands.Select(c=>c.Verb),rehearsalHost.Commands.Select(c=>c.Verb));
  Assert.Equal(ordinaryHost.State["Session.Mood"].Numeric,rehearsalHost.State["Session.Mood"].Numeric);
 }
 [Fact] public void DirectorInspectionUsesCanonicalCandidatesWithoutMutatingSelectionState() {
  var program=Compile("card A\ntags [Warmup]\nprogress 10\n{\nwait 1s\n}\ncard B\ntags [Warmup]\nprogress 10\n{\nwait 1s\n}\nsequence Test {}\n");
  const int seed=177;var inspected=new StoryDirector(program,_=>default,_=>new StoryData("Free"),new StorySessionRandom(seed),_=>{});
  var control=new StoryDirector(program,_=>default,_=>new StoryData("Free"),new StorySessionRandom(seed),_=>{});var before=inspected.Snapshot;
  var result=inspected.Inspect(new CardSelectionRequest {RequiredTags=new[]{"Warmup"},Phase="Warmup"});
  Assert.Equal(new[]{"A","B"},result.Candidates.Where(c=>c.Eligible).Select(c=>c.Id));Assert.Equal(0,inspected.History.SelectionCount);Assert.Same(before,inspected.Snapshot);
  Assert.Equal(control.Select(new CardSelectionRequest {RequiredTags=new[]{"Warmup"},Phase="Warmup"}).Id,inspected.Select(new CardSelectionRequest {RequiredTags=new[]{"Warmup"},Phase="Warmup"}).Id);
 }
 [Fact] public async Task MachineInspectsPausedDirectorCallAndRejectsStaleSnapshot() {
  var program=Compile("card A\ntags [Warmup]\nprogress 10\n{\nwait 1s\n}\nsequence Test {\nawait Director.DrawAndExecute(tags: [Warmup])\n}");
  var controller=new StoryRehearsalController();var ready=Channel.CreateUnbounded<StoryInstructionSnapshot>();controller.InstructionReady+=s=>ready.Writer.TryWrite(s);
  var machine=new StoryMachine(program,new Host(),new StorySessionRandom(9),controller);using var cancel=new CancellationTokenSource();var run=machine.RunAsync("Test",cancel.Token);
  var paused=await ready.Reader.ReadAsync();var report=machine.InspectDirector(paused);Assert.Contains(report.Candidates,c=>c.Id=="A"&&c.Eligible);Assert.Equal(0,machine.Director.History.SelectionCount);
  controller.Step();var after=await ready.Reader.ReadAsync();Assert.Throws<InvalidOperationException>(()=>machine.InspectDirector(paused));
  cancel.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>run);
 }
 [Fact] public async Task StepGateStopsBeforeNextInstructionAndCancellationSettlesIt() {
  var program=Compile("sequence Test {\nlet value = 1\nSession.Mood += value\nSession.Mood += 2\n}");
  var snapshots=Channel.CreateUnbounded<StoryInstructionSnapshot>();var controller=new StoryRehearsalController();controller.InstructionReady+=s=>snapshots.Writer.TryWrite(s);
  var host=new Host();using var cancel=new CancellationTokenSource();var run=new StoryMachine(program,host,null,controller).RunAsync("Test",cancel.Token);
  var first=await snapshots.Reader.ReadAsync();Assert.Equal(StoryCode.Store,first.Code);Assert.Equal("Test",first.EntryName);Assert.Equal("sequence",first.EntryKind);Assert.Empty(first.Locals);
  Assert.False(run.IsCompleted);controller.Step();
  var second=await snapshots.Reader.ReadAsync();Assert.Equal("Session.Mood",second.Name);Assert.Equal(1,second.ProgramCounter-first.ProgramCounter);Assert.Equal(50,host.State["Session.Mood"].Numeric);Assert.Equal(1,second.Locals["value"].Numeric);
  controller.Step();var third=await snapshots.Reader.ReadAsync();Assert.Equal(1,third.ProgramCounter-second.ProgramCounter);Assert.Equal(51,host.State["Session.Mood"].Numeric);
  cancel.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>run);Assert.True(run.IsCompleted);
 }
 [Fact] public async Task NestedGlobalCardInstructionsCarryCallStackSnapshot() {
  var program=Compile("card Intro {\nawait Wait(1s)\n}\nsequence Test {\nawait Director.Execute(Intro)\n}");
  var controller=new StoryRehearsalController();controller.Continue();var host=new Host();var snapshots=new List<StoryInstructionSnapshot>();controller.InstructionReady+=snapshots.Add;
  await new StoryMachine(program,host,new StorySessionRandom(4),controller).RunAsync("Test");
  var nested=snapshots.First(s=>s.EntryName=="Intro");Assert.Equal("card",nested.EntryKind);Assert.Equal(2,nested.CallStack.Count);Assert.Equal("Intro",nested.CallStack[1].EntryName);
 }
 [Fact] public async Task InjectedWaitResponseBypassesHostAndAppearsInLocals() {
  var program=Compile("sequence Test {\nlet result = await wait any {\nUI.Pressed(Ready)\n}\nSession.Mood = 75\n}");
  var wait=program.instructions.Single(i=>i.code==StoryCode.WaitAny);var controller=new StoryRehearsalController();
  controller.SetEventResponse(StoryRehearsalController.SourceKey(wait.source,Array.IndexOf(program.instructions,wait)),new StoryData("UI.Pressed:Ready"));
  controller.Continue();var host=new Host();var seen=new List<StoryInstructionSnapshot>();controller.InstructionReady+=seen.Add;
  await new StoryMachine(program,host,null,controller).RunAsync("Test");
  Assert.Equal(0,host.WaitCalls);Assert.Contains(seen,s=>s.Locals.TryGetValue("result",out var value) && value.String=="UI.Pressed:Ready");
 }
 [Fact] public async Task InjectedWaitResponseOutsideSelectedEventsIsRejected() {
  var program=Compile("sequence Test {\nlet result = await wait any {\nUI.Pressed(Ready)\n}\n}");var wait=program.instructions.Single(i=>i.code==StoryCode.WaitAny);
  var controller=new StoryRehearsalController();controller.SetEventResponse(StoryRehearsalController.SourceKey(wait.source,Array.IndexOf(program.instructions,wait)),new StoryData("UI.Pressed:NotReady"));controller.Continue();
  var host=new Host();var error=await Assert.ThrowsAsync<StorySourceException>(()=>new StoryMachine(program,host,null,controller).RunAsync("Test"));
  Assert.Contains("not one of the selected WaitAny inputs",error.Message);Assert.Equal(0,host.WaitCalls);
 }
 [Fact] public async Task ExplicitBranchOverrideIsReportedAndChangesCanonicalPath() {
  var program=Compile("sequence Test {\nif true {\nSession.Mood = 99\n}\n}");var jump=program.instructions.Single(i=>i.code==StoryCode.JumpFalse);
  var controller=new StoryRehearsalController();controller.SetBranchOverride(StoryRehearsalController.BranchKey(jump.source,Array.IndexOf(program.instructions,jump)),false);
  StoryBranchSnapshot observed=null;controller.BranchObserved+=b=>observed=b;controller.Continue();var host=new Host();
  await new StoryMachine(program,host,null,controller).RunAsync("Test");Assert.Equal(50,host.State["Session.Mood"].Numeric);
  Assert.NotNull(observed);Assert.True(observed.EvaluatedCondition);Assert.False(observed.SelectedCondition);Assert.True(observed.Forced);
 }
 [Fact] public void ForcedEligibleSelectionConsumesSameOneDrawAsCanonicalSelection() {
  var program=Compile("card A\ntags [Warmup]\nprogress 10\n{\nwait 1s\n}\ncard B\ntags [Warmup]\nprogress 10\n{\nwait 1s\n}\nsequence Test {}\n");
  const int seed=177;var forcedRandom=new StorySessionRandom(seed);var forced=NewDirector(program,forcedRandom);var request=new CardSelectionRequest {RequiredTags=new[]{"Warmup"},Phase="Warmup"};
  Assert.Equal("B",forced.Select(request,"B").Id);Assert.Contains("FORCED",forced.Snapshot.Outcome);
  var nextAfterForced=forced.Select(request).Id;
  var baseline=NewDirector(program,new StorySessionRandom(seed));baseline.Select(request);var nextCanonical=baseline.Select(request).Id;
  Assert.Equal(nextCanonical,nextAfterForced);
 }
 [Fact] public void IneligibleCardCannotBeForced() {
  var program=Compile("card Restricted\ntags [Warmup]\nrequires World.Ankles == Free\n{\nwait 1s\n}\nsequence Test {}\n");
  var director=new StoryDirector(program,_=>new StoryData(false),_=>new StoryData("Restricted"),new StorySessionRandom(8),_=>{});
  var error=Assert.Throws<InvalidOperationException>(()=>director.Select(new CardSelectionRequest {RequiredTags=new[]{"Warmup"},Phase="Warmup"},"Restricted"));
  Assert.Contains("not an eligible positive-weight candidate",error.Message);Assert.False(director.Snapshot.Candidates.Single().Eligible);Assert.Contains("Required",director.Snapshot.Candidates.Single().Reason);
 }
 [Fact] public async Task LoopDecisionsUseOneCanonicalVisitAndConsumeOnlySecondChoice() {
  var program=Compile("sequence Test {\nrepeat 3 {\nif true {\nSession.Mood += 1\n}\n}\n}");
  var controller=new StoryRehearsalController();var before=new List<StoryInstructionSnapshot>();var after=new List<StoryInstructionSnapshot>();var branches=new List<StoryBranchSnapshot>();
  controller.InstructionReady+=snapshot=>{before.Add(snapshot);if(snapshot.Code==StoryCode.JumpFalse&&snapshot.Visit.Encounter==2)controller.SetBranchOverride(snapshot.Visit.Key,false);};
  controller.InstructionCompleted+=after.Add;controller.BranchObserved+=branches.Add;controller.Continue();var host=new Host();
  await new StoryMachine(program,host,null,controller).RunAsync("Test");
  Assert.Equal(new[]{1,2,3},branches.Select(b=>b.Visit.Encounter));Assert.Equal(new[]{false,true,false},branches.Select(b=>b.Forced));Assert.Equal(52,host.Read("Session.Mood").Numeric);
  foreach(var snapshot in after)Assert.Contains(before,b=>ReferenceEquals(b.Visit,snapshot.Visit));
 }
 [Fact] public async Task RepeatedDirectorForcingChangesOnlySecondDrawAndKeepsRngPosition() {
  const string source="card A\ntags [Warmup]\n{\nwait 1s\n}\ncard B\ntags [Warmup]\n{\nwait 1s\n}\nsequence Test {\nrepeat 3 {\nawait Director.DrawAndExecute(tags: [Warmup])\n}\n}";
  async Task<(string[],double)> Run(bool force) {
   var controller=new StoryRehearsalController();var random=new StorySessionRandom(179);var cards=new List<string>();
   controller.InstructionReady+=s=>{if(force&&s.Name=="Director.DrawAndExecute"&&s.Visit.Encounter==2)controller.SetForcedDirectorCard(s.Visit.Key,"B");};
   controller.DirectorObserved+=s=>cards.Add(s.SelectedCardId);controller.Continue();await new StoryMachine(Compile(source),new Host(),random,controller).RunAsync("Test");return(cards.ToArray(),random.Selection.NextDouble());
  }
  var normal=await Run(false);var forced=await Run(true);Assert.Equal(normal.Item1[0],forced.Item1[0]);Assert.Equal("B",forced.Item1[1]);Assert.Equal(normal.Item1[2],forced.Item1[2]);Assert.Equal(normal.Item2,forced.Item2);
 }
 [Fact] public async Task RepeatedWaitChoicesAreSingleUseAndStableOnReplay() {
  var program=Compile("sequence Test {\nrepeat 3 {\nlet response = await wait any {\nUI.Pressed(Ready)\n}\n}\n}");
  async Task<string[]> Run() {
   var controller=new StoryRehearsalController();var keys=new List<string>();controller.InstructionReady+=s=>{if(s.Code==StoryCode.WaitAny){keys.Add(s.Visit.Key);if(s.Visit.Encounter==2)controller.SetEventResponse(s.Visit.Key,new StoryData("UI.Pressed:Ready"));}};
   controller.Continue();var host=new Host();await new StoryMachine(program,host,null,controller).RunAsync("Test");Assert.Equal(2,host.WaitCalls);return keys.ToArray();
  }
  Assert.Equal(await Run(),await Run());
 }
 [Fact] public void PublishedCardAdditionAndRemovalNeedsNoSceneRegistration() {
  var root=Compile("sequence Test {\nawait Director.DrawAndExecute(tags: [Warmup])\n}");
  var published=Compile("card Published\ntags [Warmup]\n{\nwait 1s\n}\n");
  int Count(StoryLinkUnit[] pool) {
   var linked=StoryProgramLinker.Link(root,pool,"scene");var director=NewDirector(linked,new StorySessionRandom(1));return director.Inspect(new CardSelectionRequest{RequiredTags=new[]{"Warmup"},Phase="Warmup"}).Candidates.Count(c=>c.Eligible);
  }
  Assert.Equal(0,Count(Array.Empty<StoryLinkUnit>()));Assert.Equal(1,Count(new[]{new StoryLinkUnit("published",published)}));Assert.Equal(0,Count(Array.Empty<StoryLinkUnit>()));Assert.Empty(root.cards);
 }
 static StoryDirector NewDirector(CompiledStoryProgram program,StorySessionRandom random)=>new StoryDirector(program,_=>default,_=>new StoryData("Free"),random,_=>{});
}
