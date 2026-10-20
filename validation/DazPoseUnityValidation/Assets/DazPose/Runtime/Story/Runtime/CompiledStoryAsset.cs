using DazPose.Story.Language;
using UnityEngine;
namespace DazPose.Story {
 public sealed class CompiledStoryAsset : ScriptableObject {
  public string sourceIdentity;
  public CompiledStoryProgram program;
  public StoryContentBindings content;
  public string[] diagnostics=System.Array.Empty<string>();
  public bool IsCompiled => program!=null && program.version==CompiledStoryProgram.CurrentVersion && diagnostics.Length==0;
 }
}