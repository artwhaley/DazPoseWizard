using System;
using DazPose.Story;
namespace DazPose.Game {
 [Serializable] public sealed class PlayerProfile {
  public int version=1;
  public KinkPreference[] kinks=Array.Empty<KinkPreference>();
  public EquipmentPreference[] equipment=Array.Empty<EquipmentPreference>();
  public int submissionScore=0;
  public float favor=50;
  public PersistentStoryFact[] facts=Array.Empty<PersistentStoryFact>();
  public AssignmentInstance[] assignments=Array.Empty<AssignmentInstance>();
  public SessionHistorySummary[] sessionHistory=Array.Empty<SessionHistorySummary>();
 }
}