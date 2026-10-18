using DazPose.Story.Language;
namespace DazPose.Story.Tests;
public sealed class GlobalCardTests {
 static CompiledStoryProgram Compile(string text,string file="test")=>DazStoryCompiler.Compile(text,file,allowExternalCards:true);
 const string Root="card Intro {\nSession.Mood += 1\n}\nsequence Test {\nphase Warmup {\nawait Director.DrawAndExecute(tags: [Warmup])\n}\n}\n";
 static string Card(string name,string body="Session.Mood += 2")=>"card "+name+"\ntags [Warmup]\nprogress 10\n{\n"+body+"\n}\n";
 [Fact] public async Task NewPublishedCardEntersExistingRootAndRemovalLeavesNoCandidates() {
  var root=Compile(Root);var unit=new StoryLinkUnit("new",Compile(Card("Added")));
  var linked=StoryProgramLinker.Link(root,new[]{unit});var host=new DirectorTests.Host();
  await new StoryMachine(linked,host).RunAsync("Test");
  Assert.Equal(new[]{"Added"},host.Selected);Assert.Equal(52,host.Read("Session.Mood").Numeric);
  Assert.False(linked.cards.Single(c=>c.Id=="Intro").Selectable);
  Assert.Single(root.cards);Assert.False(root.linked);Assert.False(unit.Program.linked);
  var removed=StoryProgramLinker.Link(root,Array.Empty<StoryLinkUnit>());
  await Assert.ThrowsAsync<StorySourceException>(()=>new StoryMachine(removed,new DirectorTests.Host()).RunAsync("Test"));
 }
 [Fact] public void DuplicatePublicIdsAndLocalShadowingIdentifySource() {
  var root=Compile(Root);
  var error=Assert.Throws<StorySourceException>(()=>StoryProgramLinker.Link(root,new[]{new StoryLinkUnit("a",Compile(Card("Same"),"a.story")),new StoryLinkUnit("b",Compile(Card("Same"),"b.story"))}));
  Assert.Contains("Duplicate public card ID",error.Message);Assert.Contains("b.story",error.Message);
  Assert.Throws<StorySourceException>(()=>StoryProgramLinker.Link(root,new[]{new StoryLinkUnit("a",Compile(Card("Intro")))}));
 }
 [Fact] public async Task PrivateHelpersStaySourceLocalAndNestedLoopsAndExpressionsRelocate() {
  var root=Compile("sequence Test {\nplay A\nplay B\n}\n");
  string Source(string name,int increment)=>Card(name,"play Helper")+"card Helper {\nrepeat 2 {\nif true && (false || true) {\nSession.Mood += "+increment+"\n}\n}\n}\n";
  var linked=StoryProgramLinker.Link(root,new[]{new StoryLinkUnit("a",Compile(Source("A",2))),new StoryLinkUnit("b",Compile(Source("B",3)))});
  var host=new DirectorTests.Host();await new StoryMachine(linked,host).RunAsync("Test");
  Assert.Equal(60,host.Read("Session.Mood").Numeric);
  Assert.Equal(2,linked.cards.Count(c=>c.LocalId=="Helper"));
  Assert.All(host.Commands,c=>Assert.NotNull(c.Source.unit));
 }
 [Fact] public async Task CrossSourceExplicitCallsUseTheSameHistoryAndSourceOwnership() {
  var root=Compile("sequence Test {\nphase Warmup {\nawait Director.Execute(A)\n}\n}\n");
  var linked=StoryProgramLinker.Link(root,new[]{new StoryLinkUnit("a",Compile(Card("A","await Director.Execute(B)"))),new StoryLinkUnit("b",Compile(Card("B","await Lara.Say(\"from B\")")))});
  var host=new DirectorTests.Host();var machine=new StoryMachine(linked,host);await machine.RunAsync("Test");
  Assert.Equal("b",host.Commands.Single(c=>c.Verb=="Lara.Say").Source.unit);
  Assert.Equal(1,machine.Director.History.For("B").TimesUsed);
  Assert.Equal(1,machine.Director.History.For("A").TimesUsed);
  Assert.Equal(20,host.Read("Completion").Numeric);
 }
 sealed class ResourceHost : DirectorTests.Host,IStoryCardEligibility {
  public string ResourceIssue(CardDefinition card)=>card.Id=="Missing" ? "Missing scene location" : null;
 }
 [Fact] public async Task UnavailableCardDoesNotBlockOtherCardsAndExplicitCallHasNoSideEffects() {
  var sources=new[]{new StoryLinkUnit("a",Compile(Card("Missing","Session.Mood += 100"))),new StoryLinkUnit("b",Compile(Card("Ready")))};
  var linked=StoryProgramLinker.Link(Compile(Root),sources);var host=new ResourceHost();
  await new StoryMachine(linked,host).RunAsync("Test");Assert.Equal(new[]{"Ready"},host.Selected);
  Assert.Contains(host.Rejected,r=>r.Contains("Missing scene location"));
  linked=StoryProgramLinker.Link(Compile("sequence Test {\nplay Missing\n}\n"),sources);host=new ResourceHost();
  await Assert.ThrowsAsync<StorySourceException>(()=>new StoryMachine(linked,host).RunAsync("Test"));Assert.Equal(50,host.Read("Session.Mood").Numeric);
 }
}
