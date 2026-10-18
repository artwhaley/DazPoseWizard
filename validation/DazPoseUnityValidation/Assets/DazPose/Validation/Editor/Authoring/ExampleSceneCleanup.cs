using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DazPose.Editor {
 public static class ExampleSceneCleanup {
  [Serializable] sealed class Report {public string scene;public string[] inventory,removed,renamed;}
  public static void Run() {
   if(EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s=>s.isDirty))
    throw new InvalidOperationException("Example cleanup requires saved scenes in Edit Mode; no automatic save.");
   const string old="Assets/Production/Scenes/AuthoringShowcase.unity";
   const string destination="Assets/Examples/Scenes/AuthoringShowcase.unity";
   string path=File.Exists(old) ? old : destination;
   var active=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid() || !scene.isLoaded;
   if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   var removed=new List<string>();var renamed=new List<string>();
   try {
    var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
    string[] inventory=all.Select(t=>FullName(t)+": "+string.Join(",",t.GetComponents<Component>().Select(c=>c==null ? "MISSING" : c.GetType().FullName))).ToArray();
    var host=GameSessionSceneSetup.InScene<DazPose.Performer.PerformanceHost>(scene).Single();
    if(host.Sequence==null || host.Bindings==null)throw new InvalidOperationException("Saved example runner/bindings must remain.");
    string sceneId=host.Bindings.SceneId,actorId=host.Bindings.ActorId;
    // Only pure transform marker branches with no non-transform serialized consumer may retire.
    var references=new HashSet<UnityEngine.Object>();
    foreach(var component in all.SelectMany(t=>t.GetComponents<Component>()).Where(c=>c!=null && !(c is Transform))) {
     var serialized=new SerializedObject(component);var it=serialized.GetIterator();
     while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference && it.objectReferenceValue!=null)references.Add(it.objectReferenceValue);
    }
    var markers=all.SingleOrDefault(t=>t.name=="PerformanceMarkers");
    if(markers!=null) {
     var branches=markers.GetComponentsInChildren<Transform>(true).Reverse().ToArray();
     foreach(var mark in branches) {
      if(mark==null)continue;
      var descendants=mark.GetComponentsInChildren<Transform>(true);
      if(descendants.Any(t=>references.Contains(t) || references.Contains(t.gameObject) || t.GetComponents<Component>().Any(c=>c==null || !(c is Transform))))continue;
      removed.Add(FullName(mark));UnityEngine.Object.DestroyImmediate(mark.gameObject);
     }
    }
    foreach(var t in all.Where(t=>t!=null)) {
     string replacement=t.name=="FirstPerformanceVoid" ? "Void Environment" : t.name=="PerformanceMarkers" ? "Scene Marks" : t.name=="TeleportAcceptance" ? "Teleport Staging" : t.name=="FirstContact" ? "Contact Staging" : null;
     if(replacement!=null){renamed.Add(FullName(t)+" -> "+replacement);t.name=replacement;}
    }
    if(host.Sequence==null || host.Bindings.SceneId!=sceneId || host.Bindings.ActorId!=actorId)throw new InvalidOperationException("Example identity or runner changed.");
    if(removed.Count>0 || renamed.Count>0) {EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save reviewed example.");}
    Directory.CreateDirectory("TestOutput/CleanupTask1");
    File.WriteAllText("TestOutput/CleanupTask1/example-cleanup.json",JsonUtility.ToJson(new Report {scene=path,inventory=inventory,removed=removed.ToArray(),renamed=renamed.ToArray()},true));
   } finally {if(opened)EditorSceneManager.CloseScene(scene,true);if(active.IsValid() && active.isLoaded)SceneManager.SetActiveScene(active);}
  }
  static string FullName(Transform t)=>t.parent==null ? t.name : FullName(t.parent)+"/"+t.name;
 }
}
