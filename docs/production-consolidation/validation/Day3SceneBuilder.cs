using System;using System.IO;using System.Linq;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using DazPose.Performer;using DazPose.Performer.HandGrip;using DazPose.Player;using DazPose.Validation;using DazPose.FirstPerformanceVoid;
public static class Day3SceneBuilder {
 public const string ScenePath="Assets/Production/Scenes/Day3CommandValidation.unity";
 public static void Create(){try{
 if(!Application.dataPath.Replace('\\','/').Contains("/.dazposewizard/production-consolidation/"))throw new Exception("Disposable project required");
 var scene=EditorSceneManager.OpenScene("Assets/Production/Scenes/Day2ActorValidation.unity");
 var actors=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SuccubusPerformer>(true)).OrderBy(a=>a.name).ToArray();
 var stage=scene.GetRootGameObjects().Single(x=>x.name=="FirstPerformanceVoid");var markers=stage.transform.Find("PerformanceMarkers");
 var player=stage.GetComponentInChildren<PlayerController>(true);var seat=stage.GetComponentInChildren<PerformerSeat>(true);
 var clips=new[]{AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Generated/A.mp3"),AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Generated/B.mp3")};
 if(clips.Any(x=>x==null))throw new Exception("Accepted First Contact speech clips missing");
 var root=new GameObject("Day 3 Scene Runner");var contact=root.AddComponent<FirstContactPerformance>();
 var face=actors[0].GetComponentsInChildren<Transform>(true).Single(t=>t.name=="FaceViewTarget");
 contact.Configure(actors[0],player,seat,face,markers.Find("FirstContact/LaraCloseMark"),markers.Find("PlayerView/ViewMark_Lara"),markers.Find("PlayerView/ViewMark_Lounge"),markers.Find("FirstContact/ViewMark_Final"),clips[0],clips[1]);
 var rod=new GameObject("Day 3 Contact Fixture").AddComponent<GripContactRod>();var start=new GameObject("Rod Start").transform;start.SetParent(rod.transform);start.position=new Vector3(.4f,1.0f,.2f);var end=new GameObject("Rod End").transform;end.SetParent(rod.transform);end.position=start.position+Vector3.forward*.15f;rod.StartPoint=start;rod.EndPoint=end;rod.Radius=.018f;
 var test=root.AddComponent<Day3RegressionRunner>();Set(test,"actor",actors[0]);Set(test,"other",actors[1]);Set(test,"firstContact",contact);Set(test,"moveMark",scene.GetRootGameObjects().Single(x=>x.name=="Bedside Mark").transform);Set(test,"speech",clips[0]);Set(test,"contactRod",rod);Set(test,"motionProgram",AssetDatabase.LoadAssetAtPath<DazPose.Motion.FunscriptMotionProgram>("Assets/motiondrive.funscript"));
  Set(test,"cancellationPose",All<PerformerPose>().First(x=>x.Clip!=null&&x!=actors[0].InitialPose));
  if(!AssetDatabase.IsValidFolder("Assets/Production/ValidationData"))AssetDatabase.CreateFolder("Assets/Production","ValidationData");
 if(AssetDatabase.LoadMainAssetAtPath("Assets/Production/ValidationData/Day3Face.anim")==null)AssetDatabase.CopyAsset("Assets/TestData/ProductionValidation/Smile.anim","Assets/Production/ValidationData/Day3Face.anim");
 if(AssetDatabase.LoadMainAssetAtPath("Assets/Production/ValidationData/Day3Expression.asset")==null)AssetDatabase.CopyAsset("Assets/TestData/ProductionValidation/Smile.asset","Assets/Production/ValidationData/Day3Expression.asset");
 var expression=AssetDatabase.LoadAssetAtPath<PerformerExpression>("Assets/Production/ValidationData/Day3Expression.asset");Set(expression,"clip",AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Production/ValidationData/Day3Face.anim"));
 Set(test,"cancellationExpression",expression);
 Set(test,"cancellationAction",All<PerformerAction>().First(x=>x.IsReady(out _)));
 Set(test,"cancellationGesture",All<PerformerGesture>().First(x=>x.IsReady(out _)));
 Set(test,"cancellationSeat",seat);
 Set(test,"cancellationSpell",All<PerformerSpell>().FirstOrDefault(x=>x.IsReady(out _)));
 var outfits=actors[0].GetComponent<PerformerWardrobe>().Catalog.Presets.Where(x=>x!=null).ToArray(); Set(test,"firstOutfit",outfits[0]);Set(test,"secondOutfit",outfits[1]); EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Debug.Log("DAY3_SAVED_RUNNER_SCENE_CREATED");Day3Validation.Run();
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 static T[] All<T>() where T:UnityEngine.Object => AssetDatabase.FindAssets("t:"+typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath).OrderBy(x=>x).Select(AssetDatabase.LoadAssetAtPath<T>).Where(x=>x!=null).ToArray();
 static void Set(UnityEngine.Object owner,string path,UnityEngine.Object value){using(var so=new SerializedObject(owner)){var p=so.FindProperty(path);if(p==null)throw new Exception(path);p.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}}
}




