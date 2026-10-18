using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DazPose.Editor {
 [InitializeOnLoad]
 public sealed class GlobalStoryCardIndexBuilder : AssetPostprocessor,IPreprocessBuildWithReport {
  public const string PublishedRoot="Assets/Production/Story/Cards";
  public const string IndexPath="Assets/Resources/DazPose/GlobalStoryCards.asset";
  static bool pending,writing;
  public int callbackOrder=>0;
  static GlobalStoryCardIndexBuilder() {
   Schedule();
   EditorApplication.playModeStateChanged+=state=> {
    // Flush pending publication before a user-started session, including disabled domain reload.
    // This callback never starts Play or rebuilds a scene.
    if(state==PlayModeStateChange.ExitingEditMode) {
     try {EnsureCurrent();}
     catch(Exception error) {Debug.LogError("Global card indexing before Play failed: "+error.Message);}
    } else if(state==PlayModeStateChange.EnteredEditMode)Schedule();
   };
  }
  static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom) {
   if(writing)return;
   if(imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(p=>p!=IndexPath && p!=IndexPath+".meta"))Schedule();
  }
  static void Schedule() {
   if(pending)return;pending=true;EditorApplication.delayCall+=RefreshPending;
  }
  static void RefreshPending() {
   pending=false;
   if(EditorApplication.isPlayingOrWillChangePlaymode)return;
   if(EditorApplication.isCompiling || EditorApplication.isUpdating) {Schedule();return;}
   try {EnsureCurrent();}
   catch(Exception error) {Debug.LogError("Global card indexing could not finish: "+error.Message);}
  }
  public static GlobalStoryCardCatalog EnsureCurrent(bool requireValid=false) {
   if(writing)throw new InvalidOperationException("Global card indexing is already in progress.");
   writing=true;
   try {
    var guids=AssetDatabase.IsValidFolder(PublishedRoot) ? AssetDatabase.FindAssets("t:CompiledStoryAsset",new[]{PublishedRoot}).OrderBy(g=>g,StringComparer.Ordinal).ToArray() : Array.Empty<string>();
    var assets=new List<CompiledStoryAsset>();var errors=new List<string>();var hashInput=new StringBuilder("global-card-index-v1\n");
    foreach(string guid in guids) {
     string path=AssetDatabase.GUIDToAssetPath(guid);
     if(!path.EndsWith(".dazstory",StringComparison.OrdinalIgnoreCase))continue;
     var asset=AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>(path);
     assets.Add(asset);hashInput.Append(guid).Append(':').Append(AssetDatabase.GetAssetDependencyHash(path)).Append('\n');
     if(asset==null || !asset.IsCompiled) {errors.Add(path+": "+string.Join("\n",asset?.diagnostics ?? new[]{"Missing compiled source."}));continue;}
     if(asset.sourceIdentity!=guid) {errors.Add(path+": stale source identity; reimport this source.");continue;}
     try {errors.AddRange(asset.content.Diagnostics(asset.program));}
     catch(Exception error) {errors.Add(path+": "+error.Message);}
    }
    if(errors.Count==0) {
     try {StoryProgramLinker.Link(new CompiledStoryProgram(),assets.Select(a=>new StoryLinkUnit(a.sourceIdentity,a.program)),"index-validation");}
     catch(Exception error) {errors.Add(error.Message);}
    }
    string[] diagnostics=errors.Distinct().ToArray();
    hashInput.Append(string.Join("\n",diagnostics));
    string fingerprint;
    using(var sha=SHA256.Create())fingerprint=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(hashInput.ToString()))).Replace("-","").ToLowerInvariant();
    var index=AssetDatabase.LoadAssetAtPath<GlobalStoryCardCatalog>(IndexPath);
    if(index==null) {EnsureFolder(Path.GetDirectoryName(IndexPath).Replace('\\','/'));index=ScriptableObject.CreateInstance<GlobalStoryCardCatalog>();AssetDatabase.CreateAsset(index,IndexPath);}
    if(index.fingerprint!=fingerprint || !index.sources.SequenceEqual(assets) || !index.diagnostics.SequenceEqual(diagnostics)) {
     index.fingerprint=fingerprint;index.sources=assets.ToArray();index.diagnostics=diagnostics;EditorUtility.SetDirty(index);AssetDatabase.SaveAssetIfDirty(index);
    }
    if(requireValid && diagnostics.Length>0)throw new BuildFailedException("Published global cards are invalid:\n"+string.Join("\n",diagnostics));
    return index;
   } finally {writing=false;}
  }
  static void EnsureFolder(string path) {
   if(AssetDatabase.IsValidFolder(path))return;
   string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);
   if(string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent,Path.GetFileName(path))))throw new IOException("Cannot create "+path);
  }
  public static void PublishFirstContactCards() {
   const string sourcePath=StoryFoundationBuilder.SourcePath;
   const string cardPath=PublishedRoot+"/FirstContactEngineering.dazstory";
   string companion=Path.ChangeExtension(cardPath,"bindings.asset");
   var source=AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>(sourcePath);
   if(source==null || !source.IsCompiled || source.content==null)throw new InvalidOperationException("Compile the saved First Contact source before migrating its cards.");
   var selected=source.program.cards.Where(c=>c.Tags.Length>0).Select(c=>c.Id).ToArray();
   if(selected.Length==0) {EnsureCurrent(true);return;}
   if(selected.Length!=17)throw new InvalidOperationException("The reviewed First Contact migration expects its 17 original tagged cards.");
   if(File.Exists(cardPath) || File.Exists(companion))throw new IOException("Review existing global content rather than overwriting it: "+cardPath);
   string original=File.ReadAllText(sourcePath);
   var declarations=System.Text.RegularExpressions.Regex.Matches(original,@"(?m)^(setup|card|sequence)\s+(\w+)");
   var local=new StringBuilder();var global=new StringBuilder();
   int copied=0;
   for(int i=0;i<declarations.Count;i++) {
    var declaration=declarations[i];int end=i+1<declarations.Count ? declarations[i+1].Index : original.Length;
    if(i==0 && declaration.Index>0)local.Append(original.Substring(0,declaration.Index));
    string block=original.Substring(declaration.Index,end-declaration.Index);
    if(declaration.Groups[1].Value=="card" && selected.Contains(declaration.Groups[2].Value)) {global.Append(block);copied++;}
    else local.Append(block);
   }
   if(copied!=selected.Length)throw new InvalidOperationException("Could not identify every tagged declaration without altering authored content.");
   var rootProgram=DazStoryCompiler.Compile(local.ToString(),sourcePath,source.content.Symbols(),true);
   var cardsProgram=DazStoryCompiler.Compile(global.ToString(),cardPath,source.content.Symbols(),true);
   var linked=StoryProgramLinker.Link(rootProgram,new[]{new StoryLinkUnit("migration-validation",cardsProgram)});
   if(linked.cards.Length!=source.program.cards.Length || linked.dialogue.Length!=source.program.dialogue.Length
    || !source.program.cards.Select(c=>c.Id).SequenceEqual(linked.cards.Select(c=>c.Id)))
    throw new InvalidOperationException("Card migration changed the declaration population or oracle.");
   EnsureFolder(PublishedRoot);
   bool sourceWritten=false;
   AssetDatabase.StartAssetEditing();
   try {
    var bindings=UnityEngine.Object.Instantiate(source.content);bindings.name="First Contact Engineering Bindings";
    AssetDatabase.CreateAsset(bindings,companion);
    File.WriteAllText(cardPath,global.ToString());
    File.WriteAllText(sourcePath,local.ToString());sourceWritten=true;
    AssetDatabase.ImportAsset(cardPath);AssetDatabase.ImportAsset(sourcePath);
   } catch {
    if(sourceWritten)File.WriteAllText(sourcePath,original);
    if(File.Exists(cardPath))AssetDatabase.DeleteAsset(cardPath);
    if(File.Exists(companion))AssetDatabase.DeleteAsset(companion);
    throw;
   } finally {AssetDatabase.StopAssetEditing();}
   EnsureCurrent(true);
  }
  [MenuItem("DazPose/Content/Advanced/Reindex Published Cards")]
  public static void Reindex() {
   var index=EnsureCurrent(true);
   Debug.Log("Global card index is current: "+index.sources.Length+" published sources; no scene membership changes.");
  }
  [MenuItem("DazPose/Story/New Global Card Source...")]
  public static void NewCardSource() {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Author new cards in Edit Mode.");
   EnsureFolder(PublishedRoot);
   string path=EditorUtility.SaveFilePanelInProject("New Global Card Source","NewGlobalCard","dazstory","Create automatically published tagged content",PublishedRoot);
   if(string.IsNullOrEmpty(path))return;
   if(!path.StartsWith(PublishedRoot+"/",StringComparison.Ordinal))throw new InvalidOperationException("Publish global card sources under "+PublishedRoot+".");
   string name=Path.GetFileNameWithoutExtension(path);
   if(name.Length==0 || !char.IsLetter(name[0]) || name.Any(c=>!char.IsLetterOrDigit(c) && c!='_'))throw new InvalidOperationException("Use a letter-led source/card name with letters, numbers or underscores.");
   string companion=Path.ChangeExtension(path,"bindings.asset");
   if(!File.Exists(companion))AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<StoryContentBindings>(),companion);
   File.WriteAllText(path,"card "+name+"\ntags [Warmup]\nprogress 10\n{\n    wait 1s\n}\n");
   AssetDatabase.ImportAsset(path);Selection.activeObject=AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>(path);
  }
  public void OnPreprocessBuild(BuildReport report) {EnsureCurrent(true);}
 }
}
