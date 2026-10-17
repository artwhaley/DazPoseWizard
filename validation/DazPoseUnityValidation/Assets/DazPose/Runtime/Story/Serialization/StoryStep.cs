using System;
namespace DazPose.Story {
 [Serializable] public sealed class StoryStep {
  public string id, op, note, target, asset, key, panel, eventId, value;
  public float number, seconds;
  public bool flag;
  public StoryArgument[] args;
  public StoryCondition condition;
  public StoryStep[] steps, elseSteps;
 }
}