using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DazPose.Editor {
 // A saved production environment, extracted once from the authored art source.
 // Runtime scenes never need the old validation stage or its marker hierarchy.
 public static class ProductionSceneEnvironment {
  public const string TemplatePath="Assets/Production/Environments/VoidEnvironment.unity";
  public static void EnsureTemplate() {
   if(File.Exists(TemplatePath)) return;
   Directory.CreateDirectory(Path.GetDirectoryName(TemplatePath));
   if(!AssetDatabase.CopyAsset(GameSessionSceneSetup.VoidPath,TemplatePath)) throw new IOException("Cannot extract the authored environment.");
   var previous=SceneManager.GetActiveScene();
   var scene=EditorSceneManager.OpenScene(TemplatePath,OpenSceneMode.Additive);
   try {
    ExtractEnvironmentRoots(scene);
    var keep=new[]{"Environment","SceneVolumes","Player"};
    foreach(var root in scene.GetRootGameObjects()) if(!keep.Contains(root.name)) UnityEngine.Object.DestroyImmediate(root);
    EditorSceneManager.SaveScene(scene);
   } finally { EditorSceneManager.CloseScene(scene,true); if(previous.IsValid()) SceneManager.SetActiveScene(previous); }
  }
  public static Scene Copy(string path) {
   if(File.Exists(path)) throw new InvalidOperationException("Choose a new scene path; existing scene is preserved.");
   EnsureTemplate(); Directory.CreateDirectory(Path.GetDirectoryName(path));
   if(!AssetDatabase.CopyAsset(TemplatePath,path)) throw new IOException("Cannot copy the production environment.");
   return EditorSceneManager.OpenScene(path);
  }
  static void ExtractEnvironmentRoots(Scene scene) {
   var stage=scene.GetRootGameObjects().SingleOrDefault(r=>r.name=="FirstPerformanceVoid");
   if(stage==null) return;
   foreach(string name in new[]{"Environment","SceneVolumes","Player"}) {
    var child=stage.transform.Find(name);
    if(child==null) throw new InvalidOperationException("Authored environment lacks "+name+".");
    child.SetParent(null,true);
   }
  }
  public static void RemoveLegacyStage(Scene scene) {
   foreach(var root in scene.GetRootGameObjects()) {
    if(root.name=="ToyControl" && root.transform.childCount==0 && root.GetComponents<Component>().Length==1) UnityEngine.Object.DestroyImmediate(root);
    else if(root.name=="MotionDriver" && root.GetComponent<DazPose.Motion.MotionDriver>()!=null) root.name="Performance Clock";
   }
   var stage=scene.GetRootGameObjects().SingleOrDefault(r=>r.name=="FirstPerformanceVoid");
   if(stage==null) return;
   // Save the explicitly bound production actor before deleting the old fixture container.
   var host=GameSessionSceneSetup.InScene<DazPose.Performer.PerformanceHost>(scene).Single();
   var actor=host.Bindings.Performer;
   if(actor!=null && actor.transform.IsChildOf(stage.transform)) {
    var actors=new GameObject("Actors"); SceneManager.MoveGameObjectToScene(actors,scene);
    actor.transform.SetParent(actors.transform,true);
   }
   ExtractEnvironmentRoots(scene);
   UnityEngine.Object.DestroyImmediate(stage);
   if(host.Bindings.Playback!=null) host.Bindings.Playback.gameObject.name="Performance Clock";
  }
 }
}