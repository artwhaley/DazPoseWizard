using System;
using System.IO;
using DazPose.Game;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.Editor {
 
 public static class AutomaticTuningCheck {
  static int stage;
  static double deadline;
  static float original;
  static PerformanceHost host;
  static string file,backup;

  public static void Play() {
   file=Path.Combine(Application.persistentDataPath,"DazPose","scene-tuning.json");
   backup=File.Exists(file) ? File.ReadAllText(file) : null;
   SessionState.SetString("DazPose.AutoTuning.Backup",backup ?? "");
   SessionState.SetBool("DazPose.AutoTuning.HadFile",backup!=null);
   EditorSceneManager.OpenScene("Assets/Scenes/FirstContact.unity");
   SessionState.SetBool("DazPose.AutoTuning.Check",true);
   deadline=EditorApplication.timeSinceStartup+60;
   EditorApplication.update+=Observe;
   EditorApplication.isPlaying=true;
  }
  static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
  static void Observe() {
   if(!EditorApplication.isPlaying) return;
   try {
    if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Automatic tuning saved-scene check timed out.");
    if(stage==0) {
     host=UnityEngine.Object.FindAnyObjectByType<PerformanceHost>();
     if(host==null || host.State!=PerformanceHostState.Running) return;
     original=host.Bindings.Performer.BreathsPerMinute;
     host.Bindings.Performer.BreathsPerMinute=23;
     Time.timeScale=0;
     stage=1; return;
    }
    file=Path.Combine(Application.persistentDataPath,"DazPose","scene-tuning.json");
    if(stage==1) {
     if(!File.Exists(file) || host.Bindings.Performer.BreathsPerMinute!=23) return;
     var check=new GameSettingsService(null);
     host.Bindings.Performer.BreathsPerMinute=7;
     check.ApplySceneTuning(host.Bindings);
     Require(host.Bindings.Performer.BreathsPerMinute==23,"Paused running edit did not survive a fresh settings service.");
     host.StopPerformance();
     host.Bindings.Performer.BreathsPerMinute=24;
     stage=2; return;
    }
    if(stage==2) {
     var fresh=new GameSettingsService(null);
     host.Bindings.Performer.BreathsPerMinute=7;
     fresh.ApplySceneTuning(host.Bindings);
     Require(host.Bindings.Performer.BreathsPerMinute==24,"Stopped edit did not automatically persist.");
     var directory=Path.Combine(Application.temporaryCachePath,"auto-tuning-"+Guid.NewGuid().ToString("N"));
     var isolated=new GameSettingsService(null,directory);
     isolated.CaptureSceneTuning(host.Bindings);
     var path=Path.Combine(directory,"scene-tuning.json");
     var timestamp=new DateTime(2000,1,1,0,0,0,DateTimeKind.Utc);
     File.SetLastWriteTimeUtc(path,timestamp);
     isolated.CaptureSceneTuning(host.Bindings);
     Require(File.GetLastWriteTimeUtc(path)==timestamp,"Unchanged capture rewrote the file.");
     var actor=host.Bindings.Performer;
     var curve=actor.BreathingCurve;
     actor.BreathingCurve=AnimationCurve.Linear(0,0,1,1);
     isolated.CaptureSceneTuning(host.Bindings);
     actor.BreathingCurve.AddKey(.5f,.2f);
     isolated.CaptureSceneTuning(host.Bindings);
     actor.BreathingCurve=null;
     new GameSettingsService(null,directory).ApplySceneTuning(host.Bindings);
     Require(actor.BreathingCurve!=null && actor.BreathingCurve.length==3,"In-place curve edit did not persist.");
     actor.BreathingCurve=curve;
     host.Bindings.Performer.BreathsPerMinute=original;
     Finish(true,"Saved First Contact started; paused/running and stopped edits survived a fresh service; unchanged snapshot caused no disk write; in-place curve edit persisted.");
    }
   } catch(Exception error) { Finish(false,error.ToString()); }
  }
  static void Finish(bool passed,string message) {
   Time.timeScale=1;
   SessionState.SetBool("DazPose.AutoTuning.Check",false);
   EditorApplication.update-=Observe;
   Directory.CreateDirectory("TestOutput/Authoring");
   File.WriteAllText("TestOutput/Authoring/AutomaticTuning.txt",(passed ? "PASS " : "FAIL ")+message);
   Debug.Log("AUTOMATIC_TUNING_"+(passed ? "PASS: " : "FAIL: ")+message);
   EditorApplication.isPlaying=false;
   EditorApplication.delayCall+=()=> {
    var path=Path.Combine(Application.persistentDataPath,"DazPose","scene-tuning.json");
    if(SessionState.GetBool("DazPose.AutoTuning.HadFile",false)) File.WriteAllText(path,SessionState.GetString("DazPose.AutoTuning.Backup",""));
    else if(File.Exists(path)) File.Delete(path);
    EditorApplication.Exit(passed ? 0 : 1);
   };
  }
 }
}
