using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Game;
using DazPose.Motion;
using DazPose.Performer;
using DazPose.Player;
using DazPose.Toys;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DazPose.Editor {
 public static class GameSessionSceneSetup {
  public const string LaraPath="Assets/Production/Performers/Lara.prefab";
  public const string PartnerPath="Assets/Production/Partners/Partner.prefab";
  public const string VoidPath="Assets/Scenes/FirstPerformanceVoid.unity";
  public static T[] InScene<T>(Scene scene) where T:Component =>
   scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
  static T SelectOne<T>(T assigned, T[] candidates, List<string> missing,string field) where T:Component {
   if(assigned!=null) return assigned;
   var selected=Selection.activeGameObject!=null ? Selection.activeGameObject.GetComponentInParent<T>() : null;
   if(selected!=null && candidates.Contains(selected)) return selected;
   if(candidates.Length==1) return candidates[0];
   if(candidates.Length>1) missing.Add(field+" (multiple candidates)");
   return null;
  }
  public static PerformanceHost Initialize(bool save=true) {
   if(Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before initializing a saved scene.");
   var scene=SceneManager.GetActiveScene();
   if(string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Save the scene first.");
   ProductionApplicationSetup.EnsureAssets();
   Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Initialize Game Session");
   var missing=new List<string>();
   var hosts=InScene<PerformanceHost>(scene);
   if(hosts.Length>1) { Debug.LogWarning("Needs author assignment: retain one PerformanceHost in this scene."); return null; }
   var host=hosts.SingleOrDefault();
   if(host==null) { var root=new GameObject("Game Session"); Undo.RegisterCreatedObjectUndo(root,"Create game session"); host=Undo.AddComponent<PerformanceHost>(root); }
   Undo.RecordObject(host,"Configure game session");
   var b=host.Bindings;
   if(b==null) b=host.GetComponent<PerformanceSceneBindings>();
   if(b==null) b=Undo.AddComponent<PerformanceSceneBindings>(host.gameObject);
   Undo.RecordObject(b,"Bind game session");
   var actors=InScene<SuccubusPerformer>(scene);
   var actor=SelectOne(b.Performer,actors,missing,"Bindings.performer");
   if(actor==null && actors.Length==0) actor=AddLara();
   var player=SelectOne(b.Player,InScene<PlayerController>(scene),missing,"Bindings.player");
   if(player==null && InScene<PlayerController>(scene).Length==0) {
    var cameras=InScene<Camera>(scene).Where(c=>c.enabled).ToArray();
    var camera=SelectOne<Camera>(null,cameras,missing,"Player camera");
    if(camera!=null) {
     var view=camera.GetComponent<PlayerView>() ?? Undo.AddComponent<PlayerView>(camera.gameObject);
     player=camera.GetComponent<PlayerController>() ?? Undo.AddComponent<PlayerController>(camera.gameObject);
     Undo.RecordObject(view,"Bind authored camera");
     player.Configure(camera.transform,camera.transform,camera);
    } else if(cameras.Length==0) missing.Add("Player camera (none in saved scene)");
   }
   var clocks=InScene<MotionDriver>(scene);
   var clock=SelectOne(b.Playback!=null ? b.Playback : actor!=null ? actor.MotionSource : null,clocks,missing,"Bindings.playback");
   if(clock==null && clocks.Length==0) { var go=new GameObject("Performance Clock"); Undo.RegisterCreatedObjectUndo(go,"Create clock"); clock=Undo.AddComponent<MotionDriver>(go); }
   b.Configure(actor,player,clock,null,PerformerCapability.None);
   var catalog=AssetDatabase.LoadAssetAtPath<SceneCatalog>(ProductionApplicationSetup.CatalogPath);
   string id=b.SceneId;
   var listed=catalog.entries.FirstOrDefault(e=>e.scenePath==scene.path);
   if(listed!=null) id=listed.id;
   else if(string.IsNullOrEmpty(id) || catalog.entries.Any(e=>e.id==id && e.scenePath!=scene.path)) id=Guid.NewGuid().ToString("N");
   string actorId=b.ActorId;
   if(actor!=null) {
    var identity=actor.GetComponent<SceneActorIdentity>();
    if(identity==null) { identity=Undo.AddComponent<SceneActorIdentity>(actor.gameObject); identity.Configure(string.IsNullOrEmpty(actorId) ? Guid.NewGuid().ToString("N") : actorId); }
    if(string.IsNullOrEmpty(identity.ActorId)) { Undo.RecordObject(identity,"Assign actor identity"); identity.Configure(Guid.NewGuid().ToString("N")); }
    actorId=identity.ActorId;
    if(clock!=null && actor.MotionSource!=clock) { Undo.RecordObject(actor,"Bind shared clock"); actor.ConfigureMotionSource(clock); }
    EditorUtility.SetDirty(identity);
   }
   b.ConfigureIdentity(id,actorId);
   if(b.ContactTarget==null) {
    var contacts=InScene<MonoBehaviour>(scene).Where(c=>c is DazPose.Performer.HandGrip.IGripTarget).ToArray();
    if(contacts.Length==1) b.ConfigureContactTarget(contacts[0]);
   }
   var sequence=host.Sequence;
   if(sequence==null) {
    var welcome=host.GetComponent<WelcomePerformance>() ?? Undo.AddComponent<WelcomePerformance>(host.gameObject);
    Undo.RecordObject(welcome,"Assign welcome pose"); welcome.Configure(actor!=null ? actor.InitialPose : null);
    sequence=welcome;
   }
   host.Configure(b,sequence);
   if(actor==null) missing.Add("Bindings.performer");
   if(player==null) missing.Add("Bindings.player");
   if(clock==null) missing.Add("Bindings.playback");
   if(sequence is WelcomePerformance w && w.Pose==null) missing.Add("WelcomePerformance.pose (Lara InitialPose)");
   if(!scene.GetRootGameObjects().Any(r=>r.name=="Authoring Marks")) {
    var marks=new GameObject("Authoring Marks"); Undo.RegisterCreatedObjectUndo(marks,"Create marks");
   }
   MigrateSceneToys(scene,missing);
   if(AssetDatabase.LoadAssetAtPath<GameObject>(GameUiAssetBuilder.ControlsPath)!=null) GameUiAssetBuilder.Install(host);
   EditorUtility.SetDirty(b); EditorUtility.SetDirty(host); EditorUtility.SetDirty(sequence);
   EditorSceneManager.MarkSceneDirty(scene);
   if(save) EditorSceneManager.SaveScene(scene);
   Undo.CollapseUndoOperations(group);
   if(missing.Count>0) { Selection.activeObject=host; Debug.LogWarning("Needs author assignment: "+string.Join("; ",missing),host); }
   else Debug.Log("Ready to Play: saved "+scene.path,host);
   return host;
  }
  static void MigrateSceneToys(Scene scene,List<string> missing) {
   var services=InScene<ToyControlService>(scene);
   if(services.Length==0) return;
   var custom=services.Select(s=>s.ServerAddress).Where(s=>s!=ToyControlService.DefaultServerAddress && !string.IsNullOrEmpty(s)).Distinct().ToArray();
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ProductionApplicationSetup.ApplicationPath);
   var current=prefab.GetComponent<ToyControlService>().ServerAddress;
   var pauses=services.Select(s=>s.PauseNeutralPosition).Where(v=>v!=.5f).Distinct().ToArray();
   float currentPause=prefab.GetComponent<ToyControlService>().PauseNeutralPosition;
   if(pauses.Length>1 || pauses.Length==1 && currentPause!=.5f && currentPause!=pauses[0]) {
    missing.Add("Application toy pause position (conflicting scene settings; old services preserved)"); return;
   }
   if(custom.Length>1 || custom.Length==1 && current!=ToyControlService.DefaultServerAddress && current!=custom[0]) {
    missing.Add("Application Intiface endpoint (conflicting scene settings; old services preserved)"); return;
   }
   if(custom.Length==1 || pauses.Length==1) {
    var root=PrefabUtility.LoadPrefabContents(ProductionApplicationSetup.ApplicationPath);
    if(custom.Length==1) root.GetComponent<ToyControlService>().ServerAddress=custom[0];
    if(pauses.Length==1) root.GetComponent<ToyControlService>().PauseNeutralPosition=pauses[0];
    PrefabUtility.SaveAsPrefabAsset(root,ProductionApplicationSetup.ApplicationPath); PrefabUtility.UnloadPrefabContents(root);
   }
   foreach(var service in services) Undo.DestroyObjectImmediate(service);
   foreach(var p in InScene<PlayerController>(scene)) { Undo.RecordObject(p,"Remove scene toy binding"); p.ConfigureToyService(null); EditorUtility.SetDirty(p); }
  }
  public static SuccubusPerformer AddLara() {
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(LaraPath);
   if(prefab==null) throw new InvalidOperationException("Missing production Lara prefab.");
   var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,SceneManager.GetActiveScene());
   Undo.RegisterCreatedObjectUndo(go,"Add Lara performer");
   Undo.AddComponent<SceneActorIdentity>(go).Configure(Guid.NewGuid().ToString("N"));
   Selection.activeGameObject=go;
   return go.GetComponent<SuccubusPerformer>();
  }
  public static void AddPartner() {
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PartnerPath);
   if(prefab==null) throw new InvalidOperationException("Missing production Partner prefab.");
   var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,SceneManager.GetActiveScene());
   Undo.RegisterCreatedObjectUndo(go,"Add partner"); Selection.activeGameObject=go;
   var hosts=InScene<PerformanceHost>(SceneManager.GetActiveScene());
   var contact=go.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c=>c is DazPose.Performer.HandGrip.IGripTarget);
   if(hosts.Length==1 && contact!=null && hosts[0].Bindings.ContactTarget==null) {
    Undo.RecordObject(hosts[0].Bindings,"Bind partner"); hosts[0].Bindings.ConfigureContactTarget(contact);
   }
  }
  public static PerformanceHost CopyEnvironment(string path) {
   if(File.Exists(path)) throw new InvalidOperationException("Choose a new scene path; existing scene is preserved.");
   Directory.CreateDirectory(Path.GetDirectoryName(path));
   if(!AssetDatabase.CopyAsset(VoidPath,path)) throw new IOException("Could not copy authored Void scene.");
   var scene=EditorSceneManager.OpenScene(path);
   foreach(var c in InScene<MonoBehaviour>(scene))
    if(c!=null && (c.GetType().Name=="FirstPerformanceVoidControls" || c.GetType().Name=="FirstContactPerformance" || c.GetType().Name.StartsWith("Remediation"))) UnityEngine.Object.DestroyImmediate(c);
   var stage=scene.GetRootGameObjects().SingleOrDefault(r=>r.name=="FirstPerformanceVoid");
   if(stage!=null) foreach(var component in stage.GetComponents<PerformerParticleBody>()) UnityEngine.Object.DestroyImmediate(component);
   var container=stage!=null ? stage.transform.Find("Performer") : null;
   if(container!=null) {
    foreach(var c in container.GetComponents<MonoBehaviour>()) UnityEngine.Object.DestroyImmediate(c);
    foreach(Transform child in container.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
    var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LaraPath),scene);
    go.transform.SetParent(container,false);
   }
   return Initialize();
  }
 }
}