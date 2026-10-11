using System;
using System.IO;
using System.Linq;
using DazPose.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace DazPose.Editor {
 public static class GamePlayerBuild {
  public static void BuildInteractive() {
   string path=EditorUtility.SaveFilePanel("Build Windows Player",Path.GetFullPath("Builds/Authoring"),"DazPoseGame","exe");
   if(!string.IsNullOrEmpty(path)) Build(path);
  }
  public static void BuildBatch()=>Build(Path.GetFullPath("Builds/Authoring/DazPoseGame.exe"));
  public static void BuildStartupChecks() {
   BuildBatch();
   Build(Path.GetFullPath("Builds/AuthoringStartup/DazPoseGame.exe"),"Assets/Production/Scenes/AuthoringShowcase.unity");
  }
  public static void Build(string path,string startupScene=null) {
   var catalog=AssetDatabase.LoadAssetAtPath<SceneCatalog>(ProductionApplicationSetup.CatalogPath);
   var scenes=new[]{catalog.mainMenuPath}.Concat(catalog.entries.Where(e=>e.enabled).Select(e=>e.scenePath)).Distinct().ToArray();
   if(startupScene!=null) scenes=new[]{startupScene}.Concat(scenes.Where(s=>s!=startupScene)).ToArray();
   var target=NamedBuildTarget.Standalone;
   string previous=PlayerSettings.GetScriptingDefineSymbols(target);
   var normal=previous.Split(';').Where(d=>d!="DAZPOSE_VALIDATION" && d!="DAZPOSE_DEVELOPMENT_TOOLS");
   try {
    PlayerSettings.SetScriptingDefineSymbols(target,string.Join(";",normal));
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=scenes, locationPathName=path, target=BuildTarget.StandaloneWindows64 });
    File.WriteAllText(path+".build.json",JsonUtility.ToJson(new BuildEvidence { executable=path,utc=DateTime.UtcNow.ToString("O"),scenes=scenes,result=report.summary.result.ToString(),errors=report.summary.totalErrors },true));
    if(report.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("Production build failed: "+report.summary.result);
   } finally { PlayerSettings.SetScriptingDefineSymbols(target,previous); }
  }
  [Serializable] sealed class BuildEvidence { public string executable,utc,result; public string[] scenes; public int errors; }
 }
}