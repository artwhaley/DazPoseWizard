using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DazPose.Performer;
using DazPose.Editor.Performer;
public static class Day2Preflight {
 [Serializable] public class Result {public string[] checks;public string exception;}
 public static void Run(){var r=new Result();var checks=new System.Collections.Generic.List<string>();try{
 if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').Contains("/.dazposewizard/production-consolidation/")) throw new Exception("Disposable Day 2 batch project required");

 EditorSceneManager.OpenScene(ProductionPerformerBuilder.ScenePath);
 var bs=UnityEngine.Object.FindObjectsByType<PerformanceSceneBindings>(FindObjectsSortMode.None).OrderBy(x=>x.name).ToArray();var a=bs[0].Performer;var b=bs[1].Performer;
 void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);}
 Check(bs.All(x=>x.Validate().Length==0),"fresh scene default capabilities valid");
 Check(bs[0].Validate(PerformerCapability.Teleport).Length==0,"accepted teleport profile valid");
 Check(bs[0].Validate(PerformerCapability.Seating).Any(x=>x.Owner==bs[0] && x.PropertyPath=="seats"),"required undeclared seat identifies sequence seats");
 var seat=new GameObject("Broken Seat").AddComponent<PerformerSeat>();bs[0].DeclareSeats(new[]{seat});
 Check(bs[0].Validate(PerformerCapability.Seating).Any(x=>x.Owner==seat && x.PropertyPath=="approachAnchor"),"missing seat approach anchor identifies exact property");bs[0].DeclareSeats(null);UnityEngine.Object.DestroyImmediate(seat.gameObject);
 var empty=ScriptableObject.CreateInstance<PerformerExpression>();bs[0].DeclareAssets(null,new[]{empty},null);
 Check(bs[0].Validate(PerformerCapability.Expression).Any(x=>x.PropertyPath=="expressions.Array.data[0]"),"empty expression rejected");bs[0].DeclareAssets(null,null,null);UnityEngine.Object.DestroyImmediate(empty);
 var rig=new SerializedObject(a.DissolveRig);var own=rig.FindProperty("particleBody").objectReferenceValue;rig.FindProperty("particleBody").objectReferenceValue=b.DissolveRig.ParticleBody;rig.ApplyModifiedPropertiesWithoutUndo();
 Check(bs[0].Validate(PerformerCapability.Visibility).Any(x=>x.Owner==a.DissolveRig && x.PropertyPath=="particleBody"),"cross actor particle renderer rejected");rig.FindProperty("particleBody").objectReferenceValue=own;rig.ApplyModifiedPropertiesWithoutUndo();
 bs[0].RequiresPlayer=true;bs[0].RequiresToys=true;
 Check(bs[0].Validate().Any(x=>x.PropertyPath=="player") && bs[0].Validate().Any(x=>x.PropertyPath=="toys"),"declared required services identify missing references");
 }catch(Exception e){r.exception=e.ToString();}finally{r.checks=checks.ToArray();Directory.CreateDirectory("TestOutput/day2");File.WriteAllText("TestOutput/day2/preflight.json",JsonUtility.ToJson(r,true));if(string.IsNullOrEmpty(r.exception)) Day2Build.Run(); else EditorApplication.Exit(1);}}
}



