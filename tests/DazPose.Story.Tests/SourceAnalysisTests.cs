using DazPose.Story.Language;
namespace DazPose.Story.Tests;
public sealed class SourceAnalysisTests {
 [Fact] public void NewUnboundSourceProducesTypedSourceLocatedRequirements() {
  const string source="setup First {\nLara.StartHiddenAt(Bedside)\n}\nsequence Intro {\nawait Lara.PersistentPose(SexyVamp)\nawait Lara.Say(\"Hello\")\n}\n";
  var analysis=DazStoryCompiler.Analyze(source,"new.dazstory",new StoryAnalysisOptions{SourceGuid="guid-1"});
  Assert.True(analysis.SyntaxValid);Assert.True(analysis.BindingsIncomplete);Assert.NotNull(analysis.Program);
  Assert.Contains(analysis.Requirements,r=>r.Alias=="Bedside"&&r.Kind==StoryBindingKind.Location&&r.Occurrences[0].line==2);
  Assert.Contains(analysis.Requirements,r=>r.Alias=="SexyVamp"&&r.Kind==StoryBindingKind.Pose&&r.Occurrences[0].line==5);
  var speech=Assert.Single(analysis.Requirements,r=>r.IsDialogue);Assert.Equal(StoryDialogue.IdFor("Hello"),speech.Id.Split('|').Last());Assert.Equal("AudioClip",speech.ExpectedType);
  Assert.All(analysis.Requirements,r=>Assert.StartsWith("guid-1|",r.Id));
 }
 [Fact] public void RepeatedAliasHasOneRequirementWithEveryOccurrenceAndStableId() {
  const string source="sequence S {\nawait Lara.PersistentPose(PoseA)\nawait Lara.PersistentSitPose(PoseA)\n}\n";
  var a=DazStoryCompiler.Analyze(source,"s.dazstory",new StoryAnalysisOptions{SourceGuid="source-guid"});
  var requirement=Assert.Single(a.Requirements,r=>r.Alias=="PoseA");
  Assert.Equal(2,requirement.Occurrences.Length);Assert.Equal("source-guid|Pose|PoseA",requirement.Id);
 }
 [Fact] public void AliasTypeCollisionAndUnknownVerbAreSyntaxDiagnostics() {
  var collision=DazStoryCompiler.Analyze("sequence S {\nawait Lara.PersistentPose(Same)\nawait Camera.Track(Same)\n}","s.dazstory");
  Assert.False(collision.SyntaxValid);Assert.Contains(collision.SyntaxDiagnostics,d=>d.Contains("both Pose and Performer"));
  var bad=DazStoryCompiler.Analyze("sequence S {\nawait Lara.NotACommand()\n}","s.dazstory");
  Assert.False(bad.SyntaxValid);Assert.Contains(bad.SyntaxDiagnostics,d=>d.Contains("Unknown performance command"));
 }
 [Theory] [InlineData("await Lara.PersistentPose(AssetName + \" suffix\")")][InlineData("await Lara.PersistentPose(random(1..2))")]
 public void DynamicBindingOperandsExplainStaticResolutionRequirement(string call) {
  var analysis=DazStoryCompiler.Analyze("sequence S {\n"+call+"\n}","s.dazstory");
  Assert.False(analysis.SyntaxValid);Assert.Contains(analysis.SyntaxDiagnostics,d=>d.Contains("symbolic Pose binding"));
 }
 [Fact] public void AnalyzeParityRetainsFlatProgramAndNestedBlocks() {
  string body="Session.Mood += 1\n";for(int i=0;i<32;i++)body="if true {\n"+body+"}\n";
  string source="sequence Nested {\n"+body+"}\n";
  var parsed=DazStoryCompiler.Analyze(source,"nested.dazstory");var compiled=DazStoryCompiler.Compile(source,"nested.dazstory");
  Assert.True(parsed.SyntaxValid);Assert.Equal(compiled.instructions.Select(i=>(i.code,i.name,i.jump)),parsed.Program.instructions.Select(i=>(i.code,i.name,i.jump)));
  Assert.Empty(parsed.Requirements);Assert.Empty(parsed.UnresolvedAssignments);
 }
 [Fact] public void FirstContactAndCrossSourceCardsRetainCurrentCompileLinkParity() {
  var authored=AuthoredStoryTestContent.Compile();
  var source=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"FirstContact.dazstory"));
  var analysis=DazStoryCompiler.Analyze(source,"FirstContact.dazstory",new StoryAnalysisOptions{SourceGuid="first-contact",AllowExternalCards=true});
  Assert.True(analysis.SyntaxValid);var unlinked=DazStoryCompiler.Compile(source,"FirstContact.dazstory",allowExternalCards:true);Assert.Equal(unlinked.instructions.Select(i=>(i.code,i.name,i.jump)),analysis.Program.instructions.Select(i=>(i.code,i.name,i.jump)));
  Assert.Equal(19,analysis.Program.dialogue.Length);
  Assert.NotEmpty(Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"Cards"),"*.dazstory",SearchOption.AllDirectories));
  Assert.Contains(authored.instructions,i=>i.code==StoryCode.Play);
 }
}
