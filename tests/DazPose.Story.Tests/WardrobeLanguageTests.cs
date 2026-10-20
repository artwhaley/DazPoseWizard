using DazPose.Story.Language;
namespace DazPose.Story.Tests;
public sealed class WardrobeLanguageTests {
 static readonly string[] Calls={"Lara.SetOutfit(OpeningOutfit)","Lara.SetHair(OpeningHair)","Lara.ClearHair()","Lara.Outfit.RemoveOneLayer()","Lara.Outfit.AddOneLayer()"};
 [Fact] public void FiveCommandsAreAwaitedAndTypedWithoutChangingExistingEnumValues() {
  var symbols=new Dictionary<string,StoryBindingKind>{{"OpeningOutfit",StoryBindingKind.WardrobePreset},{"OpeningHair",StoryBindingKind.Hair}};
  foreach(string call in Calls) {
   var p=DazStoryCompiler.Compile("sequence Test {\nawait "+call+"\n}","wardrobe.story",symbols);
   Assert.True(p.instructions[0].awaited);
   Assert.Equal(new[]{"Body","Visual","Wardrobe"},StoryVocabulary.Require(p.instructions[0].name,null).RequiredChannels);
   Assert.Throws<StorySourceException>(()=>DazStoryCompiler.Compile("sequence Test {\n"+call+"\n}","wardrobe.story",symbols));
  }
  Assert.Equal(7,(int)StoryBindingKind.Funscript);Assert.Equal(8,(int)StoryBindingKind.WardrobePreset);Assert.Equal(9,(int)StoryBindingKind.Hair);
  Assert.Throws<StorySourceException>(()=>DazStoryCompiler.Compile("sequence Test {\nawait Lara.SetOutfit(OpeningHair)\n}","wardrobe.story",symbols));
 }
 [Fact] public async Task WardrobeConflictNamesRunningBodyOperationAndCancelsIt() {
  var h=new DirectorTests.Host();var pending=new TaskCompletionSource<StoryData>();h.Handler=(c,t)=>{t.Register(()=>pending.TrySetCanceled(t));return pending.Task;};
  var p=DazStoryCompiler.Compile("sequence Test {\nLara.PersistentPose(Pose)\nawait Lara.ClearHair()\n}","conflict.story");
  var e=await Assert.ThrowsAsync<StorySourceException>(()=>new StoryMachine(p,h).RunAsync("Test"));
  Assert.Contains("Body",e.Message);Assert.Contains("PersistentPose",e.Message);Assert.Contains("Await",e.Message);Assert.True(h.Cancelled);
 }
 [Fact] public async Task SuccessfulWardrobeReleasesAllChannelsForFollowingCommands() {
  var h=new DirectorTests.Host();var p=DazStoryCompiler.Compile("sequence Test {\nawait Lara.ClearHair()\nawait Lara.PersistentPose(Pose)\nawait Lara.MagicShader(Material)\nawait Lara.Outfit.AddOneLayer()\n}","channels.story");
  await new StoryMachine(p,h).RunAsync("Test");Assert.Equal(4,h.Commands.Count);
 }
}
