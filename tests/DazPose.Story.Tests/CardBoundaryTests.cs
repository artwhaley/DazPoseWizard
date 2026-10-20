using DazPose.Story.Language;
namespace DazPose.Story.Tests;
public sealed class CardBoundaryTests {
 static CompiledStoryProgram Compile(string s)=>DazStoryCompiler.Compile(s,"boundary.story");
 [Fact] public async Task DirectExplicitAndDrawUseIdenticalBoundaryOrder() {
  foreach(string invoke in new[]{"play A","await Director.Execute(A)","await Director.DrawAndExecute(tags: [Warmup])"}) {
   var p=Compile("card A\ntags [Warmup]\nprogress 10\n{\nSession.Mood += 1\n}\nsequence Test {\n"+invoke+"\nSession.Mood += 2\n}");
   var h=new DirectorTests.Host();var m=new StoryMachine(p,h);await m.RunAsync("Test");
   Assert.Equal(new[]{"Enter:A:1","Return:A:1","Dispose:A:1"},h.Presentation.Events);Assert.Equal(53,h.Read("Session.Mood").Numeric);
   Assert.Equal(invoke.StartsWith("play")?0:10,h.Read("Session.PhaseCompletion").Numeric);
  }
 }
 [Fact] public async Task CallerAndDirectorProgressWaitForReturn() {
  var pending=new TaskCompletionSource();var h=new DirectorTests.Host();h.Presentation.BeforeReturn=(_,_)=>pending.Task;
  var p=Compile("card A\nprogress 10\n{\nSession.Mood += 1\n}\nsequence Test {\nawait Director.Execute(A)\nSession.Mood += 2\n}");
  var task=new StoryMachine(p,h).RunAsync("Test");Assert.False(task.IsCompleted);Assert.Equal(51,h.Read("Session.Mood").Numeric);Assert.Equal(0,h.Read("Session.PhaseCompletion").Numeric);
  pending.SetResult();await task;Assert.Equal(53,h.Read("Session.Mood").Numeric);Assert.Equal(10,h.Read("Session.PhaseCompletion").Numeric);
 }
 [Fact] public async Task NestedCardsReturnInsideOutAndSequencesInheritContext() {
  var p=Compile("sequence Helper {\nwait Card.Invocation\n}\ncard Inner {\nplay Helper\n}\ncard Outer {\nplay Inner\nplay Helper\n}\nsequence Test {\nplay Outer\n}");var h=new DirectorTests.Host();await new StoryMachine(p,h).RunAsync("Test");
  Assert.Equal(new[]{"Enter:Outer:1","Enter:Inner:2","Return:Inner:2","Dispose:Inner:2","Return:Outer:1","Dispose:Outer:1"},h.Presentation.Events);
  Assert.Equal(new[]{2d,1d},h.Commands.Where(c=>c.Verb=="Wait").Select(c=>c.Require("seconds").Numeric));
 }
 [Fact] public async Task BodyFailureSkipsReturnAndKeepsOriginalFailure() {
  var h=new DirectorTests.Host();h.Handler=(_,_)=>Task.FromException<StoryData>(new InvalidOperationException("body sentinel"));var p=Compile("card A {\nwait 1s\n}\nsequence Test {\nplay A\nSession.Mood = 0\n}");
  var e=await Assert.ThrowsAsync<StorySourceException>(()=>new StoryMachine(p,h).RunAsync("Test"));Assert.Contains("body sentinel",e.Message);Assert.Equal(new[]{"Enter:A:1","Dispose:A:1"},h.Presentation.Events);Assert.Equal(50,h.Read("Session.Mood").Numeric);
 }
 [Fact] public async Task ReturnFailureHasNoProgressOrTrailingCue() {
  var h=new DirectorTests.Host();h.Presentation.BeforeReturn=(_,_)=>Task.FromException(new InvalidOperationException("return sentinel"));var p=Compile("card A\nprogress 10\n{\n}\nsequence Test {\nawait Director.Execute(A)\nSession.Mood = 0\n}");
  var e=await Assert.ThrowsAsync<StorySourceException>(()=>new StoryMachine(p,h).RunAsync("Test"));Assert.Contains("return sentinel",e.Message);Assert.Equal(0,h.Read("Session.PhaseCompletion").Numeric);Assert.Equal(50,h.Read("Session.Mood").Numeric);Assert.True(h.Cancelled);
 }
 [Fact] public async Task StopDuringReturnIsTerminal() {
  using var cancellation=new CancellationTokenSource();var h=new DirectorTests.Host();h.Presentation.BeforeReturn=async(_,token)=>await Task.Delay(Timeout.Infinite,token);
  var task=new StoryMachine(Compile("card A {\n}\nsequence Test {\nplay A\nSession.Mood = 0\n}"),h).RunAsync("Test",cancellation.Token);Assert.False(task.IsCompleted);cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>task);Assert.DoesNotContain(h.Presentation.Events,e=>e.StartsWith("Return:"));Assert.Equal(50,h.Read("Session.Mood").Numeric);
 }
 [Fact] public async Task FiniteUnawaitedBodyWorkSettlesBeforeReturn() {
  var pending=new TaskCompletionSource<StoryData>();var h=new DirectorTests.Host();h.Handler=(_,_)=>pending.Task;
  var task=new StoryMachine(Compile("card A {\nLara.PersistentPose(Pose)\n}\nsequence Test {\nplay A\n}"),h).RunAsync("Test");Assert.Equal(new[]{"Enter:A:1"},h.Presentation.Events);pending.SetResult(default);await task;Assert.Contains("Return:A:1",h.Presentation.Events);
 }
 [Fact] public async Task SequentialCardsRetainBoundedMachineTasksAndUniqueIds() {
  var h=new DirectorTests.Host();var m=new StoryMachine(Compile("card A {\nwait 0s\n}\nsequence Test {\nrepeat 1000 {\nplay A\n}\n}"),h);await m.RunAsync("Test");Assert.InRange(m.RetainedChildTaskCount,0,2);Assert.Contains("Enter:A:1000",h.Presentation.Events);
 }
 sealed class MissingHost : IStoryExecutionHost {
  public int Calls;public StoryData Read(string key)=>new StoryData(1d);public void Write(string key,StoryData value){}public Task<StoryData> InvokeAsync(StoryCommand c,CancellationToken t){Calls++;return Task.FromResult(default(StoryData));}public Task<StoryData> WaitAnyAsync(StoryData[] e,CancellationToken t)=>throw new NotSupportedException();public StoryData Function(string n,StoryData[] a)=>default;public Task YieldAsync(CancellationToken t)=>Task.CompletedTask;public void CancelOperations(){}public void Trace(StorySource s,string m){}
 }
 [Fact] public async Task MissingPresentationInterfaceFailsBeforeBody() {var h=new MissingHost();var e=await Assert.ThrowsAsync<StorySourceException>(()=>new StoryMachine(Compile("card A {\nwait 1s\n}\nsequence Test {\nplay A\n}"),h).RunAsync("Test"));Assert.Contains("IStoryCardPresentationHost",e.Message);Assert.Equal(0,h.Calls);}
 [Fact] public async Task DeliveredGateScriptsCompileLinkAndUseTheRealCardRoutes() {
  var folder=Path.Combine(AppContext.BaseDirectory,"GateB");
  var root=DazStoryCompiler.Compile(File.ReadAllText(Path.Combine(folder,"GateBRoot.dazstory")),"Root.dazstory",allowExternalCards:true);
  var cards=DazStoryCompiler.Compile(File.ReadAllText(Path.Combine(folder,"GateBCards.dazstory")),"Cards.dazstory",allowExternalCards:true);
  var linked=StoryProgramLinker.Link(root,new[]{new StoryLinkUnit("gate-cards",cards)},"gate-root");
  Assert.Single(linked.cards,c=>c.Selectable);
  foreach(string entry in new[]{"Direct","Explicit","Draw","Nested","Persistent","Contained","Unchanged","Motion"}) {
   var h=new DirectorTests.Host();await new StoryMachine(linked,h).RunAsync(entry);
   Assert.Equal(51,h.Read("Session.Mood").Numeric);
   if(entry=="Persistent")Assert.Empty(h.Presentation.Events);else Assert.Contains(h.Presentation.Events,e=>e.StartsWith("Return:"));
   Assert.Equal(CompiledStoryProgram.CurrentVersion,StoryProgramReachability.ForSequence(linked,entry).version);
  }
 }
}
