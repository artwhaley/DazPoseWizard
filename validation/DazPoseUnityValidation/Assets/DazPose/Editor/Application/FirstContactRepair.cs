using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using DazPose.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor {
 public static class FirstContactRepair {
  public static bool IsRepairing {get;private set;}
  [MenuItem("DazPose/Content/Advanced/Repair First Contact Bindings")]
  public static void Prepare() {
   if(IsRepairing)return;
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Repair the saved First Contact scene in Edit Mode.");
   if(Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s=>s.isDirty))
    throw new InvalidOperationException("Save or close your modified scenes before explicit First Contact repair. No changes were saved.");
   if(!File.Exists(StorySceneBuilder.ScenePath))throw new InvalidOperationException("Restore the saved First Contact scene; this repair never recreates a test composition.");
   IsRepairing=true;
   try {
    var scene=SceneManager.GetSceneByPath(StorySceneBuilder.ScenePath);
    if(!scene.IsValid() || !scene.isLoaded)scene=EditorSceneManager.OpenScene(StorySceneBuilder.ScenePath);
    var host=GameSessionSceneSetup.InScene<PerformanceHost>(scene).SingleOrDefault()
     ?? throw new InvalidOperationException("Restore the scene's saved Game Session and identities before repairing content bindings.");
    var bindings=host.GetComponent<StorySceneBindings>()
     ?? throw new InvalidOperationException("Restore the saved story bindings; repair will not replace your authored actor/location identities.");
    bool sceneChanged=false;
    if(bindings.lara==null) {bindings.lara=host.Bindings.Performer;sceneChanged=true;}
    if(bindings.playerBody==null && host.Bindings.Player!=null) {bindings.playerBody=host.Bindings.Player.BodyPresence;sceneChanged=true;}
    if(bindings.lara==null || bindings.playerBody==null)throw new InvalidOperationException("Bind the existing Lara and prepared male PlayerBody before content repair.");
    var compiled=AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>(StoryFoundationBuilder.SourcePath);
    if(compiled==null || !compiled.IsCompiled)throw new InvalidOperationException("Fix .dazstory compilation before repairing the saved scene.");
    var content=compiled.content ?? throw new InvalidOperationException("Create/select the source companion bindings in the compiled asset Inspector.");
    var marks=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="Authoring Marks")?.transform;
    foreach(string id in new[]{"lara_start","player_start","stepforward","Lounger","MainPosition1","Bedside"}) {
     var row=bindings.locations.FirstOrDefault(l=>l.id==id);
     if(row!=null && row.placement!=null)continue;
     var seat=id=="Lounger" ? GameSessionSceneSetup.InScene<PerformerSeat>(scene).SingleOrDefault() : null;
     var mark=seat!=null ? seat.ApproachAnchor : marks?.Find(id);
     if(mark==null) {
      var saved=AssetDatabase.LoadAssetAtPath<GameObject>(StorySceneBuilder.MarksPath)?.transform.Find(id);
      if(saved!=null && marks!=null) {
       mark=new GameObject(id).transform;Undo.RegisterCreatedObjectUndo(mark.gameObject,"Restore missing authored mark");
       mark.SetParent(marks,false);mark.SetPositionAndRotation(saved.position,saved.rotation);
      }
     }
     if(mark==null)throw new InvalidOperationException("Bind existing production location '"+id+"'; no approximate/example staging was substituted.");
     if(row==null) {row=new StoryLocationBinding {id=id};bindings.locations=bindings.locations.Concat(new[]{row}).ToArray();}
     row.placement=mark;if(seat!=null){row.seat=seat;row.focus=seat.SeatAnchor;}sceneChanged=true;
    }
    bool contentChanged=StoryImportedPoseSetup.FillMissing(content,bindings);
    foreach(var row in content.motion)if(row.value==null) {row.value=StoryFullSessionSetup.EnsureMotion(row.id);contentChanged=true;}
    var sequence=host.GetComponent<StoryProgramSequence>();
    if(sequence==null) {sequence=Undo.AddComponent<StoryProgramSequence>(host.gameObject);sequence.Configure(compiled,bindings);host.Configure(host.Bindings,sequence);sceneChanged=true;}
    else if(sequence.CompiledSource!=compiled || sequence.Bindings!=bindings)
     throw new InvalidOperationException("The saved sequence has different authored source/bindings. Review them explicitly instead of replacing a valid choice.");
    if(!string.IsNullOrEmpty(sequence.StopBeforePhase)) {sequence.ConfigurePreview("");EditorUtility.SetDirty(sequence);sceneChanged=true;}
    var linked=StoryLinkedContent.Create(compiled);
    var errors=linked.Diagnostics(linked.Program,bindings);
    if(errors.Length>0)throw new InvalidOperationException(string.Join("\n",errors));
    if(contentChanged) {EditorUtility.SetDirty(content);AssetDatabase.SaveAssetIfDirty(content);}
    if(sceneChanged) {EditorUtility.SetDirty(bindings);EditorUtility.SetDirty(host);EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save repaired bindings.");}
    Debug.Log("First Contact bindings checked. Existing marks, poses, audio, wardrobe, scene/actor IDs and artwork were preserved; "+(sceneChanged || contentChanged ? "missing wiring repaired." : "no changes were needed."),host);
   } finally {IsRepairing=false;}
  }
 }
}
