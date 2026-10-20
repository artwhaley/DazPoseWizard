using DazPose.Editor.Studio;
using DazPose.Story.Language;
using System.Diagnostics;

namespace DazPose.Rehearsal.Tests;
public class VisualAuthoringTests {
 static StorySourceAnalysis Parse(string text,string owner="root") {
  var a=DazStoryCompiler.Analyze(text,owner+".dazstory",new StoryAnalysisOptions{SourceGuid=owner});
  Assert.True(a.SyntaxValid,string.Join("\n",a.SyntaxDiagnostics));return a;
 }
 static AuthoringCueIndex Index(string text,string global=null,bool complete=true)=>new AuthoringCueIndex(Parse(text).Authoring.Concat(global==null?Array.Empty<StoryAuthoringNode>():Parse(global,"global").Authoring),"root","Test",complete);
 static AuthoringCueState Cue(AuthoringCueIndex i,string name,int occurrence=0)=>i.Cues.Where(c=>c.Node.Name==name).ElementAt(occurrence);
 [Fact] public void ExplicitIntroInheritsSetupAndResolvesOwner(){
  var i=Index("setup Test { Lara.StartHiddenAt(Start); Camera.StartAt(Camera); Lara.StartIdle() }\nsequence Intro { await Lara.LocomoteTo(Step); await Lara.PersistentPose(Vamp); Camera.Track(Lara) }\nsequence Test { play Intro; await Lara.Say(\"Hi\") }");
  var pose=Cue(i,"Lara.PersistentPose").After;
  Assert.Equal("Step",pose[AuthoringField.Placement].Symbol);Assert.Equal("root",pose[AuthoringField.Pose].Source.unit);
  var speech=Cue(i,"Lara.Say").After;Assert.Equal("true",speech[AuthoringField.Tracking].Op);Assert.Equal("Vamp",speech[AuthoringField.Pose].Symbol);
 }
 [Fact] public void SkippedChoicesDoNotLeakAndExplicitAssignmentResolves(){
  var i=Index("sequence Test { await Lara.PersistentPose(A); if Session.Mood > 0 { await Lara.PersistentPose(B) } else { wait 1s }; await Lara.Say(\"Hi\"); await Lara.PersistentPose(C) }");
  Assert.Contains("Inside skipped",Cue(i,"Lara.PersistentPose",1).Block);
  Assert.Equal(AuthoringKnowledge.Unknown,Cue(i,"Lara.Say").Before[AuthoringField.Pose].Knowledge);
  Assert.Equal(AuthoringKnowledge.Baseline,Cue(i,"Lara.Say").Before[AuthoringField.Camera].Knowledge);
  Assert.Equal("C",Cue(i,"Lara.PersistentPose",2).After[AuthoringField.Pose].Symbol);
 }
 [Fact] public void NonvisualLogicHasNoEffectOrExecution(){
  var i=Index("sequence Test { await Lara.PersistentPose(A); let x = random(1..5); while true { wait 1s }; if Session.Mood > 0 { await Lara.Say(\"Hi\") }; repeat x { wait 5s }; await Popup.Ready(\"Wait forever\") }");
  Assert.Equal("A",i.Cues.Last().After[AuthoringField.Pose].Symbol);Assert.Empty(i.Cues.Last().After.Diagnostics);
 }
 [Fact] public void DynamicCardsPreserveStateAndExplicitSeatStillResolves(){
  var i=Index("sequence Test { while Completion < 100% { await Director.DrawAndExecute(tags: [Warmup]) }; await Lara.DissolveTo(Chair, seated: Rest) }","card Beat tags [Warmup]\n{ await Lara.PersistentPose(A) }");
  var block=i.Cues.First().After;Assert.Equal(AuthoringKnowledge.Baseline,block[AuthoringField.Pose].Knowledge);Assert.Equal(AuthoringKnowledge.Baseline,block[AuthoringField.Camera].Knowledge);
  Assert.Equal("seatpose",Cue(i,"Lara.DissolveTo").After[AuthoringField.Placement].Op);Assert.Equal("Chair",Cue(i,"Lara.DissolveTo").After[AuthoringField.Placement].Symbol);
 }
 [Fact] public void MissingCatalogDoesNotInventPersistentCardChanges(){var i=Index("sequence Test { await Director.DrawAndExecute(); await Lara.PersistentPose(A) }",complete:false);Assert.Equal(2,i.Cues.Count);Assert.Equal(AuthoringKnowledge.Baseline,i.Cues.Last().After[AuthoringField.Camera].Knowledge);Assert.Equal("A",i.Cues.Last().After[AuthoringField.Pose].Symbol);}
 [Fact] public void SameLineRowsAreDistinctAndMultilineArgumentsNormalized(){
  var i=Index("sequence Test { await Lara.DissolveTo(Chair,\n seated: Rest); await Lara.PersistentPose(A); await Lara.PersistentPose(A) }");
  Assert.Equal(3,i.Cues.Select(c=>c.Node.Id).Distinct().Count());Assert.Equal(i.Cues[1].After.Key,i.Cues[2].After.Key);Assert.Equal("Rest",i.Cues[0].After[AuthoringField.Pose].Symbol);
 }
 [Fact] public void MultipleCardInputsJoinLocally(){
  var i=Index("card Beat { await Lara.Say(\"Hi\") }\nsequence Test { await Lara.PersistentPose(A); play Beat; await Lara.PersistentPose(B); play Beat }");
  Assert.Equal(AuthoringKnowledge.Unknown,Cue(i,"Lara.Say").Before[AuthoringField.Pose].Knowledge);Assert.Equal(AuthoringKnowledge.Baseline,Cue(i,"Lara.Say").Before[AuthoringField.Camera].Knowledge);
 }
 [Fact] public void RecursionAndInfiniteLoopsTerminate(){var i=Index("card Beat { play Beat }\nsequence Test { play Beat; while true { await Lara.PersistentPose(A) } }");Assert.NotEmpty(i.Cues.Single(c=>c.Node.Kind=="while").After.Diagnostics);Assert.True(i.StatementsAnalyzed<30);}
 [Fact] public void SeatedPoseCannotInventIncomingSeat(){var i=Index("card Beat { await Lara.PersistentSitPose(Rest) }\nsequence Test { wait 1s }");var s=Cue(i,"Lara.PersistentSitPose").After;Assert.Equal(AuthoringKnowledge.Unknown,s[AuthoringField.Placement].Knowledge);}
 [Fact] public void LocomotionHeightRecipeDoesNotGrowWithMoves(){
  var text="setup Test { Lara.StartHiddenAt(Start) }\nsequence Test {"+string.Concat(Enumerable.Repeat("await Lara.LocomoteTo(Step);",1000))+"}";var i=Index(text);var v=i.Cues.Last().After[AuthoringField.Placement];Assert.Equal("full",v.Parent.Op);Assert.Null(v.Parent.Parent);
 }
 [Fact] public void TenThousandCueRandomLookupDoesNotAnalyzeEarlierCues(){
  var i=Index("sequence Test { await Lara.PersistentPose(A);"+string.Concat(Enumerable.Repeat("wait 1s;",10000))+"}");int work=i.StatementsAnalyzed;var ids=i.Cues.Select(c=>c.Node.Id).ToArray();var watch=Stopwatch.StartNew();for(int n=0;n<10000;n++)Assert.NotNull(i.Find(ids[(n*7919)%ids.Length]));
  Assert.Equal(work,i.StatementsAnalyzed);Assert.Equal(i.Cues[0].After.Key,i.Cues.Last().After.Key);System.Console.WriteLine($"Visual index {i.BuildMilliseconds:F2}ms; 10000 direct lookups {watch.Elapsed.TotalMilliseconds:F2}ms; statements {work}");
 }
 [Fact] public void BindingOwnersNeverCollapse(){var i=Index("sequence Test { await Lara.PersistentPose(Shared); play Other }","card Other tags [Main]\n{ await Lara.PersistentPose(Shared) }");var values=i.Cues.Where(c=>c.Node.Name=="Lara.PersistentPose").Select(c=>c.After[AuthoringField.Pose]).ToArray();Assert.NotEqual(values[0].Key,values[1].Key);}
 [Fact] public void CancellationIsHonored(){var c=new CancellationTokenSource();c.Cancel();Assert.Throws<OperationCanceledException>(()=>new AuthoringCueIndex(Parse("sequence Test { wait 1s }").Authoring,"root","Test",token:c.Token));}
 [Fact] public void AnalysisDoesNotChangeExecutableInstructions(){var a=Parse("sequence Test { if Session.Mood > 0 { wait 1s } else { wait 2s } }");Assert.Contains(a.Program.instructions,i=>i.code==StoryCode.JumpFalse);Assert.Equal("if",a.Authoring.Single().Body.Single().Kind);}

