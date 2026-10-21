using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Motion;
using DazPose.Performer;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Editor {
  public static class StoryFullSessionChecks {
  const string Active="DazPose.FullStory.Play";
  static readonly List<string> trace=new List<string>(),errors=new List<string>();
  static readonly HashSet<Button> confirmed=new HashSet<Button>();
  static readonly HashSet<string> phases=new HashSet<string>();
  static readonly Dictionary<string,float> maxCompletion=new Dictionary<string,float>();
  static double deadline,loopStarted=-1;
  static string loggedPhase="Setup";
  static bool pressed,sawHiddenSeatedArrival,sawSeatedPose,entered;
  static int assertions;
  [Serializable] sealed class SourceReport {public int assertions,cards,dialogue;public string[] missing;public string preview,scene;public int seed;}

  static void Require(bool valid,string why) {assertions++;if(!valid)throw new InvalidOperationException(why);}
  static void RequireIsolated() {if(!Application.dataPath.Replace('\\','/').Contains("/.dazposewizard/"))throw new InvalidOperationException("Automated checks use the isolated project.");}
  public static void SourceChecks() {
   RequireIsolated();assertions=0;StoryFullSessionSetup.Prepare();StoryFoundationChecks.Run();
   var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var sequence=GameSessionSceneSetup.InScene<StoryProgramSequence>(scene).Single();var linked=StoryLinkedContent.Create(sequence.CompiledSource);var program=linked.Program;
   Require(string.IsNullOrEmpty(sequence.StopBeforePhase),"The saved full scene still has a preview cutoff.");
   Require(sequence.SessionSeed.HasValue && sequence.PacingMultiplier>0,"The saved test seed and pacing are missing.");
   Require(program.cards.Length>=18,"The authored engineering card library is incomplete.");
   foreach(var pool in new[]{new[]{"Warmup"},new[]{"Main","Standing"},new[]{"Main","Sitting"}})Require(program.cards.Count(c=>pool.All(c.Tags.Contains))>=3,"Missing card coverage: "+string.Join(",",pool));
   Require(program.cards.Count(c=>c.Tags.Contains("Resolution"))>=2,"Two normal Resolution cards are required.");
   foreach(var equipment in StoryCatalogs.Load().Equipment.entries)Require(program.cards.Any(c=>c.Tags.Contains("equipment:"+equipment.id)),"Missing equipment coverage: "+equipment.id);
   Require(program.dialogue.Length==19,"The fixed dialogue oracle changed.");
   Require(sequence.Bindings.locations.All(l=>l.placement!=null),"Every semantic transform must be assigned.");
   Require(sequence.Bindings.RequireLocation("MainPosition1").placement.name=="MainPosition1" && sequence.Bindings.RequireLocation("Bedside").placement.name=="Bedside","The movable staging marks are missing.");
   string[] missing=linked.Diagnostics(program,sequence.Bindings);Require(missing.Length==0,string.Join("\n",missing));
   Require(sequence.CompiledSource.content.poses.All(p=>p.value!=null && p.value.Clip!=null),"Imported pose wrappers are missing.");
   Require(sequence.GetComponent<PerformanceHost>().Bindings.Playback!=null,"The saved playback driver is missing.");
   var positions=sequence.Bindings.locations.ToDictionary(l=>l.id,l=>l.placement.position);
   int transforms=GameSessionSceneSetup.InScene<Transform>(scene).Length;
   StoryFullSessionSetup.Prepare();sequence=GameSessionSceneSetup.InScene<StoryProgramSequence>(UnityEngine.SceneManagement.SceneManager.GetActiveScene()).Single();
   Require(GameSessionSceneSetup.InScene<Transform>(sequence.gameObject.scene).Length==transforms,"Refresh duplicated scene objects.");
   Require(sequence.Bindings.locations.All(l=>Vector3.Distance(l.placement.position,positions[l.id])<.00001f),"Refresh moved an authored mark.");
   CheckMotion(sequence.CompiledSource.content.Motion("Finisher1"),sequence.CompiledSource.content.Motion("FinishLoop"));
   Directory.CreateDirectory("TestOutput/StoryFullSession");
   File.WriteAllText("TestOutput/StoryFullSession/source.json",JsonUtility.ToJson(new SourceReport {assertions=assertions,cards=program.cards.Length,dialogue=program.dialogue.Length,missing=missing,preview=sequence.StopBeforePhase,seed=sequence.SessionSeed ?? 0,scene=StorySceneBuilder.ScenePath},true));
   Debug.Log("D6_D8_SOURCE_PASS: assertions="+assertions+"; cards="+program.cards.Length+"; dialogue="+program.dialogue.Length+"; all bindings assigned.");
  }
  static void CheckMotion(FunscriptMotionProgram finite,FunscriptMotionProgram loop) {
   var go=new GameObject("Transient motion lifecycle check");
   try {
    var driver=go.AddComponent<MotionDriver>();int ended=0;long generation=-1;driver.Ended+=g=>{ended++;generation=g;};
    driver.FunscriptProgram=finite;driver.SourceMode=MotionSourceMode.Funscript;driver.Loop=false;driver.RestartMotion();long started=driver.CommandGeneration;driver.Advance(100);
    Require(ended==1 && !driver.IsRunning && generation==started,"Non-looping natural end must emit once for its invocation.");
    driver.Advance(100);Require(ended==1,"Natural end emitted twice.");
    driver.RestartMotion();driver.Advance(.1f);driver.StopMotion();Require(ended==1,"Explicit Stop emitted a natural end.");
    driver.FunscriptProgram=loop;driver.Loop=true;driver.RestartMotion();driver.Advance(100);
    Require(driver.IsRunning && ended==1,"Loop playback emitted Ended or stopped at a loop boundary.");driver.StopMotion();
   }finally {UnityEngine.Object.DestroyImmediate(go);}
  }
  public static void PlayA()=>Play("A");
  public static void PlayB()=>Play("B");
  public static void PlayC()=>Play("C");
  static void Play(string scenario) {
   RequireIsolated();PlayerSettings.productName="DazPoseStoryFullSessionValidation";
   EditorSceneManager.OpenScene(StorySceneBuilder.ScenePath);
   var sequence=GameSessionSceneSetup.InScene<StoryProgramSequence>(UnityEngine.SceneManagement.SceneManager.GetActiveScene()).Single();
   if(!string.IsNullOrEmpty(sequence.StopBeforePhase))throw new InvalidOperationException("The exact saved full session must have no preview cutoff.");
   UnityEditor.SessionState.SetString("DazPose.FullStory.Case",scenario);UnityEditor.SessionState.SetBool(Active,true);ObserveStart();EditorApplication.isPlaying=true;
  }
  static void ObserveStart() {deadline=EditorApplication.timeSinceStartup+600;EditorApplication.update-=Observe;EditorApplication.update+=Observe;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
  static void Log(string message,string stack,LogType type) {
   if(message.Contains("[Story ") || message.StartsWith("DazPose performance"))trace.Add(message);
   foreach(string phase in new[]{"Warmup","Main","Climax","Resolution"})if(message.Contains("Enter phase "+phase+" at zero completion.")) {loggedPhase=phase;maxCompletion[phase]=0;}
   const string marker=" pacing = ";int progressAt=message.IndexOf(marker,StringComparison.Ordinal);
   if(progressAt>=0 && message.Contains(" base progress * ")) {
    string number=message.Substring(progressAt+marker.Length).Trim().TrimEnd('.');
    if(float.TryParse(number,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float credit))maxCompletion[loggedPhase]=(maxCompletion.TryGetValue(loggedPhase,out float total) ? total : 0)+credit;
   }
   if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors.Add(message+"\n"+stack);
  }
  static void Observe() {
   if(EditorApplication.timeSinceStartup>deadline) {Finish(false,"Saved full scene timed out.");return;}
   if(!EditorApplication.isPlaying)return;
   if(!Application.persistentDataPath.Contains("DazPoseStoryFullSessionValidation")) {Finish(false,"Validation preferences are not isolated.");return;}
   var hosts=GameSessionSceneSetup.InScene<PerformanceHost>(UnityEngine.SceneManagement.SceneManager.GetActiveScene());if(hosts.Length!=1)return;var host=hosts[0];
   if(host.State==PerformanceHostState.Failed) {Finish(false,host.Failure?.ToString());return;}
   if(host.State==PerformanceHostState.Running)entered=true;
   var sequence=host.Sequence as StoryProgramSequence;if(sequence==null || sequence.Snapshot==null)return;
   var snap=sequence.Snapshot;string scenario=UnityEditor.SessionState.GetString("DazPose.FullStory.Case","A");
   phases.Add(snap.Phase);maxCompletion[snap.Phase]=Mathf.Max(maxCompletion.TryGetValue(snap.Phase,out float old) ? old : 0,snap.PhaseCompletion);
   var actor=host.Bindings.Performer;
   if(actor.HasPersistentSeatedPose) {
    sawSeatedPose=true;
    if(actor.SeatingOwnershipWeight<.99f || actor.CurrentSeat==null) {Finish(false,"Authored seated pose lost seating ownership.");return;}
    if(snap.Phase=="Main" && actor.VisibilityState==PerformerVisibilityState.DissolvingIn)sawHiddenSeatedArrival=true;
   }
   var prompts=sequence.Bindings.prompts;
   if(prompts.IsOpen) {
    var confirm=prompts.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.gameObject.activeInHierarchy && b.interactable && new[]{"Apply interests","Apply equipment","Ready","Continue","Removed"}.Contains(b.GetComponentInChildren<Text>()?.text));
    if(confirm!=null && confirmed.Add(confirm)) {
     var toggles=prompts.GetComponentsInChildren<Toggle>().Where(t=>t.gameObject.activeInHierarchy).ToArray();string label=confirm.GetComponentInChildren<Text>().text;
     if(label=="Apply equipment")foreach(var toggle in toggles)toggle.isOn=scenario!="B";
     if(label=="Ready" && toggles.Length==6) {int count=scenario=="A" ? 0 : scenario=="B" ? 2 : 4;for(int i=0;i<toggles.Length;i++)toggles[i].isOn=i<count;}
     confirm.onClick.Invoke();
    }
   }
   var playback=host.Bindings.Playback;
   if(!pressed && snap.Phase=="Climax" && prompts.HasAction("ICame") && playback!=null && playback.IsRunning) {
    bool looping=playback.FunscriptProgram.name.StartsWith("FinishLoop",StringComparison.Ordinal);
    if(looping && loopStarted<0)loopStarted=EditorApplication.timeSinceStartup;
    bool ready=scenario=="B" ? looping && EditorApplication.timeSinceStartup-loopStarted>5 : !looping && playback.CurrentSample.TimeSeconds>=1;
    if(ready) {
     var action=prompts.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<Text>()?.text=="I came");
     pressed=true;action.onClick.Invoke();action.onClick.Invoke(); // Repeated clicks must still emit once.
    }
   }
   if(host.State==PerformanceHostState.Completed) {
    bool allPhases=new[]{"Warmup","Main","Climax","Resolution"}.All(phases.Contains);
    bool reached=maxCompletion.TryGetValue("Warmup",out var warmup) && warmup>=100 && maxCompletion.TryGetValue("Main",out var main) && main>=100;
    bool equipment=scenario=="B" ? string.IsNullOrEmpty(snap.PreparedEquipment) && string.IsNullOrEmpty(snap.ActiveEquipment) : !string.IsNullOrEmpty(snap.PreparedEquipment) && !string.IsNullOrEmpty(snap.ActiveEquipment);
    bool branches=scenario=="A" ? !trace.Any(t=>t.Contains("Marked shoes as removed.")) : trace.Any(t=>t.Contains("all clothing") || t.Contains("all recorded clothing"));
    if(scenario=="C")branches &= trace.Any(t=>t.Contains("Disapproval")) && trace.Any(t=>t.Contains("Marked shoes as removed."));
    bool loopPath=scenario=="B" ? trace.Any(t=>t.Contains("Start motion program FinishLoop")) && trace.Count(t=>t.Contains("Motion program reached its natural end."))==1 : !trace.Any(t=>t.Contains("Start motion program FinishLoop"));
    bool illegal=trace.Any(t=>t.Contains("WarmupStandingOnly: rejected; Required Lara.Posture == Standing"));
    bool resolution=trace.Any(t=>t.Contains("Director selected Resolution"));
    bool once=trace.Count(t=>t.Contains("You pressed ICame."))==1 && !prompts.HasAction("ICame");
    bool passed=entered && allPhases && reached && equipment && branches && loopPath && illegal && resolution && once && pressed && sawSeatedPose && sawHiddenSeatedArrival && !playback.IsRunning && errors.Count==0;
    Finish(passed,"allPhases="+allPhases+"; thresholds="+reached+"; equipment="+equipment+"; clothing="+branches+"; eventPath="+loopPath+"; illegalRejected="+illegal+"; resolutionDraw="+resolution+"; actionOnce="+once+"; seated="+sawSeatedPose+"; hiddenSeatedArrival="+sawHiddenSeatedArrival+"; errors="+errors.Count+"; events="+trace.Count);
   }
  }
  static void Finish(bool passed,string details) {
   UnityEditor.SessionState.SetBool(Active,false);EditorApplication.update-=Observe;Application.logMessageReceived-=Log;
   string scenario=UnityEditor.SessionState.GetString("DazPose.FullStory.Case","A");Directory.CreateDirectory("TestOutput/StoryFullSession");
   File.WriteAllText("TestOutput/StoryFullSession/scenario-"+scenario+".txt",DateTime.UtcNow.ToString("O")+"\n"+passed+"\n"+details+"\n"+string.Join("\n",trace)+"\nERRORS:\n"+string.Join("\n",errors));
   Debug.Log("D6_D8_SAVED_SCENE_"+scenario+": "+passed+"; "+details);EditorApplication.Exit(passed?0:1);
  }
  public static void BuildWithNavigation() {
   RequireIsolated();SourceChecks();PlayerSettings.productName="DazPoseFirstContact";
   string path=Path.GetFullPath("Builds/FirstContactFull/DazPoseFirstContact.exe");
   GamePlayerBuild.Build(path,StorySceneBuilder.ScenePath,BuildOptions.Development);
   Directory.CreateDirectory("TestOutput/StoryFullSession");
   File.WriteAllText("TestOutput/StoryFullSession/player-navigation-build.txt",File.ReadAllText(path+".build.json"));
  }
  public static void BuildSaved() {
   RequireIsolated();Directory.CreateDirectory("TestOutput/StoryFullSession");
   string path=Path.GetFullPath("Builds/FirstContactFull/DazPoseFirstContact.exe");Directory.CreateDirectory(Path.GetDirectoryName(path));
   var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{StorySceneBuilder.ScenePath},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
   File.WriteAllText("TestOutput/StoryFullSession/player-build.txt",report.summary.result+"; errors="+report.summary.totalErrors+"; scene="+StorySceneBuilder.ScenePath);
   if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Full session player build failed.");
  }
 }
}
