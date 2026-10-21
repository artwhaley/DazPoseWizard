namespace DazPose.Story.Language {
 /// <summary>The fixed player camera heights a story may name: Camera.StartAt(High) and Camera.StartAt(Low).</summary>
 public static class StoryCameraHeights {
  public const string High="High",Low="Low";
  public static bool IsValid(string name)=>name==High || name==Low;
 }
}
