using System;
using DazPose.Player;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor {
 /// <summary>Adds PlayerBreathing to PlayerBody.prefab. Idempotent: an existing component (and Art's tuning of it) is never touched.</summary>
 public static class PlayerBreathingSetup {
  [MenuItem("DazPose/Player/Add Breathing to PlayerBody")]
  static void Menu() {
   try { string report=Execute(); Debug.Log(report); EditorUtility.DisplayDialog("Player breathing",report,"OK"); }
   catch(Exception error) { Debug.LogException(error); EditorUtility.DisplayDialog("Player breathing failed",error.Message,"OK"); }
  }
  /// <summary>Headless entry point: -executeMethod DazPose.Editor.PlayerBreathingSetup.RunHeadless</summary>
  public static void RunHeadless() {
   try { Debug.Log(Execute()); }
   catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
  }
  public static string Execute() {
   const string path=ProductionPlayerBodyBuilder.PrefabPath;
   if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null) throw new InvalidOperationException("Missing "+path+".");
   var root=PrefabUtility.LoadPrefabContents(path);
   try {
    if(root.GetComponent<PlayerBodyPresence>()==null) throw new InvalidOperationException(path+" is not the player body (no PlayerBodyPresence on its root).");
    var existing=root.GetComponent<PlayerBreathing>();
    if(existing!=null) return "Breathing already present on "+path+"; nothing changed ("+existing.BreathsPerMinute.ToString("F1")+" breaths per minute, strength "+existing.Strength.ToString("F2")+").";
    var breathing=root.AddComponent<PlayerBreathing>();
    breathing.Prepare(); // fails here, naming the bone, if any default channel is missing or forbidden
    breathing.RestoreRest();
    PrefabUtility.SaveAsPrefabAsset(root,path);
    return "Added breathing to "+path+": "+breathing.BreathsPerMinute.ToString("F1")+" breaths per minute, strength "+breathing.Strength.ToString("F2")+", "+breathing.Channels.Count+" chest and abdomen bones. Tune it on the component in the prefab; the pelvis, hip and shaft bones are never moved.";
   } finally { PrefabUtility.UnloadPrefabContents(root); }
  }
 }
}
