using System;
using System.IO;
using System.Linq;
using DazPose.Game;
using DazPose.Performer;
using DazPose.Player;
using DazPose.Story;
using DazPose.UnityValidation.Partner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DazPose.Editor {
 public static class StorySceneBuilder {
  public const string ScenePath="Assets/Production/Scenes/FirstContactD1D5.unity";
  public const string MarksPath="Assets/Production/Story/FirstContactMarks.prefab";
  const string ProgramPath="Assets/Production/Story/Programs/FirstContactD1D5.story.json";
  const string CatalogPath="Assets/Resources/DazPose/StoryCatalogs.asset";
  static readonly string[] MarkIds={"lara_start","step_forward","view_lara","view_lounge"};
  [MenuItem("DazPose/Story/Build or Refresh First Contact D1-D5")]
  public static void BuildOrRefresh() {
   if(Application.isPlaying) throw new InvalidOperationException("Story scene setup requires Edit Mode.");
   if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
   ConfigureCatalogs();
   bool created=!File.Exists(ScenePath);
   var scene=created ? ProductionSceneEnvironment.Copy(ScenePath) : EditorSceneManager.OpenScene(ScenePath);
   ProductionSceneEnvironment.EnsureTemplate();
   GameUiAssetBuilder.EnsureAssets();
   var host=GameSessionSceneSetup.Initialize(false);
   if(host==null) throw new InvalidOperationException("Story composition requires exactly one host.");
   GameUiAssetBuilder.Install(host);
   var actor=host.Bindings.Performer;
   var sequence=host.GetComponent<StoryProgramSequence>() ?? Undo.AddComponent<StoryProgramSequence>(host.gameObject);
   var bindings=host.GetComponent<StorySceneBindings>() ?? Undo.AddComponent<StorySceneBindings>(host.gameObject);
   Undo.RecordObject(sequence,"Configure story"); Undo.RecordObject(bindings,"Bind story"); Undo.RecordObject(host,"Configure story host");
   if(created) {
    actor.ConfigureInitialState(actor.InitialPose,actor.InitialExpression,actor.InitialExpressionIntensity,true);
    EditorUtility.SetDirty(actor); PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
    var markPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(MarksPath);
    if(markPrefab==null) throw new InvalidOperationException("Assign the saved production First Contact mark composition.");
    var marks=(GameObject)PrefabUtility.InstantiatePrefab(markPrefab,scene);
    var empty=scene.GetRootGameObjects().SingleOrDefault(r=>r!=marks && r.name=="Authoring Marks" && r.transform.childCount==0);
    if(empty!=null) UnityEngine.Object.DestroyImmediate(empty);
    marks.name="Authoring Marks";
    var arrival=marks.transform.Find("lara_start"); actor.transform.SetPositionAndRotation(arrival.position,arrival.rotation);
    var head=actor.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="head");
    var face=new GameObject("Story Face Target").transform; face.SetParent(head,false); face.localPosition=new Vector3(0,.08f,.08f);
    bindings.targets=MarkIds.Select(id=>new StoryTransformBinding { id=id,value=marks.transform.Find(id) }).Concat(new[] {
     new StoryTransformBinding { id="lara_face",value=face },new StoryTransformBinding { id="player_head",value=host.Bindings.Player.HeadTransform }
    }).ToArray();
    bindings.assets=new[] {
     new StoryObjectBinding { id="voice:first-contact-a",value=AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("4284b0d5624bc0f49a0463d2f8374245")) },
     new StoryObjectBinding { id="voice:first-contact-b",value=AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("85425b705aedcf840ac41b53f1062cd1")) },
     new StoryObjectBinding { id="seat:lounge",value=GameSessionSceneSetup.InScene<PerformerSeat>(scene).Single() }
    };
   }
   MigrateStoryMarks(bindings);
   ProductionSceneEnvironment.RemoveLegacyStage(scene);
   if(bindings.prompts==null) {
    bindings.prompts=StoryPromptService.Create(null);
    Undo.RegisterCreatedObjectUndo(bindings.prompts.gameObject,"Add saved story prompt canvas");
   }
   if(bindings.debugPresenter==null) bindings.debugPresenter=Undo.AddComponent<StoryDebugPresenter>(host.gameObject);
   // Replace the incorrect legacy Lara visual copy with the prepared production male variant.
   if(bindings.playerBody!=null && bindings.playerBody.GetComponent<PartnerContactTarget>()==null) {
    Undo.DestroyObjectImmediate(bindings.playerBody.gameObject); bindings.playerBody=null;
   }
   var playerPrefab=ProductionPlayerBodyBuilder.EnsurePrefab();
   if(bindings.playerBody==null) {
    var body=(GameObject)PrefabUtility.InstantiatePrefab(playerPrefab,scene);
    Undo.RegisterCreatedObjectUndo(body,"Add prepared player body"); body.name="Player Body";
    var actors=scene.GetRootGameObjects().SingleOrDefault(r=>r.name=="Actors");
    if(actors==null) { actors=new GameObject("Actors"); actor.transform.SetParent(actors.transform,true); }
    body.transform.SetParent(actors.transform,true);
    bindings.playerBody=body.GetComponent<PlayerBodyPresence>();
   }
   Undo.RecordObject(host.Bindings.Player,"Bind prepared player body");
   host.Bindings.Player.ConfigureBodyPresence(bindings.playerBody);
   PrefabUtility.RecordPrefabInstancePropertyModifications(host.Bindings.Player); EditorUtility.SetDirty(host.Bindings.Player);
   Undo.RecordObject(host.Bindings,"Bind prepared contact column");
   host.Bindings.ConfigureContactTarget(bindings.playerBody.GetComponent<PartnerContactTarget>()); EditorUtility.SetDirty(host.Bindings);
   sequence.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>(ProgramPath),bindings);
   host.Configure(host.Bindings,sequence); host.PlayOnStart=true;
   var welcome=host.GetComponent<WelcomePerformance>(); if(welcome!=null) Undo.DestroyObjectImmediate(welcome);
   StoryProgramValidator.Validate(StoryProgramSerializer.Parse(sequence.Program.text),bindings);
   bindings.playerBody.ValidateConfiguration();
   EditorUtility.SetDirty(sequence); EditorUtility.SetDirty(bindings); EditorUtility.SetDirty(host);
   EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
   DazPoseProductionMenu.AddCurrentScene();
   var catalog=AssetDatabase.LoadAssetAtPath<SceneCatalog>(ProductionApplicationSetup.CatalogPath);
   var entry=catalog.entries.Single(e=>e.scenePath==ScenePath);
   if(entry.title=="FirstContactD1D5") entry.title="First Contact · session setup";
   if(string.IsNullOrWhiteSpace(entry.description)) entry.description="Meet Lara and agree on interests, equipment, and clothing.";
   EditorUtility.SetDirty(catalog);
   var list=EditorBuildSettings.scenes.ToList();
   if(!list.Any(s=>s.path==ScenePath)) { list.Add(new EditorBuildSettingsScene(ScenePath,true)); EditorBuildSettings.scenes=list.ToArray(); }
   AssetDatabase.SaveAssets();
   Debug.Log("D1_D5_SOURCE_READY: saved clean production scene "+ScenePath+" with the prepared male body/contact target; Play Mode NOT RUN.");
  }
  static void MigrateStoryMarks(StorySceneBindings bindings) {
   var scene=bindings.gameObject.scene;
   var marks=scene.GetRootGameObjects().Single(r=>r.name=="Authoring Marks").transform;
   foreach(var binding in bindings.targets.Where(b=>MarkIds.Contains(b.id))) {
    if(binding.value==null) throw new InvalidOperationException("Missing authored story mark "+binding.id);
    if(binding.value.parent!=marks) {
     var clean=new GameObject(binding.id).transform;
     clean.SetParent(marks,false); clean.SetPositionAndRotation(binding.value.position,binding.value.rotation);
     binding.value=clean;
    } else binding.value.name=binding.id;
   }
   if(!File.Exists(MarksPath)) {
    // The reusable composition contains only the four scene poses actually used by this story.
    var composition=new GameObject("First Contact Marks");
    try {
     foreach(var binding in bindings.targets.Where(b=>MarkIds.Contains(b.id))) {
      var mark=new GameObject(binding.id).transform; mark.SetParent(composition.transform,false);
      mark.SetPositionAndRotation(binding.value.position,binding.value.rotation);
     }
     PrefabUtility.SaveAsPrefabAsset(composition,MarksPath);
    } finally { UnityEngine.Object.DestroyImmediate(composition); }
   }
  }
  static void ConfigureCatalogs() {
   Directory.CreateDirectory("Assets/Resources/DazPose");
   var config=AssetDatabase.LoadAssetAtPath<StoryCatalogs>(CatalogPath);
   if(config==null) { config=ScriptableObject.CreateInstance<StoryCatalogs>(); AssetDatabase.CreateAsset(config,CatalogPath); }
   Undo.RecordObject(config,"Bind story catalogs");
   config.kinkCatalog=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Production/Story/Catalogs/kink-catalog.json");
   config.equipmentCatalog=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Production/Story/Catalogs/equipment-catalog.json");
   config.ValidateAndLoad(); EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
  }
 }
}