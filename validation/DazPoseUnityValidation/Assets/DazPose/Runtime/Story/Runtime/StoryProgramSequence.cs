using System;
using DazPose.Game;
using DazPose.Performer;
using UnityEngine;
namespace DazPose.Story {
 [DisallowMultipleComponent]
 public sealed class StoryProgramSequence : PerformanceSequence {
  [SerializeField] TextAsset program;
  [SerializeField] StorySceneBindings storyBindings;
  StoryRuntimeContext active;
  public void EmitEvent(string eventId,string value=null) {
   if(active==null) throw new InvalidOperationException("No active story invocation can receive this event.");
   active.CheckAuthority(); active.Events.Emit(eventId,value);
  }
  public TextAsset Program => program;
  public StorySceneBindings Bindings => storyBindings;
  public StoryRuntimeSnapshot Snapshot { get; private set; }
  public void Configure(TextAsset source,StorySceneBindings bindings) { program=source; storyBindings=bindings; }
  public override async Awaitable ExecuteAsync(PerformanceRun run) {
   if(program==null) throw new StoryProgramException(null,null,null,"Assign the story JSON TextAsset.");
   // Reparse on every Start so Editor asset edits never leave a stale instruction graph.
   var document=StoryProgramSerializer.Parse(program.text);
   StoryProgramValidator.Validate(document,storyBindings);
   var app=GameApplication.EnsureStarted();
   using(var context=new StoryRuntimeContext(run,storyBindings,app,document.id)) {
    active=context;
    Snapshot=context.Snapshot;
    if(storyBindings.debugPresenter!=null) storyBindings.debugPresenter.Observe(Snapshot);
    try { await StoryRunner.ExecuteAsync(document,context); }
    finally { if(ReferenceEquals(active,context)) active=null; }
   }
  }
 }
}