using System;
using System.IO;
using System.Linq;
using DazPose.Game;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DazPose.Editor {
 public static class DazPoseProductionMenu {
  [MenuItem("DazPose/Scene/New Performance Scene...")] static void NewScene() {
   if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
   string path=EditorUtility.SaveFilePanelInProject("New Performance Scene","NewPerformance","unity","Choose a new scene path","Assets/Scenes");
   if(!string.IsNullOrEmpty(path)) GameSessionSceneSetup.CopyEnvironment(path);
  }
  [MenuItem("DazPose/Scene/Initialize Game Session")] public static void Initialize() => GameSessionSceneSetup.Initialize();
  [MenuItem("DazPose/Scene/Add Lara Performer")] static void AddLara()=>GameSessionSceneSetup.AddLara();
  [MenuItem("DazPose/Scene/Add Partner")] static void AddPartner()=>GameSessionSceneSetup.AddPartner();
  [MenuItem("DazPose/Content/Advanced/New Sequence Script...")] static void NewScript() {
   string path=EditorUtility.SaveFilePanelInProject("New Sequence Script","MyPerformance","cs","Create an authoring script");
   if(string.IsNullOrEmpty(path)) return;
   string name=Path.GetFileNameWithoutExtension(path);
   if(name.Any(c=>!char.IsLetterOrDigit(c) && c!='_') || !char.IsLetter(name[0])) throw new InvalidOperationException("Use a C# class name.");
   File.WriteAllText(path,"using UnityEngine;\nusing DazPose.Performer;\n\npublic sealed class "+name+" : PerformanceSequence {\n [SerializeField] PerformerPose pose;\n public override async Awaitable ExecuteAsync(PerformanceRun run) {\n  await run.Performer.PoseAsync(pose);\n  await run.Delay(1f);\n }\n}\n");
   AssetDatabase.ImportAsset(path);
  }
  [MenuItem("DazPose/Game/Open Main Menu")] static void OpenMenu() {
   if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene("Assets/Production/Scenes/MainMenu.unity");
  }
  [MenuItem("DazPose/Game/Add Current Scene to Scene List")] public static void AddCurrentScene() {
   var scene=SceneManager.GetActiveScene();
   var hosts=GameSessionSceneSetup.InScene<PerformanceHost>(scene);
   if(hosts.Length!=1) throw new InvalidOperationException("Initialize one saved session first.");
   var b=hosts[0].Bindings;
   var catalog=AssetDatabase.LoadAssetAtPath<SceneCatalog>(ProductionApplicationSetup.CatalogPath);
   Undo.RecordObject(catalog,"Add authored scene");
   var entry=catalog.entries.FirstOrDefault(e=>e.scenePath==scene.path);
   if(entry==null) {
    var list=catalog.entries.ToList();
    string id=b.SceneId;
    if(string.IsNullOrEmpty(id) || list.Any(e=>e.id==id)) { Undo.RecordObject(b,"Assign distinct scene ID"); id=Guid.NewGuid().ToString("N"); b.ConfigureIdentity(id,b.ActorId); }
    entry=new SceneCatalog.Entry { id=id,title=scene.name,scenePath=scene.path }; list.Add(entry); catalog.entries=list.ToArray();
   } else b.ConfigureIdentity(entry.id,b.ActorId);
   EditorUtility.SetDirty(b); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
  }
  [MenuItem("DazPose/Game/Build Windows Player...")] static void BuildPlayer()=>GamePlayerBuild.BuildInteractive();
  [MenuItem("DazPose/Help/Content Authoring API")] static void Api()=>OpenRoot("CONTENT_AUTHORING_API.md");
  [MenuItem("DazPose/Help/Story Authoring Guide")] static void StoryGuide()=>OpenRoot("docs/story-runtime/DAZSTORY_AUTHORING.md");
  static void OpenRoot(string file) => EditorUtility.OpenWithDefaultApp(Path.GetFullPath(Path.Combine(Application.dataPath,"../../..",file)));
 }
}