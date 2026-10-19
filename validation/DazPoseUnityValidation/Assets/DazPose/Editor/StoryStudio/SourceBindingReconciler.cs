using System;
using System.IO;
using System.Linq;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor {
 public sealed class SourceBindingManifest {
  public string SourceGuid,SourcePath;
  public StorySourceAnalysis Analysis;
  public StoryBindingRequirement[] Requirements=>Analysis?.Requirements??Array.Empty<StoryBindingRequirement>();
  public static SourceBindingManifest Analyze(string sourcePath,string sourceGuid) {
   sourcePath=sourcePath.Replace('\\','/');return new SourceBindingManifest{SourceGuid=sourceGuid,SourcePath=sourcePath,Analysis=DazStoryCompiler.Analyze(File.ReadAllText(sourcePath),sourcePath,new StoryAnalysisOptions{SourceGuid=sourceGuid,AllowExternalCards=true})};
  }
 }
 /// <summary>Idempotently creates typed empty source-owned content rows from the compiler manifest.</summary>
 public static class SourceBindingReconciler {
  static readonly System.Collections.Generic.HashSet<string> queued=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
  public static string CompanionPath(string sourcePath)=>Path.ChangeExtension(sourcePath,"bindings.asset").Replace('\\','/');
  public static StoryContentBindings FindOwner(string sourceGuid,string adjacentPath=null) {
   var matches=new System.Collections.Generic.List<StoryContentBindings>();
   if(!string.IsNullOrEmpty(adjacentPath)) {var adjacent=AssetDatabase.LoadAssetAtPath<StoryContentBindings>(adjacentPath);if(adjacent!=null&&(string.IsNullOrEmpty(adjacent.ownerSourceGuid)||adjacent.ownerSourceGuid==sourceGuid))matches.Add(adjacent);}
   if(string.IsNullOrEmpty(sourceGuid))return matches.SingleOrDefault();
   matches.AddRange(AssetDatabase.FindAssets("t:StoryContentBindings").Select(g=>AssetDatabase.LoadAssetAtPath<StoryContentBindings>(AssetDatabase.GUIDToAssetPath(g))).Where(x=>x!=null&&x.ownerSourceGuid==sourceGuid));
   if(matches.Count==0) {
    foreach(string guid in AssetDatabase.FindAssets("t:CompiledStoryAsset")) {
     var imported=AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>(AssetDatabase.GUIDToAssetPath(guid));
     if(imported!=null&&imported.sourceIdentity==sourceGuid&&imported.content!=null&&!AssetDatabase.IsSubAsset(imported.content))matches.Add(imported.content);
    }
   }
   var distinct=matches.Distinct().ToArray();if(distinct.Length>1)throw new InvalidOperationException("Multiple StoryContentBindings assets claim source GUID "+sourceGuid+"; refusing ambiguous reuse.");
   return distinct.SingleOrDefault();
  }
  public static void Schedule(string sourcePath,string sourceGuid) {
   if(string.IsNullOrEmpty(sourcePath)||string.IsNullOrEmpty(sourceGuid)||!queued.Add(sourceGuid))return;
   EditorApplication.delayCall+=()=> { queued.Remove(sourceGuid); try { var currentPath=AssetDatabase.GUIDToAssetPath(sourceGuid);if(!string.IsNullOrEmpty(currentPath))ReconcileAsset(currentPath,sourceGuid,false); } catch(Exception e) { Debug.LogWarning("Could not reconcile .dazstory bindings for "+sourcePath+": "+e.Message); } };
  }
  public static StoryContentBindings ReconcileAsset(string sourcePath,string sourceGuid,bool recordUndo=true) {
   var manifest=SourceBindingManifest.Analyze(sourcePath,sourceGuid);sourcePath=manifest.SourcePath;var analysis=manifest.Analysis;
   if(!analysis.SyntaxValid)throw new InvalidOperationException(string.Join("\n",analysis.SyntaxDiagnostics));
   string adjacent=CompanionPath(sourcePath);var adjacentAsset=AssetDatabase.LoadAssetAtPath<StoryContentBindings>(adjacent);
   if(adjacentAsset!=null&&!string.IsNullOrEmpty(adjacentAsset.ownerSourceGuid)&&adjacentAsset.ownerSourceGuid!=sourceGuid)throw new InvalidOperationException("Adjacent bindings belong to a different source GUID; refusing to merge or overwrite them.");
   var content=FindOwner(sourceGuid,adjacent);bool created=false;
   if(content==null) { content=ScriptableObject.CreateInstance<StoryContentBindings>();content.name=Path.GetFileNameWithoutExtension(adjacent);AssetDatabase.CreateAsset(content,adjacent);created=true; }
   bool changed=created||(recordUndo&&content.ownerSourceGuid!=sourceGuid);
   var scene=content.sceneSymbols.ToList();var poses=content.poses.ToList();var gestures=content.gestures.ToList();var shaders=content.shaders.ToList();var motion=content.motion.ToList();var dialogue=content.dialogue.ToList();
   foreach(var requirement in analysis.Requirements) {
    if(requirement.IsDialogue) {string id=StoryDialogue.IdFor(requirement.Transcript);int i=dialogue.FindIndex(x=>x.id==id);if(i<0){dialogue.Add(new StoryDialogueContent{id=id,transcript=requirement.Transcript});changed=true;}else if(dialogue[i].transcript!=requirement.Transcript){throw new InvalidOperationException("Transcript hash collision or corrupted dialogue row: "+id);}continue;}
    string alias=requirement.Alias;switch(requirement.Kind.Value) {
     case StoryBindingKind.Pose: if(!poses.Any(x=>x.id==alias)){poses.Add(new StoryPoseContent{id=alias});changed=true;}break;
     case StoryBindingKind.Gesture: if(!gestures.Any(x=>x.id==alias)){gestures.Add(new StoryGestureContent{id=alias});changed=true;}break;
     case StoryBindingKind.MagicShaderProfile: if(!shaders.Any(x=>x.id==alias)){shaders.Add(new StoryShaderContent{id=alias});changed=true;}break;
     case StoryBindingKind.Funscript: if(!motion.Any(x=>x.id==alias)){motion.Add(new StoryMotionContent{id=alias});changed=true;}break;
     default: var existing=scene.FirstOrDefault(x=>x.id==alias);if(existing==null){scene.Add(new StorySceneSymbol{id=alias,kind=requirement.Kind.Value});changed=true;}else if(existing.kind!=requirement.Kind.Value)throw new InvalidOperationException("Existing scene symbol '"+alias+"' has type "+existing.kind+" but source requires "+requirement.Kind+".");break;
    }
   }
   if(!changed)return content;
   if(recordUndo&&!created)Undo.RecordObject(content,"Reconcile story source bindings");
   content.ownerSourceGuid=sourceGuid;content.sceneSymbols=scene.ToArray();content.poses=poses.ToArray();content.gestures=gestures.ToArray();content.shaders=shaders.ToArray();content.motion=motion.ToArray();content.dialogue=dialogue.ToArray();
   EditorUtility.SetDirty(content);AssetDatabase.SaveAssetIfDirty(content);return content;
  }
 }
}
