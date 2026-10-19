using System;
namespace DazPose.Story {
 [Serializable] public sealed class KinkDefinition {
  public string id,displayName;
  public string description="",displayCategory="";
  public string[] themeTags=Array.Empty<string>();
 }
}