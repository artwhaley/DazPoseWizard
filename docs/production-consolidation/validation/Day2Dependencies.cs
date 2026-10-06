using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using DazPose.Editor.Performer;
public static class Day2Dependencies {
 [Serializable] public class Row {public string path,sha256;}
 [Serializable] public class Report {public Row[] rows; public string exception;}
 public static void Run(){var r=new Report();try{
 if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').Contains("/.dazposewizard/production-consolidation/")) throw new Exception("Disposable Day 2 batch project required");

 r.rows=AssetDatabase.GetDependencies(new[]{ProductionPerformerBuilder.PrefabPath,ProductionPerformerBuilder.ScenePath},true).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>{
 string hash=null;
 return new Row{path=p,sha256=hash};}).ToArray();
 }catch(Exception e){r.exception=e.ToString();}finally{Directory.CreateDirectory("TestOutput/day2");File.WriteAllText("TestOutput/day2/dependencies.json",JsonUtility.ToJson(r,true));EditorApplication.Exit(string.IsNullOrEmpty(r.exception)?0:1);}}
}



