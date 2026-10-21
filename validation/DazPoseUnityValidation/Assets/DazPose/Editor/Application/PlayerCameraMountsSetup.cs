using System;
using System.Linq;
using System.Text;
using DazPose.Player;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor {
 /// <summary>
 /// Adds the High and Low camera mounts to PlayerBody.prefab. Idempotent: once the mounts exist the command
 /// changes nothing, so Art's tuning of the mount transforms is never overwritten.
 /// </summary>
 public static class PlayerCameraMountsSetup {
  public const string HighName="CameraMount_High",LowName="CameraMount_Low";
  /// <summary>High mount: this far above the head, looking level toward the feet.</summary>
  public const float HighHeightAboveHead=0.8f;
  /// <summary>Low mount: this far above the head, looking down the body toward the pelvis.</summary>
  public const float LowHeightAboveHead=0.2f;
  /// <summary>The body is placed lying face up by First Contact's player_start mark (-90 degrees about X).</summary>
  public static readonly Quaternion LyingRotation=Quaternion.Euler(-90f,0f,0f);

  public struct MountPlan {
   public Vector3 highPosition,lowPosition;
   public Quaternion highRotation,lowRotation;
  }

  /// <summary>Pure placement rule, in the world the body is lying in. Inputs are world positions.</summary>
  public static MountPlan Plan(Vector3 head,Vector3 feet,Vector3 pelvis) {
   Vector3 level=Vector3.ProjectOnPlane(feet-head,Vector3.up);
   if(level.sqrMagnitude<1e-6f) throw new InvalidOperationException("The head and feet are vertically aligned; the body is not lying down, so the mounts cannot be placed.");
   var plan=new MountPlan { highPosition=head+Vector3.up*HighHeightAboveHead,lowPosition=head+Vector3.up*LowHeightAboveHead };
   plan.highRotation=Quaternion.LookRotation(level.normalized,Vector3.up);
   Vector3 down=pelvis-plan.lowPosition;
   if(down.sqrMagnitude<1e-6f) throw new InvalidOperationException("The pelvis coincides with the Low mount; the body proportions look wrong.");
   plan.lowRotation=Quaternion.LookRotation(down.normalized,Vector3.up);
   return plan;
  }

  [MenuItem("DazPose/Player/Add Camera Mounts to PlayerBody")]
  static void Menu() {
   try {
    string report=Execute();
    Debug.Log(report);
    EditorUtility.DisplayDialog("Camera mounts",report,"OK");
   } catch(Exception error) {
    Debug.LogException(error);
    EditorUtility.DisplayDialog("Camera mounts failed",error.Message,"OK");
   }
  }

  /// <summary>Headless entry point: -executeMethod DazPose.Editor.PlayerCameraMountsSetup.RunHeadless</summary>
  public static void RunHeadless() {
   try { Debug.Log(Execute()); }
   catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
  }

  /// <summary>Applies the change and returns a report saying what was done.</summary>
  public static string Execute() {
   const string path=ProductionPlayerBodyBuilder.PrefabPath;
   if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null) throw new InvalidOperationException("Missing "+path+".");
   var root=PrefabUtility.LoadPrefabContents(path);
   try {
    if(root.GetComponent<PlayerBodyPresence>()==null) throw new InvalidOperationException(path+" is not the player body (no PlayerBodyPresence on its root).");
    var existing=root.GetComponent<PlayerCameraMounts>();
    if(existing!=null) {
     existing.Validate();
     return "Camera mounts already present on "+path+"; nothing changed. "+Describe(existing);
    }
    if(root.transform.Find(HighName)!=null || root.transform.Find(LowName)!=null)
     throw new InvalidOperationException("The prefab already has a camera mount child but no PlayerCameraMounts component; resolve it by hand rather than guessing.");
    Transform Bone(string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name)
     ?? throw new InvalidOperationException("Player body has no '"+name+"' bone.");
    var head=Bone("head");var pelvis=Bone("pelvis");var left=Bone("lFoot");var right=Bone("rFoot");
    // Compute with the body in its lying placement, then put the root back exactly as it was.
    var originalPosition=root.transform.localPosition;var originalRotation=root.transform.localRotation;
    MountPlan plan;Vector3 headWorld;
    try {
     root.transform.SetLocalPositionAndRotation(originalPosition,LyingRotation);
     headWorld=head.position;
     plan=Plan(headWorld,(left.position+right.position)*0.5f,pelvis.position);
     var highMount=CreateMount(root.transform,HighName,plan.highPosition,plan.highRotation);
     var lowMount=CreateMount(root.transform,LowName,plan.lowPosition,plan.lowRotation);
     root.AddComponent<PlayerCameraMounts>().Configure(highMount,lowMount);
    } finally { root.transform.SetLocalPositionAndRotation(originalPosition,originalRotation); }
    var added=root.GetComponent<PlayerCameraMounts>();
    added.Validate();
    PrefabUtility.SaveAsPrefabAsset(root,path);
    var report=new StringBuilder("Added camera mounts to "+path+". Computed with the body lying (rotation -90 about X), head bone at world ");
    report.Append(headWorld.ToString("F3")).Append(". ").Append(Describe(added,headWorld,root.transform,plan));
    return report.ToString();
   } finally { PrefabUtility.UnloadPrefabContents(root); }
  }

  static Transform CreateMount(Transform parent,string name,Vector3 worldPosition,Quaternion worldRotation) {
   var mount=new GameObject(name).transform;
   mount.SetParent(parent,false);
   mount.SetPositionAndRotation(worldPosition,worldRotation);
   return mount;
  }

  static string Describe(PlayerCameraMounts mounts) =>
   "High local position "+mounts.High.localPosition.ToString("F3")+", Low local position "+mounts.Low.localPosition.ToString("F3")+".";

  static string Describe(PlayerCameraMounts mounts,Vector3 headWorld,Transform root,MountPlan plan) =>
   "High: "+HighHeightAboveHead.ToString("F2")+" m above the head, level toward the feet, local position "+mounts.High.localPosition.ToString("F3")
   +" local euler "+mounts.High.localEulerAngles.ToString("F1")+". Low: "+LowHeightAboveHead.ToString("F2")+" m above the head, toward the pelvis, local position "
   +mounts.Low.localPosition.ToString("F3")+" local euler "+mounts.Low.localEulerAngles.ToString("F1")+". Tune by moving the two transforms in the prefab.";
 }
}
