using DazPose.Editor;
using DazPose.Editor.Studio;
using DazPose.Story.Language;
namespace DazPose.Rehearsal.Tests;
// Canonical harness regression remains separate from the visual authoring editor.
public sealed class RuntimeRehearsalIntegrationTests {
 [Fact] public async Task VisualFailureStopsCanonicalExecutionBeforeMovementAndDirector() {
  var program=DazStoryCompiler.Compile("sequence FirstContact {\nawait Lara.SitAt(Seat)\nawait Lara.LocomoteTo(Mark)\nawait Director.DrawAndExecute(tags: [Warmup])\n}\ncard CardA\ntags [Warmup]\nprogress 20\n{ wait 1s }","Failure.dazstory");
  var fixture=RehearsalTestSupport.Bindings(program);var effects=new List<string>();var draws=0;
  using var session=new RehearsalSession(program,"FirstContact",RehearsalTestSupport.Profile(),RehearsalTestSupport.Kinks,RehearsalTestSupport.Equipment,fixture.Content,null,fixture.Scene,asyncVisualEffect:command=>{effects.Add(command.Verb);return Task.FromException(new InvalidOperationException("invalid native seat"));});
  session.DirectorObserved+=_=>draws++;session.Continue();await Assert.ThrowsAnyAsync<Exception>(()=>session.RunTask);
  Assert.Equal(new[]{"Lara.SitAt"},effects);Assert.Equal(0,draws);Assert.Null(session.CompletedSnapshot);
 }
 [Fact] public async Task VisualAcknowledgementIsAwaitedAndSurvivesSessionReset() {
  var program=DazStoryCompiler.Compile("setup FirstContact {\nCamera.StartAt(High)\n}\nsequence FirstContact {\nSession.Mood += 1\n}","Ack.dazstory");var fixture=RehearsalTestSupport.Bindings(program);int calls=0;
  var ack=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
  using var session=new RehearsalSession(program,"FirstContact",RehearsalTestSupport.Profile(),RehearsalTestSupport.Kinks,RehearsalTestSupport.Equipment,fixture.Content,null,fixture.Scene,asyncVisualEffect:_=>{calls++;return ack.Task;});
  var step=session.StepAsync();await Task.Delay(30);Assert.False(step.IsCompleted);Assert.Equal(50,session.Host.Session.mood);ack.SetResult(true);await step;Assert.Equal(1,calls);
  await session.ResetAsync();await session.StepAsync();Assert.Equal(2,calls);
 }
 [Fact] public async Task BeforeAfterExactVisitReplayIsDeterministicAndUnreachableIsExplicit() {
  var program=DazStoryCompiler.Compile("sequence FirstContact {\nrepeat 2 {\nSession.Mood += 1\n}\n}","Visits.dazstory");
  using var session=RehearsalTestSupport.Session(program);var pc=Array.FindIndex(program.instructions,i=>i.name=="Session.Mood");var owner=program.instructions[pc].source.unit??program.instructions[pc].source.file;
  var before=await session.RunBeforeSelectedAsync(owner,"FirstContact",pc,2);Assert.Equal(2,before.Visit.Encounter);Assert.Equal(51,session.Host.Session.mood);
  var after=await session.RunThroughSelectedAsync(owner,"FirstContact",pc,2);Assert.Equal(2,after.Visit.Encounter);Assert.Equal(52,session.Host.Session.mood);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>session.RunBeforeSelectedAsync(owner,"FirstContact",pc,3));
 }
 [Fact] public async Task ForwardCueNavigationRetainsCanonicalStateAndDoesNotReplayPriorEffects() {
  var program=DazStoryCompiler.Compile("sequence FirstContact {\nawait Lara.PersistentPose(A)\nSession.Mood += 1\nawait Lara.Say(\"hello\")\nawait Lara.PersistentPose(B)\n}","Forward.dazstory");
  var fixture=RehearsalTestSupport.Bindings(program);var commands=new List<string>();
  using var session=new RehearsalSession(program,"FirstContact",RehearsalTestSupport.Profile(),RehearsalTestSupport.Kinks,RehearsalTestSupport.Equipment,fixture.Content,null,fixture.Scene,visualEffect:c=>commands.Add(c.Verb));
  int first=Array.FindIndex(program.instructions,i=>i.name=="Lara.PersistentPose");
  int last=Array.FindLastIndex(program.instructions,i=>i.name=="Lara.PersistentPose");
  string owner=program.instructions[first].source.unit??program.instructions[first].source.file;
  await session.RunThroughSelectedAsync(owner,"FirstContact",first);
  var host=session.Host;
  await session.RunBeforeSelectedAsync(owner,"FirstContact",last,restart:false);
  Assert.Same(host,session.Host);Assert.Equal(51,session.Host.Session.mood);
  Assert.Equal(new[]{"Lara.PersistentPose","Lara.Say"},commands);
  await session.RunThroughSelectedAsync(owner,"FirstContact",last,restart:false);
  Assert.Same(host,session.Host);Assert.Equal(3,commands.Count);
  await session.RunThroughSelectedAsync(owner,"FirstContact",first);
  Assert.NotSame(host,session.Host);Assert.Equal(50,session.Host.Session.mood);Assert.Equal(4,commands.Count);
 }
 [Fact] public async Task ForwardNavigationPreservesExactRepeatedOccurrence() {
  var program=DazStoryCompiler.Compile("sequence FirstContact {\nrepeat 3 {\nSession.Mood += 1\n}\n}","ForwardVisits.dazstory");
  using var session=RehearsalTestSupport.Session(program);
  int pc=Array.FindIndex(program.instructions,i=>i.name=="Session.Mood");
  string owner=program.instructions[pc].source.unit??program.instructions[pc].source.file;
  await session.RunThroughSelectedAsync(owner,"FirstContact",pc,1);
  var host=session.Host;
  var third=await session.RunBeforeSelectedAsync(owner,"FirstContact",pc,3,restart:false);
  Assert.Same(host,session.Host);Assert.Equal(3,third.Visit.Encounter);Assert.Equal(52,host.Session.mood);
  await session.RunThroughSelectedAsync(owner,"FirstContact",pc,3,restart:false);
  Assert.Equal(53,host.Session.mood);
 }
 [Fact] public void SpatialLedgerUpdatesDoNotReadOrAdoptUnrelatedBoneEdits(){
  int root=1,bone=2,boneReads=0;var ledger=new PreviewPropertyLedger();
  ledger.Capture("root",()=>root,v=>root=v,group:"spatial");
  ledger.Capture("bone",()=>{boneReads++;return bone;},v=>bone=v);
  root=10;bone=20;ledger.RecordApplied();boneReads=0;
  root=11;bone=99;ledger.RecordApplied("spatial");
  Assert.Empty(ledger.Conflicts("spatial"));Assert.Equal(0,boneReads);
  Assert.Equal(new[]{"bone"},ledger.Conflicts());
  ledger.PrepareRestore();ledger.Restore();Assert.Equal(1,root);Assert.Equal(99,bone);
 }
 [Fact] public async Task PoseReplacementKeepsTheSessionVisitStateAndRandomStream(){
  var program=DazStoryCompiler.Compile("sequence FirstContact {\nawait Lara.PersistentPose(A)\nSession.Mood += 1\nawait Lara.PersistentPose(A)\n}","Swap.dazstory");
  var fixture=RehearsalTestSupport.Bindings(program);var effects=new List<string>();
  using var session=new RehearsalSession(program,"FirstContact",RehearsalTestSupport.Profile(),RehearsalTestSupport.Kinks,RehearsalTestSupport.Equipment,fixture.Content,null,fixture.Scene,visualEffect:c=>effects.Add(c.Verb));
  int first=Array.FindIndex(program.instructions,i=>i.name=="Lara.PersistentPose");
  int last=Array.FindLastIndex(program.instructions,i=>i.name=="Lara.PersistentPose");
  string owner=program.instructions[first].source.unit??program.instructions[first].source.file;
  await session.RunThroughSelectedAsync(owner,"FirstContact",first);
  var host=session.Host;var randoms=host.Randoms;var before=session.ReadySnapshot;int history=session.History.Count;
  fixture.Content.poses["A"].value=new DazPose.Performer.PerformerPose{Clip=new object()};
  Assert.True(host.RefreshPresentationBindings());
  Assert.Same(host,session.Host);Assert.Same(randoms,host.Randoms);Assert.Same(before,session.ReadySnapshot);Assert.Equal(history,session.History.Count);Assert.Single(effects);
  await session.RunThroughSelectedAsync(owner,"FirstContact",last,restart:false);
  Assert.Equal(51,host.Session.mood);Assert.Equal(2,effects.Count);
  fixture.Content.poses["A"].posture=DazPose.Story.PlayerPosture.Sitting;
  Assert.False(host.RefreshPresentationBindings());
 }
}
