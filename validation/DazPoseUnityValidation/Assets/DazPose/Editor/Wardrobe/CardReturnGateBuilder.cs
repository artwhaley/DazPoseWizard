using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DazPose.Editor;
using DazPose.Motion;
using DazPose.Performer;
using DazPose.Story;
using DazPose.Validation.WardrobeProtectedCards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DazPose.UnityValidation {
 public static class CardReturnGateBuilder {
  public const string Root="Assets/__WardrobeProtectedCards/GateB";
  public const string ScenePath="Assets/__WardrobeProtectedCards/Scenes/GateB_ProtectedCards.unity";
  const string Released="Assets/Production/Wardrobe/Rigs/d0b34840249bc9e812337938d1214c5f5cfb79f958dbd764afe2b6bca12cce0a";
  public static string CreateOrOpen() {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play before preparing Gate B.");
   if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return "Cancelled by scene save prompt.";
   if(File.Exists(ScenePath)){
    var existing=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var saved=GameSessionSceneSetup.InScene<CardReturnGateRunner>(existing).SingleOrDefault();
    if(saved!=null&&saved.setupVersion==CardReturnGateRunner.SetupVersion&&saved.performance!=null&&saved.root!=null&&saved.cards!=null)
     return "Opened saved Gate B. Enter Play yourself; its Card Return Gate panel contains the labelled checks.";
    // Retry only the byte-identical, untouched copy from our interrupted preparation.
    // Authored or saved partial compositions stay protected.
    string receipt=Root+"/scene-copy.sha256";
    if(!File.Exists(receipt)||File.ReadAllText(receipt)!=FileHash(ScenePath))throw new InvalidOperationException("Existing Gate B composition was edited or has no preparation receipt; preserved: "+ScenePath);
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    if(!AssetDatabase.DeleteAsset(ScenePath))throw new IOException("Could not retire unchanged interrupted Gate B copy.");
   }
   var released=AssetDatabase.LoadAssetAtPath<WardrobeCatalog>(Released+"/Catalog.asset");var manifest=AssetDatabase.LoadAssetAtPath<WardrobeRigManifest>(Released+"/WardrobeRigManifest.asset");
   string reason=null;if(released==null||manifest==null||!manifest.Validate(released,out reason))throw new InvalidOperationException("Accepted release/manifest is unavailable: "+reason);
   var source=AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>("Assets/Production/Story/Programs/FirstContact.dazstory");
   if(source==null||!source.IsCompiled||source.content==null)throw new InvalidOperationException("Reimport FirstContact.dazstory before creating Gate B.");
   var expression=Find<PerformerExpression>("t:PerformerExpression");var aura=AssetDatabase.LoadAssetAtPath<PerformerAura>("Assets/Production/Content/Magic/Auras/Aura_VioletSerenity.asset");var motion=Find<FunscriptMotionProgram>("t:FunscriptMotionProgram");
   if(expression==null||aura==null||motion==null||!aura.IsReady(out reason))throw new InvalidOperationException("Gate B needs existing expression, aura and engineering motion assets: "+reason);
   Ensure(Root);Ensure("Assets/__WardrobeProtectedCards/Scenes");Ensure(Root+"/Fits");Ensure(Root+"/Presets");
   var diagnostic=Clone(released,Root+"/Catalog.asset");var presets=released.Presets.Select(p=>{
    var copy=Clone(p,Root+"/Presets/"+p.PresetId+".asset");var fits=p.FitStates.Select(f=>Clone(f,Root+"/Fits/"+p.PresetId+"-"+f.VisibleMask+".asset")).ToArray();copy.SetGeneratedFitStates(fits);EditorUtility.SetDirty(copy);return copy;
   }).ToArray();
   var catalogObject=new SerializedObject(diagnostic);catalogObject.Update();var rows=catalogObject.FindProperty("presets");rows.arraySize=presets.Length;for(int i=0;i<presets.Length;i++)rows.GetArrayElementAtIndex(i).objectReferenceValue=presets[i];catalogObject.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(diagnostic);
   var content=Content(source.content,diagnostic,manifest,Root+"/Root.bindings.asset");var publishedContent=Content(source.content,diagnostic,manifest,Root+"/Cards.bindings.asset");
   string repository=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
   WriteSource(repository,"GateBRoot.dazstory",Root+"/Root.dazstory");WriteSource(repository,"GateBCards.dazstory",Root+"/Cards.dazstory");
   content.ownerSourceGuid=AssetDatabase.AssetPathToGUID(Root+"/Root.dazstory");publishedContent.ownerSourceGuid=AssetDatabase.AssetPathToGUID(Root+"/Cards.dazstory");EditorUtility.SetDirty(content);EditorUtility.SetDirty(publishedContent);AssetDatabase.SaveAssets();
   AssetDatabase.ImportAsset(Root+"/Root.dazstory",ImportAssetOptions.ForceUpdate);AssetDatabase.ImportAsset(Root+"/Cards.dazstory",ImportAssetOptions.ForceUpdate);
   var root=AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>(Root+"/Root.dazstory");var cards=AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>(Root+"/Cards.dazstory");if(!root.IsCompiled||!cards.IsCompiled)throw new InvalidOperationException(root.CompilationIssue+"\n"+cards.CompilationIssue);
   if(!AssetDatabase.CopyAsset("Assets/Scenes/FirstContact.unity",ScenePath))throw new IOException("Could not copy First Contact into Gate B.");
   File.WriteAllText(Root+"/scene-copy.sha256",FileHash(ScenePath));
   var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
   foreach(var host in GameSessionSceneSetup.InScene<PerformanceHost>(scene)){host.PlayOnStart=false;host.enabled=false;EditorUtility.SetDirty(host);}
   var performance=GameSessionSceneSetup.InScene<PerformanceSceneBindings>(scene).Single();var story=GameSessionSceneSetup.InScene<StorySceneBindings>(scene).Single();var actor=performance.Performer;var wardrobe=actor.GetComponent<PerformerWardrobe>();
   if(wardrobe==null||wardrobe.Body==null||performance.Player?.BodyPresence==null||performance.Playback==null)throw new InvalidOperationException("Copied scene needs Lara's configured body, original PlayerBodyPresence and local MotionDriver.");
   var changed=source.content.poses.FirstOrDefault(p=>p.value!=null && p.value!=actor.InitialPose && p.posture==PlayerPosture.Standing);if(changed==null)throw new InvalidOperationException("Gate B needs an existing pose distinct from the entrance pose.");
   content.poses[0].value=changed.value;publishedContent.poses[0].value=changed.value;EditorUtility.SetDirty(content);EditorUtility.SetDirty(publishedContent);
   actor.ConfigureInitialState(actor.InitialPose,actor.InitialExpression,actor.InitialExpressionIntensity,false);actor.gameObject.SetActive(true);wardrobe.ConfigureBinding(diagnostic,null,wardrobe.Body,actor);
   var binding=actor.GetComponent<WardrobeRigBinding>()??actor.gameObject.AddComponent<WardrobeRigBinding>();binding.Configure(manifest,Child(wardrobe.Body.transform,"GateB clothing"),Child(wardrobe.Body.transform,"GateB hair"));
   var serialized=new SerializedObject(wardrobe);serialized.FindProperty("rigBinding").objectReferenceValue=binding;serialized.ApplyModifiedPropertiesWithoutUndo();
   serialized=new SerializedObject(actor);serialized.FindProperty("motionDriver").objectReferenceValue=performance.Playback;serialized.ApplyModifiedPropertiesWithoutUndo();
   var entrance=Mark("Gate B entrance",story.RequireLocation("lara_start").placement.position,story.RequireLocation("lara_start").placement.rotation);
   var camera=Mark("Gate B fixed camera (pitch and roll)",story.RequireLocation("camera_start").placement.position,story.RequireLocation("camera_start").placement.rotation*Quaternion.Euler(7,0,13));
   var cameraShift=Mark("Gate B outgoing camera",camera.position+new Vector3(.5f,.15f,-.35f),camera.rotation*Quaternion.Euler(-12,24,-20));
   var shift=Mark("GateShift",entrance.position+new Vector3(1,0,.5f),entrance.rotation*Quaternion.Euler(0,30,0));var outer=Mark("GateOuter",entrance.position+new Vector3(-.7f,0,.4f),entrance.rotation*Quaternion.Euler(0,-25,0));
   story.locations=story.locations.Concat(new[]{new StoryLocationBinding{id="GateShift",placement=shift},new StoryLocationBinding{id="GateOuter",placement=outer}}).ToArray();
   var runner=new GameObject("Card Return Gate").AddComponent<CardReturnGateRunner>();runner.setupVersion=CardReturnGateRunner.SetupVersion;runner.performance=performance;runner.scene=story;runner.root=root;runner.cards=cards;runner.openingOutfit=diagnostic.Presets.Single(p=>p.PresetId=="first-outfit");runner.openingHair=manifest.Hair.First().prefab;runner.seatedPose=source.content.Pose("Lounging").value;runner.changedExpression=expression;runner.entranceAura=aura;runner.entranceMark=entrance;runner.cameraMark=camera;runner.shiftCamera=cameraShift;runner.seat=story.RequireLocation("Lounger").seat;runner.replacementMotion=motion;
   if(runner.seat==null||runner.seatedPose==null)throw new InvalidOperationException("Copied Lounger seat / Lounging pose is missing.");
   runner.sceneGuid=AssetDatabase.AssetPathToGUID(ScenePath);runner.sourceFingerprint=Fingerprint(repository);
   foreach(var component in new Component[]{actor,wardrobe,binding,performance,story}){PrefabUtility.RecordPrefabInstancePropertyModifications(component);EditorUtility.SetDirty(component);}EditorUtility.SetDirty(runner);EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save Gate B.");AssetDatabase.SaveAssets();
   scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);if(GameSessionSceneSetup.InScene<CardReturnGateRunner>(scene).Single().root==null)throw new InvalidOperationException("Gate B failed saved-reference verification.");
   return "Gate B is saved and open. Enter Play yourself; use the Card Return Gate panel. Production assets were not modified.";
  }
  static StoryContentBindings Content(StoryContentBindings source,WardrobeCatalog catalog,WardrobeRigManifest manifest,string path){
   var content=AssetDatabase.LoadAssetAtPath<StoryContentBindings>(path);if(content!=null)return content;
   content=ScriptableObject.CreateInstance<StoryContentBindings>();content.poses=new[]{new StoryPoseContent{id="ChangedPose",value=source.Pose("Sassy").value,posture=PlayerPosture.Standing}};content.shaders=new[]{new StoryShaderContent{id="ChangedShader",value=source.Shader("GlowyWireframe")}};content.wardrobe=new[]{new StoryWardrobeContent{id="ChangedOutfit",value=catalog.Presets.First(p=>p.PresetId!="first-outfit")}};content.sceneSymbols=new[]{new StorySceneSymbol{id="GateShift",kind=DazPose.Story.Language.StoryBindingKind.Location},new StorySceneSymbol{id="GateOuter",kind=DazPose.Story.Language.StoryBindingKind.Location}};AssetDatabase.CreateAsset(content,path);return content;
  }
  static T Clone<T>(T source,string path)where T:UnityEngine.Object{var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing!=null)return existing;if(File.Exists(path))throw new IOException("Preserved incompatible diagnostic output: "+path);var copy=UnityEngine.Object.Instantiate(source);copy.name=source.name;AssetDatabase.CreateAsset(copy,path);return copy;}
  static string FileHash(string path){using var hash=SHA256.Create();return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));}
  static T Find<T>(string query)where T:UnityEngine.Object=>AssetDatabase.FindAssets(query).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p,StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<T>).FirstOrDefault(p=>p!=null);
  static Transform Child(Transform parent,string name){if(parent.Find(name)!=null)throw new InvalidOperationException("Unexpected existing "+name);var value=new GameObject(name).transform;value.SetParent(parent,false);return value;}
  static Transform Mark(string name,Vector3 position,Quaternion rotation){var mark=new GameObject(name).transform;mark.SetPositionAndRotation(position,rotation);return mark;}
  static void WriteSource(string repository,string name,string output){string text=File.ReadAllText(Path.Combine(repository,"docs/wardrobe-directing/fixtures",name));if(File.Exists(output)&&File.ReadAllText(output)!=text)throw new IOException("Preserved edited diagnostic script: "+output);if(!File.Exists(output))File.WriteAllText(output,text);AssetDatabase.ImportAsset(output,ImportAssetOptions.ForceUpdate);}
  static string Fingerprint(string repository){using var hash=SHA256.Create();var files=Directory.GetFiles(Path.Combine(repository,"validation/DazPoseUnityValidation/Assets/DazPose"),"*.cs",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal);string content=string.Join("\n",files.Select(f=>Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(f)))));return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content))).Replace("-","").ToLowerInvariant();}
  static void Ensure(string path){if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Ensure(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
 }
}
