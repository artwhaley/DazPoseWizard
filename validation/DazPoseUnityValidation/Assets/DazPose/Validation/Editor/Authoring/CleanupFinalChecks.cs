using System;
using System.IO;
using System.Linq;
using DazPose.Game;
using DazPose.Performer;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DazPose.Editor {
 public static class CleanupFinalChecks {
  [Serializable] sealed class Report {public bool passed;public int assertions,cards,dialogue,poses,marks;public string scene,sceneId,actorId;public string[] buildScenes,dependencies;}
  public static void Run() {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit Mode only.");
   int assertions=0;
   void Require(bool condition,string message) {assertions++;if(!condition)throw new InvalidOperationException(message);}
   var catalog=AssetDatabase.LoadAssetAtPath<SceneCatalog>(ProductionApplicationSetup.CatalogPath);
   var scenes=GamePlayerBuild.ComputeScenes(catalog);
   Require(scenes.SequenceEqual(new[]{"Assets/Production/Scenes/MainMenu.unity",StorySceneBuilder.ScenePath}),"Public catalog includes an example/fixture or misses First Contact.");
   Require(EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).SequenceEqual(scenes),"Build settings disagree with catalog.");
   Require(catalog.entries.Single().id=="c3bbf40d9a2949ffac3e068b552ed71c","First Contact catalog identity changed.");
   Require(scenes.All(File.Exists),"Public scene path is missing.");
   var active=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(StorySceneBuilder.ScenePath);bool opened=!scene.IsValid() || !scene.isLoaded;
   if(opened)scene=EditorSceneManager.OpenScene(StorySceneBuilder.ScenePath,OpenSceneMode.Additive);
   try {
    var sequence=GameSessionSceneSetup.InScene<StoryProgramSequence>(scene).Single();var host=sequence.GetComponent<PerformanceHost>();
    var linked=StoryLinkedContent.Create(sequence.CompiledSource);
    Require(host.Sequence==sequence && host.PlayOnStart,"Saved host does not start the saved source.");
    Require(string.IsNullOrEmpty(sequence.StopBeforePhase),"Preview cutoff remains.");
    Require(sequence.Program==null && sequence.CompiledSource.IsCompiled,"Saved scene is not using valid compiled source.");
    Require(linked.Program.cards.Length==18 && linked.Program.cards.Count(c=>c.Selectable)==17,"Original card population changed.");
    Require(linked.Program.dialogue.Length==19,"Fixed dialogue population changed.");
    Require(linked.Diagnostics(linked.Program,sequence.Bindings).Length==0,"Full First Contact content has missing bindings.");
    Require(sequence.CompiledSource.content.poses.Length==6 && sequence.CompiledSource.content.poses.All(p=>p.value!=null && p.value.Clip!=null),"Six imported pose assignments are incomplete.");
    Require(sequence.Bindings.locations.Length==7 && sequence.Bindings.locations.All(l=>l.placement!=null),"Seven movable semantic locations are incomplete.");
    Require(sequence.Bindings.playerBody==host.Bindings.Player.BodyPresence && sequence.Bindings.playerBody.GetComponent<DazPose.UnityValidation.Partner.PartnerContactTarget>()!=null,"Prepared male body/contact binding changed.");
    Require(GameSessionSceneSetup.InScene<Transform>(scene).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"Missing script in saved First Contact.");
    var index=GlobalStoryCardIndexBuilder.EnsureCurrent(true);
    var dependencies=AssetDatabase.GetDependencies(GlobalStoryCardIndexBuilder.IndexPath,true);
    Require(index.sources.All(s=>dependencies.Contains(AssetDatabase.GetAssetPath(s)) && dependencies.Contains(AssetDatabase.GetAssetPath(s.content))),"Generated runtime index lost card code/content.");
    Require(!scene.isDirty,"Structural check dirtied First Contact.");
    Directory.CreateDirectory("TestOutput/CleanupTask1");
    File.WriteAllText("TestOutput/CleanupTask1/final-structure.json",JsonUtility.ToJson(new Report {passed=true,assertions=assertions,cards=linked.Program.cards.Length,dialogue=linked.Program.dialogue.Length,poses=sequence.CompiledSource.content.poses.Length,marks=sequence.Bindings.locations.Length,scene=scene.path,sceneId=host.Bindings.SceneId,actorId=host.Bindings.ActorId,buildScenes=scenes,dependencies=dependencies},true));
    Debug.Log("Cleanup saved-scene structural checks passed: "+assertions+" assertions. User Play/visual acceptance pending.");
   } finally {if(opened)EditorSceneManager.CloseScene(scene,true);if(active.IsValid() && active.isLoaded)SceneManager.SetActiveScene(active);}
  }
 }
}
