using System;
using System.IO;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
namespace DazPose.Editor {
 [ScriptedImporter(1,"dazstory")]
 public sealed class DazStoryImporter : ScriptedImporter {
  public override void OnImportAsset(AssetImportContext context) {
   var asset=ScriptableObject.CreateInstance<CompiledStoryAsset>(); asset.name=Path.GetFileNameWithoutExtension(context.assetPath);
   string bindingPath=Path.ChangeExtension(context.assetPath,"bindings.asset").Replace('\\','/');
   try {
    context.DependsOnSourceAsset(bindingPath);
    asset.content=AssetDatabase.LoadAssetAtPath<StoryContentBindings>(bindingPath);
    if(asset.content==null) throw new InvalidOperationException("Assign the companion content bindings at "+bindingPath+". Use DazPose > Story > Prepare First Contact Authoring to create them.");
    asset.program=DazStoryCompiler.Compile(File.ReadAllText(context.assetPath),context.assetPath,asset.content.Symbols());
   } catch(Exception error) { asset.diagnostics=new[]{error.Message}; context.LogImportError(error.Message); }
   context.AddObjectToAsset("compiled",asset); context.SetMainObject(asset);
  }
 }
 [CustomEditor(typeof(CompiledStoryAsset))]
 public sealed class CompiledStoryAssetEditor : UnityEditor.Editor {
  public override void OnInspectorGUI() {
   var asset=(CompiledStoryAsset)target;
   if(GUILayout.Button("Open .dazstory source")) EditorUtility.OpenWithDefaultApp(Path.GetFullPath(AssetDatabase.GetAssetPath(asset)));
   if(asset.content!=null && GUILayout.Button("Select content bindings")) Selection.activeObject=asset.content;
   if(!asset.IsCompiled) { foreach(var message in asset.diagnostics) EditorGUILayout.HelpBox(message,MessageType.Error); return; }
   EditorGUILayout.LabelField("Compiled instructions",asset.program.instructions.Length.ToString());
   EditorGUILayout.LabelField("Dialogue bindings",asset.program.dialogue.Length.ToString());
   foreach(var message in asset.content.Diagnostics(asset.program)) EditorGUILayout.HelpBox(message,MessageType.Warning);
  }
 }
}