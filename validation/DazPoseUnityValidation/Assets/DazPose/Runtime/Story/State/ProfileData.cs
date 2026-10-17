using System;
namespace DazPose.Story {
 public enum KinkFrequency { No=0, Normal=1, Frequent=2, VeryFrequent=4 }
 public enum KinkSentiment { Loved, Neutral, Punishment }
 public enum AssignmentStatus { Pending, Completed, Failed, Expired, Cancelled }
 [Serializable] public sealed class KinkPreference {
  public string id;
  public KinkFrequency frequency;
  public KinkSentiment sentiment=KinkSentiment.Neutral;
 }
 [Serializable] public sealed class EquipmentPreference {
  public string id;
  public bool ownedAndWilling, useAsReward, useAsPunishment;
 }
 [Serializable] public sealed class PersistentStoryFact {
  public string key, valueType, stringValue;
  public float numberValue;
  public bool boolValue;
 }
 [Serializable] public sealed class AssignmentInstance {
  public string instanceId, definitionId, issuedAtUtc;
  public AssignmentStatus status;
  public StoryArgument[] parameters=Array.Empty<StoryArgument>();
 }
 [Serializable] public sealed class SessionHistorySummary {
  public string sessionId, endedAtUtc, summary;
 }
}