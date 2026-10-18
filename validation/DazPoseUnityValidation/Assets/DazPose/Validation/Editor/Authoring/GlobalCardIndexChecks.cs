using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor {
 public static class GlobalCardIndexChecks {
  static int assertions;
  static void Require(bool valid,string message) {assertions++;if(!valid)throw new InvalidOperationException(message);}
  static string Hash(string path) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));}
  public static void Run() {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit Mode only.");
   assertions=0;
   string folder=GlobalStoryCardIndexBuilder.PublishedRoot+"/CleanupProbe_"+Guid.NewGuid().ToString("N");
   string source=folder+"/Probe.dazstory";
   var protectedPaths=new[]{StoryFoundationBuilder.SourcePath,Path.ChangeExtension(StoryFoundationBuilder.SourcePath,"bindings.asset"),StorySceneBuilder.ScenePath,ProductionApplicationSetup.CatalogPath};
   var hashes=protectedPaths.Select(Hash).ToArray();
   var baseline=GlobalStoryCardIndexBuilder.EnsureCurrent(true);
   string originalFingerprint=baseline.fingerprint;
   AssetDatabase.CreateFolder(GlobalStoryCardIndexBuilder.PublishedRoot,Path.GetFileName(folder));
   try {
    File.WriteAllText(source,"card CleanupProbe\ntags [Warmup]\nprogress 10\n{\nwait 1s\n}\n");
    AssetDatabase.ImportAsset(source,ImportAssetOptions.ForceSynchronousImport);
    var index=GlobalStoryCardIndexBuilder.EnsureCurrent(true);
    var root=AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>(StoryFoundationBuilder.SourcePath);
    var linked=StoryLinkedContent.Create(root);
    var director=new StoryDirector(linked.Program,i=>new StoryData(true),k=>new StoryData(0d),new StorySessionRandom(1),s=>{});
    director.Select(new CardSelectionRequest {RequiredTags=new[]{"Warmup"},Phase="Warmup"});
    Require(director.Snapshot.Candidates.Any(c=>c.Id=="CleanupProbe" && c.Eligible),"New published source was not eligible in the unchanged old root.");
    string guid=AssetDatabase.AssetPathToGUID(source);
    string moved=folder+"/Moved.dazstory";
    Require(string.IsNullOrEmpty(AssetDatabase.MoveAsset(source,moved)),"Could not move disposable source.");
    source=moved;
    GlobalStoryCardIndexBuilder.EnsureCurrent(true);
    Require(AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>(source).sourceIdentity==guid,"Source identity changed during move.");
    // A running linked snapshot does not mutate when publication changes.
    File.WriteAllText(source,"card CleanupProbeEdited\ntags [Warmup]\nprogress 10\n{\nwait 1s\n}\n");
    AssetDatabase.ImportAsset(source,ImportAssetOptions.ForceSynchronousImport);
    GlobalStoryCardIndexBuilder.EnsureCurrent(true);
    Require(StoryLinkedContent.Create(root).Program.cards.Any(c=>c.Id=="CleanupProbeEdited"),"Edited source was not refreshed.");
    Require(linked.Program.cards.Any(c=>c.Id=="CleanupProbe") && !linked.Program.cards.Any(c=>c.Id=="CleanupProbeEdited"),"Existing session population changed.");
    string before=Hash(GlobalStoryCardIndexBuilder.IndexPath);var stamp=File.GetLastWriteTimeUtc(GlobalStoryCardIndexBuilder.IndexPath);
    GlobalStoryCardIndexBuilder.EnsureCurrent(true);
    Require(before==Hash(GlobalStoryCardIndexBuilder.IndexPath) && stamp==File.GetLastWriteTimeUtc(GlobalStoryCardIndexBuilder.IndexPath),"Unchanged indexing rewrote the generated asset.");
    // Invalid owned binding is a publication error, recovered by fixing only its companion.
    string companion=Path.ChangeExtension(source,"bindings.asset");
    var content=ScriptableObject.CreateInstance<StoryContentBindings>();
    content.poses=new[]{new StoryPoseContent {id="Shared"}};
    AssetDatabase.CreateAsset(content,companion);
    File.WriteAllText(source,"card CleanupProbeEdited\ntags [Warmup]\nprogress 10\n{\nawait Lara.PersistentPose(Shared)\n}\n");
    AssetDatabase.ImportAsset(source,ImportAssetOptions.ForceSynchronousImport);
    index=GlobalStoryCardIndexBuilder.EnsureCurrent();
    Require(index.diagnostics.Length>0,"Invalid owned binding was silently accepted.");
    var poses=root.content.poses.Where(p=>p.value!=null).Take(2).ToArray();
    Require(poses.Length==2 && poses[0].value!=poses[1].value,"Need two distinct real pose assets for alias ownership.");
    content.poses[0].value=poses[0].value;EditorUtility.SetDirty(content);AssetDatabase.SaveAssetIfDirty(content);
    AssetDatabase.ImportAsset(source,ImportAssetOptions.ForceSynchronousImport);
    GlobalStoryCardIndexBuilder.EnsureCurrent(true);
    linked=StoryLinkedContent.Create(root);
    var use=linked.Program.bindings.Single(b=>b.source.unit==guid && b.symbol=="Shared");
    Require(linked.ContentFor(use.source).Pose("Shared").value==poses[0].value,"Published alias resolved to another source.");
    var oldContent=linked.ContentFor(use.source);
    content.poses[0].value=poses[1].value;EditorUtility.SetDirty(content);AssetDatabase.SaveAssetIfDirty(content);
    AssetDatabase.ImportAsset(source,ImportAssetOptions.ForceSynchronousImport);
    GlobalStoryCardIndexBuilder.EnsureCurrent(true);
    linked=StoryLinkedContent.Create(root);
    Require(linked.ContentFor(use.source).Pose("Shared").value==poses[1].value,"Binding edit was not incorporated.");
    string second=folder+"/Other.dazstory";
    var secondContent=ScriptableObject.CreateInstance<StoryContentBindings>();
    secondContent.poses=new[]{new StoryPoseContent {id="Shared",value=poses[0].value}};
    AssetDatabase.CreateAsset(secondContent,Path.ChangeExtension(second,"bindings.asset"));
    File.WriteAllText(second,"card CleanupProbeOther\ntags [Warmup]\nprogress 10\n{\nawait Lara.PersistentPose(Shared)\n}\n");
    AssetDatabase.ImportAsset(second,ImportAssetOptions.ForceSynchronousImport);
    GlobalStoryCardIndexBuilder.EnsureCurrent(true);linked=StoryLinkedContent.Create(root);
    var aliases=linked.Program.bindings.Where(b=>b.symbol=="Shared").ToArray();
    Require(aliases.Length==2 && aliases.Select(b=>linked.ContentFor(b.source).Pose("Shared").value).Distinct().Count()==2,"Two source aliases resolved to the same pose owner.");
    string absent=folder+"/AbsentLocation.dazstory";
    var absentContent=ScriptableObject.CreateInstance<StoryContentBindings>();
    absentContent.sceneSymbols=new[]{new StorySceneSymbol {id="CleanupProbeLocation",kind=StoryBindingKind.Location}};
    AssetDatabase.CreateAsset(absentContent,Path.ChangeExtension(absent,"bindings.asset"));
    File.WriteAllText(absent,"card CleanupProbeLocation\ntags [Warmup]\nprogress 10\n{\nawait Lara.LocomoteTo(CleanupProbeLocation)\n}\n");
    AssetDatabase.ImportAsset(absent,ImportAssetOptions.ForceSynchronousImport);
    GlobalStoryCardIndexBuilder.EnsureCurrent(true);linked=StoryLinkedContent.Create(root);
    var actual=GameSessionSceneSetup.InScene<StoryProgramSequence>(UnityEngine.SceneManagement.SceneManager.GetActiveScene()).Single().Bindings;
    var unavailable=linked.Program.cards.Single(c=>c.Id=="CleanupProbeLocation");
    Require(linked.ResourceIssue(unavailable,actual)?.Contains("CleanupProbeLocation")==true,"Missing scene resource was not diagnosed.");
    Require(linked.MandatoryDiagnostics(actual,"FirstContact").Length==0,"Unrelated global resource blocked the original scene.");
    director=new StoryDirector(linked.Program,i=>new StoryData(true),k=>new StoryData(0d),new StorySessionRandom(1),s=>{},c=>linked.ResourceIssue(c,actual));
    director.Select(new CardSelectionRequest {RequiredTags=new[]{"Warmup"},Phase="Warmup"});
    Require(director.Snapshot.Candidates.Any(c=>c.Id=="CleanupProbeLocation" && !c.Eligible) && director.Snapshot.Candidates.Any(c=>c.Eligible),"Missing resource did not isolate its card.");
    var preview=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
    try {
     var go=new GameObject("Disposable resource binding");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,preview);
     var complete=go.AddComponent<StorySceneBindings>();
     complete.lara=actual.lara;complete.laraFocus=actual.laraFocus;complete.playerBody=actual.playerBody;
     complete.locations=actual.locations.Concat(new[]{new StoryLocationBinding {id="CleanupProbeLocation",placement=go.transform}}).ToArray();
     Require(linked.ResourceIssue(unavailable,complete)==null,"The same published card stayed unavailable when its required location existed.");
    } finally {UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);}
    var dependencies=AssetDatabase.GetDependencies(GlobalStoryCardIndexBuilder.IndexPath,true);
    Require(dependencies.Contains(source) && dependencies.Contains(companion) && dependencies.Contains(AssetDatabase.GetAssetPath(poses[1].value)),"Generated runtime root does not retain source/bindings/pose dependencies.");
   } finally {
    AssetDatabase.DeleteAsset(folder);
    GlobalStoryCardIndexBuilder.EnsureCurrent(true);
   }
   Require(!StoryLinkedContent.Create(AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>(StoryFoundationBuilder.SourcePath)).Program.cards.Any(c=>c.Id.StartsWith("CleanupProbe",StringComparison.Ordinal)),"Deleted source remains in pool.");
   Require(GlobalStoryCardIndexBuilder.EnsureCurrent(true).fingerprint==originalFingerprint,"Removing probe failed to restore publication population.");
   Require(protectedPaths.Select(Hash).SequenceEqual(hashes),"Probe changed old scene, source, companion bindings or public catalog.");
   Directory.CreateDirectory("TestOutput/CleanupTask1");
   File.WriteAllText("TestOutput/CleanupTask1/global-card-index.json",JsonUtility.ToJson(new Report {assertions=assertions,passed=true,protectedPaths=protectedPaths,hashes=hashes},true));
   Debug.Log("Global card index Edit Mode checks passed: "+assertions+" assertions; no scene playback.");
  }
  [Serializable] sealed class Report {public bool passed;public int assertions;public string[] protectedPaths,hashes;}
 }
}
