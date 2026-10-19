using DazPose.Editor;
using DazPose.Game;
using DazPose.Story;
using DazPose.Story.Language;
namespace DazPose.Rehearsal.Tests;
public sealed class FirstContactRehearsalTests {
 [Theory]
 [InlineData(0,"Now that's a go-getter",true)]
 [InlineData(2,"I like you naked",false)]
 [InlineData(6,"That's way too dressed",false)]
 public async Task ActualFirstContactClothingBranchesAndEarlyClimaxRunToResolution(int clothing,string branch,bool moodBonus) {
  var program=RehearsalTestSupport.Program;var scenario=new RehearsalScenario {seed=617,popupClothing=Enumerable.Range(0,6).Select(i=>i<clothing).ToArray(),popupKinks=RehearsalTestSupport.Profile().kinks};
  scenario.events=new[]{new RehearsalEventChoice{key=RehearsalTestSupport.WaitKey(program),eventId="UI.Pressed:ICame"}};
  var sourceProfile=RehearsalTestSupport.Profile();using var session=RehearsalTestSupport.Session(program,scenario,sourceProfile);var trace=new List<string>();session.TraceLine+=(source,line)=>trace.Add(line);session.Continue();await session.RunTask;
  Assert.Contains(trace,line=>line.Contains(branch,StringComparison.Ordinal));Assert.Equal("Resolution",session.Host.Session.phase);Assert.True(session.Host.VirtualSeconds>0);
  Assert.Contains(trace,line=>line.Contains("logical only; real playback required",StringComparison.Ordinal));
  Assert.Equal(PlayerProfileService.CalculateSubmissionScore(session.Host.Profile,RehearsalTestSupport.Kinks),session.Host.Profile.submissionScore);Assert.NotSame(sourceProfile,session.Host.Profile);Assert.Equal(0,sourceProfile.submissionScore);Assert.Equal(50,sourceProfile.favor);Assert.True(session.Host.Session.mood>=(moodBonus?55:50));
  if(clothing==6)Assert.Contains(trace,line=>line.Contains("marked shoes removed",StringComparison.OrdinalIgnoreCase));
  Assert.Equal(0,session.Host.Session.world.ClothingCount);
 }
 [Fact] public async Task ActualFirstContactNaturalMotionEndEntersLoopAndSimulatedPressResolves() {
  var program=RehearsalTestSupport.Program;var scenario=new RehearsalScenario {seed=811,popupClothing=new bool[6],popupKinks=RehearsalTestSupport.Profile().kinks};
  scenario.events=new[]{new RehearsalEventChoice{key=RehearsalTestSupport.WaitKey(program),eventId="MotionDriver.Ended"}};
  using var session=RehearsalTestSupport.Session(program,scenario);var trace=new List<string>();session.TraceLine+=(source,line)=>{trace.Add(line);if(line.Contains("Virtual action shown: ICame",StringComparison.Ordinal))session.SimulateAction("ICame");};session.Continue();await session.RunTask;
  Assert.Contains(trace,line=>line.Contains("Synthetic player press: ICame",StringComparison.Ordinal));Assert.Contains(trace,line=>line.Contains("Natural-end event can be selected",StringComparison.Ordinal));Assert.Contains(trace,line=>line.Contains("Simulated player pressed ICame",StringComparison.Ordinal));
  Assert.Contains(trace,line=>line.Contains("Virtual motion driver stopped",StringComparison.Ordinal));Assert.Equal("Resolution",session.Host.Session.phase);
 }
 [Fact] public async Task RehearsalSessionStepRunToBackAndResetReplayCanonicalInstructionsDeterministically() {
  const string source="sequence FirstContact {\nSession.Mood += 1\nSession.Mood += 2\nSession.Mood += 3\n}";
  var program=DazStoryCompiler.Compile(source,"Replay.dazstory");using var session=RehearsalTestSupport.Session(program);_ = session.Start();Assert.True(await session.StepAsync());Assert.Equal(51,session.Host.Session.mood);
  Assert.True(await session.StepAsync());Assert.Equal(53,session.Host.Session.mood);var next=session.History.Last();
  Assert.Equal(2,next.ProgramCounter);await session.BackAsync();Assert.Equal(51,session.Host.Session.mood);Assert.Equal(1,session.ReadySnapshot.ProgramCounter);
  await session.ResetAsync(true);Assert.Equal(50,session.Host.Session.mood);Assert.True(await session.StepAsync());Assert.Equal(51,session.Host.Session.mood);
 }
 [Fact] public async Task RunToBudgetAndCancellationAreBounded() {
  const string source="sequence FirstContact {\nSession.Mood += 1\nSession.Mood += 2\nSession.Mood += 3\n}";
  var program=DazStoryCompiler.Compile(source,"Budget.dazstory");var scenario=new RehearsalScenario{maxRunToSteps=1};using var session=RehearsalTestSupport.Session(program,scenario);_ = session.Start();
  await Assert.ThrowsAsync<InvalidOperationException>(()=>session.RunToPCAsync("FirstContact",2));using var cancelled=new CancellationTokenSource();cancelled.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>session.StepAsync(cancelled.Token));await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>session.RunToPCAsync("FirstContact",99,cancelled.Token));await session.StopAsync();Assert.True(session.Host.IsCancelled);Assert.True(session.RunTask.IsCompleted);
 }
 [Fact] public async Task KnownVirtualToyEffectIsExplicitlyNoOutputAndUnknownVerbHasSourceDiagnostic() {
  const string source="sequence FirstContact {\nToy.Vibrate(2)\n}";var program=DazStoryCompiler.Compile(source,"VirtualEffects.dazstory");using var session=RehearsalTestSupport.Session(program);var trace=new List<string>();session.TraceLine+=(location,line)=>trace.Add(line);session.Continue();await session.RunTask;Assert.Contains(trace,line=>line.Contains("output intentionally disabled",StringComparison.Ordinal));
  var error=Assert.Throws<StorySourceException>(()=>DazStoryCompiler.Compile("sequence FirstContact {\nMystery.Command()\n}","Unknown.dazstory"));Assert.Contains("Unknown.dazstory:2",error.Message);
 }
 [Fact] public void CardResourceFailureIsReportedBeforeCardExecution() {
  const string source="card MissingPose\ntags [Warmup]\nprogress 1\n{\nawait Lara.PersistentPose(Absent)\n}\nsequence FirstContact {}";
  var program=DazStoryCompiler.Compile(source,"fixture.dazstory",new Dictionary<string,StoryBindingKind>{{"Absent",StoryBindingKind.Pose}});
  var empty=new StoryContentBindings();var host=new RehearsalStoryHost(program,RehearsalTestSupport.Profile(),RehearsalTestSupport.Kinks,RehearsalTestSupport.Equipment,12,empty,null,new StorySceneBindings(Array.Empty<string>()));var issue=host.ResourceIssue(program.cards.Single());Assert.Contains("Absent",issue);
 }
 [Fact] public async Task RunThroughStagesSelectedLogicalResultAndPausesBeforeNextInstruction() {
  var program=DazStoryCompiler.Compile("sequence FirstContact {\nSession.Mood += 1\nSession.Mood += 2\nSession.Mood += 3\n}","Navigation.dazstory");
  using var session=RehearsalTestSupport.Session(program);var selected=await session.RunThroughSelectedAsync("Navigation.dazstory","FirstContact",1);
  Assert.Equal(53,session.Host.Session.mood);Assert.Equal(1,selected.ProgramCounter);Assert.Equal(2,session.ReadySnapshot.ProgramCounter);
  var visit=selected.Visit.Key;await session.BackAsync();await session.RunThroughSelectedAsync("Navigation.dazstory","FirstContact",1);Assert.Equal(visit,session.CompletedSnapshot.Visit.Key);
 }
 [Fact] public async Task RunThroughCanChooseRepeatedVisitAndReportsUnreachablePath() {
  var program=DazStoryCompiler.Compile("sequence FirstContact {\nrepeat 3 {\nSession.Mood += 1\n}\n}","Repeated.dazstory");int pc=Array.FindIndex(program.instructions,i=>i.name=="Session.Mood");
  using var session=RehearsalTestSupport.Session(program);var completed=await session.RunThroughSelectedAsync("Repeated.dazstory","FirstContact",pc,2);Assert.Equal(52,session.Host.Session.mood);Assert.Equal(2,completed.Visit.Encounter);
  var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>session.RunThroughSelectedAsync("Repeated.dazstory","FirstContact",pc,4));Assert.Contains("completed",error.Message,StringComparison.OrdinalIgnoreCase);
 }
 [Fact] public async Task UnconfiguredRaceWaitsForVisitResponseAndCancellationSettlesNavigation() {
  var program=DazStoryCompiler.Compile("sequence FirstContact {\nlet result = await wait any {\nUI.Pressed(Ready)\n}\nSession.Mood += 1\n}","Pending.dazstory");
  using var session=RehearsalTestSupport.Session(program);session.Continue();
  for(int i=0;i<100&&session.Controller.PendingWait==null;i++)await Task.Delay(5);
  Assert.NotNull(session.Controller.PendingWait);Assert.False(session.RunTask.IsCompleted);
  Assert.Throws<InvalidOperationException>(()=>session.SetEventResponse(session.ReadySnapshot.Source,session.ReadySnapshot.ProgramCounter,"Wrong"));
  session.SetEventResponse(session.ReadySnapshot.Source,session.ReadySnapshot.ProgramCounter,"UI.Pressed:Ready");await session.RunTask;Assert.Equal(51,session.Host.Session.mood);
  await session.ResetAsync(false);session.Continue();await session.RunTask;Assert.Equal(51,session.Host.Session.mood);
  using var pending=RehearsalTestSupport.Session(program);pending.Continue();await pending.StopAsync();Assert.True(pending.RunTask.IsCompleted);
 }
 [Fact] public void LegacyAndChangedProgramScenariosRequireReview() {
  Assert.Throws<InvalidOperationException>(()=>RehearsalSession.ImportScenarioJson("{\"seed\":1,\"branches\":[]}"));
  var program=DazStoryCompiler.Compile("sequence FirstContact {}","Fingerprint.dazstory");
  Assert.Throws<InvalidOperationException>(()=>RehearsalTestSupport.Session(program,new RehearsalScenario{programFingerprint="different"}));
 }

 [Fact] public async Task GlobalPoseNavigationUsesOwningContentAfterAuthoredLocomotion() {
  var root=DazStoryCompiler.Compile("sequence FirstContact {\nawait Lara.LocomoteTo(Mark)\nawait Director.DrawAndExecute(tags: [Warmup])\n}","Root.dazstory",new Dictionary<string,StoryBindingKind>{{"Mark",StoryBindingKind.Location}});
  var card=DazStoryCompiler.Compile("card PoseCard\ntags [Warmup]\n{\nawait Lara.PersistentPose(GlobalPose)\n}\n","Published.dazstory",new Dictionary<string,StoryBindingKind>{{"GlobalPose",StoryBindingKind.Pose}});
  var linked=StoryProgramLinker.Link(root,new[]{new StoryLinkUnit("published-guid",card)},"root-guid");
  var rootFixture=RehearsalTestSupport.Bindings(StoryProgramLinker.Link(root,Array.Empty<StoryLinkUnit>(),"root-guid"));
  var globalFixture=RehearsalTestSupport.Bindings(linked);var content=new StoryLinkedContent(linked,rootFixture.Content,new Dictionary<string,StoryContentBindings>{{"root-guid",rootFixture.Content},{"published-guid",globalFixture.Content}});
  var effects=new List<StoryCommand>();using var session=new RehearsalSession(root,"FirstContact",RehearsalTestSupport.Profile(),RehearsalTestSupport.Kinks,RehearsalTestSupport.Equipment,rootFixture.Content,content,globalFixture.Scene,visualEffect:effects.Add);
  int pc=Array.FindIndex(linked.instructions,i=>i.name=="Lara.PersistentPose");var entry=linked.entries.Single(e=>pc>=e.start&&pc<e.end);
  var completed=await session.RunThroughSelectedAsync("published-guid",entry.name,pc);
  Assert.Equal("published-guid",completed.Source.unit);Assert.Equal("Published.dazstory",completed.Source.file);
  Assert.Equal(new[]{"Lara.LocomoteTo","Lara.PersistentPose"},effects.Where(c=>c.Verb.StartsWith("Lara.")).Select(c=>c.Verb));
  Assert.Equal("Mark",effects.First().Require("location").String);Assert.Equal("published-guid",effects.Last().Source.unit);Assert.Equal("GlobalPose",effects.Last().Require("pose").String);
 }

}
