using System;
using System.IO;
using System.Linq;
using DazPose.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DazPose.Editor {
 /// <summary>
 /// Creates the temporary Player Setup scene: the Void environment, the player rig, the player body lying at a
 /// mark, a movable track target and a test panel. Refuses to overwrite an existing scene unless asked to.
 /// </summary>
 public static class PlayerSetupSceneBuilder {
  public const string ScenePath="Assets/Scenes/PlayerSetup.unity";
  /// <summary>First Contact's player_start mark: lying face up, -90 degrees about X.</summary>
  public static readonly Vector3 LyingPosition=new Vector3(0f,1.172133f,0f);
  public static readonly Quaternion LyingRotation=Quaternion.Euler(-90f,0f,0f);
  public static readonly Vector3 TrackTargetPosition=new Vector3(0.5f,1.6f,0.9f);

  [MenuItem("DazPose/Player/Create Player Setup Scene")]
  static void Menu() {
   try {
    bool overwrite=false;
    if(File.Exists(ScenePath)) {
     if(!EditorUtility.DisplayDialog("Player Setup scene exists",ScenePath+" already exists. Replace it with a fresh one?","Replace","Keep")) return;
     overwrite=true;
    }
    if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
    string report=Execute(overwrite);
    Debug.Log(report);
    EditorUtility.DisplayDialog("Player Setup scene",report,"OK");
   } catch(Exception error) { Debug.LogException(error); EditorUtility.DisplayDialog("Player Setup scene failed",error.Message,"OK"); }
  }
  /// <summary>Headless entry point: -executeMethod DazPose.Editor.PlayerSetupSceneBuilder.RunHeadless (never overwrites).</summary>
  public static void RunHeadless() {
   try { Debug.Log(Execute(false)); }
   catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
  }
  public static string Execute(bool overwrite) {
   if(File.Exists(ScenePath) && !overwrite) return "Player Setup scene already exists at "+ScenePath+"; nothing changed.";
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ProductionPlayerBodyBuilder.PrefabPath) ?? throw new InvalidOperationException("Missing "+ProductionPlayerBodyBuilder.PrefabPath+".");
   if(prefab.GetComponent<PlayerCameraMounts>()==null) throw new InvalidOperationException("Run DazPose > Player > Add Camera Mounts to PlayerBody first.");
   if(File.Exists(ScenePath) && !AssetDatabase.DeleteAsset(ScenePath)) throw new IOException("Cannot remove the existing "+ScenePath+".");
   var scene=ProductionSceneEnvironment.Copy(ScenePath);
   var players=GameSessionSceneSetup.InScene<PlayerController>(scene);
   if(players.Length!=1) throw new InvalidOperationException("The Void environment should contain exactly one Player; found "+players.Length+".");
   var player=players[0];
   if(player.ConfiguredView==null || player.MainCamera==null) throw new InvalidOperationException("The environment's Player rig is missing its view or camera.");

   var marks=new GameObject("Authoring Marks");
   var mark=new GameObject("player_start"); mark.transform.SetParent(marks.transform,false);
   mark.transform.SetPositionAndRotation(LyingPosition,LyingRotation);

   var body=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
   body.name="Player Body";
   body.transform.SetPositionAndRotation(mark.transform.position,mark.transform.rotation);
   var presence=body.GetComponent<PlayerBodyPresence>();
   player.ConfigureBodyPresence(presence);
   EditorUtility.SetDirty(player);

   var cameras=player.GetComponent<PlayerCameraController>() ?? player.gameObject.AddComponent<PlayerCameraController>();

   var target=new GameObject("Track Target");
   target.transform.position=TrackTargetPosition;

   var panelHost=new GameObject("Player Setup Panel");
   var panel=panelHost.AddComponent<PlayerSetupPanel>();
   panel.Configure(player,cameras,presence,target.transform,body.GetComponent<PlayerBreathing>());
   var missing=panel.MissingReferences();
   if(missing.Count>0) throw new InvalidOperationException("Panel references did not resolve: "+string.Join(", ",missing));

   EditorSceneManager.MarkSceneDirty(scene);
   if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save "+ScenePath+".");
   return "Created "+ScenePath+" from the Void environment: Player Body lying at player_start "+LyingPosition.ToString("F3")+", camera mounts "
    +(body.GetComponent<PlayerBreathing>()!=null ? "and breathing present" : "present, breathing not added yet (run DazPose > Player > Add Breathing to PlayerBody, then create the scene again)")
    +", Track Target at "+TrackTargetPosition.ToString("F2")+", test panel wired. Press Play and use the on-screen panel.";
  }
 }
}
