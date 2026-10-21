using System;using System.IO;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using DazPose.UnityValidation.Partner;using DazPose.UnityValidation.Editor.Partner;
public static class Day4Measure{
 [Serializable]public class Report{public string exception;public PartnerContactProfileBuilder.Measurement[] candidates;}
 public static void Run(){var r=new Report();try{
 if(!Application.dataPath.Replace('\\','/').Contains("/.dazposewizard/production-consolidation/"))throw new Exception("Disposable required");
 EditorSceneManager.OpenScene("Assets/Scenes/PartnerRigAcceptance.unity");var c=UnityEngine.Object.FindFirstObjectByType<PartnerAnatomyTestController>();if(!c.ConfigureRuntimeBody(out string reason))throw new Exception(reason);var b=c.RuntimeBody;
 var line=b.ErectCenterlineRoot;var straight=new[]{line[0],line[line.Length-1]}; r.candidates=new[]{PartnerContactProfileBuilder.Measure(b.transform,b.StructuralBody,b.ShellRenderer,straight,17,.2f,.75f),PartnerContactProfileBuilder.Measure(b.transform,b.StructuralBody,b.ShellRenderer,b.ErectCenterlineRoot,17,.15f,.85f),PartnerContactProfileBuilder.Measure(b.transform,b.StructuralBody,b.ShellRenderer,b.ErectCenterlineRoot,17,.2f,.75f),PartnerContactProfileBuilder.Measure(b.transform,b.StructuralBody,b.ShellRenderer,b.ErectCenterlineRoot,9,.3f,.7f)};
 }catch(Exception e){r.exception=e.ToString();}finally{Directory.CreateDirectory("TestOutput/day4");File.WriteAllText("TestOutput/day4/geometry-straight-diagnostic.json",JsonUtility.ToJson(r,true));EditorApplication.Exit(string.IsNullOrEmpty(r.exception)?0:1);}}
}

