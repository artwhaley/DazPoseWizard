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
  public static string[] ComputeScenes(SceneCatalog catalog,string startupScene=null) {
   if(catalog==null)throw new InvalidOperationException("Assign the application SceneCatalog.");
   var scenes=new[]{catalog.mainMenuPath}.Concat(catalog.entries.Where(e=>e.enabled).Select(e=>e.scenePath)).Distinct().ToArray();
   if(scenes.Any(string.IsNullOrWhiteSpace))throw new InvalidOperationException("The public scene catalog contains an empty path.");
   if(startupScene!=null) {
    if(!scenes.Contains(startupScene))throw new InvalidOperationException("A startup override must already be in the public scene catalog.");
    scenes=new[]{startupScene}.Concat(scenes.Where(s=>s!=startupScene)).ToArray();
   }
   return scenes;
  }
  public static void SynchronizePublicSceneList() {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Update public scenes in Edit Mode.");
   var catalog=AssetDatabase.LoadAssetAtPath<SceneCatalog>(ProductionApplicationSetup.CatalogPath);
   var first=catalog.entries.Single(e=>e.scenePath==StorySceneBuilder.ScenePath);
   if(catalog.entries.Length!=1 || catalog.entries[0]!=first) {
    catalog.entries=new[]{first};EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
   }
   var scenes=ComputeScenes(catalog);
   if(!EditorBuildSettings.scenes.Select(s=>s.path+"|"+s.enabled).SequenceEqual(scenes.Select(s=>s+"|True")))
    EditorBuildSettings.scenes=scenes.Select(s=>new EditorBuildSettingsScene(s,true)).ToArray();
  }
  public static void Build(string path,string startupScene=null,BuildOptions options=BuildOptions.None) {
   var catalog=AssetDatabase.LoadAssetAtPath<SceneCatalog>(ProductionApplicationSetup.CatalogPath);
   var scenes=ComputeScenes(catalog,startupScene);
   var target=NamedBuildTarget.Standalone;
   string previous=PlayerSettings.GetScriptingDefineSymbols(target);
   var normal=previous.Split(';').Where(d=>d!="DAZPOSE_VALIDATION" && d!="DAZPOSE_DEVELOPMENT_TOOLS");
   try {
    PlayerSettings.SetScriptingDefineSymbols(target,string.Join(";",normal));
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=scenes, locationPathName=path, target=BuildTarget.StandaloneWindows64,options=options });
    File.WriteAllText(path+".build.json",JsonUtility.ToJson(new BuildEvidence { executable=path,utc=DateTime.UtcNow.ToString("O"),scenes=scenes,result=report.summary.result.ToString(),errors=report.summary.totalErrors },true));
    if(report.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("Production build failed: "+report.summary.result);
   } finally { PlayerSettings.SetScriptingDefineSymbols(target,previous); }
  }
  [Serializable] sealed class BuildEvidence { public string executable,utc,result; public string[] scenes; public int errors; }
 }
}