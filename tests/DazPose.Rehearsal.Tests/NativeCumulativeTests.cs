using DazPose.Editor;
using DazPose.Editor.Studio;
using DazPose.Story.Language;
namespace DazPose.Rehearsal.Tests;
public sealed class NativeCumulativeTests {
 static StoryCommand Command(string verb,bool awaited=true,params (string,string)[] args) {
  var source=new StorySource("native.dazstory",7,1,"FirstContact"){unit="native-owner"};
  var command=new StoryCommand{Verb=verb,Awaited=awaited,Source=source,Visit=new StoryInstructionVisit(source,3,2)};
  foreach(var (key,value) in args)command.Arguments.Add(key,new StoryData(value));return command;
 }
 [Fact] public void CumulativeIntentKeepsPoseCameraAndTrackingAcrossDialogueAndSeats() {
  var state=new RehearsalVisualState();
  var chain=new[]{Command("Lara.LocomoteTo",true,("location","stepforward")),Command("Lara.PersistentPose",true,("pose","SexyVamp")),Command("Lara.Say",true,("text","hello")),Command("Camera.StartAt",false,("location","camera_start")),Command("Camera.Track",false,("performer","Lara")),Command("Lara.SitAt",true,("location","Lounger")),Command("Lara.PersistentSitPose",true,("pose","Lounging")),Command("Lara.Say",true,("text","still seated"))};
  foreach(var command in chain)state=state.After(command);
  Assert.Equal(new[]{VisualIntentKind.Move,VisualIntentKind.Pose,VisualIntentKind.Speech,VisualIntentKind.CameraStart,VisualIntentKind.CameraTrack,VisualIntentKind.Sit,VisualIntentKind.SitPose,VisualIntentKind.Speech},state.Effects.Select(x=>x.Kind));
  Assert.Equal("camera_start",state.CameraAnchor);Assert.True(state.TrackLara);Assert.True(state.Seated);Assert.Equal("Lounging",state.PersistentPose);
  Assert.Equal("SexyVamp",state.Effects[1].Pose);Assert.Equal("Lounging",state.Effects[6].Pose);Assert.All(state.Effects,x=>Assert.Equal("native-owner",x.Source.unit));Assert.All(state.Effects,x=>Assert.Equal(2,x.Visit.Encounter));
 }
 [Fact] public void ProjectionIsImmutableAndContainsNoSceneCoordinatesOrRandomState() {
  var initial=new RehearsalVisualState();var moved=initial.After(Command("Lara.LocomoteTo",true,("location","Mark")));var said=moved.After(Command("Lara.Say",true,("text","Hi")));
  Assert.Empty(initial.Effects);Assert.Single(moved.Effects);Assert.Equal(2,said.Effects.Count);Assert.Equal("Mark",moved.Effects[0].Symbol);
  Assert.DoesNotContain(typeof(VisualIntent).GetProperties(),p=>p.PropertyType.Name.Contains("Vector")||p.Name.Contains("Random"));
 }
 [Theory][InlineData(true)][InlineData(false)] public void GestureIsObservationAndNeverBecomesPersistentPose(bool awaited) {
  var state=new RehearsalVisualState().After(Command("Lara.PersistentPose",true,("pose","Base"))).After(Command("Lara.PlayOneShotGesture",awaited,("gesture","Gesture")));
  Assert.Equal("Base",state.Effects[0].Pose);Assert.Null(state.Effects[1].Pose);Assert.Contains(awaited?"faded out":"Non-awaited",state.Observation);
 }
 [Fact] public void CameraOrderIsAuthoredIncludingStartAfterTrack() {
  var state=new RehearsalVisualState().After(Command("Camera.Track",false,("performer","Lara"))).After(Command("Camera.StartAt",false,("location","Other")));
  Assert.False(state.TrackLara);Assert.Equal("Other",state.CameraAnchor);
  Assert.Equal(new[]{VisualIntentKind.CameraTrack,VisualIntentKind.CameraStart},state.Effects.Select(x=>x.Kind));
  Assert.Throws<StorySourceException>(()=>state.After(Command("Camera.Track",false,("performer","Invented"))));
 }
 [Fact] public void UnsupportedVisualDoesNotPartiallyMutateLastGoodProjection() {
  var state=new RehearsalVisualState().After(Command("Lara.StartIdle"));Assert.Throws<StorySourceException>(()=>state.After(Command("Lara.Invented")));Assert.Single(state.Effects);
 }
 [Fact] public async Task VisualFailureStopsCanonicalExecutionBeforeMovementAndDirector() {
  var program=DazStoryCompiler.Compile("sequence FirstContact {\nawait Lara.SitAt(Seat)\nawait Lara.LocomoteTo(Mark)\nawait Director.DrawAndExecute(tags: [Warmup])\n}\ncard CardA\ntags [Warmup]\nprogress 20\n{ wait 1s }","Failure.dazstory");
  var fixture=RehearsalTestSupport.Bindings(program);var effects=new List<string>();var draws=0;
  using var session=new RehearsalSession(program,"FirstContact",RehearsalTestSupport.Profile(),RehearsalTestSupport.Kinks,RehearsalTestSupport.Equipment,fixture.Content,null,fixture.Scene,asyncVisualEffect:command=>{effects.Add(command.Verb);return Task.FromException(new InvalidOperationException("invalid native seat"));});
  session.DirectorObserved+=_=>draws++;session.Continue();await Assert.ThrowsAnyAsync<Exception>(()=>session.RunTask);
  Assert.Equal(new[]{"Lara.SitAt"},effects);Assert.Equal(0,draws);Assert.Null(session.CompletedSnapshot);
 }
 [Fact] public async Task VisualAcknowledgementIsAwaitedAndSurvivesSessionReset() {
  var program=DazStoryCompiler.Compile("setup FirstContact {\nCamera.StartAt(Mark)\n}\nsequence FirstContact {\nSession.Mood += 1\n}","Ack.dazstory");var fixture=RehearsalTestSupport.Bindings(program);int calls=0;
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
 [Fact] public void StandingDissolveResetsPoseWhileLocomotionRetainsItAndBodyLookPersist() {
  var state=new RehearsalVisualState().After(Command("Lara.PersistentPose",true,("pose","SexyVamp"))).After(Command("Lara.LocomoteTo",true,("location","NewMark"))).After(Command("Lara.MagicShader",true,("profile","GlowyWireframe"))).After(Command("PlayerBody.DissolveIn"));
  Assert.Equal("SexyVamp",state.PersistentPose);Assert.Equal("NewMark",state.ActorLocation);Assert.False(state.BodyHidden);Assert.Equal("GlowyWireframe",state.Material);
  var dissolved=state.After(Command("Lara.DissolveTo",true,("location","MainPosition1")));Assert.Equal("configured InitialPose",dissolved.PersistentPose);Assert.False(dissolved.Seated);Assert.Equal("GlowyWireframe",dissolved.Material);Assert.False(dissolved.BodyHidden);
 }
 [Fact] public async Task ActualFirstContactReplaysEveryExecutedVisualIncludingGlobalOwnerWithoutNewLogic() {
  var program=RehearsalTestSupport.Program;var fixture=RehearsalTestSupport.Bindings(program);var state=new RehearsalVisualState();
  var scenario=new RehearsalScenario{seed=617,popupClothing=new bool[6],popupKinks=RehearsalTestSupport.Profile().kinks,events=new[]{new RehearsalEventChoice{key=RehearsalTestSupport.WaitKey(program),eventId="UI.Pressed:ICame"}}};
  using var session=new RehearsalSession(program,"FirstContact",RehearsalTestSupport.Profile(),RehearsalTestSupport.Kinks,RehearsalTestSupport.Equipment,fixture.Content,null,fixture.Scene,scenario,visualEffect:command=>state=state.After(command));
  session.Continue();await session.RunTask;
  Assert.Contains(state.Effects,e=>e.Kind==VisualIntentKind.Sit);Assert.Contains(state.Effects,e=>e.Kind==VisualIntentKind.DissolveSeated);Assert.Contains(state.Effects,e=>e.Kind==VisualIntentKind.BodyShow);Assert.Contains(state.Effects,e=>e.Kind==VisualIntentKind.Motion);Assert.Equal("Resolution",session.Host.Session.phase);
 }
}
