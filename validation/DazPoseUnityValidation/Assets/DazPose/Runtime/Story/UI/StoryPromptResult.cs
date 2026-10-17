namespace DazPose.Story {
 public sealed class StoryPromptResult {
  public string Value { get; }
  public StoryPromptResult(string value="confirmed") { Value=value; }
 }
}