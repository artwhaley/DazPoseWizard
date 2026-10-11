using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DazPose.Editor {
 // Bounded checks drive editor Play only. They never inject a scene runner.
 [InitializeOnLoad]
 public static class AuthoringBatch {
  static AuthoringBatch() { if(SessionState.GetBool("DazPose.B05.Play",false)) { deadline=EditorApplication.timeSinceStartup+180; EditorApplication.update+=ObserveWelcome; } }
  static double deadline;
  static bool entered;
  public static void CreateAndPlayWelcome() {
   const string path="Assets/Production/Scenes/AuthoringWelcome.unity";
   PerformanceHost host;
   if(!File.Exists(path)) host=GameSessionSceneSetup.CopyEnvironment(path);
   else { EditorSceneManager.OpenScene(path); host=GameSessionSceneSetup.Initialize(); }
   int count=GameSessionSceneSetup.InScene<Transform>(host.gameObject.scene).Length;
   string actor=host.Bindings.ActorId,scene=host.Bindings.SceneId;
   float rate=host.Bindings.Performer.BreathsPerMinute;
   GameSessionSceneSetup.Initialize();
   if(count!=GameSessionSceneSetup.InScene<Transform>(host.gameObject.scene).Length || actor!=host.Bindings.ActorId || scene!=host.Bindings.SceneId || rate!=host.Bindings.Performer.BreathsPerMinute)
    throw new InvalidOperationException("Initializer changed saved objects, IDs or tuning on repeat.");
   DazPoseProductionMenu.AddCurrentScene();
   SessionState.SetBool("DazPose.B05.Play",true);
   deadline=EditorApplication.timeSinceStartup+60;
   EditorApplication.update+=ObserveWelcome;
   EditorApplication.isPlaying=true;
  }
  public static void PlayShowcase() => PlaySaved("AuthoringShowcase");
  public static void PlayCompanion() => PlaySaved("AuthoringCompanion");
  static void PlaySaved(string name) {
   EditorSceneManager.OpenScene("Assets/Production/Scenes/"+name+".unity");
   SessionState.SetString("DazPose.Authoring.PlayScene",name);
   SessionState.SetBool("DazPose.B05.Play",true);
   deadline=EditorApplication.timeSinceStartup+180;
   EditorApplication.update+=ObserveWelcome;
   EditorApplication.isPlaying=true;
  }
  static void ObserveWelcome() {
   if(EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) return;
   if(EditorApplication.isPlaying) {
    var hosts=GameSessionSceneSetup.InScene<PerformanceHost>(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    if(hosts.Length==1 && hosts[0].State!=PerformanceHostState.Idle) {
     entered=true;
     if(hosts[0].State==PerformanceHostState.Completed) { Finish(true,"Exact saved scene completed through its saved host."); return; }
     if(hosts[0].State==PerformanceHostState.Failed) { Finish(false,hosts[0].Failure?.ToString()); return; }
    }
   }
   if(EditorApplication.timeSinceStartup>deadline) Finish(false,"Saved scene timed out; entered="+entered);
  }
  static void Finish(bool passed,string details) {
   SessionState.SetBool("DazPose.B05.Play",false);
   EditorApplication.update-=ObserveWelcome;
   Directory.CreateDirectory("TestOutput/Authoring");
   string name=SessionState.GetString("DazPose.Authoring.PlayScene","B05a");
   File.WriteAllText("TestOutput/Authoring/"+name+".txt",DateTime.UtcNow.ToString("O")+"\n"+passed+"\n"+details);
   Debug.Log("AUTHORING_SAVED_PLAY: "+passed+"; "+details);
   EditorApplication.Exit(passed?0:1);
  }
 }
}