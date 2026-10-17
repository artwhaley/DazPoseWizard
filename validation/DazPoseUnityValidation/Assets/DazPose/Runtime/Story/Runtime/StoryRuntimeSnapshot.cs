namespace DazPose.Story {
 public sealed class StoryRuntimeSnapshot {
  readonly SessionContract contract;
  readonly SessionState session;
  public string StoryId { get; }
  public string StepId { get; private set; }
  public string Opcode { get; private set; }
  public string Checkpoint { get; private set; }
  public string LastEvent { get; private set; }
  public string Phase => session.phase;
  public float PhaseCompletion => session.phaseCompletion;
  public float Mood => session.mood;
  public float Intensity => session.intensityMultiplier;
  public int Seed => contract.randomSeed;
  public string PreparedEquipment => string.Join(", ",contract.preparedEquipmentIds);
  public string ActiveEquipment => string.Join(", ",session.activeEquipmentIds);
  public int ClothingCount => session.world.ClothingCount;
  public string Clothing => session.world.ClothingDescription;
  public StoryRuntimeSnapshot(string id,SessionContract contract,SessionState session) { StoryId=id; this.contract=contract; this.session=session; }
  internal void SetStep(StoryStep step) { StepId=step.id; Opcode=step.op; }
  internal void SetCheckpoint(string text) { Checkpoint=text; }
  internal void SetEvent(StoryEvent value) { LastEvent=value.Id+(value.Value==null ? "" : " = "+value.Value); }
 }
}