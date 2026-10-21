using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
public static class Day3Build {
 [Serializable] public class Report {public string exception,result,output,backend;public int errors,warnings;public ulong bytes;}
 public static void Run(){var report=new Report();try{
 if(!Application.dataPath.Replace('\\','/').Contains("/.dazposewizard/production-consolidation/"))throw new Exception("Disposable project required");
 report.output=Path.GetFullPath("TestOutput/day3/player/Day3Validation.exe");Directory.CreateDirectory(Path.GetDirectoryName(report.output));
 var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Production/Scenes/Day3CommandValidation.unity"},locationPathName=report.output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
 report.result=build.summary.result.ToString();report.errors=(int)build.summary.totalErrors;report.warnings=(int)build.summary.totalWarnings;report.bytes=build.summary.totalSize;report.backend=PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone).ToString();
 }catch(Exception e){report.exception=e.ToString();}finally{Directory.CreateDirectory("TestOutput/day3");File.WriteAllText("TestOutput/day3/build.json",JsonUtility.ToJson(report,true));EditorApplication.Exit(string.IsNullOrEmpty(report.exception) && report.result=="Succeeded"?0:1);}}
}

