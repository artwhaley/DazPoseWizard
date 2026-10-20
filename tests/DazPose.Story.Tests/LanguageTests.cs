using DazPose.Story.Language;
using System.Threading;
using System.Threading.Tasks;
namespace DazPose.Story.Tests;
public sealed class LanguageTests {
 sealed class Host : IStoryCardPresentationHost,IStoryExecutionHost {
  public readonly DazPose.Tests.RecordingCardPresentation Presentation=new();
  public Task<IStoryCardPresentationScope> EnterCardAsync(CardPresentationRequest request,CancellationToken cancellation)=>Presentation.EnterAsync(request,cancellation);
  public readonly Dictionary<string,StoryData> State=new() { ["Session.Mood"]=new StoryData(50d),["World.Posture"]=new StoryData("Standing"),["World.Ankles"]=new StoryData("Free") };
  public readonly List<string> Calls=new(),TraceLines=new();
  public readonly Random Random=new(42);
  public Func<StoryCommand,CancellationToken,Task<StoryData>> Handler;
  public bool Cancelled;
  public StoryData Read(string key)=>State[key];
  public void Write(string key,StoryData value) {State[key]=value;}
  public Task<StoryData> InvokeAsync(StoryCommand command,CancellationToken cancellation) { Calls.Add(command.Verb); return Handler?.Invoke(command,cancellation) ?? Task.FromResult(default(StoryData)); }
  public Task<StoryData> WaitAnyAsync(StoryData[] events,CancellationToken cancellation)=>throw new NotSupportedException();
  public StoryData Function(string name,StoryData[] args) {
   if(name=="round")return new StoryData(Math.Round(args[0].Numeric,MidpointRounding.AwayFromZero));
   if(name=="random")return new StoryData((double)Random.Next((int)args[0].Number,(int)args[0].Upper+1));
   return new StoryData("shoes");
  }
  public Task YieldAsync(CancellationToken cancellation) { cancellation.ThrowIfCancellationRequested(); return Task.CompletedTask; }
  public void CancelOperations()=>Cancelled=true;
  public void Trace(StorySource source,string message)=>TraceLines.Add(message);
 }
 static CompiledStoryProgram Compile(string source,IReadOnlyDictionary<string,StoryBindingKind> symbols=null)=>DazStoryCompiler.Compile(source,"test.dazstory",symbols);
 [Fact] public async Task ThirtyTwoNestedBlocksCompileAndExecuteFlat() {
  string source="Session.Mood += 5\n"; for(int i=0;i<32;i++)source="if true\n{\n"+source+"}\n";
  var program=Compile("sequence Test\n{\n"+source+"}\n");var host=new Host();
  await new StoryMachine(program,host).RunAsync("Test");
  Assert.Equal(55,host.State["Session.Mood"].Numeric);Assert.True(program.instructions.Length>64);
  Assert.All(program.instructions,i=>Assert.NotNull(i.source));
 }
 [Fact] public async Task VariablesDurationsRepeatAndCompoundPredicates() {
  var program=Compile("sequence Test {\nlet repetitions = random(11..22)\nif World.Posture == Standing && (World.Ankles == Free || !true)\n{\nrepeat repetitions {\nwait 500ms\nSession.Mood += 1\n}\n}\n}\n");
  var host=new Host();await new StoryMachine(program,host).RunAsync("Test");
  Assert.InRange(host.Calls.Count,11,22);Assert.Equal(50+host.Calls.Count,host.State["Session.Mood"].Numeric);
  Assert.Contains(program.expressions,e=>e.atoms.Any(a=>a.code==StoryAtomCode.Number && a.number==.5));
 }
 [Fact] public async Task BooleanOperatorsShortCircuitValueFunctions() {
  var program=Compile("sequence Test {\nif false && World.Clothing.Count > 0 {\nSession.Mood = 0\n}\nif true || World.Clothing.Count > 0 {\nSession.Mood += 5\n}\n}\n");
  var host=new Host(); await new StoryMachine(program,host).RunAsync("Test"); Assert.Equal(55,host.State["Session.Mood"].Numeric);
 }
 [Fact] public void MissingAndWrongTypeSymbolsNameSourceAndExpectedType() {
  var symbols=new Dictionary<string,StoryBindingKind>{{"SexyVamp",StoryBindingKind.Location}};
  var error=Assert.Throws<StorySourceException>(()=>Compile("card Intro {\nawait Lara.PersistentPose(SexyVamp)\n}\n",symbols));
  Assert.Contains("test.dazstory:2:",error.Message);Assert.Contains("card Intro",error.Message);Assert.Contains("Pose",error.Message);Assert.Contains("SexyVamp",error.Message);
  error=Assert.Throws<StorySourceException>(()=>Compile("card Intro {\nawait Lara.PersistentPose(SexyVmamp)\n}\n",symbols));Assert.Contains("SexyVmamp",error.Message);
 }
 [Theory] [InlineData("await Lara.Unknown()")][InlineData("Lara.Say(\"hello\")")][InlineData("await Lara.PersistentPose()")][InlineData("await Lara.PersistentPose(SexyVamp, surprise: true)")]
 public void InvalidCallsFailAtCompileTime(string call)=>Assert.Throws<StorySourceException>(()=>Compile("sequence Test {\n"+call+"\n}\n"));
 [Fact] public void DialogueIdentityDependsOnExactTranscript() {
  var first=Compile("sequence Test {\nawait Lara.Say(\"Hello. (giggles)\")\n}\n");var second=Compile("sequence Test {\nawait Lara.Say(\"Hello.\")\n}\n");
  Assert.Equal(StoryDialogue.IdFor("Hello. (giggles)"),first.dialogue[0].id); Assert.NotEqual(first.dialogue[0].id,second.dialogue[0].id);
 }
 [Fact] public void WrappedNamedArgumentsAndListsHaveSameMeaning() {
  var one=Compile("sequence Test {\nawait Director.DrawAndExecute(tags: [Main, Sitting], intensity: 1.2)\n}\n");
  var wrapped=Compile("sequence Test {\nawait Director.DrawAndExecute(\ntags: [Main, Sitting],\nintensity: 1.2\n)\n}\n");
  Assert.Equal(one.instructions.Length,wrapped.instructions.Length);Assert.Equal(one.expressions.SelectMany(e=>e.atoms).Select(a=>(a.code,a.text,a.number)),wrapped.expressions.SelectMany(e=>e.atoms).Select(a=>(a.code,a.text,a.number)));
 }
 [Fact] public async Task PoseCanOverlapSpeechButIsDrainedBeforeCompletion() {
  var pose=new TaskCompletionSource<StoryData>();var host=new Host();
  host.Handler=(c,token)=> { if(c.Verb=="Lara.PersistentPose")return pose.Task; if(c.Verb=="Lara.Say") { Assert.False(pose.Task.IsCompleted);pose.SetResult(default); }return Task.FromResult(default(StoryData)); };
  await new StoryMachine(Compile("sequence Test {\nLara.PersistentPose(SexyVamp)\nawait Lara.Say(\"hello\")\n}\n"),host).RunAsync("Test");
  Assert.Equal(new[]{"Lara.PersistentPose","Lara.Say"},host.Calls);Assert.Contains(host.TraceLines,l=>l=="Finished: Blend Lara into the SexyVamp pose.");
 }
 [Fact] public async Task OverlappingBodyCommandsReportSourceConflictAndCancel() {
  var pending=new TaskCompletionSource<StoryData>();var host=new Host();host.Handler=(c,token)=> {token.Register(()=>pending.TrySetCanceled(token));return pending.Task;};
  var error=await Assert.ThrowsAsync<StorySourceException>(()=>new StoryMachine(Compile("sequence Test {\nLara.PersistentPose(SexyVamp)\nawait Lara.LocomoteTo(stepforward)\n}\n"),host).RunAsync("Test"));
  Assert.Contains("Runtime conflict",error.Message);Assert.Contains("test.dazstory:3:",error.Message);Assert.True(host.Cancelled);
 }
 [Fact] public async Task BackgroundFailureCancelsBlockingCommandAndReportsOriginalSource() {
  var pose=new TaskCompletionSource<StoryData>(); var speech=new TaskCompletionSource<StoryData>();var host=new Host();
  host.Handler=(c,token)=> {token.Register(()=>speech.TrySetCanceled(token)); if(c.Verb=="Lara.PersistentPose")return pose.Task; pose.TrySetException(new InvalidOperationException("pose failed"));return speech.Task;};
  var error=await Assert.ThrowsAsync<StorySourceException>(()=>new StoryMachine(Compile("sequence Test {\nLara.PersistentPose(SexyVamp)\nawait Lara.Say(\"hello\")\n}\n"),host).RunAsync("Test"));
  Assert.Contains("pose failed",error.Message);Assert.Contains("test.dazstory:2:",error.Message);Assert.True(host.Cancelled);
 }
 [Fact] public async Task StopCancelsPendingBackgroundAndAwaitedOperations() {
  var cancel=new CancellationTokenSource();var pending=new TaskCompletionSource<StoryData>();var host=new Host();
  host.Handler=(c,token)=> {token.Register(()=>pending.TrySetCanceled(token));if(c.Verb=="Lara.Say")cancel.Cancel();return pending.Task;};
  await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new StoryMachine(Compile("sequence Test {\nLara.PersistentPose(SexyVamp)\nawait Lara.Say(\"hello\")\n}\n"),host).RunAsync("Test",cancel.Token)); Assert.True(host.Cancelled);
 }
 [Fact] public async Task SetupRunsBeforeSequenceAndPhasesResetOnlyTheirProgress() {
  var host=new Host();await new StoryMachine(Compile("setup Test {\nLara.StartIdle()\n}\nsequence Test {\nphase Warmup {\nSession.Mood += 5\n}\n}\n"),host).RunAsync("Test");
  Assert.Equal("Lara.StartIdle",host.Calls[0]); Assert.Equal("Warmup",host.State["Session.Phase"].String); Assert.Equal(0,host.State["Session.PhaseCompletion"].Numeric);Assert.Equal(55,host.State["Session.Mood"].Numeric);
 }
 [Fact] public void ActualNorthStarCompilesAllNineteenDialogueBeatsAndAllPhases() {
  var program=AuthoredStoryTestContent.Compile();
  Assert.Equal(19,program.dialogue.Length);
  Assert.Equal(new[]{"Warmup","Main","Climax","Resolution"},program.instructions.Where(i=>i.code==StoryCode.EnterPhase).Select(i=>i.name));
  Assert.Contains(program.instructions,i=>i.code==StoryCode.WaitAny);
  Assert.Contains(program.dialogue,d=>d.transcript.Contains("(giggles)"));
  var intro=program.RequireEntry("Intro","sequence");
  Assert.DoesNotContain(program.instructions.Skip(intro.start).Take(intro.end-intro.start),i=>i.name=="Lara.SitAt" || i.name=="Camera.MoveTo");
 } [Theory] [InlineData(0,true,55,false)][InlineData(2,false,50,false)][InlineData(4,true,50,true)]
 public async Task ActualIntroAndNegotiationReachWarmupInAllClothingBranches(int clothes,bool equipment,int mood,bool disapproval) {
  var program=AuthoredStoryTestContent.Compile();
  var host=new Host();host.State["World.Clothing.Count"]=new StoryData((double)clothes);host.State["Session.PreparedEquipment.Count"]=new StoryData(0d);host.State["Session.PreparedEquipment"]=new StoryData(Array.Empty<string>());

  var commands=new List<StoryCommand>();
  host.Handler=(command,token)=> {
   commands.Add(command);
   if(command.Verb=="Session.SelectPreparedEquipment") {host.State["Session.PreparedEquipment.Count"]=new StoryData(equipment ? 1d : 0d);host.State["Session.PreparedEquipment"]=new StoryData(equipment ? new[]{"rope"} : Array.Empty<string>());}
   if(command.Verb=="World.Clothing.MarkRemoved")host.State["World.Clothing.Count"]=new StoryData((double)(clothes-1));
   return Task.FromResult(default(StoryData));
  };
  var machine=new StoryMachine(program,host);await machine.RunAsync("FirstContact",stopBeforePhase:"Warmup");
  Assert.Equal("Warmup",machine.ReachedCheckpoint);Assert.DoesNotContain("Session.Phase",host.State.Keys);
  Assert.Equal(mood,host.State["Session.Mood"].Numeric);
  Assert.Equal(disapproval,host.Calls.Contains("Lara.PlayOneShotGesture"));
  Assert.Equal(equipment,host.Calls.Contains("Popup.ShowNeededEquipment"));
  Assert.Equal(10,commands.Count(c=>c.Verb=="Lara.Say"));
  Assert.Equal(new[]{"Lara.StartHiddenAt","PlayerBody.StartHiddenAt","Camera.StartAt","Lara.StartIdle","Lara.SetOutfit","Lara.SetHair","Lara.DissolveIn","Camera.Track"},host.Calls.Take(8));
  Assert.DoesNotContain("Lara.SitAt",host.Calls);Assert.DoesNotContain("Camera.MoveTo",host.Calls);
  if(disapproval)Assert.Equal("shoes",commands.Single(c=>c.Verb=="Popup.RemoveClothingItem").Require("item").String);
 } [Fact] public void PreviewValidationIncludesAllSetupBranchesAndExcludesLaterContent() {
  var program=AuthoredStoryTestContent.Compile();
  var view=StoryPlaybackWindow.BeforePhase(program,"FirstContact","Warmup");
  Assert.Contains(view.bindings,b=>b.symbol=="Disapproval");Assert.Contains(view.bindings,b=>b.symbol=="SexyVamp");Assert.Contains(view.bindings,b=>b.symbol=="Lounger");
  Assert.DoesNotContain(view.bindings,b=>b.kind==StoryBindingKind.Funscript || b.symbol=="MainPosition1" || b.symbol=="Bedside");
  Assert.Equal(12,view.dialogue.Length);Assert.Equal(19,program.dialogue.Length);Assert.Same(program.instructions,view.instructions);
  Assert.Throws<InvalidOperationException>(()=>StoryPlaybackWindow.BeforePhase(program,"FirstContact","Typo"));
 }
 [Fact] public async Task PreviewDrainsBackgroundWorkBeforeReportingCheckpoint() {
  var pose=new TaskCompletionSource<StoryData>();var host=new Host();
  host.Handler=(command,token)=>command.Verb=="Lara.PersistentPose" ? pose.Task : Task.FromResult(default(StoryData));
  var machine=new StoryMachine(Compile("sequence Test {\nLara.PersistentPose(SexyVamp)\nphase Warmup {\nawait Lara.Say(\"later\")\n}\n}\n"),host);
  var run=machine.RunAsync("Test",stopBeforePhase:"Warmup");Assert.False(run.IsCompleted);pose.SetResult(default);await run;
  Assert.Equal("Warmup",machine.ReachedCheckpoint);Assert.DoesNotContain("Lara.Say",host.Calls);Assert.False(host.Cancelled);
 }
 [Fact] public async Task ProgramsFromAnotherFrontendStillEnforceAwaitContracts() {
  var program=Compile("sequence Test {\nawait Lara.Say(\"hello\")\n}\n");program.instructions[0].awaited=false;
  var host=new Host();var error=await Assert.ThrowsAsync<StorySourceException>(()=>new StoryMachine(program,host).RunAsync("Test"));
  Assert.Contains("requires await",error.Message);Assert.Empty(host.Calls);Assert.True(host.Cancelled);
 }}