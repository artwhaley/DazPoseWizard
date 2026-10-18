using UnityEditor;
namespace DazPose.Editor {
 // Compatibility entry point. Production composition is now authored by the .dazstory foundation builder.
 public static class StorySceneBuilder {
  public const string ScenePath="Assets/Production/Scenes/FirstContact.unity";
  public const string MarksPath="Assets/Production/Story/FirstContactMarks.prefab";
  public static void BuildOrRefresh()=>StoryFoundationBuilder.Prepare();
 }
}