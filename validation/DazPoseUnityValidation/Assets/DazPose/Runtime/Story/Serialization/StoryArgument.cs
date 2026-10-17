using System;
namespace DazPose.Story {
 [Serializable] public sealed class StoryArgument {
  public string name, stringValue, valueType;
  public float numberValue;
  public bool boolValue;
 }
}