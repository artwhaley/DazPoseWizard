using System;
using System.IO;
using System.Linq;
using DazPose.Game;
using DazPose.Performer;
using DazPose.Player;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DazPose.Editor {
 public static class StoryFoundationBuilder {
  public const string SourcePath="Assets/Production/Story/Programs/FirstContact.dazstory";
  public const string ContentPath="Assets/Production/Story/Programs/FirstContact.bindings.asset";
  public const string DialogueMapPath="Assets/Production/Story/Programs/FirstContact.dialogue-map.json";
  public static bool IsBuilding {get;private set;}
  static readonly string[] PoseNames={"SexyVamp","LeaningForward","CrossedArmsAmused","Lounging","Main1","Sassy"};
  static readonly string[] LocationNames={"lara_start","player_start","camera_start","stepforward","Lounger","MainPosition1","Bedside"};
  [Serializable] sealed class AudioMap { public AudioRow[] clips; }
  [Serializable] sealed class AudioRow {public string id,transcript,file;public int index;}
  [MenuItem("DazPose/Story/Prepare First Contact Authoring")]
  public static void Prepare() {
   if(IsBuilding)return;
   if(Application.isPlaying)throw new InvalidOperationException("Prepare the saved story in Edit Mode.");
   if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
   IsBuilding=true;
   try {
    var content=EnsureContent();
    AssetDatabase.ImportAsset(SourcePath,ImportAssetOptions.ForceUpdate);
    var compiled=AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>(SourcePath);
    if(compiled==null || !compiled.IsCompiled)throw new InvalidOperationException("FirstContact.dazstory did not compile: "+string.Join("\n",compiled?.diagnostics ?? Array.Empty<string>()));
    var scene=File.Exists(StorySceneBuilder.ScenePath) ? EditorSceneManager.OpenScene(StorySceneBuilder.ScenePath) : ProductionSceneEnvironment.Copy(StorySceneBuilder.ScenePath);
    GameUiAssetBuilder.EnsureAssets();EnsureCatalogs();
    var host=GameSessionSceneSetup.Initialize(false);if(host==null)throw new InvalidOperationException("Keep exactly one saved Game Session.");
    GameUiAssetBuilder.Install(host);
    var bindings=host.GetComponent<StorySceneBindings>() ?? Undo.AddComponent<StorySceneBindings>(host.gameObject);
    Undo.RecordObject(bindings,"Bind authored First Contact");
    bindings.lara=host.Bindings.Performer;
    if(bindings.playerBody==null)bindings.playerBody=ProductionPlayerBodyBuilder.AddToHost(host);
    host.Bindings.Player.ConfigureBodyPresence(bindings.playerBody);
    host.Bindings.ConfigureContactTarget(bindings.playerBody.GetComponent<DazPose.UnityValidation.Partner.PartnerContactTarget>());
    ProductionSceneEnvironment.RemoveLegacyStage(scene);
    var marks=scene.GetRootGameObjects().Single(r=>r.name=="Authoring Marks").transform;
    Transform Existing(string symbol,string legacy=null)=>bindings.locations.FirstOrDefault(l=>l.id==symbol)?.placement
     ?? bindings.targets.FirstOrDefault(t=>t.id==(legacy ?? symbol))?.value ?? marks.Find(symbol);
    Transform Mark(string name,Vector3 position,Quaternion rotation) {
     var value=Existing(name);if(value!=null)return value;
     var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Add authored setup mark");go.transform.SetParent(marks,false);go.transform.SetPositionAndRotation(position,rotation);return go.transform;
    }
    var arrival=Existing("lara_start") ?? Mark("lara_start",bindings.lara.transform.position,bindings.lara.transform.rotation);
    var forward=Existing("stepforward","step_forward");
    if(forward==null) {
     var layout=AssetDatabase.LoadAssetAtPath<GameObject>(StorySceneBuilder.MarksPath);
     var saved=layout!=null ? layout.transform.Find("stepforward") ?? layout.transform.Find("step_forward") : null;
     if(saved!=null)forward=Mark("stepforward",saved.position,saved.rotation);
    }
    if(forward==null)throw new InvalidOperationException("Bind the authored stepforward location; no approximate movement mark is generated.");
    forward.name="stepforward";
    var player=host.Bindings.Player;
    var playerStart=Mark("player_start",bindings.playerBody.transform.position,bindings.playerBody.transform.rotation);
    var cameraStart=Mark("camera_start",player.View.PlayerPosition,player.View.ViewRotation);
    var seat=GameSessionSceneSetup.InScene<PerformerSeat>(scene).Single();
    var mainPosition=Mark("MainPosition1",forward.position,forward.rotation);
    var bedside=Existing("Bedside");
    if(bedside==null) {
     var contactFrame=ContactStaging();bedside=Mark("Bedside",contactFrame.position,contactFrame.rotation);
    }
    bindings.locations=new[] {
     new StoryLocationBinding{id="lara_start",placement=arrival},new StoryLocationBinding{id="player_start",placement=playerStart},
     new StoryLocationBinding{id="camera_start",placement=cameraStart},new StoryLocationBinding{id="stepforward",placement=forward},
     new StoryLocationBinding{id="Lounger",placement=seat.ApproachAnchor,seat=seat,focus=seat.SeatAnchor},
     new StoryLocationBinding{id="MainPosition1",placement=mainPosition},new StoryLocationBinding{id="Bedside",placement=bedside}
    };
    bindings.laraFocus=bindings.laraFocus ?? bindings.targets.FirstOrDefault(t=>t.id=="lara_face")?.value;
    if(bindings.laraFocus==null) {
     var head=bindings.lara.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="head");
     var focus=new GameObject("Story Face Target").transform;focus.SetParent(head,false);focus.localPosition=new Vector3(0,.08f,.08f);bindings.laraFocus=focus;
    }
    foreach(var old in bindings.targets.Where(t=>t.id=="view_lara" || t.id=="view_lounge").ToArray())
     if(old.value!=null && old.value.parent==marks)Undo.DestroyObjectImmediate(old.value.gameObject);
    SaveSetupMarks(arrival,playerStart,cameraStart,forward,mainPosition,bedside);
    bindings.targets=Array.Empty<StoryTransformBinding>();bindings.assets=Array.Empty<StoryObjectBinding>();
    if(bindings.prompts==null)bindings.prompts=StoryPromptService.Create(null);
    if(bindings.debugPresenter==null)bindings.debugPresenter=Undo.AddComponent<StoryDebugPresenter>(host.gameObject);
    var sequence=host.GetComponent<StoryProgramSequence>() ?? Undo.AddComponent<StoryProgramSequence>(host.gameObject);
    sequence.Configure(compiled,bindings);host.Configure(host.Bindings,sequence);host.PlayOnStart=true;
    var welcome=host.GetComponent<WelcomePerformance>();if(welcome!=null)Undo.DestroyObjectImmediate(welcome);
    bindings.lara.ConfigureInitialState(bindings.lara.InitialPose,bindings.lara.InitialExpression,bindings.lara.InitialExpressionIntensity,true);
    PrefabUtility.RecordPrefabInstancePropertyModifications(bindings.lara);
    PrefabUtility.RecordPrefabInstancePropertyModifications(player);
    foreach(var value in new UnityEngine.Object[]{bindings,sequence,host,host.Bindings,player,bindings.lara})EditorUtility.SetDirty(value);
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    DazPoseProductionMenu.AddCurrentScene();AssetDatabase.SaveAssets();
    Debug.Log("First Contact scene references saved. Active authored source: "+SourcePath+". Six user-selected pose wrappers are still required; D6–D8 playback is not claimed.",host);
   } finally {IsBuilding=false;}
  }
  static (Vector3 position,Quaternion rotation) ContactStaging() {
   const string path="Assets/Production/Scenes/AuthoringShowcase.unity";
   if(!File.Exists(path))return (new Vector3(0,0,-.6f),Quaternion.identity);
   var reference=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   try {
    var mark=GameSessionSceneSetup.InScene<Transform>(reference).FirstOrDefault(t=>t.name=="Contact" && t.parent!=null && t.parent.name=="Authoring Marks");
    return mark!=null ? (mark.position,mark.rotation) : (new Vector3(0,0,-.6f),Quaternion.identity);
   } finally {EditorSceneManager.CloseScene(reference,true);}
  }
  static void SaveSetupMarks(params Transform[] marks) {
   var root=new GameObject("First Contact Marks");
   try {
    foreach(var value in marks) {
     var copy=new GameObject(value.name).transform;copy.SetParent(root.transform,false);copy.SetPositionAndRotation(value.position,value.rotation);
    }
    PrefabUtility.SaveAsPrefabAsset(root,StorySceneBuilder.MarksPath);
   } finally {UnityEngine.Object.DestroyImmediate(root);}
  }
  static StoryContentBindings EnsureContent() {
   Directory.CreateDirectory("Assets/Production/Story/Visuals");AssetDatabase.Refresh();
   var content=AssetDatabase.LoadAssetAtPath<StoryContentBindings>(ContentPath);
   if(content==null) {
    content=ScriptableObject.CreateInstance<StoryContentBindings>();AssetDatabase.CreateAsset(content,ContentPath);
    content.sceneSymbols=LocationNames.Select(id=>new StorySceneSymbol{id=id,kind=StoryBindingKind.Location}).Concat(new[]{
     new StorySceneSymbol{id="Lara",kind=StoryBindingKind.Performer},new StorySceneSymbol{id="PlayerBody",kind=StoryBindingKind.PlayerBody},new StorySceneSymbol{id="Penis",kind=StoryBindingKind.ContactTarget}}).ToArray();
    content.poses=PoseNames.Select(id=>new StoryPoseContent{id=id,posture=id=="Lounging" ? PlayerPosture.Sitting : PlayerPosture.Standing}).ToArray();
    content.gestures=new[]{new StoryGestureContent{id="Disapproval"}};
    content.motion=new[]{new StoryMotionContent{id="Finisher1"},new StoryMotionContent{id="FinishLoop"}};
   }
   var wire=Profile("GlowyWireframe",PerformerMaterialMode.GlowyWireframe);var normal=Profile("Regular",PerformerMaterialMode.Regular);
   if(content.shaders.Length==0)content.shaders=new[]{new StoryShaderContent{id="GlowyWireframe",value=wire},new StoryShaderContent{id="Regular",value=normal}};
   var map=JsonUtility.FromJson<AudioMap>(File.ReadAllText(DialogueMapPath));
   if(map.clips.Length!=19)throw new InvalidOperationException("The frozen fixed-dialogue oracle requires 19 lines.");
   foreach(var row in map.clips) {
    if(row.id!=StoryDialogue.IdFor(row.transcript))throw new InvalidOperationException("Frozen dialogue hash differs from its transcript.");
    if(content.dialogue.Any(d=>d.id==row.id))continue;
    var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Production/Story/Audio/Placeholder/FirstContact/"+row.file);
    if(clip==null)throw new InvalidOperationException("Missing supplied engineering placeholder: "+row.file);
    content.dialogue=content.dialogue.Concat(new[]{new StoryDialogueContent{id=row.id,transcript=row.transcript,clip=clip,engineeringPlaceholder=true}}).ToArray();
   }
   EditorUtility.SetDirty(content);AssetDatabase.SaveAssets();return content;
  }
  static PerformerMagicShaderProfile Profile(string name,PerformerMaterialMode mode) {
   string path="Assets/Production/Story/Visuals/"+name+".asset";
   var profile=AssetDatabase.LoadAssetAtPath<PerformerMagicShaderProfile>(path);
   if(profile!=null)return profile;
   profile=ScriptableObject.CreateInstance<PerformerMagicShaderProfile>();profile.mode=mode;
   if(mode==PerformerMaterialMode.GlowyWireframe) {
    const string materialPath="Assets/Production/Story/Visuals/GlowyWireframe.mat";
    var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
    if(material==null) {
     var shader=Shader.Find("DazPose/Performer/Glowing Wireframe");if(shader==null)throw new InvalidOperationException("Import the project-owned glowing wireframe shader first.");
     material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);
    }
    profile.wireframeMaterial=material;
   }
   profile.ValidateConfiguration();AssetDatabase.CreateAsset(profile,path);return profile;
  }
  static void EnsureCatalogs() {
   const string path="Assets/Resources/DazPose/StoryCatalogs.asset";
   var catalog=AssetDatabase.LoadAssetAtPath<StoryCatalogs>(path);
   if(catalog==null) {catalog=ScriptableObject.CreateInstance<StoryCatalogs>();AssetDatabase.CreateAsset(catalog,path);}
   catalog.kinkCatalog=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Production/Story/Catalogs/kink-catalog.json");
   catalog.equipmentCatalog=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Production/Story/Catalogs/equipment-catalog.json");
   catalog.ValidateAndLoad();EditorUtility.SetDirty(catalog);
  }
 }
}