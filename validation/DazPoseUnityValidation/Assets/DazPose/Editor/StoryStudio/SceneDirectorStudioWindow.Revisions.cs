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
  UndoPropertyModification[] Modified(UndoPropertyModification[] changes){
   // Gizmo drags are handled by the spatial watcher. Do not hash the entire asset graph per mouse move.
   if(changes.Any(change=>!(change.currentValue.target is Transform)))revisionDirty=true;
   return changes;
  }
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
    text.Append(host.GetEntityId().ToString()).Append('|').Append(host.gameObject.scene.path).Append('|').Append(EditorJsonUtility.ToJson(host));
    Asset(host.lara!=null?host.lara.InitialPose:null);
    foreach(var location in host.locations??Array.Empty<StoryLocationBinding>())if(location?.seat!=null)Asset(location.seat.SeatingProfile);
    // Spatial anchors and head-local focus are watched separately from logical revisions.

   }
   return StoryDialogue.IdFor(text.ToString());
  }
  void CheckRevision() {
   if(!revisionDirty||refreshCueQueued)return;revisionDirty=false;
   string next=CaptureStamp();if(sourceStamp!=null&&sourceStamp!=next) {
    QueueCueRefresh();return;
   }
   sourceStamp=next;
  }
  void SelectHost(StorySceneBindings value) {
   if(value==host)return;Release();host=value;selectedPc=-1;sourceStamp=null;revisionDirty=true;
   error="Scene host selected. Connect native directing to inspect its real scene references.";
  }
 }
}
