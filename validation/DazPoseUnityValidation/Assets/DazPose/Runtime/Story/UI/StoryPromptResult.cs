namespace DazPose.Story {
 public sealed class StoryPromptResult {
  public string Value { get; }
  public bool Applied=>Value=="confirmed";
  public StoryPromptResult(string value="confirmed") { Value=value; }
 }
}