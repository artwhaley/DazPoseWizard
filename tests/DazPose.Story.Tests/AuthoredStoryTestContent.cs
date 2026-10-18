using DazPose.Story.Language;
namespace DazPose.Story.Tests;
internal static class AuthoredStoryTestContent {
 internal static CompiledStoryProgram Compile() {
  var root=DazStoryCompiler.Compile(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"FirstContact.dazstory")),"FirstContact.dazstory",allowExternalCards:true);
  var folder=Path.Combine(AppContext.BaseDirectory,"Cards");
  var units=Directory.Exists(folder) ? Directory.GetFiles(folder,"*.dazstory",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>new StoryLinkUnit(Path.GetRelativePath(folder,p).Replace('\\','/'),DazStoryCompiler.Compile(File.ReadAllText(p),p,allowExternalCards:true))).ToArray() : Array.Empty<StoryLinkUnit>();
  return units.Length==0 ? root : StoryProgramLinker.Link(root,units);
 }
}
