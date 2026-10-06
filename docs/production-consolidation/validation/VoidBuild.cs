using System;using System.IO;using System.Linq;using UnityEditor;using UnityEngine;using DazPose.Editor.Performer;
public static class VoidBuild {
 [Serializable]class Dependencies{public string[] paths;}
 public static void Run(){try{var paths=AssetDatabase.GetDependencies(new[]{ProductionPerformerBuilder.PrefabPath,ProductionPerformerBuilder.ScenePath},true).OrderBy(x=>x).ToArray();File.WriteAllText("TestOutput/day2/environment-dependencies.json",JsonUtility.ToJson(new Dependencies{paths=paths},true));Day2Preflight.Run();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
