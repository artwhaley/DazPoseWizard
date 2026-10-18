using System;

using System.Collections.Generic;

using System.IO;

using System.Linq;

using DazPose.Performer;

using DazPose.Story;

using UnityEditor;

using UnityEditor.Build;

using UnityEditor.Build.Reporting;

using UnityEditor.SceneManagement;

using UnityEngine;

using UnityEngine.UI;

namespace DazPose.Editor {

 // Editor-only observer. It presses the saved production UI; it adds no runtime runner or scene objects.

  public static class StoryCheckpointPlaybackChecks {

  const string Key="DazPose.StoryCheckpoint.Check";

  static double deadline;static readonly HashSet<Button> confirmed=new HashSet<Button>();

  static readonly List<string> trace=new List<string>();static readonly List<string> errors=new List<string>();



  public static void PlaySaved() {

   if(!Application.dataPath.Replace('\\','/').Contains("/.dazposewizard/"))throw new InvalidOperationException("Automated UI playback must use the isolated validation project.");

   PlayerSettings.productName="DazPoseStoryCheckpointValidation";

   EditorSceneManager.OpenScene(StorySceneBuilder.ScenePath);

   var sequence=GameSessionSceneSetup.InScene<StoryProgramSequence>(UnityEngine.SceneManagement.SceneManager.GetActiveScene()).Single();

   if(sequence.StopBeforePhase!="Warmup")throw new InvalidOperationException("The delivered checkpoint must be saved on the existing runner.");

   UnityEditor.SessionState.SetBool(Key,true);BeginObserve();EditorApplication.isPlaying=true;

  }

  static void BeginObserve() {deadline=EditorApplication.timeSinceStartup+420;EditorApplication.update-=Observe;EditorApplication.update+=Observe;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}

  static void Log(string message,string stack,LogType type) {if(message.Contains("[Story ") || message.StartsWith("DazPose performance"))trace.Add(message);if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors.Add(message+"\n"+stack);}

  static void Observe() {

   if(EditorApplication.timeSinceStartup>deadline) {Finish(false,"Saved-scene playback timed out.");return;}

   if(!EditorApplication.isPlaying)return;

   if(!Application.persistentDataPath.Contains("DazPoseStoryCheckpointValidation")) {Finish(false,"Validation profile was not isolated: "+Application.persistentDataPath);return;}

   var hosts=GameSessionSceneSetup.InScene<PerformanceHost>(UnityEngine.SceneManagement.SceneManager.GetActiveScene());if(hosts.Length!=1)return;var host=hosts[0];

   if(host.State==PerformanceHostState.Failed) {Finish(false,host.Failure?.ToString());return;}

   var sequence=(StoryProgramSequence)host.Sequence;

   if(sequence.Bindings.prompts.IsOpen) {

    var buttons=sequence.Bindings.prompts.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy && b.interactable).ToArray();

    var confirm=buttons.FirstOrDefault(b=>new[]{"Apply interests","Apply equipment","Ready","Continue","Removed"}.Contains(b.GetComponentInChildren<Text>()?.text));

    if(confirm!=null && confirmed.Add(confirm)) {

     string title=confirm.GetComponentInChildren<Text>().text;

     var toggles=sequence.Bindings.prompts.GetComponentsInChildren<Toggle>().Where(t=>t.gameObject.activeInHierarchy).ToArray();

     if(title=="Apply equipment") {foreach(var toggle in toggles)toggle.isOn=true;}

     if(title=="Ready" && toggles.Length==6) {for(int i=0;i<toggles.Length;i++)toggles[i].isOn=i<4;}

     confirm.onClick.Invoke();

    }

   }

   if(host.State==PerformanceHostState.Completed) {

    bool passed=sequence.Snapshot?.Checkpoint?.Contains("stopped before Warmup")==true && trace.Any(t=>t.Contains("Disapproval")) && trace.Any(t=>t.Contains("Ask you to prepare")) && errors.Count==0;

    Finish(passed,"Saved host completed at "+sequence.Snapshot?.Checkpoint+"; errors="+errors.Count+"; trace events="+trace.Count+"; profile="+Application.persistentDataPath);

   }

  }

  static void Finish(bool passed,string details) {

   UnityEditor.SessionState.SetBool(Key,false);EditorApplication.update-=Observe;Application.logMessageReceived-=Log;

   Directory.CreateDirectory("TestOutput/StoryFoundation");File.WriteAllText("TestOutput/StoryFoundation/saved-playback.txt",DateTime.UtcNow.ToString("O")+"\n"+passed+"\n"+details+"\n"+string.Join("\n",trace)+"\nERRORS:\n"+string.Join("\n",errors));

   Debug.Log("FIRST_CONTACT_SAVED_CHECKPOINT_PLAY: "+passed+"; "+details);EditorApplication.Exit(passed?0:1);

  }

  public static void CheckAndBuildSaved() {StoryFoundationChecks.Run();BuildSaved();}
  public static void BuildSaved() {

   if(!Application.dataPath.Replace('\\','/').Contains("/.dazposewizard/"))throw new InvalidOperationException("Build checkpoint validation in the isolated project.");

   string path=Path.GetFullPath("Builds/FirstContactCheckpoint/DazPoseFirstContact.exe");Directory.CreateDirectory(Path.GetDirectoryName(path));

   var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{StorySceneBuilder.ScenePath},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});

   File.WriteAllText("TestOutput/StoryFoundation/player-build.txt",report.summary.result+"; errors="+report.summary.totalErrors+"; scene="+StorySceneBuilder.ScenePath);

   if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Checkpoint player build failed.");

  }

 }

}

