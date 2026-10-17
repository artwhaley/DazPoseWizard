using System;
namespace DazPose.Story {
 [Serializable] public sealed class EquipmentDefinition {
  public string id,displayName;
  public string[] identityTags=Array.Empty<string>(),themeTags=Array.Empty<string>(),excludesTags=Array.Empty<string>();
 }
}