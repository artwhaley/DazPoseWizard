using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
public static class Day4Build {
 [Serializable]public class Legacy{public int assertions;public string[] failures;}[Serializable]public class Dependencies{public string[] paths;}
 [Serializable] public class Report {public string exception,result,output,backend;public int errors,warnings;public ulong bytes;}
 public static void Run(){var report=new Report();try{
 if(!Application.dataPath.Replace('\\','/').Contains("/.dazposewizard/production-consolidation/"))throw new Exception("Disposable project required");
  var failures=DazPose.Performer.HandGrip.HandGripRuntimeSelfTests.Run();
 File.WriteAllText("TestOutput/day4/legacy-grip.json",JsonUtility.ToJson(new Legacy{assertions=DazPose.Performer.HandGrip.HandGripRuntimeSelfTests.AssertionCount,failures=failures},true));
 if(failures.Length!=0)throw new Exception("Legacy grip self tests failed: "+string.Join(";",failures));
 var dependencies=AssetDatabase.GetDependencies(new[]{Day4SceneBuilder.ScenePath},true);
 File.WriteAllText("TestOutput/day4/dependencies.json",JsonUtility.ToJson(new Dependencies{paths=dependencies},true));
 report.output=Path.GetFullPath("TestOutput/day4/player/Day4Validation.exe");Directory.CreateDirectory(Path.GetDirectoryName(report.output));
 var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Production/Scenes/Day4PartnerContactValidation.unity"},locationPathName=report.output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
 report.result=build.summary.result.ToString();report.errors=(int)build.summary.totalErrors;report.warnings=(int)build.summary.totalWarnings;report.bytes=build.summary.totalSize;report.backend=PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone).ToString();
 }catch(Exception e){report.exception=e.ToString();}finally{Directory.CreateDirectory("TestOutput/day4");File.WriteAllText("TestOutput/day4/build.json",JsonUtility.ToJson(report,true));EditorApplication.Exit(string.IsNullOrEmpty(report.exception) && report.result=="Succeeded"?0:1);}}
}



