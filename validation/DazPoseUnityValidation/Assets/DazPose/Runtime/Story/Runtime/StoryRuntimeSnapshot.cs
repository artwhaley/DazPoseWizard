namespace DazPose.Story {
 public sealed class StoryRuntimeSnapshot {
  readonly SessionContract contract;
  readonly SessionState session;
  public Language.DirectorSnapshot Director {get;private set;}
  public Language.CardExecutionContext Card {get;private set;}
  public float Pacing=>contract.pacingMultiplier;
  public string World=>"Posture "+session.world.posture+"; hands "+session.world.hands+"; ankles "+session.world.ankles+"; oral "+session.world.oral+"; anal "+session.world.anal+"; genital "+session.world.genital;
  internal void SetDirector(Language.DirectorSnapshot director,Language.CardExecutionContext card) {Director=director;Card=card;}
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
  internal void SetSource(DazPose.Story.Language.StorySource source,string message) { StepId=source?.ToString(); Opcode=message; }
  internal void SetStep(StoryStep step) { StepId=step.id; Opcode=step.op; }
  internal void SetCheckpoint(string text) { Checkpoint=text; }
  internal void SetEvent(StoryEvent value) { LastEvent=value.Id+(value.Value==null ? "" : " = "+value.Value); }
 }
}