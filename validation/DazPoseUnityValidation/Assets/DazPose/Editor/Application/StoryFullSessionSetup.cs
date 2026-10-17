using System;
using System.IO;
using System.Linq;
using DazPose.Motion;
using DazPose.Game;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DazPose.Editor {
 public static class StoryFullSessionSetup {
  [MenuItem("DazPose/Story/Prepare Full First Contact Session")]
  public static void Prepare() {
   StoryFoundationBuilder.Prepare();
   var sequence=GameSessionSceneSetup.InScene<StoryProgramSequence>(UnityEngine.SceneManagement.SceneManager.GetActiveScene()).Single();var content=sequence.CompiledSource.content;
   foreach(var row in content.motion)if(row.value==null)row.value=EnsureMotion(row.id);
   sequence.ConfigurePreview("");EditorUtility.SetDirty(sequence);EditorUtility.SetDirty(content);
   var catalog=AssetDatabase.LoadAssetAtPath<SceneCatalog>(ProductionApplicationSetup.CatalogPath);
   var entry=catalog.entries.Single(e=>e.scenePath==StorySceneBuilder.ScenePath);
   if(entry.title=="First Contact · session setup" || entry.title=="FirstContactD1D5")entry.title="First Contact";
   if(entry.description=="Meet Lara and agree on interests, equipment, and clothing.")entry.description="Meet Lara and play the complete authored session.";
   EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
   AssetDatabase.ImportAsset(StoryFoundationBuilder.SourcePath,ImportAssetOptions.ForceUpdate);
   var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   string[] errors=content.Diagnostics(sequence.CompiledSource.program,sequence.Bindings);
   if(errors.Length>0)throw new InvalidOperationException(string.Join("\n",errors));
   CardCatalog.Validate(sequence.CompiledSource.program,StoryCatalogs.Load().Equipment.entries.Select(e=>e.id));
   Debug.Log("FIRST_CONTACT_FULL_SESSION_CONFIGURED: full saved sequence; six imported poses; movable MainPosition1 and Bedside; normal imported engineering motion assets; no preview cutoff.",sequence);
  }
  static FunscriptMotionProgram EnsureMotion(string id) {
   if(id!="Finisher1" && id!="FinishLoop")throw new InvalidOperationException("Assign authored motion "+id);
   const string folder="Assets/Production/Story/Motion/Engineering";Directory.CreateDirectory(folder);
   string path=folder+"/"+id+"_Engineering.funscript";
   if(!File.Exists(path)) {
    int duration=id=="Finisher1" ? 18000 : 4000;
    var actions=Enumerable.Range(0,duration/1000+1).Select(i=>"{\"at\":"+i*1000+",\"pos\":"+(i%2==0 ? 30 : 70)+"}");
    File.WriteAllText(path,"{\"version\":\"1.0\",\"inverted\":false,\"range\":100,\"actions\":["+string.Join(",",actions)+"]}");
    File.WriteAllText(folder+"/README.txt","Temporary engineering motion assets for First Contact. Finisher1 lasts 18 seconds; FinishLoop lasts 4 seconds. Imported through the normal FunscriptImporter. Replace with final authored programs in FirstContact.bindings.asset.");
   }
   AssetDatabase.Refresh();AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
   return AssetDatabase.LoadAssetAtPath<FunscriptMotionProgram>(path) ?? throw new InvalidOperationException("Normal funscript importer failed for "+path);
  }
 }
}
