using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
public sealed class Day2Probe : MonoBehaviour {
 public SuccubusPerformer a,b; public AudioClip speech; public PerformerExpression expression; public Transform mark;
 [Serializable] public class Result { public string exception,pose,expression,speech,movement,visibility,outfit; public string[] checks; }
 Result report=new Result(); List<string> checks=new List<string>(); bool finished; double started;
 void Awake(){Application.runInBackground=true;started=Time.realtimeSinceStartupAsDouble;}
 void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
 void Update(){if(!finished && Time.realtimeSinceStartupAsDouble-started>120) {report.exception="Runtime timeout";Finish();}}
 async void Start(){try{
  for(int i=0;i<5;i++) await Awaitable.NextFrameAsync();
  Check(a.IsRuntimeReady && b.IsRuntimeReady,"both actors ready without harness initialization");
  Check(a.MotionSource!=b.MotionSource && !a.MotionSource.IsRunning && !b.MotionSource.IsRunning,"distinct idle clocks");
  var wa=a.GetComponent<PerformerWardrobe>();var wb=b.GetComponent<PerformerWardrobe>();
  Check(wa.Body!=wb.Body && wa.Body.transform.IsChildOf(a.transform) && wb.Body.transform.IsChildOf(b.transform),"distinct actor body bindings");
  Check(a.DissolveRig.ParticleBody!=b.DissolveRig.ParticleBody,"distinct particle bodies");
  Check(a.GetComponent<PerformerHandGripController>().IsAvailable && b.GetComponent<PerformerHandGripController>().IsAvailable,"calibrated grip graphs attached");
  Check(a.GetComponent<PerformerHandGripController>().State==GripInteractionState.Idle,"contact remains idle until authored");
  report.pose=(await a.PoseAsync(a.InitialPose,PoseTransition.Snap)).ToString(); Check(report.pose=="Settled","pose settled");
  report.expression=(await a.ExpressionAsync(expression,.6f,.1f)).ToString(); Check(report.expression=="Settled","expression settled");
  report.speech=(await a.SayAsync(speech)).ToString();Check(report.speech=="Finished","speech finished");
  var bPosition=b.transform.position;
  report.movement=(await a.WalkToAsync(mark)).ToString(); Check(report.movement=="Arrived","movement arrived");
  Check(Vector3.Distance(bPosition,b.transform.position)<.001f,"other actor position unchanged");
  report.visibility=(await a.DissolveOutAsync(.15f)).ToString();Check(a.IsHidden && !b.IsHidden,"visibility independent");
  await a.DissolveInAsync(.15f);Check(!a.IsHidden,"appearance restored");
  report.outfit=(await a.OutfitAsync("first-outfit")).Status.ToString();Check(a.CurrentWardrobe.VisibleMask!=0,"released outfit visible mask populated");
  Check(b.CurrentWardrobe.IsNaked && string.IsNullOrEmpty(b.CurrentWardrobe.OutfitId),"other actor wardrobe unchanged");
  a.MotionSource.StartMotion();for(int i=0;i<5;i++)await Awaitable.NextFrameAsync();
  Check(a.MotionSource.IsRunning && !b.MotionSource.IsRunning && a.MotionSource.CurrentSample.Sequence!=b.MotionSource.CurrentSample.Sequence,"clock samples independent");a.MotionSource.StopMotion();
  a.BreathsPerMinute=17f;b.BreathsPerMinute=19f;
#if UNITY_EDITOR
  var apply=AppDomain.CurrentDomain.GetAssemblies().Select(x=>x.GetType("DazPose.Editor.Performer.PerformerDefaultsEditor")).First(x=>x!=null).GetMethod("QueueApply");
  object[] args={a,null};Check((bool)apply.Invoke(null,args),"explicit supported defaults queued: "+args[1]);
  var clone=Instantiate(a.gameObject); object[] transient={clone.GetComponent<SuccubusPerformer>(),null};
  Check(!(bool)apply.Invoke(null,transient),"runtime clone rejected for saved defaults");Destroy(clone);
#endif
 }catch(Exception e){report.exception=e.ToString();}finally{Finish();}}
 async void Finish(){if(finished)return;finished=true;report.checks=checks.ToArray();Directory.CreateDirectory("TestOutput/day2");File.WriteAllText(Application.isEditor ? "TestOutput/day2/runtime.json" : Path.Combine(Path.GetDirectoryName(Application.dataPath),"day2-player.json"),JsonUtility.ToJson(report,true));
#if UNITY_EDITOR
 UnityEditor.EditorApplication.ExitPlaymode();
#else
 if(!Application.isBatchMode) await Awaitable.WaitForSecondsAsync(20f);
 Application.Quit(string.IsNullOrEmpty(report.exception)?0:1);
#endif
 }
}

