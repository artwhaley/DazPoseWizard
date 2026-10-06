using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DazPose.Performer;
using DazPose.Player;
using DazPose.Motion;
using DazPose.Toys;
using DazPose.Editor.Performer;
[InitializeOnLoad] public static class Day2Validation {
 const string Key="Day2Validation.Active"; const string Checks="Day2Validation.Checks";
 [Serializable] public class Rows {public List<string> checks=new List<string>();public string exception;}
 static Day2Validation(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key,false)) EditorApplication.delayCall+=()=>EditorApplication.delayCall+=Finish;};}
 static void Check(Rows rows,bool ok,string name){if(!ok)throw new Exception(name);rows.checks.Add(name);}
 public static void Run(){var rows=new Rows();try{
  if(!Application.dataPath.Replace('\\','/').Contains("/.dazposewizard/production-consolidation/"))throw new Exception("Disposable project required");
  if(AssetDatabase.LoadMainAssetAtPath(ProductionPerformerBuilder.PrefabPath)==null)ProductionPerformerBuilder.Create();
  EditorSceneManager.OpenScene(ProductionPerformerBuilder.ScenePath);
  var bindings=UnityEngine.Object.FindObjectsByType<PerformanceSceneBindings>(FindObjectsSortMode.None).OrderBy(x=>x.name).ToArray();
  Check(rows,bindings.Length==2,"fresh scene has two prefab instances");
  foreach(var binding in bindings)Check(rows,binding.Validate().Length==0,"declared capabilities valid: "+binding.name);
  var a=bindings[0].Performer;var b=bindings[1].Performer;
  Check(rows,a.GetComponent<PerformanceSceneBindings>()==bindings[0] && b.GetComponent<PerformanceSceneBindings>()==bindings[1],"performer and scene binding are directly available on prefab root");
  ProductionPerformerBuilder.RejectExternalReferences(a.gameObject);
  ProductionPerformerBuilder.RejectExternalReferences(b.gameObject);
  var missing=new GameObject("Missing performer").AddComponent<PerformanceSceneBindings>();
  Check(rows,missing.Validate().Any(x=>x.Owner==missing && x.PropertyPath=="performer"),"missing actor identifies owner/reference");UnityEngine.Object.DestroyImmediate(missing.gameObject);
  Check(rows,bindings[0].Validate(PerformerCapability.Contact).Any(x=>x.PropertyPath=="contactTarget"),"missing contact target precise and optional");
  var so=new SerializedObject(a);var profile=so.FindProperty("locomotionProfile").objectReferenceValue;so.FindProperty("locomotionProfile").objectReferenceValue=null;so.ApplyModifiedPropertiesWithoutUndo();
  Check(rows,bindings[0].Validate(PerformerCapability.Movement).Any(x=>x.Owner==a && x.PropertyPath=="locomotionProfile"),"missing movement identifies actor/profile");
  Check(rows,!bindings[0].Validate(PerformerCapability.Speech).Any(x=>x.PropertyPath=="locomotionProfile"),"unused missing movement does not block speech");
  so.FindProperty("locomotionProfile").objectReferenceValue=profile;so.ApplyModifiedPropertiesWithoutUndo();
  Check(rows,PerformerDefaultsEditor.IsSupported("breathsPerMinute") && !PerformerDefaultsEditor.IsSupported("breathingBones") && !PerformerDefaultsEditor.IsSupported("speechAudioSource"),"tuning whitelist includes rate and excludes rig references");
  foreach(var suffix in new[]{"A","B"}){
   var scope=new GameObject("Scoped routing "+suffix);var playerObject=new GameObject("Player");playerObject.transform.SetParent(scope.transform);var player=playerObject.AddComponent<PlayerController>();
   var toyObject=new GameObject("Devices");toyObject.transform.SetParent(scope.transform);var toys=toyObject.AddComponent<ToyControlService>();var clockObject=new GameObject("Clock");clockObject.transform.SetParent(scope.transform);var clock=clockObject.AddComponent<MotionDriver>();
   SceneBindingMigration.Bind(scope);Check(rows,player.Toys==toys && toys.PlaybackDriver==clock,"scoped player/toy routing "+suffix);
   var ambiguous=new GameObject("Other Devices");ambiguous.transform.SetParent(scope.transform);ambiguous.AddComponent<ToyControlService>();bool refused=false;try{SceneBindingMigration.Bind(scope);}catch(InvalidOperationException){refused=true;}
   Check(rows,refused && player.Toys==toys && toys.PlaybackDriver==clock,"ambiguous scope rejected without changing references "+suffix);UnityEngine.Object.DestroyImmediate(scope);
  }
  SessionState.SetFloat("Day2Validation.ABaseline",a.BreathsPerMinute);SessionState.SetFloat("Day2Validation.BBaseline",b.BreathsPerMinute);
  Directory.CreateDirectory("Assets/TestData/ProductionValidation");AssetDatabase.Refresh();
  var expression=AssetDatabase.LoadAssetAtPath<PerformerExpression>("Assets/TestData/ProductionValidation/Smile.asset");
  if(expression==null){
   var brow=a.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="rBrowInner");
   string relative=AnimationUtility.CalculateTransformPath(brow,a.transform);var position=brow.localPosition+new Vector3(0f,.001f,0f);
   var clip=new AnimationClip();clip.name="Day 2 technical facial bone expression";clip.SetCurve(relative,typeof(Transform),"m_LocalPosition.y",AnimationCurve.Constant(0f,1f,position.y));
   AssetDatabase.CreateAsset(clip,"Assets/TestData/ProductionValidation/Smile.anim");
   expression=ScriptableObject.CreateInstance<PerformerExpression>();var es=new SerializedObject(expression);es.FindProperty("clip").objectReferenceValue=clip;
   var channels=es.FindProperty("boneChannels");channels.arraySize=1;var channel=channels.GetArrayElementAtIndex(0);channel.FindPropertyRelative("transformPath").stringValue=relative;channel.FindPropertyRelative("dazBoneId").stringValue=brow.name;channel.FindPropertyRelative("properties").intValue=(int)PerformerExpressionBoneProperties.LocalPosition;channel.FindPropertyRelative("targetLocalPosition").vector3Value=position;es.ApplyModifiedPropertiesWithoutUndo();
   AssetDatabase.CreateAsset(expression,"Assets/TestData/ProductionValidation/Smile.asset");AssetDatabase.SaveAssets();
  }
  var speech=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Generated/A.mp3") ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Crazy Minnow Studio/Examples/Audio/Promo-male.mp3");
  Check(rows,speech!=null,"existing voice clip assigned");
  var probe=new GameObject("Day 2 Observer").AddComponent<Day2Probe>();probe.a=a;probe.b=b;probe.speech=speech;probe.expression=expression;probe.mark=bindings[0].TargetNamed("bedside");
  Directory.CreateDirectory("Assets/TestData/ProductionValidation");AssetDatabase.Refresh();
  EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),"Assets/TestData/ProductionValidation/Day2Probe.unity");
  SessionState.SetString(Checks,JsonUtility.ToJson(rows));SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
 }catch(Exception e){rows.exception=e.ToString();Write(rows);EditorApplication.Exit(1);}}
 static void Finish(){var rows=JsonUtility.FromJson<Rows>(SessionState.GetString(Checks,"{}"));try{
  var runtime=JsonUtility.FromJson<Day2Probe.Result>(File.ReadAllText("TestOutput/day2/runtime.json"));
  Check(rows,string.IsNullOrEmpty(runtime.exception),"runtime operations passed: "+runtime.exception);
  rows.checks.AddRange(runtime.checks);
  var actors=UnityEngine.Object.FindObjectsByType<PerformanceSceneBindings>(FindObjectsSortMode.None).OrderBy(x=>x.name).Select(x=>x.Performer).ToArray();
  Check(rows,Mathf.Approximately(actors[0].BreathsPerMinute,17f),"explicit apply retained on saved actor");
  Check(rows,Mathf.Approximately(actors[1].BreathsPerMinute,SessionState.GetFloat("Day2Validation.BBaseline",0)),"temporary tuning discarded on other actor");
  Check(rows,PrefabUtility.GetPropertyModifications(actors[0]).Any(p=>p.propertyPath=="breathsPerMinute"),"apply records prefab override");
  Undo.PerformUndo();Check(rows,Mathf.Approximately(actors[0].BreathsPerMinute,SessionState.GetFloat("Day2Validation.ABaseline",0)),"Undo restores saved defaults");
  Undo.PerformRedo();Check(rows,Mathf.Approximately(actors[0].BreathsPerMinute,17f),"Redo reapplies defaults");
  var path=EditorSceneManager.GetActiveScene().path;EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());EditorSceneManager.OpenScene(path);
  var reopened=UnityEngine.Object.FindObjectsByType<PerformanceSceneBindings>(FindObjectsSortMode.None).OrderBy(x=>x.name).ToArray();
  Check(rows,Mathf.Approximately(reopened[0].Performer.BreathsPerMinute,17f),"defaults survive save/reopen");
  Check(rows,reopened.All(x=>x.Validate().Length==0),"bindings survive save/reopen");
 }catch(Exception e){rows.exception=e.ToString();}finally{SessionState.SetBool(Key,false);Write(rows);EditorApplication.Exit(string.IsNullOrEmpty(rows.exception)?0:1);}}
 static void Write(Rows rows){Directory.CreateDirectory("TestOutput/day2");File.WriteAllText("TestOutput/day2/validation.json",JsonUtility.ToJson(rows,true));}
}






