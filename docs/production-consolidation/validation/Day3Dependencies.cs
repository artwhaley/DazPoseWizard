using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using DazPose.Editor.Performer;
public static class Day3Dependencies {
 [Serializable] public class Row {public string path,sha256;}
 [Serializable] public class Report {public Row[] rows; public string exception;}
 public static void Run(){var r=new Report();try{
 r.rows=AssetDatabase.GetDependencies(new[]{Day3SceneBuilder.ScenePath},true).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>{
 string hash=null;
 return new Row{path=p,sha256=hash};}).ToArray();
 }catch(Exception e){r.exception=e.ToString();}finally{Directory.CreateDirectory("TestOutput/day3");File.WriteAllText("TestOutput/day3/dependencies.json",JsonUtility.ToJson(r,true));EditorApplication.Exit(string.IsNullOrEmpty(r.exception)?0:1);}}
}


