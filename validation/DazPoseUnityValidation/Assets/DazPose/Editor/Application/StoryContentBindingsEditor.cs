using System;
using System.Linq;
using DazPose.Story;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor {
 [CustomEditor(typeof(StoryContentBindings))]
 public sealed class StoryContentBindingsEditor : UnityEditor.Editor {
  public override void OnInspectorGUI() {
   var content=(StoryContentBindings)target;serializedObject.Update();
   EditorGUILayout.LabelField("Story content bindings",EditorStyles.boldLabel);
   EditorGUILayout.HelpBox("Source symbols are reconciled from .dazstory. Assign generated PerformerPose, gesture, shader, motion and dialogue assets here; retained rows no longer used by the source are preserved for recovery.",MessageType.Info);
   foreach(string name in new[]{"poses","gestures","shaders","motion","wardrobe","hair"})EditorGUILayout.PropertyField(serializedObject.FindProperty(name),true);
   using(new EditorGUI.DisabledScope(true))EditorGUILayout.PropertyField(serializedObject.FindProperty("sceneSymbols"),true);
   string sourcePath=null;
   if(!string.IsNullOrEmpty(content.ownerSourceGuid))sourcePath=AssetDatabase.GUIDToAssetPath(content.ownerSourceGuid);
   if(string.IsNullOrEmpty(sourcePath))sourcePath=AssetDatabase.GetAssetPath(content).Replace(".bindings.asset",".dazstory");
   var compiled=AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>(sourcePath);
   var currentIds=compiled!=null&&compiled.program!=null?compiled.program.dialogue.Select(d=>d.id).ToArray():Array.Empty<string>();
   EditorGUILayout.Space();EditorGUILayout.LabelField("Dialogue AudioClip bindings",EditorStyles.boldLabel);
   var rows=serializedObject.FindProperty("dialogue");
   for(int i=0;i<rows.arraySize;i++) {
    var row=rows.GetArrayElementAtIndex(i);string id=row.FindPropertyRelative("id").stringValue;
    bool active=currentIds.Contains(id);EditorGUILayout.LabelField(active?"Current transcript":"Unused transcript (retained)",active?EditorStyles.boldLabel:EditorStyles.miniLabel);
    EditorGUILayout.PropertyField(row.FindPropertyRelative("transcript"),new GUIContent("Transcript"));
    EditorGUILayout.PropertyField(row.FindPropertyRelative("clip"),new GUIContent("AudioClip"));
    EditorGUILayout.PropertyField(row.FindPropertyRelative("engineeringPlaceholder"),new GUIContent("Engineering placeholder"));
   }
   serializedObject.ApplyModifiedProperties();
   if(compiled==null)EditorGUILayout.HelpBox("The source asset is not currently imported. Existing assignments are preserved.",MessageType.Warning);
   if(!string.IsNullOrEmpty(sourcePath)&&System.IO.File.Exists(sourcePath)&&GUILayout.Button("Reconcile from source (recovery)")) {
    SourceBindingReconciler.ReconcileAsset(sourcePath,AssetDatabase.AssetPathToGUID(sourcePath),true);GUIUtility.ExitGUI();
   }
  }
 }
}
