using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DazPose.Editor {
 public static class CleanupReferenceChecks {
  [Serializable] sealed class Row {public string path;public int objects,references;public bool clean;public string[] errors;}
  [Serializable] sealed class Report {public bool passed;public Row[] roots;public bool generatedOutputProbe;}
  public static void Run() {
   if(EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s=>s.isDirty))
    throw new InvalidOperationException("Reference inspection requires idle Edit Mode and saved scenes.");
   var rows=new List<Row>();var active=SceneManager.GetActiveScene();
   foreach(string path in new[]{"Assets/Scenes/FirstPerformanceVoid.unity","Assets/Production/Scenes/RemediationToeValidation.unity","Assets/Production/Scenes/MainMenu.unity",StorySceneBuilder.ScenePath,"Assets/Scenes/ClothingSetup.unity","Assets/Examples/Scenes/AuthoringShowcase.unity",ProductionSceneEnvironment.TemplatePath}) {
    var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid() || !scene.isLoaded;
    if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
    try {var row=Inspect(path,scene.GetRootGameObjects());row.clean=!scene.isDirty;if(!row.clean)row.errors=row.errors.Concat(new[]{"Scene dirtied during read-only inspection."}).ToArray();rows.Add(row);}
    finally {if(opened)EditorSceneManager.CloseScene(scene,true);if(active.IsValid() && active.isLoaded)SceneManager.SetActiveScene(active);}
   }
   foreach(string path in new[]{"Assets/Production/Performers/Lara.prefab","Assets/Production/Player/PlayerBody.prefab","Assets/Production/Partners/Partner.prefab"}) {
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
    if(prefab==null)throw new InvalidOperationException("Missing retained prefab "+path);
    rows.Add(Inspect(path,new[]{prefab}));
   }
   string folder=DazPose.UnityValidation.LaraCandidateBuilder.GeneratedFolder+"/CleanupProbe_"+Guid.NewGuid().ToString("N");
   bool probe=false;
   try {
    var method=typeof(DazPose.UnityValidation.LaraCandidateBuilder).GetMethod("Save",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).MakeGenericMethod(typeof(AnimationClip));
    string path=folder+"/ProducerOutput.asset";
    var output=(AnimationClip)method.Invoke(null,new object[]{new AnimationClip(),path});
    probe=output!=null && AssetDatabase.GetAssetPath(output)==path && File.Exists(path+".meta");
    if(!probe)throw new InvalidOperationException("Generated Lara output did not use its new owned path.");
   } finally {if(AssetDatabase.IsValidFolder(folder))AssetDatabase.DeleteAsset(folder);}
   Directory.CreateDirectory("TestOutput/CleanupTask1");
   var report=new Report {passed=rows.All(r=>r.errors.Length==0),roots=rows.ToArray(),generatedOutputProbe=probe};
   File.WriteAllText("TestOutput/CleanupTask1/retained-references.json",JsonUtility.ToJson(report,true));
   if(!report.passed)throw new InvalidOperationException(string.Join("\n",rows.SelectMany(r=>r.errors.Select(e=>r.path+": "+e))));
   Debug.Log("Retained scenes/prefabs resolved without missing scripts or nonzero broken references; generated-output probe passed. No Play.");
  }
  static Row Inspect(string path,GameObject[] roots) {
   var errors=new List<string>();int objects=0,references=0;
   foreach(var t in roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true))) {
    objects++;
    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)errors.Add("Missing script on "+t.name);
    foreach(var c in t.GetComponents<Component>().Where(c=>c!=null)) {
     var data=new SerializedObject(c);var property=data.GetIterator();
     while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference) {
      if(property.objectReferenceValue!=null)references++;
      else if(!property.objectReferenceEntityIdValue.Equals(default(UnityEngine.EntityId)))errors.Add(t.name+"."+c.GetType().Name+"."+property.propertyPath+" has a missing object reference.");
     }
    }
   }
   return new Row {path=path,objects=objects,references=references,clean=true,errors=errors.ToArray()};
  }
 }
}
