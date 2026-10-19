using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DazPose.Performer;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DazPose.Editor.Studio {
 // Explicit Edit Mode diagnostic only; never run on load, never creates a saved scene.
 public static class StudioCorrectiveChecks {
  static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
  static void Refuses(Action action){try{action();}catch(InvalidOperationException){return;}throw new InvalidOperationException("Ambiguous/corrupt bindings were accepted.");}
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  [MenuItem("DazPose/Development/Validation/Studio Corrective Checks (Edit Mode)")]
  public static void Run() {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before this Edit Mode check.");
   if(AnimationMode.InAnimationMode())throw new InvalidOperationException("Close the existing Animation Mode owner first.");
   string folder="Assets/__StudioCorrective_"+Guid.NewGuid().ToString("N");AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
   var previewScene=EditorSceneManager.NewPreviewScene();SceneDirectorStudioWindow window=null;
   try {
    string path=folder+"/Source.dazstory";File.WriteAllText(path,"sequence Test {\nawait Lara.PersistentPose(Standing)\n}\n");AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
    string guid=AssetDatabase.AssetPathToGUID(path);var bindings=SourceBindingReconciler.ReconcileAsset(path,guid,false);
    var pose=ScriptableObject.CreateInstance<PerformerPose>();AssetDatabase.CreateAsset(pose,folder+"/Pose.asset");
    bindings.poses.Single().value=pose;bindings.poses.Single().posture=PlayerPosture.Sitting;bindings.ownerSourceGuid="";EditorUtility.SetDirty(bindings);AssetDatabase.SaveAssetIfDirty(bindings);
    Require(SourceBindingReconciler.ReconcileAsset(path,guid,false)==bindings&&bindings.ownerSourceGuid==guid,"Non-Undo migration did not backfill source GUID.");
    Require(bindings.poses.Single().value==pose&&bindings.poses.Single().posture==PlayerPosture.Sitting,"Migration changed existing references/metadata.");
    string bindingPath=AssetDatabase.GetAssetPath(bindings);byte[] first=File.ReadAllBytes(bindingPath);SourceBindingReconciler.ReconcileAsset(path,guid,false);
    Require(first.SequenceEqual(File.ReadAllBytes(bindingPath))&&!EditorUtility.IsDirty(bindings),"No-op reconcile wrote or dirtied the asset.");
    string moved=folder+"/Renamed.dazstory";Require(string.IsNullOrEmpty(AssetDatabase.MoveAsset(path,moved)),"Source move failed.");
    Require(SourceBindingReconciler.FindOwner(guid,SourceBindingReconciler.CompanionPath(moved))==bindings,"Move lost GUID ownership.");
    var duplicate=ScriptableObject.CreateInstance<StoryContentBindings>();duplicate.ownerSourceGuid=guid;AssetDatabase.CreateAsset(duplicate,folder+"/Duplicate.asset");Refuses(()=>SourceBindingReconciler.ReconcileAsset(moved,guid,false));AssetDatabase.DeleteAsset(folder+"/Duplicate.asset");
    bindings.ownerSourceGuid="";var foreign=ScriptableObject.CreateInstance<CompiledStoryAsset>();foreign.sourceIdentity=Guid.NewGuid().ToString("N");foreign.content=bindings;AssetDatabase.CreateAsset(foreign,folder+"/Foreign.asset");
    Refuses(()=>SourceBindingReconciler.FindOwner(guid,bindingPath));AssetDatabase.DeleteAsset(folder+"/Foreign.asset");bindings.ownerSourceGuid=guid;
    bindings.sceneSymbols=new[]{new StorySceneSymbol{id="Standing",kind=StoryBindingKind.Location}};string before=EditorJsonUtility.ToJson(bindings);Refuses(()=>SourceBindingReconciler.ReconcileAsset(moved,guid,false));Require(before==EditorJsonUtility.ToJson(bindings),"Refusal partially changed bindings.");bindings.sceneSymbols=Array.Empty<StorySceneSymbol>();
    string broken=folder+"/Broken.dazstory";File.WriteAllText(broken,"sequence Broken { mystery");Refuses(()=>SourceBindingReconciler.ReconcileAsset(broken,Guid.NewGuid().ToString("N"),false));Require(!File.Exists(SourceBindingReconciler.CompanionPath(broken)),"Broken syntax created a companion.");
    var hostObject=new GameObject("Transient host"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(hostObject,previewScene);var host=hostObject.AddComponent<StorySceneBindings>();
    var mark=new GameObject("Mark"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(mark,previewScene);host.locations=new[]{new StoryLocationBinding{id="Mark",placement=mark.transform}};
    window=ScriptableObject.CreateInstance<SceneDirectorStudioWindow>();window.hideFlags=HideFlags.HideAndDontSave;
    typeof(SceneDirectorStudioWindow).GetField("host",Private).SetValue(window,host);var capture=typeof(SceneDirectorStudioWindow).GetMethod("CaptureStamp",Private);
    string stamp=(string)capture.Invoke(window,null);mark.transform.position=Vector3.one;Require(stamp!=(string)capture.Invoke(window,null),"Saved-mark staging revision did not change the source stamp.");
    typeof(SceneDirectorStudioWindow).GetMethod("CheckRevision",Private).Invoke(window,null);
    var stage=(ScenePreviewSession)typeof(SceneDirectorStudioWindow).GetField("stage",Private).GetValue(window);
    stage.Build(null,mark,null);Require(stage.Ready,"Transient preview failed to build.");
    // Preview transforms and camera drafts must not affect the authored-host stamp.
    stamp=(string)capture.Invoke(window,null);stage.Lara.position=Vector3.right*3;stage.PreviewCameraAt(new Vector3(0,1,-4),Quaternion.identity);stage.TrackLara();stage.UpdateTrackedCamera();
    Require(stamp==(string)capture.Invoke(window,null),"Preview-only movement invalidated authored data.");
    var rotation=stage.CameraRotation;stage.Lara.position=Vector3.left*2;stage.UpdateTrackedCamera();Require(Quaternion.Angle(rotation,stage.CameraRotation)>1,"Tracking did not follow second movement.");
    Require(stage.CameraPosition==new Vector3(0,1,-4),"Tracking moved the authored camera origin.");
    stage.PreviewCameraAt(Vector3.zero,Quaternion.identity);stage.Lara.position=Vector3.forward*4;stage.UpdateTrackedCamera();Require(Quaternion.Angle(stage.CameraRotation,Quaternion.identity)<.001f,"StartAt did not disable tracking.");
    mark.transform.position=Vector3.up*2;typeof(SceneDirectorStudioWindow).GetMethod("RevisionChanged",Private).Invoke(window,null);typeof(SceneDirectorStudioWindow).GetMethod("CheckRevision",Private).Invoke(window,null);Require(!stage.Ready,"Changed staging retained the old preview owner.");
    Debug.Log("Studio corrective native checks PASS: ownership/no-op/move/conflicts/syntax, staging invalidation, preview isolation and camera tracking. UI keyboard/rendering and full authored scene remain manual checks.");
   } finally {if(window!=null)UnityEngine.Object.DestroyImmediate(window);EditorSceneManager.ClosePreviewScene(previewScene);AssetDatabase.DeleteAsset(folder);}
  }
 }
}