 [Fact] public void ActualFirstContactLateCueDoesNotTraverseDirectorLoops(){
  var root=Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"FirstContact.dazstory")));
  var cards=Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"FirstContactEngineering.dazstory")),"cards");
  var index=new AuthoringCueIndex(root.Authoring.Concat(cards.Authoring),"root","FirstContact");
  var sexy=index.Cues.Single(c=>c.Node.Name=="Lara.PersistentPose"&&c.Node.Literals["pose"]=="SexyVamp"&&c.Node.Source.unit=="root");
  Assert.Equal("stepforward",sexy.After[AuthoringField.Placement].Symbol);Assert.Equal("true",sexy.After[AuthoringField.Tracking].Op);
  var sassy=index.Cues.Single(c=>c.Node.Name=="Lara.PersistentPose"&&c.Node.Literals["pose"]=="Sassy"&&c.Node.Source.unit=="root");
  Assert.Equal("lara_start",sassy.After[AuthoringField.Placement].Symbol);Assert.Empty(sassy.After.Diagnostics);
  Assert.Same(sassy,index.Find(sassy.Node.Id));
 }
 [Fact] public void RapidSelectionOnlyReturnsLatestAndStopInvalidatesIt(){
  var index=Index("sequence Test { wait 1s; wait 2s; wait 3s }");var q=new AuthoringSelectionQueue();
  q.Select(index.Cues[0],true);q.Select(index.Cues[1],true);q.Select(index.Cues[2],false);
  Assert.True(q.TryTake(out var next));Assert.Same(index.Cues[2],next.Cue);Assert.False(next.After);Assert.Equal(2,q.Discarded);Assert.False(q.TryTake(out _));
  q.Select(index.Cues[0],true);int generation=q.Generation;q.Cancel();Assert.True(q.Generation>generation);Assert.False(q.TryTake(out _));
 }
 [Fact] public void GroupPoseResetPreservesCameraAndSparseReplacementClearsOldBones(){
  int bone=1,face=2,camera=3;var ledger=new PreviewPropertyLedger();ledger.Capture("bone",()=>bone,v=>bone=v,group:"pose");ledger.Capture("face",()=>face,v=>face=v,group:"pose");ledger.Capture("camera",()=>camera,v=>camera=v,group:"spatial");
  bone=8;face=9;camera=10;ledger.RecordApplied();ledger.PrepareRestore("pose");ledger.Restore("pose");bone=20;ledger.RecordApplied("pose");
  Assert.Equal(2,face);Assert.Equal(10,camera);ledger.PrepareRestore();ledger.Restore();Assert.Equal(1,bone);Assert.Equal(3,camera);
 }
 [Fact] public void SourceTextRetainsDynamicArgumentsAndSameLineStatementSpans(){
  var index=Index("sequence Test { await Director.DrawAndExecute(\n equipment: Session.PreparedEquipment\n); wait 1s; wait 2s }");
  Assert.Contains("Session.PreparedEquipment",index.Cues[0].Node.Text);Assert.Equal("wait 1s",index.Cues[1].Node.Text);Assert.Equal("wait 2s",index.Cues[2].Node.Text);
 }
 [Fact] public void SharedStandingConventionMatchesRuntimeEndpoint(){Assert.Equal(.5f,DazPose.Performer.PoseSamplingConventions.StandingSeconds);}

 [Fact] public void DynamicCardIdentifierPreservesCallerPresentation(){
  var index=Index("card Beat { await Lara.PersistentPose(A) }\nsequence Test { let Beat = \"Other\"; await Director.Execute(Beat) }");
  var call=Cue(index,"Director.Execute");Assert.False(call.Node.Literals.ContainsKey("card"));Assert.Equal(AuthoringKnowledge.Baseline,call.After[AuthoringField.Pose].Knowledge);
 }
 [Fact] public void DynamicTagExpressionsProtectCallerPresentation(){
  var index=Index("sequence Test { let labels = [Warmup]; await Director.DrawAndExecute(tags: labels) }","card Beat tags [Main]\n{ await Lara.PersistentPose(A) }");
  var call=Cue(index,"Director.DrawAndExecute");Assert.False(call.Node.Literals.ContainsKey("tags"));Assert.Equal(AuthoringKnowledge.Baseline,call.After[AuthoringField.Pose].Knowledge);
 }
 [Fact] public void ExplicitCameraStartCancelsTrackingAndStandingDissolveResetsPose(){
  var index=Index("setup Test { Lara.StartHiddenAt(Start); Camera.StartAt(Camera); Lara.StartIdle() }\nsequence Test { Camera.Track(Lara); await Lara.PersistentPose(A); await Lara.DissolveTo(End) }");
  var setup=Cue(index,"Camera.StartAt");Assert.Equal("false",setup.After[AuthoringField.Tracking].Op);
  var end=Cue(index,"Lara.DissolveTo").After;Assert.Equal("initial",end[AuthoringField.Pose].Op);Assert.Equal("yaw",end[AuthoringField.Placement].Op);Assert.Equal("true",end[AuthoringField.Tracking].Op);
 }

 [Fact] public void MaterialAndBodyVisibilitySurviveStandingReset(){
  var index=Index("sequence Test { await Lara.PersistentPose(A); await Lara.MagicShader(Wire); await PlayerBody.DissolveIn(); await Lara.DissolveTo(End) }");
  var state=Cue(index,"Lara.DissolveTo").After;Assert.Equal("initial",state[AuthoringField.Pose].Op);Assert.Equal("Wire",state[AuthoringField.Material].Symbol);Assert.Equal("false",state[AuthoringField.BodyHidden].Op);
 }
 [Theory][InlineData(true)][InlineData(false)] public void GestureNeverReplacesPersistentVisualPose(bool awaited){
  var index=Index("sequence Test { await Lara.PersistentPose(A); "+(awaited?"await ":"")+"Lara.PlayOneShotGesture(Wave) }");Assert.Equal("A",index.Cues.Last().After[AuthoringField.Pose].Symbol);Assert.Equal(index.Cues[0].After.Key,index.Cues[1].After.Key);
 }

 [Fact] public void OutfitAndHairAreIndependentAndLayersUseSparseSemantics(){
  var index=Index("sequence Test { await Lara.SetOutfit(Opening); await Lara.SetHair(Hair); await Lara.Outfit.RemoveOneLayer(); await Lara.Outfit.RemoveOneLayer(); await Lara.Outfit.RemoveOneLayer(); await Lara.Outfit.AddOneLayer(); await Lara.SetOutfit(Other) }");
  var remove=index.Cues.Where(c=>c.Node.Name=="Lara.Outfit.RemoveOneLayer").ToArray();
  Assert.Equal(1,AuthoringVisualReducer.LayerCeiling(remove[0].After[AuthoringField.Layers],6));
  Assert.Equal(-1,AuthoringVisualReducer.LayerCeiling(remove[1].After[AuthoringField.Layers],6));
  Assert.Equal(-1,AuthoringVisualReducer.LayerCeiling(remove[2].After[AuthoringField.Layers],6));
  Assert.Equal(1,AuthoringVisualReducer.LayerCeiling(Cue(index,"Lara.Outfit.AddOneLayer").After[AuthoringField.Layers],6));
  Assert.Equal("Hair",index.Cues.Last().After[AuthoringField.Hair].Symbol);
  Assert.Equal(2,AuthoringVisualReducer.LayerCeiling(index.Cues.Last().After[AuthoringField.Layers],6));
 }
 [Fact] public void ProtectedCardReturnsEveryFieldAndExposesStableReturnCue(){
  var index=Index("card Beat { await Lara.SetOutfit(Other); await Lara.ClearHair(); await Lara.PersistentPose(Pose); if Session.Mood > 0 { await Lara.Outfit.RemoveOneLayer() } }\nsequence Test { await Lara.SetOutfit(Opening); await Lara.SetHair(Hair); play Beat; wait 1s }");
  var call=Cue(index,"Beat");Assert.Equal(call.Before.Key,call.After.Key);
  var row=index.Cues.Single(c=>c.Node.Kind=="return");Assert.EndsWith("/return",row.Node.Id);Assert.Equal("Opening",row.After[AuthoringField.Outfit].Symbol);Assert.Equal("Hair",row.After[AuthoringField.Hair].Symbol);
  Assert.Equal("Other",Cue(index,"Lara.ClearHair").After[AuthoringField.Outfit].Symbol);Assert.Equal("clear",Cue(index,"Lara.ClearHair").After[AuthoringField.Hair].Op);
 }
 [Fact] public void SequenceWardrobeChangesPersistAndDynamicCardsAreIdentity(){
  var index=Index("sequence Dress { await Lara.SetOutfit(Opening); await Lara.SetHair(Hair) }\nsequence Test { play Dress; await Director.DrawAndExecute(); wait 1s }");
  var last=index.Cues.Last().After;Assert.Equal("Opening",last[AuthoringField.Outfit].Symbol);Assert.Equal("Hair",last[AuthoringField.Hair].Symbol);Assert.Empty(last.Diagnostics);
 }
 [Fact] public void RepeatedLayerHelperCallsHaveBoundedRecipeAndWork(){
  var index=Index("sequence Strip { await Lara.Outfit.RemoveOneLayer() }\nsequence Test { await Lara.SetOutfit(Opening);"+string.Concat(Enumerable.Repeat("play Strip;",1000))+"wait 1s }");
  var value=index.Cues.Last().After[AuthoringField.Layers];Assert.Equal(-1,AuthoringVisualReducer.LayerCeiling(value,6));Assert.Equal("full",value.Parent.Op);Assert.Null(value.Parent.Parent);Assert.Equal(32,value.Symbol.Length);
 }
 [Fact] public void UnknownWardrobeResolvesAfterExplicitAssignments(){
  var index=Index("sequence Test { if Session.Mood > 0 { await Lara.SetOutfit(Other) }; wait 1s; await Lara.SetOutfit(Opening) }");
  Assert.Equal(AuthoringKnowledge.Unknown,Cue(index,"Wait").Before[AuthoringField.Outfit].Knowledge);
  Assert.Equal("Opening",index.Cues.Last().After[AuthoringField.Outfit].Symbol);Assert.Empty(index.Cues.Last().After.Diagnostics);
  Assert.Equal(11,AuthoringVisualState.FieldCount);Assert.Equal((1<<11)-1,AuthoringVisualState.AllMask);
 }
}
