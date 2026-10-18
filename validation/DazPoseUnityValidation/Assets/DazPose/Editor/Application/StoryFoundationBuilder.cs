namespace DazPose.Editor {
 // Compatibility names for callers; one preservation-aware repair owns authored First Contact.
 public static class StoryFoundationBuilder {
  public const string SourcePath="Assets/Production/Story/Programs/FirstContact.dazstory";
  public const string ContentPath="Assets/Production/Story/Programs/FirstContact.bindings.asset";
  public const string DialogueMapPath="Assets/Production/Story/Programs/FirstContact.dialogue-map.json";
  public static bool IsBuilding=>FirstContactRepair.IsRepairing;
  public static void Prepare()=>FirstContactRepair.Prepare();
 }
}
