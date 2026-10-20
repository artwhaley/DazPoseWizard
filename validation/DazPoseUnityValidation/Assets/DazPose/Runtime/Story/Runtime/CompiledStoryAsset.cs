using DazPose.Story.Language;
using UnityEngine;
namespace DazPose.Story {
 public sealed class CompiledStoryAsset : ScriptableObject {
  public string sourceIdentity;
  public CompiledStoryProgram program;
  public StoryContentBindings content;
  public string[] diagnostics=System.Array.Empty<string>();
  public string CompilationIssue => program==null ? "Reimport the .dazstory source; no compiled program is available." : program.version!=CompiledStoryProgram.CurrentVersion ? "Reimport the .dazstory source; compiled program version "+program.version+" is stale (expected "+CompiledStoryProgram.CurrentVersion+")." : string.Join("\n",diagnostics);
  public bool IsCompiled => program!=null && program.version==CompiledStoryProgram.CurrentVersion && diagnostics.Length==0;
 }
}