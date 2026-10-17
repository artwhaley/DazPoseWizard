namespace DazPose.Story {
 public sealed class StoryEvent {
  public string Id { get; }
  public string Value { get; }
  public StoryEvent(string id,string value) { Id=id; Value=value; }
 }
}