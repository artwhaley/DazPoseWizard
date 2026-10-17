using System;
namespace DazPose.Story {
 [Serializable] public sealed class StoryCondition {
  public string scope, key, comparison, stringValue, valueType;
  public float numberValue;
  public bool boolValue;
 }
}