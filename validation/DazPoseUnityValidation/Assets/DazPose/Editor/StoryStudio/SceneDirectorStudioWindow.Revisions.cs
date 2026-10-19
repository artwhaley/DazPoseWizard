using System;
using System.IO;
using System.Linq;
using System.Text;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor.Studio {
 public sealed partial class SceneDirectorStudioWindow {
  string sourceStamp;
  bool revisionDirty=true;
  void PlayStateChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingEditMode)Release();}
  void ClosedScene(UnityEngine.SceneManagement.Scene scene){if(host==null||host.gameObject.scene==scene){Release();sourceStamp=null;revisionDirty=true;}}
  void RevisionChanged()=>revisionDirty=true;
  UndoPropertyModification[] Modified(UndoPropertyModification[] changes){revisionDirty=true;return changes;}
  string CaptureStamp() {
   var text=new StringBuilder();
   void Asset(UnityEngine.Object asset) {
    if(asset==null){text.Append("null;");return;}
    string path=AssetDatabase.GetAssetPath(asset);text.Append(AssetDatabase.AssetPathToGUID(path)).Append('|').Append(AssetDatabase.GetAssetDependencyHash(path)).Append('|').Append(EditorJsonUtility.ToJson(asset)).Append(';');
    // Unsaved pose/audio assignments must count before an asset save/import event.
    foreach(string dependency in AssetDatabase.GetDependencies(path,true)) {
     var value=AssetDatabase.LoadMainAssetAtPath(dependency);if(value!=null)text.Append(dependency).Append(':').Append(EditorUtility.GetDirtyCount(value)).Append(';');
    }
   }
   Asset(source);Asset(content);
   if(source!=null){string path=AssetDatabase.GetAssetPath(source);if(File.Exists(path))text.Append(StoryDialogue.IdFor(File.ReadAllText(path)));}
   var catalog=Resources.Load<GlobalStoryCardCatalog>("DazPose/GlobalStoryCards");Asset(catalog);
   if(catalog!=null){text.Append(catalog.fingerprint);foreach(var published in catalog.sources??Array.Empty<CompiledStoryAsset>()){Asset(published);if(published!=null)Asset(published.content);}}
   if(host!=null) {
    text.Append(host.GetEntityId().ToString()).Append('|').Append(AssetDatabase.AssetPathToGUID(host.gameObject.scene.path)).Append(string.IsNullOrEmpty(host.gameObject.scene.path)?"":AssetDatabase.GetAssetDependencyHash(host.gameObject.scene.path).ToString()).Append('|').Append(EditorJsonUtility.ToJson(host));
    // Saved scene objects only: preview hierarchy changes cannot invalidate their own owner.
    foreach(var root in host.gameObject.scene.GetRootGameObjects())foreach(var transform in root.GetComponentsInChildren<Transform>(true)) {
     text.Append(transform.GetEntityId().ToString()).Append(':').Append(transform.localPosition.ToString("R")).Append(transform.localRotation.ToString("R")).Append(transform.localScale.ToString("R")).Append(transform.gameObject.activeSelf);
     foreach(var component in transform.GetComponents<Component>())if(component!=null)text.Append(component.GetEntityId().ToString()).Append(':').Append(EditorUtility.GetDirtyCount(component));
    }
   }
   return StoryDialogue.IdFor(text.ToString());
  }
  void CheckRevision() {
   if(!revisionDirty)return;revisionDirty=false;
   string next=CaptureStamp();if(sourceStamp!=null&&sourceStamp!=next) {
    Release();selectedPc=-1;selectedRequirement=-1;selectedKey=null;selectedSource=null;detailManifest=null;stagedResult=null;editLocation=false;
    Refresh();error="Story or scene changed — rebuild rehearsal with Start / Reset.";
   }
   sourceStamp=next;
  }
  void SelectHost(StorySceneBindings value) {
   if(value==host)return;Release();host=value;selectedPc=-1;editLocation=false;sourceStamp=null;revisionDirty=true;
   error="Scene host changed — rebuild rehearsal with Start / Reset.";
  }
 }
}
