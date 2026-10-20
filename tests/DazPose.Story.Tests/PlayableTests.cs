using DazPose.Story.Language;
namespace DazPose.Story.Tests;
public sealed class PlayableTests {
 static CompiledStoryProgram Compile(string s)=>DazStoryCompiler.Compile(s,"playable.story",allowExternalCards:true);
 [Fact] public async Task HelperSequencePersistsWritesAndRunsNoImplicitSetup() {
  var p=Compile("setup Root {\nSession.Mood += 1\n}\nsetup Helper {\nSession.Mood = 0\n}\nsequence Helper {\nSession.Mood += 2\n}\nsequence Root {\nplay Helper\nplay Helper\n}\n");
  var h=new DirectorTests.Host();await new StoryMachine(p,h).RunAsync("Root");Assert.Equal(55,h.Read("Session.Mood").Numeric);
 }
 [Fact] public void CrossKindLocalAndPublicCollisionsFail() {
  Assert.ThrowsAny<Exception>(()=>Compile("card Same {\n}\nsequence Same {\n}\n"));
  var root=Compile("sequence Same {\n}\nsequence Root {\n}\n");
  var pub=Compile("card Same\ntags [Warmup]\n{\n}\n");
  Assert.Throws<StorySourceException>(()=>StoryProgramLinker.Link(root,new[]{new StoryLinkUnit("public",pub)}));
  Assert.Throws<InvalidOperationException>(()=>StoryProgramLinker.Link(root,new[]{new StoryLinkUnit("public",Compile("sequence Helper {\n}\n"))}));
  Assert.Throws<StorySourceException>(()=>StoryProgramLinker.ResolveCard(root,"Same"));
 }
 [Fact] public async Task MixedRecursionIsBoundedAndTraversalTerminates() {
  var p=Compile("sequence Root {\nplay A\n}\ncard A {\nplay Root\n}\n");
  Assert.Equal(CompiledStoryProgram.CurrentVersion,StoryProgramReachability.ForSequence(p,"Root").version);
  var e=await Assert.ThrowsAsync<StorySourceException>(()=>new StoryMachine(p,new DirectorTests.Host()).RunAsync("Root"));Assert.Contains("128",e.Message);
 }
 [Fact] public void StaleProgramsCannotRunOrLink() {
  var p=Compile("sequence Root {\n}\n");p.version=1;
  Assert.Throws<InvalidOperationException>(()=>new StoryMachine(p,new DirectorTests.Host()));
  Assert.Throws<InvalidOperationException>(()=>StoryProgramLinker.Link(p,Array.Empty<StoryLinkUnit>()));
 }
 [Fact] public async Task DirectCardInvocationNumbersAreUniqueAcrossCalls() {
  var p=Compile("card A {\nwait Card.Invocation\n}\nsequence Root {\nplay A\nplay A\n}\n");
  var h=new DirectorTests.Host();await new StoryMachine(p,h).RunAsync("Root");
  Assert.Equal(new[]{1d,2d},h.Commands.Where(c=>c.Verb=="Wait").Select(c=>c.Require("seconds").Numeric));
 }
}
