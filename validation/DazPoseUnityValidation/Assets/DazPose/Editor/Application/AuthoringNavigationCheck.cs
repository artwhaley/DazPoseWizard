using System;
using System.IO;
using System.Linq;
using DazPose.Game;
using DazPose.Performer;
using DazPose.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace DazPose.Editor {
 [InitializeOnLoad]
 public static class AuthoringNavigationCheck {
  static double deadline,next;
  static int stage;
  static string appId,oldHost;
  static float rate,frequency,otherRate;
  static string sceneId;
  static AuthoringNavigationCheck() {
   if(SessionState.GetBool("DazPose.Authoring.Navigation",false)) {
    deadline=EditorApplication.timeSinceStartup+180;
    EditorApplication.update+=Observe;
   }
  }
  public static void PrepareDelivery() {
   GameUiAssetBuilder.RepairUiSizing();
   AuthoringContentComposer.BuildSavedContent();
   var scene=EditorSceneManager.OpenScene("Assets/Production/Scenes/AuthoringWelcome.unity");
   var host=GameSessionSceneSetup.Initialize();
   foreach(var body in GameSessionSceneSetup.InScene<PerformerParticleBody>(scene))
    if(!body.transform.IsChildOf(host.Bindings.Performer.transform)) UnityEngine.Object.DestroyImmediate(body);
   EditorSceneManager.SaveScene(scene);
  }
  public static void PrepareAndPlay() { PrepareDelivery(); Play(); }
  public static void Play() {
   EditorSceneManager.OpenScene("Assets/Production/Scenes/MainMenu.unity");
   SessionState.SetBool("DazPose.Authoring.Navigation",true);
   deadline=EditorApplication.timeSinceStartup+180;
   EditorApplication.update+=Observe;
   EditorApplication.isPlaying=true;
  }
  static T Field<T>(UnityEngine.Object owner,string name) where T:UnityEngine.Object =>
   new SerializedObject(owner).FindProperty(name).objectReferenceValue as T;
  static void Click(UnityEngine.Object owner,string name) => AuthoringPointerCheck.Click(Field<Button>(owner,name));
  static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
  static T One<T>() where T:Component => GameSessionSceneSetup.InScene<T>(SceneManager.GetActiveScene()).Single();
  static void Choose(MainMenuController menu,string title) {
   var button=Field<Transform>(menu,"sceneList").GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<Text>().text.StartsWith(title,StringComparison.Ordinal));
   AuthoringPointerCheck.Click(button);
  }
  static void Observe() {
   if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next) return;
   if(EditorApplication.timeSinceStartup>deadline) { Finish(false,"Navigation deadline at stage "+stage); return; }
   try {
    var scene=SceneManager.GetActiveScene();
    if(stage==0) {
     if(!scene.path.EndsWith("MainMenu.unity")) return;
     var menu=One<MainMenuController>();
     if(Field<Transform>(menu,"sceneList").GetComponentsInChildren<Button>().Length!=3) return;
     var app=GameApplication.Current; appId=app.GetEntityId().ToString();
     Require(GameApplication.EnsureStarted()==app,"EnsureStarted replaced the app.");
     Click(menu,"settingsButton");
     var settings=Field<SettingsMenuController>(menu,"settings");
     Require(settings.gameObject.activeSelf,"Saved Settings button did not open Settings.");
     stage=10; next=EditorApplication.timeSinceStartup+.2;
    } else if(stage==10) {
     var menu=One<MainMenuController>(); var settings=Field<SettingsMenuController>(menu,"settings");
     Click(settings,"close"); Require(!settings.gameObject.activeSelf,"Saved Close did not close Settings.");
     stage=11; next=EditorApplication.timeSinceStartup+.2;
    } else if(stage==11) {
     Choose(One<MainMenuController>(),"Invitation"); stage=1; next=EditorApplication.timeSinceStartup+1;
    } else if(stage==1) {
     if(!scene.path.EndsWith("AuthoringShowcase.unity")) return;
     var host=One<PerformanceHost>();
     Require(host.State==PerformanceHostState.Running,"Showcase did not start: "+host.Failure);
     sceneId=host.Bindings.SceneId; oldHost=host.GetEntityId().ToString();
     rate=host.Bindings.Performer.BreathsPerMinute+3;
     frequency=host.Bindings.Playback.FrequencyHz+.1f;
     host.Bindings.Performer.BreathsPerMinute=rate;
     host.Bindings.Playback.FrequencyHz=frequency;
     stage=2; next=EditorApplication.timeSinceStartup+1;
    } else if(stage==2) {
     var host=One<PerformanceHost>();
     Require(host.State!=PerformanceHostState.Failed,"Showcase failed: "+host.Failure);
     if(!host.Bindings.Performer.IsGripping || !host.Bindings.Playback.IsRunning) return;
     Time.timeScale=0; Click(One<PerformanceControlsController>(),"stop");
     Require(host.State==PerformanceHostState.Stopped,"Paused Stop did not latch Stopped.");
     Require(!host.Bindings.Playback.IsRunning && !host.Bindings.Performer.IsGripping,"Paused Stop left motion/contact running.");
     Require(host.Bindings.Performer.BreathsPerMinute==rate,"Stop changed tuning.");
     Time.timeScale=1; Click(One<PerformanceControlsController>(),"start");
     stage=3; next=EditorApplication.timeSinceStartup+2;
    } else if(stage==3) {
     var host=One<PerformanceHost>();
     Require(host.State==PerformanceHostState.Running && host.GetEntityId().ToString()==oldHost,"Saved Start did not restart the stopped host.");
     Require(host.Bindings.Performer.BreathsPerMinute==rate,"Start changed tuning.");
     Click(One<PerformanceControlsController>(),"restart");
     stage=4; next=EditorApplication.timeSinceStartup+1;
    } else if(stage==4) {
     var hosts=GameSessionSceneSetup.InScene<PerformanceHost>(scene);
     if(hosts.Length!=1 || hosts[0].GetEntityId().ToString()==oldHost) return;
     var host=hosts[0];
     Require(host.State==PerformanceHostState.Running,"Reloaded saved host did not start: "+host.Failure);
     Require(GameApplication.Current.GetEntityId().ToString()==appId,"Reload replaced app service.");
     Require(host.Bindings.Performer.BreathsPerMinute==rate && host.Bindings.Playback.FrequencyHz==frequency,"Reload lost live actor/clock tuning.");
     Click(One<PerformanceControlsController>(),"menu");
     stage=5; next=EditorApplication.timeSinceStartup+1;
    } else if(stage==5) {
     if(!scene.path.EndsWith("MainMenu.unity")) return;
     var menu=One<MainMenuController>();
     if(Field<Transform>(menu,"sceneList").GetComponentsInChildren<Button>().Length!=3) return;
     Require(GameApplication.Current.GetEntityId().ToString()==appId,"Return to menu replaced app service.");
     Choose(menu,"Encore"); stage=6; next=EditorApplication.timeSinceStartup+1;
    } else if(stage==6) {
     if(!scene.path.EndsWith("AuthoringCompanion.unity")) return;
     var host=One<PerformanceHost>(); otherRate=host.Bindings.Performer.BreathsPerMinute;
     Require(host.State==PerformanceHostState.Running,"Companion did not start: "+host.Failure);
     Require(host.Bindings.SceneId!=sceneId && otherRate!=rate,"Scene A tuning leaked into scene B.");
     Require(GameApplication.Current.GetEntityId().ToString()==appId,"Scene switch replaced app service.");
     Click(One<PerformanceControlsController>(),"stop");
     Click(One<PerformanceControlsController>(),"menu");
     stage=7; next=EditorApplication.timeSinceStartup+1;
    } else if(stage==7 && scene.path.EndsWith("MainMenu.unity")) {
     Finish(true,"Raycast-resolved pointer events passed MainMenu > Settings > Showcase > paused Stop/Start > Restart > menu > Companion > menu. One application survived; live actor and clock tuning survived reload; scene B kept its scope. No UI runner was injected. Editor pointer-path acceptance only; built-player clicks and appearance remain separate.");
    }
   } catch(Exception error) { Finish(false,"Stage "+stage+": "+error); }
  }
  static void Finish(bool pass,string details) {
   Time.timeScale=1; SessionState.SetBool("DazPose.Authoring.Navigation",false);
   EditorApplication.update-=Observe;
   Directory.CreateDirectory("TestOutput/Authoring");
   File.WriteAllText("TestOutput/Authoring/Navigation.txt",DateTime.UtcNow.ToString("O")+"\n"+pass+"\n"+details);
   Debug.Log("AUTHORING_NAVIGATION: "+pass+"; "+details); EditorApplication.Exit(pass?0:1);
  }
 }
}
