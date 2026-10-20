using System;
using System.IO;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
namespace DazPose.Editor {
 [ScriptedImporter(3,"dazstory")]
 public sealed class DazStoryImporter : ScriptedImporter {
  public override void OnImportAsset(AssetImportContext context) {
   var asset=ScriptableObject.CreateInstance<CompiledStoryAsset>(); asset.name=Path.GetFileNameWithoutExtension(context.assetPath);asset.sourceIdentity=AssetDatabase.AssetPathToGUID(context.assetPath);
   string bindingPath=Path.ChangeExtension(context.assetPath,"bindings.asset").Replace('\\','/');
   try {
    context.DependsOnSourceAsset(bindingPath);
    asset.content=SourceBindingReconciler.FindOwner(asset.sourceIdentity,bindingPath);
    if(asset.content!=null)context.DependsOnSourceAsset(AssetDatabase.GetAssetPath(asset.content));
    if(asset.content==null) {asset.content=ScriptableObject.CreateInstance<StoryContentBindings>();asset.content.name="Content bindings";context.AddObjectToAsset("bindings",asset.content);}
    var analysis=DazStoryCompiler.Analyze(File.ReadAllText(context.assetPath,new System.Text.UTF8Encoding(false,true)),context.assetPath,new StoryAnalysisOptions{SourceGuid=asset.sourceIdentity,AllowExternalCards=true});
    if(!analysis.SyntaxValid)throw new StorySourceException(new StorySource(context.assetPath,1,1),string.Join("\n",analysis.SyntaxDiagnostics));
    asset.program=analysis.Program;
    SourceBindingReconciler.Schedule(context.assetPath,asset.sourceIdentity);
   } catch(Exception error) { asset.diagnostics=new[]{error.Message}; context.LogImportError(error.Message); }
   context.AddObjectToAsset("compiled",asset); context.SetMainObject(asset);
  }
 }
 [CustomEditor(typeof(CompiledStoryAsset))]
 public sealed class CompiledStoryAssetEditor : UnityEditor.Editor {
  public override void OnInspectorGUI() {
   var asset=(CompiledStoryAsset)target;
   if(GUILayout.Button("Open .dazstory source")) EditorUtility.OpenWithDefaultApp(Path.GetFullPath(AssetDatabase.GetAssetPath(asset)));
   string bindingPath=Path.ChangeExtension(AssetDatabase.GetAssetPath(asset),"bindings.asset").Replace('\\','/');
   if(asset.content==null && GUILayout.Button("Reconcile source bindings")) {
    var content=SourceBindingReconciler.ReconcileAsset(AssetDatabase.GetAssetPath(asset),asset.sourceIdentity);
    AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(asset));Selection.activeObject=content;return;
   }
   if(asset.content!=null && GUILayout.Button("Select content bindings")) Selection.activeObject=asset.content;
   if(!asset.IsCompiled) { foreach(var message in asset.diagnostics) EditorGUILayout.HelpBox(message,MessageType.Error); return; }
   EditorGUILayout.LabelField("Compiled instructions",asset.program.instructions.Length.ToString());
   EditorGUILayout.LabelField("Dialogue bindings",asset.program.dialogue.Length.ToString());
   foreach(var message in asset.content.Diagnostics(asset.program)) EditorGUILayout.HelpBox(message,MessageType.Warning);
   if(!AssetDatabase.GetAssetPath(asset).StartsWith(GlobalStoryCardIndexBuilder.PublishedRoot+"/",StringComparison.Ordinal)) {
    try {
     var linked=StoryLinkedContent.Create(asset);
     EditorGUILayout.LabelField("Global selectable cards",System.Linq.Enumerable.Count(linked.Program.cards,c=>c.Selectable).ToString());
     foreach(var message in linked.Diagnostics(linked.Program))EditorGUILayout.HelpBox(message,MessageType.Warning);
    } catch(Exception error) {EditorGUILayout.HelpBox(error.Message,MessageType.Error);}
   }
  }
 }
}