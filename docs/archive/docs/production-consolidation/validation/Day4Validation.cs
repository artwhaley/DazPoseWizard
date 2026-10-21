using System;using System.IO;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using DazPose.UnityValidation;
[InitializeOnLoad]public static class Day4Validation {
 static Day4Validation(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("Day4Validation.Active",false))EditorApplication.delayCall+=Finish;};}
 public static void Run(){try{if(!Application.dataPath.Replace('\\','/').Contains("/.dazposewizard/production-consolidation/"))throw new Exception("Disposable project required");var scene=EditorSceneManager.OpenScene(Day4SceneBuilder.ScenePath);var runner=UnityEngine.Object.FindObjectsByType<Day4RegressionRunner>(FindObjectsSortMode.None);if(runner.Length!=1)throw new Exception("Delivered saved runner missing or ambiguous");File.Delete("TestOutput/day4/runtime.json");SessionState.SetBool("Day4Validation.Active",true);EditorApplication.EnterPlaymode();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 static void Finish(){SessionState.SetBool("Day4Validation.Active",false);try{var json=File.ReadAllText("TestOutput/day4/runtime.json");Debug.Log(json);if(json.Contains("\"result\": \"passed\""))Day4Build.Run();else EditorApplication.Exit(1);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}




