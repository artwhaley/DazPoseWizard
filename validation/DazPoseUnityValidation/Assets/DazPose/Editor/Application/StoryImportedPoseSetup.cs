using System;

using System.IO;

using System.Linq;

using DazPose.Performer;

using DazPose.Story;

using UnityEditor;

using UnityEditor.SceneManagement;

using UnityEngine;

namespace DazPose.Editor {

 public static class StoryImportedPoseSetup {

  [Serializable] public sealed class Choice {public string alias,posture,pose,clip;}

  [Serializable] public sealed class Report {public int seed;public Choice[] choices;public string gesture,preview;}

  [MenuItem("DazPose/Story/Assign Missing First Contact Poses from Imports")]

  public static void PrepareImportedPoses() {

   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Assign story poses in Edit Mode.");

   if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;

   StoryFoundationBuilder.Prepare();

   var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();

   var sequence=GameSessionSceneSetup.InScene<StoryProgramSequence>(scene).Single();var content=sequence.CompiledSource.content;

   var candidates=AssetDatabase.FindAssets("t:PerformerPose",new[]{"Assets/Animations/DazPoses"}).Select(AssetDatabase.GUIDToAssetPath)

    .Where(path=>File.Exists("Assets/DazPoseImports/"+Path.GetFileNameWithoutExtension(path)+".dazpose.json"))

    .Select(AssetDatabase.LoadAssetAtPath<PerformerPose>).Where(p=>p!=null && p.Clip!=null && !p.Clip.empty && !p.Clip.legacy).OrderBy(p=>p.name,StringComparer.Ordinal).ToList();

   int seed=unchecked((int)DateTime.UtcNow.Ticks);var random=new System.Random(seed);

   foreach(var row in content.poses) {

    if(row.value!=null)continue; // Refresh never overwrites an author's existing choice.

    string term=row.posture==PlayerPosture.Sitting ? "sit" : "stand";

    var pool=candidates.Where(p=>p.name.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0 && !content.poses.Any(r=>r.value==p)).ToArray();

    if(pool.Length==0)throw new InvalidOperationException("Import another distinct PerformerPose with '"+term+"' in its name for "+row.id+".");

    row.value=pool[random.Next(pool.Length)];

   }

   foreach(var row in content.poses)ValidatePose(row.value,sequence.Bindings.lara.ConfiguredAnimator);

   var gesture=content.gestures.Single(g=>g.id=="Disapproval");

   if(gesture.value==null)gesture.value=CreateDisapprovalFixture(sequence.Bindings.lara);

   if(!gesture.value.IsReady(out string reason))throw new InvalidOperationException(reason);

   EditorUtility.SetDirty(content);EditorUtility.SetDirty(sequence);

   AssetDatabase.SaveAssets();AssetDatabase.ImportAsset(StoryFoundationBuilder.SourcePath,ImportAssetOptions.ForceUpdate);

   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);

   var report=new Report {seed=seed,preview=string.IsNullOrEmpty(sequence.StopBeforePhase) ? "full session" : "before "+sequence.StopBeforePhase,choices=content.poses.Select(p=>new Choice {alias=p.id,posture=p.posture.ToString(),pose=AssetDatabase.GetAssetPath(p.value),clip=AssetDatabase.GetAssetPath(p.value.Clip)}).ToArray(),gesture=AssetDatabase.GetAssetPath(gesture.value)};

   Directory.CreateDirectory("TestOutput/StoryFoundation");File.WriteAllText("TestOutput/StoryFoundation/imported-poses.json",JsonUtility.ToJson(report,true));

   var preview=string.IsNullOrEmpty(sequence.StopBeforePhase) ? sequence.CompiledSource.program : DazPose.Story.Language.StoryPlaybackWindow.BeforePhase(sequence.CompiledSource.program,"FirstContact",sequence.StopBeforePhase);

   string[] errors=content.Diagnostics(preview,sequence.Bindings);if(errors.Length>0)throw new InvalidOperationException(string.Join("\n",errors));

   Debug.Log("FIRST_CONTACT_IMPORTED_POSES_READY: "+JsonUtility.ToJson(report));

  }

  static void ValidatePose(PerformerPose pose,Animator animator) {

   if(pose==null || pose.Clip==null || pose.Clip.empty || pose.Clip.legacy)throw new InvalidOperationException("Assign a nonempty prepared PerformerPose wrapper.");

   foreach(var binding in AnimationUtility.GetCurveBindings(pose.Clip)) {

    if(binding.type==typeof(Transform) && !string.IsNullOrEmpty(binding.path) && animator.transform.Find(binding.path)==null)throw new InvalidOperationException(pose.name+": missing skeletal path "+binding.path);

    var curve=AnimationUtility.GetEditorCurve(pose.Clip,binding);

    if(curve==null || curve.keys.Any(k=>!float.IsFinite(k.time) || !float.IsFinite(k.value) || float.IsNaN(k.inTangent) || float.IsNaN(k.outTangent)))throw new InvalidOperationException(pose.name+": invalid animation curve "+binding.path);

   }

  }

  static PerformerGesture CreateDisapprovalFixture(SuccubusPerformer performer) {

   const string folder="Assets/Production/Story/Gestures/Engineering";Directory.CreateDirectory(folder);AssetDatabase.Refresh();

   string clipPath=folder+"/Disapproval_Engineering.anim",gesturePath=folder+"/Disapproval_Engineering.asset";

   var animator=performer.ConfiguredAnimator;var head=animator.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="head");

   string path=AnimationUtility.CalculateTransformPath(head,animator.transform);

   var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

   if(clip==null) {

    clip=new AnimationClip {name="Disapproval - temporary engineering head shake",frameRate=30};

    float[] times={0,.2f,.4f,.6f,.8f,1};float[] yaw={0,-12,12,-10,8,0};var curves=Enumerable.Range(0,4).Select(_=>new AnimationCurve()).ToArray();

    for(int i=0;i<times.Length;i++) {var q=head.localRotation*Quaternion.Euler(0,yaw[i],0);curves[0].AddKey(times[i],q.x);curves[1].AddKey(times[i],q.y);curves[2].AddKey(times[i],q.z);curves[3].AddKey(times[i],q.w);}

    string[] axes={"x","y","z","w"};for(int i=0;i<4;i++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+axes[i]),curves[i]);

    clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;settings.hasAdditiveReferencePose=true;settings.additiveReferencePoseTime=0;settings.additiveReferencePoseClip=null;AnimationUtility.SetAnimationClipSettings(clip,settings);AssetDatabase.CreateAsset(clip,clipPath);

   }

   var gesture=AssetDatabase.LoadAssetAtPath<PerformerGesture>(gesturePath);

   if(gesture==null) {gesture=ScriptableObject.CreateInstance<PerformerGesture>();gesture.name="Disapproval - temporary engineering gesture";gesture.ConfigureInEditor(clip,.12f,.18f);AssetDatabase.CreateAsset(gesture,gesturePath);}

   return gesture;

  }

 }

}

