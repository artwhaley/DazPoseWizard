using System;
using System.IO;
using System.Linq;
using DazPose.Story;
using DazPose.Performer;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor.Studio {
 public sealed partial class SceneDirectorStudioWindow {
  bool revisionDirty=true;string hostStamp,geometryStamp,materialStamp;
  void PlayStateChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingEditMode){Release();CancelIndex();}}
  void ClosedScene(UnityEngine.SceneManagement.Scene scene){if(host==null||host.gameObject.scene==scene){Release();CancelIndex();}}
  void RevisionChanged()=>revisionDirty=true;
  UndoPropertyModification[] Modified(UndoPropertyModification[] changes){if(changes.Any(c=>!(c.currentValue.target is Transform)))revisionDirty=true;return changes;}
  // Lightweight identity comparison retained for explicit diagnostic callers; no asset graph traversal.
  string CaptureStamp()=>host==null?"":host.GetEntityId()+"/"+host.lara?.GetEntityId()+"/"+host.playerBody?.GetEntityId()+"/"+string.Join(";",(host.locations??Array.Empty<StoryLocationBinding>()).Where(l=>l!=null).Select(l=>l.id+":"+l.placement?.GetEntityId()+":"+l.seat?.GetEntityId()));
  string ObjectRevision(UnityEngine.Object o)=>o==null?"null":o.GetEntityId()+":"+EditorUtility.GetDirtyCount(o);
  string GeometryStamp()=>string.Join(";",activeDependencies.Where(o=>o is AnimationClip||o is PerformerPose||o is PerformerSeat||o!=null&&o.GetType().Name.Contains("SeatingProfile")).Select(ObjectRevision));
  string MaterialStamp()=>string.Join(";",activeDependencies.Where(o=>o is PerformerMagicShaderProfile||o is Material).Select(ObjectRevision));
  void RememberDependencies(){dependenciesStamp=DependencyStamp();geometryStamp=GeometryStamp();materialStamp=MaterialStamp();hostStamp=CaptureStamp();}
  bool sourceImportsPending;
  void ImportedAssets(string[] paths){
   if(authorCatalog==null)return;
   if(paths.Any(p=>authorCatalog.Units.Any(u=>u.Path==p&&(!File.Exists(p)||File.GetLastWriteTimeUtc(p)!=u.Write)))){sourceImportsPending=true;revisionDirty=true;return;}
   if(paths.Contains("Assets/Resources/DazPose/GlobalStoryCards.asset"))revisionDirty=true;
   var changed=activeDependencies.Where(o=>o!=null&&paths.Contains(AssetDatabase.GetAssetPath(o))).ToArray();
   if(changed.Length==0)return;
   if(changed.Any(o=>o is AnimationClip||o is PerformerPose||o.GetType().Name.Contains("SeatingProfile")))poseRevision++;
   if(changed.Any(o=>o is PerformerMagicShaderProfile||o is Material))presentationRevision++;
   RefreshOwnerReferences();if(stage!=null)RequestProjection();revisionDirty=true;
  }
  void CheckRevision(){
   if(indexTask!=null||source==null||authorCatalog==null)return;
   bool changedEvent=revisionDirty;
   if(!changedEvent&&EditorApplication.timeSinceStartup<nextRevisionPoll)return;
   nextRevisionPoll=EditorApplication.timeSinceStartup+1;revisionDirty=false;
   if(sourceImportsPending){sourceImportsPending=false;BeginAuthoringIndex();return;}
   if(authorCatalog.Units.Any(u=>!File.Exists(u.Path)||File.GetLastWriteTimeUtc(u.Path)!=u.Write)){BeginAuthoringIndex();return;}
   if(changedEvent){
    var catalog=Resources.Load<GlobalStoryCardCatalog>("DazPose/GlobalStoryCards");
    var published=(catalog?.sources??Array.Empty<CompiledStoryAsset>()).Where(a=>a!=source).ToArray();
    if(!published.OrderBy(a=>a!=null?a.GetEntityId().ToString():"").SequenceEqual(authorCatalog.Units.Where(u=>u.Asset!=source).Select(u=>u.Asset).OrderBy(a=>a.GetEntityId().ToString()))){BeginAuthoringIndex();return;}
   }
   if(stage==null)return;
   string nextHost=CaptureStamp();bool changedHost=hostStamp!=null&&nextHost!=hostStamp;hostStamp=nextHost;
   if(!changedEvent&&!changedHost)return;
   string nextGeometry=GeometryStamp();
   string nextMaterial=MaterialStamp();
   if(geometryStamp!=null&&geometryStamp!=nextGeometry)poseRevision++;if(materialStamp!=null&&materialStamp!=nextMaterial)presentationRevision++;
   geometryStamp=nextGeometry;materialStamp=nextMaterial;
   string next=DependencyStamp();if(changedHost||dependenciesStamp!=next){RefreshOwnerReferences();RequestProjection();}
  }
  void SelectHost(StorySceneBindings value){if(value==host)return;Release();host=value;hostStamp=null;revisionDirty=true;error=null;}
 }
}
