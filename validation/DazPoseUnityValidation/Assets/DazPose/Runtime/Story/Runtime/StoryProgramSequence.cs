using System;
using DazPose.Game;
using DazPose.Performer;
using UnityEngine;
namespace DazPose.Story {
 [DisallowMultipleComponent]
 public sealed class StoryProgramSequence : PerformanceSequence {
  [SerializeField] TextAsset program; // Legacy JSON compatibility only.
  [SerializeField] CompiledStoryAsset compiledSource;
  [SerializeField] string sequenceName="FirstContact";
  [Tooltip("Optional authoring preview: run the exact source up to this phase. Empty runs the full sequence.")]
  [SerializeField] string stopBeforePhase;
  [Header("Session reproducibility and pacing")]
  [SerializeField] bool useFixedSeed=true;
  [SerializeField] int sessionSeed=82017;
  [SerializeField,Min(.001f)] float pacingMultiplier=1;
  public int? SessionSeed=>useFixedSeed ? (int?)sessionSeed : null;
  public float PacingMultiplier=>pacingMultiplier;
  public string StopBeforePhase => stopBeforePhase;
  public void ConfigurePreview(string phase) {stopBeforePhase=phase;}
  [SerializeField] StorySceneBindings storyBindings;
  StoryRuntimeContext active;
  public TextAsset Program => program;
  public CompiledStoryAsset CompiledSource => compiledSource;
  public StorySceneBindings Bindings => storyBindings;
  public StoryRuntimeSnapshot Snapshot { get; private set; }
  public void EmitEvent(string eventId,string value=null) {
   if(active==null) throw new InvalidOperationException("No active story invocation can receive this event.");
   active.CheckAuthority(); active.Events.Emit(eventId,value);
  }
  public void Configure(TextAsset source,StorySceneBindings bindings) {program=source;compiledSource=null;storyBindings=bindings;}
  public string EntrySequence=>sequenceName;
  public void Configure(CompiledStoryAsset source,StorySceneBindings bindings,string entry="FirstContact") {compiledSource=source;program=null;storyBindings=bindings;sequenceName=entry;}
  public override async Awaitable ExecuteAsync(PerformanceRun run) {
   if(compiledSource!=null) {
    if(!compiledSource.IsCompiled) throw new InvalidOperationException(compiledSource.CompilationIssue);
    var linked=StoryLinkedContent.Create(compiledSource);
    string[] missing=linked.MandatoryDiagnostics(storyBindings,sequenceName,stopBeforePhase);
    if(missing.Length>0) throw new InvalidOperationException("First Contact content bindings require attention:\n"+string.Join("\n",missing));
    using(var context=new StoryRuntimeContext(run,storyBindings,GameApplication.EnsureStarted(),sequenceName,SessionSeed,pacingMultiplier)) {
     Observe(context);
     try {await StoryRunner.ExecuteAsync(linked,context,sequenceName,stopBeforePhase);}
     finally {if(ReferenceEquals(active,context))active=null;}
    }
    return;
   }
   if(program==null) throw new StoryProgramException(null,null,null,"Assign the compiled .dazstory asset or a legacy story JSON TextAsset.");
   var document=StoryProgramSerializer.Parse(program.text); StoryProgramValidator.Validate(document,storyBindings);
   using(var context=new StoryRuntimeContext(run,storyBindings,GameApplication.EnsureStarted(),document.id)) {
    Observe(context);
    try {await StoryRunner.ExecuteAsync(document,context);}
    finally {if(ReferenceEquals(active,context))active=null;}
   }
  }
  void Observe(StoryRuntimeContext context) {
   active=context;Snapshot=context.Snapshot;
   if(storyBindings.debugPresenter!=null)storyBindings.debugPresenter.Observe(Snapshot);
  }
 }
}