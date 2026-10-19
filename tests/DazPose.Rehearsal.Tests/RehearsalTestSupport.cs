using DazPose.Editor;
using DazPose.Game;
using DazPose.Motion;
using DazPose.Performer;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEngine;
namespace DazPose.Rehearsal.Tests;
internal static class RehearsalTestSupport {
 public static readonly string RootSource=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"FirstContact.dazstory"));
 public static readonly string CardSource=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"FirstContactEngineering.dazstory"));
 public static readonly CompiledStoryProgram Program=StoryProgramLinker.Link(
  DazStoryCompiler.Compile(RootSource,"FirstContact.dazstory",allowExternalCards:true),
  new[]{new StoryLinkUnit("FirstContactEngineering",DazStoryCompiler.Compile(CardSource,"FirstContactEngineering.dazstory",allowExternalCards:true))},"FirstContact");
 public static readonly KinkCatalog Kinks=KinkCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"kink-catalog.json")));
 public static readonly EquipmentCatalog Equipment=EquipmentCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"equipment-catalog.json")));
 public static PlayerProfile Profile()=>new PlayerProfile {favor=50,kinks=Kinks.entries.Select(k=>new KinkPreference{id=k.id,frequency=KinkFrequency.Normal,sentiment=KinkSentiment.Neutral}).ToArray(),equipment=Equipment.entries.Select(e=>new EquipmentPreference{id=e.id,ownedAndWilling=false}).ToArray()};
 public static FixtureContent Bindings(CompiledStoryProgram program) {
  var bindings=new StoryContentBindings();var locations=new HashSet<string>(StringComparer.Ordinal);
  foreach(var use in program.bindings) {
   switch(use.kind) {
    case StoryBindingKind.Pose:bindings.poses[use.symbol]=new StoryPoseContent{value=new PerformerPose{Clip=new object()},posture=use.symbol=="Lounging"?PlayerPosture.Sitting:PlayerPosture.Standing};break;
    case StoryBindingKind.Gesture:bindings.gestures[use.symbol]=new PerformerGesture();break;
    case StoryBindingKind.MagicShaderProfile:bindings.shaders[use.symbol]=new PerformerMagicShaderProfile();break;
    case StoryBindingKind.Funscript:bindings.motion[use.symbol]=new FunscriptMotionProgram();break;
    case StoryBindingKind.Location:locations.Add(use.symbol);break;
   }
  }
  foreach(var line in program.dialogue)bindings.dialogue[line.source.unit+"|"+line.id]=(line.transcript,new AudioClip());
  return new FixtureContent(bindings,new StorySceneBindings(locations));
 }
 public sealed record FixtureContent(StoryContentBindings Content,StorySceneBindings Scene);
 public static RehearsalSession Session(CompiledStoryProgram program,RehearsalScenario scenario=null,PlayerProfile sourceProfile=null) {
  var fixture=Bindings(program);return new RehearsalSession(program,"FirstContact",sourceProfile??Profile(),Kinks,Equipment,fixture.Content,null,fixture.Scene,scenario);
 }
 public static string WaitKey(CompiledStoryProgram program) {
  var pair=program.instructions.Select((instruction,pc)=>(instruction,pc)).Single(x=>x.instruction.code==StoryCode.WaitAny&&x.instruction.source.file.EndsWith("FirstContact.dazstory",StringComparison.Ordinal));
  return StoryRehearsalController.SourceKey(pair.instruction.source,pair.pc);
 }
}
