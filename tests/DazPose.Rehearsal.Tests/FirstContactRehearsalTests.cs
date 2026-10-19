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
}
