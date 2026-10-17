using System;
namespace DazPose.Story {
 [Serializable] public sealed class StoryProgramDocument {
  public string schema = "dazpose.story";
  public int version = 1;
  public string id, title;
  public StoryStep[] steps = Array.Empty<StoryStep>();
 }
}