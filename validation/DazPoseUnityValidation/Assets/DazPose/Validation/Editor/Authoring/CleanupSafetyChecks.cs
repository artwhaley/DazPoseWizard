using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DazPose.Editor {
 public static class CleanupSafetyChecks {
  [Serializable] sealed class Input {public string path;}
  [Serializable] sealed class Manifest {public Input[] files;}
  [Serializable] sealed class Report {public int assertions;public string[] paths,hashes;public bool passed;}
  static string Hash(string path) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));}
  public static void Run() {
   if(EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s=>s.isDirty))
    throw new InvalidOperationException("Safety checks require idle Edit Mode and saved scenes; nothing was saved.");
   int assertions=0;
   string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
   var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(root,"docs/cleanup-task1/protected-manifest.json")));
   var paths=manifest.files.Select(f=>f.path).Where(File.Exists).Distinct().ToArray();var hashes=paths.Select(Hash).ToArray();
   var active=SceneManager.GetActiveScene();
   foreach(string path in new[]{"Assets/Production/Scenes/MainMenu.unity",StorySceneBuilder.ScenePath,ProductionSceneEnvironment.TemplatePath,DazPose.UnityValidation.ClothingSetupBuilder.ScenePath}) {
    var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid() || !scene.isLoaded;
    if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
    try {
     assertions++;
     if(scene.isDirty)throw new InvalidOperationException("Opening a saved scene made it dirty: "+path);
    } finally {if(opened)EditorSceneManager.CloseScene(scene,true);if(active.IsValid() && active.isLoaded)SceneManager.SetActiveScene(active);}
   }
   if(!paths.Select(Hash).SequenceEqual(hashes))throw new InvalidOperationException("Opening scenes changed protected source bytes.");assertions++;
   var repairPaths=new[]{StorySceneBuilder.ScenePath,StoryFoundationBuilder.SourcePath,Path.ChangeExtension(StoryFoundationBuilder.SourcePath,"bindings.asset"),GlobalStoryCardIndexBuilder.PublishedRoot+"/FirstContactEngineering.bindings.asset"};
   var before=repairPaths.Select(Hash).ToArray();
   FirstContactRepair.Prepare();FirstContactRepair.Prepare();
   if(!repairPaths.Select(Hash).SequenceEqual(before))throw new InvalidOperationException("Repeated repair changed valid authored scene or bindings.");assertions++;
   if(Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s=>s.isDirty))throw new InvalidOperationException("Safety checks left a scene dirty.");assertions++;
   Directory.CreateDirectory("TestOutput/CleanupTask1");
   File.WriteAllText("TestOutput/CleanupTask1/scene-open-and-repair.json",JsonUtility.ToJson(new Report {passed=true,assertions=assertions,paths=paths,hashes=hashes},true));
   Debug.Log("Cleanup scene-open/repair checks passed without Play: "+assertions+" assertions, "+paths.Length+" protected files.");
  }
 }
}
