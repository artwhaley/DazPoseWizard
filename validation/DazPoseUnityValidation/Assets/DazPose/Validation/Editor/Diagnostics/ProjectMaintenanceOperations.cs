using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor {
 // Explicit, expiring local requests. Opening scenes never triggers a migration.
 [InitializeOnLoad]
 public static class ProjectMaintenanceOperations {
  [Serializable] public sealed class Request {
   public string id, operation, createdUtc;
   public string[] paths = Array.Empty<string>();
   public string[] destinations = Array.Empty<string>();
   public string[] expectedGuids = Array.Empty<string>();
  }
  [Serializable] public sealed class Dependency { public string path, guid; public string[] dependencies; }
  [Serializable] public sealed class LoadedScene { public string path; public bool dirty; }
  [Serializable] public sealed class AssemblyInfo { public string name; public string[] references; }
  [Serializable] public sealed class Report {
   public string id, operation, error, unityVersion;
   public bool passed, playing, compiling;
   public LoadedScene[] loadedScenes;
   public Dependency[] assets;
   public AssemblyInfo[] assemblies;
   public string[] buildScenes;
  }
  static double nextCheck;
  static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.dazposewizard/cleanup-task1/editor"));
  static ProjectMaintenanceOperations() { EditorApplication.update += CheckRequest; }
  static void CheckRequest() {
   if(EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
   nextCheck = EditorApplication.timeSinceStartup + 1;
   string path = Path.Combine(Folder, "request.json");
   if(!File.Exists(path)) return;
   try {
    var request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
    if(!Guid.TryParse(request.id, out _) || !DateTime.TryParse(request.createdUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var created)
     || Math.Abs((DateTime.UtcNow-created.ToUniversalTime()).TotalMinutes)>10)
     throw new InvalidDataException("Maintenance requests need a unique ID and must be less than ten minutes old.");
    string consumed=Path.Combine(Folder,request.id+".request.json");
    if(File.Exists(consumed)) throw new InvalidDataException("Maintenance request was already consumed.");
    File.Move(path,consumed);
    var report = Execute(request);
    File.WriteAllText(Path.Combine(Folder,request.id+".result.json"),JsonUtility.ToJson(report,true));
   } catch(Exception error) {
    File.WriteAllText(Path.Combine(Folder,"request-error.txt"),error.ToString());
    if(File.Exists(path)) File.Move(path,Path.Combine(Folder,"rejected-"+Guid.NewGuid().ToString("N")+".json"));
    Debug.LogException(error);
   }
  }
  public static Report Execute(Request request) {
   var report=new Report {id=request.id,operation=request.operation,unityVersion=Application.unityVersion,
    playing=EditorApplication.isPlayingOrWillChangePlaymode,compiling=EditorApplication.isCompiling};
   report.loadedScenes=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i))
    .Select(s=>new LoadedScene {path=s.path,dirty=s.isDirty}).ToArray();
   try {
    if(report.playing || report.compiling) throw new InvalidOperationException("Project maintenance requires idle Edit Mode.");
    if(request.operation=="MoveAssets") MoveAssets(request,report.loadedScenes);
    else if(request.operation=="MigrateClothingStartup") MigrateClothingStartup();
    else if(request.operation=="PublishFirstContactCards") GlobalStoryCardIndexBuilder.PublishFirstContactCards();
    else if(request.operation=="ValidateRetainedExamples") AuthoringContentComposer.ValidateSavedContent();
    else if(request.operation=="RetireVendorDemoScenes") RetireVendorDemoScenes();
    else if(request.operation=="RetireAbandonedDrafts") RetireAbandonedDrafts();
    else if(request.operation=="RetireRedundantAuthoringScenes") RetireRedundantAuthoringScenes();
    else if(request.operation=="RetireInactiveFirstContactJson") RetireInactiveFirstContactJson();
    else if(request.operation=="RepairFirstContact") FirstContactRepair.Prepare();
    else if(request.operation=="SynchronizePublicScenes") GamePlayerBuild.SynchronizePublicSceneList();
    else if(request.operation=="ReindexCards") GlobalStoryCardIndexBuilder.EnsureCurrent(true);
    else if(request.operation=="MigrateReviewMetadata") DazPose.UnityValidation.WardrobeReviewMetadata.MigrateLegacyReports();
    else if(request.operation=="CheckCleanupSafety" || request.operation=="CheckImportRetry") {
     string name=request.operation=="CheckCleanupSafety" ? "CleanupSafetyChecks" : "ImportRetryChecks";
     Type.GetType((request.operation=="CheckImportRetry" ? "DazPose.UnityValidation." : "DazPose.Editor.")+name+", DazPose.Validation.Editor",true).GetMethod("Run").Invoke(null,null);
    }
    else if(request.operation=="CheckImportPipeline") ImportPipelineChecks.Run();
    else if(request.operation=="CheckRetainedReferences") CleanupReferenceChecks.Run();
    else if(request.operation=="CheckCleanupFinal") Type.GetType("DazPose.Editor.CleanupFinalChecks, DazPose.Validation.Editor",true).GetMethod("Run").Invoke(null,null);
    else if(request.operation=="CleanExample") Type.GetType("DazPose.Editor.ExampleSceneCleanup, DazPose.Validation.Editor",true).GetMethod("Run").Invoke(null,null);
    else if(request.operation=="CheckGlobalCards") {
     var type=Type.GetType("DazPose.Editor.GlobalCardIndexChecks, DazPose.Validation.Editor",true);
     type.GetMethod("Run").Invoke(null,null);
    }
    else if(request.operation=="Refresh") AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    else if(request.operation!="Inspect") throw new InvalidOperationException("Unknown maintenance operation: "+request.operation);
    report.assets=(request.paths??Array.Empty<string>()).Select(p=> {
     File.WriteAllText(Path.Combine(Folder,"progress.txt"),request.id+" "+p);
     return new Dependency {path=p,guid=AssetDatabase.AssetPathToGUID(p),dependencies=AssetDatabase.GetDependencies(p,true).OrderBy(x=>x,StringComparer.Ordinal).ToArray()};
    }).ToArray();
    report.assemblies=CompilationPipeline.GetAssemblies(AssembliesType.Editor).Select(a=>new AssemblyInfo {
     name=a.name,references=a.assemblyReferences.Select(r=>r.name).OrderBy(x=>x,StringComparer.Ordinal).ToArray()
    }).ToArray();
    report.buildScenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
    report.passed=true;
   } catch(Exception error) {report.error=error.ToString();}
   return report;
  }
  static void RetireVendorDemoScenes() {
   var paths=new[]{"Assets/Crazy Minnow Studio/Examples/Scenes/SALSA-basic3D-boxhead.unity","Assets/FemaleMovementAnimsetPro/Scenes/FemaleDemo.unity","Assets/FemaleMovementAnimsetPro/Scenes/FemaleTopDownDemo.unity","Assets/FemaleMovementAnimsetPro/Standard Assets/Effects/TessellationShaders/Scenes/TessellationSample.unity","Assets/INab Studio/Demo Assets/StarterAssets/Environment/Art/Demo Scene.unity","Assets/KAWAII_ANIMATIOMS_100/Demo/KAWAII_ANIMATIOMS_100.unity"}.Where(File.Exists).ToArray();
   if(paths.Length==0)return;
   if(paths.Any(p=>SceneManager.GetSceneByPath(p).isLoaded))throw new InvalidOperationException("A demo scene is open; no retirement performed.");
   var all=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal) && !paths.Contains(p) && !AssetDatabase.IsValidFolder(p)).ToArray();
   var incoming=AssetDatabase.GetDependencies(all,false).Intersect(paths).ToArray();
   if(incoming.Length>0)throw new InvalidOperationException("Demo scenes still have incoming Unity references: "+string.Join(", ",incoming));
   if(EditorBuildSettings.scenes.Any(s=>paths.Contains(s.path)))throw new InvalidOperationException("A demo scene remains in build settings.");
   foreach(string path in paths)if(!AssetDatabase.DeleteAsset(path))throw new IOException("Could not retire demo scene "+path);
  }
  static void RetireAbandonedDrafts() {
   var roots=new[]{"Assets/TestData/WardrobeRuntime/DraftAttempts/02aa66ebd298461cb41043fd44d637e1","Assets/TestData/WardrobeRuntime/DraftAttempts/94c58ce304554ea4bef67c908ad40e6c"}.Where(AssetDatabase.IsValidFolder).ToArray();
   if(roots.Length==0)return;
   bool Within(string path)=>roots.Any(root=>path==root || path.StartsWith(root+"/",StringComparison.Ordinal));
   var all=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)).ToArray();
   var candidates=all.Where(Within).ToArray();
   var remaining=all.Where(p=>!Within(p) && !AssetDatabase.IsValidFolder(p)).ToArray();
   if(AssetDatabase.GetDependencies(remaining,false).Intersect(candidates).Any())throw new InvalidOperationException("An abandoned draft gained a live incoming Unity reference; retirement aborted.");
   foreach(var root in roots)if(!AssetDatabase.DeleteAsset(root))throw new IOException("Could not retire verified abandoned draft "+root);
  }
  static void RetireRedundantAuthoringScenes() {
   var paths=new[]{"Assets/Production/Scenes/AuthoringWelcome.unity","Assets/Production/Scenes/AuthoringCompanion.unity"}.Where(File.Exists).ToArray();
   if(paths.Length==0)return;
   if(paths.Any(p=>SceneManager.GetSceneByPath(p).isLoaded))throw new InvalidOperationException("Close the obsolete authoring scenes before retiring them.");
   var all=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal) && !paths.Contains(p) && !AssetDatabase.IsValidFolder(p)).ToArray();
   if(AssetDatabase.GetDependencies(all,false).Intersect(paths).Any())throw new InvalidOperationException("An authoring scene still has a live Unity consumer.");
   var catalog=AssetDatabase.LoadAssetAtPath<DazPose.Game.SceneCatalog>(ProductionApplicationSetup.CatalogPath);
   if(catalog.entries.Any(e=>paths.Contains(e.scenePath)) || EditorBuildSettings.scenes.Any(e=>paths.Contains(e.path)))
    throw new InvalidOperationException("An obsolete example remains in navigation/build settings.");
   foreach(var path in paths)if(!AssetDatabase.DeleteAsset(path))throw new IOException("Could not retire "+path);
  }
  static void RetireInactiveFirstContactJson() {
   const string path="Assets/Production/Story/Programs/FirstContactD1D5.story.json";
   if(!File.Exists(path))return;
   if(AssetDatabase.AssetPathToGUID(path)!="0d86ef1eb63643a3bce7cde7ddef5044")throw new InvalidOperationException("Legacy JSON identity changed; retirement aborted.");
   var all=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal) && p!=path && !AssetDatabase.IsValidFolder(p)).ToArray();
   if(AssetDatabase.GetDependencies(all,false).Contains(path))throw new InvalidOperationException("The legacy JSON still has an incoming Unity reference.");
   var sequence=GameSessionSceneSetup.InScene<DazPose.Story.StoryProgramSequence>(SceneManager.GetSceneByPath(StorySceneBuilder.ScenePath)).Single();
   if(sequence.Program!=null || sequence.CompiledSource==null || !sequence.CompiledSource.IsCompiled)throw new InvalidOperationException("The saved scene is not using valid compiled source.");
   string archive=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/story-runtime/history/FirstContactD1D5.story.json"));
   Directory.CreateDirectory(Path.GetDirectoryName(archive));
   byte[] original=File.ReadAllBytes(path);
   if(File.Exists(archive) && !File.ReadAllBytes(archive).SequenceEqual(original))throw new InvalidOperationException("Historical sample already differs; no overwrite.");
   if(!File.Exists(archive))File.WriteAllBytes(archive,original);
   if(!AssetDatabase.DeleteAsset(path))throw new IOException("Could not retire inactive legacy JSON asset.");
  }
  static void MigrateClothingStartup() {
   const string path=DazPose.UnityValidation.ClothingSetupBuilder.ScenePath;
   var scene=SceneManager.GetSceneByPath(path);
   bool opened=!scene.IsValid() || !scene.isLoaded;
   if(!opened && scene.isDirty) throw new InvalidOperationException("Save or close Clothing Setup before its explicit startup migration.");
   if(opened) scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
   try {
    if(DazPose.UnityValidation.ClothingSetupBuilder.MigrateSavedStartup(scene)
     && !UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Clothing Setup startup migration.");
   } finally {if(opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
  }
  static void EnsureFolder(string path) {
   if(AssetDatabase.IsValidFolder(path)) return;
   string parent=Path.GetDirectoryName(path).Replace('\\','/');
   EnsureFolder(parent);
   string guid=AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
   if(string.IsNullOrEmpty(guid)) throw new IOException("Cannot create asset folder: "+path);
  }
  static void MoveAssets(Request request,LoadedScene[] loaded) {
   if(!File.Exists(Path.Combine(Folder,"../recovery-manifest.json"))) throw new InvalidOperationException("Capture the recovery manifest before asset moves.");
   if(request.paths.Length!=request.destinations.Length || request.paths.Length!=request.expectedGuids.Length)
    throw new InvalidDataException("Every asset move requires its destination and original GUID.");
   for(int i=0;i<request.paths.Length;i++) {
    string source=request.paths[i],destination=request.destinations[i];
    if(!source.StartsWith("Assets/",StringComparison.Ordinal) || !destination.StartsWith("Assets/",StringComparison.Ordinal)
     || source.Contains("..") || destination.Contains("..") || string.IsNullOrEmpty(request.expectedGuids[i])
     || AssetDatabase.AssetPathToGUID(source)!=request.expectedGuids[i])
     throw new InvalidDataException("Invalid source or changed GUID: "+source);
    if(loaded.Any(scene=>scene.path==source && scene.dirty)) throw new InvalidOperationException("Save or close your dirty scene before moving it: "+source);
    EnsureFolder(Path.GetDirectoryName(destination).Replace('\\','/'));
    string error=AssetDatabase.ValidateMoveAsset(source,destination);
    if(!string.IsNullOrEmpty(error)) throw new IOException(error);
   }
   int moved=0;
   Exception failure=null;
   AssetDatabase.StartAssetEditing();
   try {
    for(int i=0;i<request.paths.Length;i++) {
     string error=AssetDatabase.MoveAsset(request.paths[i],request.destinations[i]);
     if(!string.IsNullOrEmpty(error)) throw new IOException(error);
     moved++;
    }
   } catch(Exception error) {failure=error;}
   finally {AssetDatabase.StopAssetEditing();}
   // GUID lookup is reliable only after deferred AssetDatabase edits have been committed.
   if(failure==null) {
    for(int i=0;i<moved;i++)
     if(AssetDatabase.AssetPathToGUID(request.destinations[i])!=request.expectedGuids[i])
      failure=new InvalidDataException("GUID changed while moving "+request.paths[i]);
   }
   if(failure!=null) {
    for(int i=moved-1;i>=0;i--) {
     string rollback=AssetDatabase.MoveAsset(request.destinations[i],request.paths[i]);
     if(!string.IsNullOrEmpty(rollback)) throw new AggregateException(failure,new IOException("Rollback failed: "+rollback));
    }
    throw failure;
   }
   var catalog=AssetDatabase.LoadAssetAtPath<DazPose.Game.SceneCatalog>(ProductionApplicationSetup.CatalogPath);bool catalogChanged=false;
   if(catalog!=null) {
    for(int i=0;i<moved;i++) {
     if(catalog.mainMenuPath==request.paths[i]) {catalog.mainMenuPath=request.destinations[i];catalogChanged=true;}
     foreach(var entry in catalog.entries)if(entry.scenePath==request.paths[i]) {entry.scenePath=request.destinations[i];catalogChanged=true;}
    }
    if(catalogChanged){EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);}
   }
   var build=EditorBuildSettings.scenes;bool buildChanged=false;
   foreach(var entry in build)for(int i=0;i<moved;i++)if(entry.path==request.paths[i]){entry.path=request.destinations[i];buildChanged=true;}
   if(buildChanged)EditorBuildSettings.scenes=build;
  }

  public static void InspectProject() {
   Directory.CreateDirectory(Folder);
   var request=new Request {id=Guid.NewGuid().ToString("N"),operation="Inspect",createdUtc=DateTime.UtcNow.ToString("O"),
    paths=AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath).ToArray()};
   var report=Execute(request);File.WriteAllText(Path.Combine(Folder,"inventory.json"),JsonUtility.ToJson(report,true));
   if(!report.passed) throw new InvalidOperationException(report.error);
   Debug.Log("Project dependency inventory written. No scenes or assets were changed.");
  }
 }
}
