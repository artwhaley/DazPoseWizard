using System;
using System.Linq;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor {
 [CustomEditor(typeof(StoryContentBindings))]
 public sealed class StoryContentBindingsEditor : UnityEditor.Editor {
  public override void OnInspectorGUI() {
   serializedObject.Update();
   EditorGUILayout.LabelField("First Contact pose choices",EditorStyles.boldLabel);
   EditorGUILayout.HelpBox("Import each choice as Pose in DazPoseTool. Bind its generated PerformerPose wrapper here; the raw .anim is not the pose binding.",MessageType.Info);
   var poses=serializedObject.FindProperty("poses");
   for(int i=0;i<poses.arraySize;i++) {
    var row=poses.GetArrayElementAtIndex(i);
    EditorGUILayout.PropertyField(row.FindPropertyRelative("value"),new GUIContent(row.FindPropertyRelative("id").stringValue));
    using(new EditorGUI.DisabledScope(true))EditorGUILayout.PropertyField(row.FindPropertyRelative("posture"),new GUIContent("Expected posture"));
   }
   EditorGUILayout.Space();
   foreach(string name in new[]{"gestures","shaders","motion","sceneSymbols"})EditorGUILayout.PropertyField(serializedObject.FindProperty(name),true);
   serializedObject.ApplyModifiedProperties();
   var content=(StoryContentBindings)target;
   string path=AssetDatabase.GetAssetPath(content).Replace(".bindings.asset",".dazstory");
   var source=AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>(path);
   if(source==null || !source.IsCompiled)return;
   EditorGUILayout.Space();EditorGUILayout.LabelField("Dialogue audio",EditorStyles.boldLabel);
   EditorGUILayout.HelpBox("The transcript is authored in .dazstory. Editing it creates a new unbound dialogue entry; select its AudioClip explicitly. Generated IDs are handled automatically.",MessageType.Info);
   if(GUILayout.Button("Refresh dialogue entries from source")) {
    Undo.RecordObject(content,"Refresh source dialogue bindings");
    foreach(var line in source.program.dialogue)if(!content.dialogue.Any(d=>d.id==line.id))
     content.dialogue=content.dialogue.Concat(new[]{new StoryDialogueContent {id=line.id,transcript=line.transcript}}).ToArray();
    EditorUtility.SetDirty(content);AssetDatabase.SaveAssets();GUIUtility.ExitGUI();
   }
   serializedObject.Update();var rows=serializedObject.FindProperty("dialogue");
   foreach(var line in source.program.dialogue) {
    int index=Array.FindIndex(content.dialogue,row=>row.id==line.id);
    EditorGUILayout.LabelField(line.transcript,EditorStyles.wordWrappedLabel);
    if(index<0) {EditorGUILayout.HelpBox("Refresh dialogue entries to bind this revised transcript.",MessageType.Warning);continue;}
    var row=rows.GetArrayElementAtIndex(index);
    EditorGUILayout.PropertyField(row.FindPropertyRelative("clip"),new GUIContent("AudioClip"));
    EditorGUILayout.PropertyField(row.FindPropertyRelative("engineeringPlaceholder"),new GUIContent("Engineering placeholder"));
   }
   serializedObject.ApplyModifiedProperties();
  }
 }
}