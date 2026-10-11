using System;
using System.IO;
using System.Linq;
using DazPose.Content;
using DazPose.Motion;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
using DazPose.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DazPose.Editor {
 public static class AuthoringContentComposer {
  const string AssetsPath="Assets/Production/Content/Assets/";
  static T Prepared<T>(string source,string name,string clipField=null) where T:UnityEngine.Object {
   string path=AssetsPath+name+".asset";
   if(!File.Exists(path)) {
    if(!AssetDatabase.CopyAsset(source,path)) throw new IOException("Missing prepared asset "+source);
    AssetDatabase.ImportAsset(path);
    if(clipField!=null) {
     var so=new SerializedObject(AssetDatabase.LoadAssetAtPath<T>(path));
     var property=so.FindProperty(clipField);
     var clip=property.objectReferenceValue as AnimationClip;
     if(clip!=null) {
      var copy=UnityEngine.Object.Instantiate(clip); copy.name=name+" Clip";
      string clipPath=AssetsPath+name+"-Clip.anim"; AssetDatabase.CreateAsset(copy,clipPath);
      property.objectReferenceValue=copy; so.ApplyModifiedPropertiesWithoutUndo();
     }
    }
   }
   return AssetDatabase.LoadAssetAtPath<T>(path);
  }
  static AudioClip Speech(string guid,string name) {
   string source=AssetDatabase.GUIDToAssetPath(guid),path=AssetsPath+name+".mp3";
   if(!File.Exists(path) && !AssetDatabase.CopyAsset(source,path)) throw new IOException("Missing authored speech "+source);
   AssetDatabase.ImportAsset(path);
   return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
  }
  static Transform Mark(Transform parent,string name,Vector3 position,Quaternion rotation) {
   var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.SetPositionAndRotation(position,rotation); return go.transform;
  }
  static PerformanceHost Scene(string path) {
   if(!File.Exists(path)) return GameSessionSceneSetup.CopyEnvironment(path);
   EditorSceneManager.OpenScene(path); return GameSessionSceneSetup.Initialize();
  }
  public static void BuildSavedContent() {
   GameUiAssetBuilder.EnsureAssets();
   foreach(string name in new[]{"Invitation","Greeting","Celebration"}) {
    string oldPath=AssetsPath+name+"-Clip.asset",newPath=AssetsPath+name+"-Clip.anim";
    if(File.Exists(oldPath) && !File.Exists(newPath)) {
     string error=AssetDatabase.MoveAsset(oldPath,newPath);
     if(!string.IsNullOrEmpty(error)) throw new IOException(error);
    }
   }
   Directory.CreateDirectory(AssetsPath); AssetDatabase.Refresh();
   var expression=Prepared<PerformerExpression>("Assets/Production/ValidationData/Day3Expression.asset","Invitation","clip");
   var gesture=Prepared<PerformerGesture>("Assets/DazPose/Generated/Gestures/Acceptance/RightHandWave.asset","Greeting","clip");
   var action=Prepared<PerformerAction>("Assets/DazPose/Generated/Actions/Acceptance/JumpForJoy.asset","Celebration","bodyClip");
   var lineA=Speech("4284b0d5624bc0f49a0463d2f8374245","Welcome");
   var lineB=Speech("85425b705aedcf840ac41b53f1062cd1","Closer");
   var motion=AssetDatabase.LoadAssetAtPath<FunscriptMotionProgram>("Assets/motiondrive.funscript");
   if(motion==null) throw new InvalidOperationException("Missing authored motiondrive.funscript import.");
   var host=Scene("Assets/Production/Scenes/AuthoringShowcase.unity");
   if(host.GetComponent<AuthoringShowcase>()==null) {
    var b=host.Bindings; var actor=b.Performer;
    var marks=host.gameObject.scene.GetRootGameObjects().Single(r=>r.name=="Authoring Marks").transform;
    var sequence=host.gameObject.AddComponent<AuthoringShowcase>();
    sequence.arrivalPose=actor.InitialPose;
    sequence.invitingPose=AssetDatabase.LoadAssetAtPath<PerformerPose>("Assets/Generated/DazPoses/G8F/Vintage Glamour Genesis 8 Female 22.asset");
    sequence.expression=expression; sequence.greeting=gesture; sequence.welcomeLine=lineA; sequence.contactLine=lineB; sequence.motion=motion;
    sequence.entrance=Mark(marks,"Arrival",new Vector3(.65f,0,-.95f),Quaternion.Euler(0,-90,0));
    sequence.contactMark=Mark(marks,"Contact",new Vector3(.65f,0,-.95f),Quaternion.Euler(0,-90,0));
    sequence.playerHead=b.Player.HeadTransform;
    sequence.actorFocus=Mark(actor.transform,"Actor Focus",actor.transform.position+Vector3.up*1.6f,Quaternion.identity);
    sequence.contactView=Mark(marks,"Contact View",new Vector3(1.6f,1.5f,-2.2f),Quaternion.identity);
    var partner=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GameSessionSceneSetup.PartnerPath),host.gameObject.scene);
    partner.name="Partner"; partner.transform.SetPositionAndRotation(new Vector3(0,1.172133f,0),Quaternion.Euler(-90,0,0));
    var target=partner.GetComponentsInChildren<MonoBehaviour>(true).Single(c=>c is IGripTarget);
    b.ConfigureContactTarget(target);
    host.Configure(b,sequence);
    var welcome=host.GetComponent<WelcomePerformance>(); if(welcome!=null) UnityEngine.Object.DestroyImmediate(welcome);
    EditorUtility.SetDirty(sequence); EditorUtility.SetDirty(b); EditorUtility.SetDirty(host);
   }
   DazPoseProductionMenu.AddCurrentScene();
   host=Scene("Assets/Production/Scenes/AuthoringCompanion.unity");
   if(host.GetComponent<AuthoringCompanion>()==null) {
    var b=host.Bindings; var actor=b.Performer;
    var marks=host.gameObject.scene.GetRootGameObjects().Single(r=>r.name=="Authoring Marks").transform;
    var sequence=host.gameObject.AddComponent<AuthoringCompanion>();
    sequence.home=Mark(marks,"Home",Vector3.zero,Quaternion.identity);
    sequence.walkMark=Mark(marks,"Walk Mark",new Vector3(-.7f,0,-1.2f),Quaternion.identity);
    sequence.playerHead=b.Player.HeadTransform;
    sequence.actorFocus=Mark(actor.transform,"Actor Focus",actor.transform.position+Vector3.up*1.6f,Quaternion.identity);
    sequence.standingPose=actor.InitialPose; sequence.celebration=action;
    sequence.seat=GameSessionSceneSetup.InScene<PerformerSeat>(host.gameObject.scene).Single();
    sequence.spell=AssetDatabase.LoadAssetAtPath<PerformerSpell>("Assets/DazPose/Generated/Magic/Spells/Spell_ArcCyan.asset");
    sequence.aura=AssetDatabase.LoadAssetAtPath<PerformerAura>("Assets/DazPose/Generated/Magic/Auras/Aura_ArcCyan.asset");
    host.Configure(b,sequence);
    var welcome=host.GetComponent<WelcomePerformance>(); if(welcome!=null) UnityEngine.Object.DestroyImmediate(welcome);
    EditorUtility.SetDirty(sequence); EditorUtility.SetDirty(host);
   }
   DazPoseProductionMenu.AddCurrentScene();
   var catalog=AssetDatabase.LoadAssetAtPath<SceneCatalog>(ProductionApplicationSetup.CatalogPath);
   foreach(var entry in catalog.entries) {
    if(entry.scenePath.EndsWith("AuthoringWelcome.unity")) { entry.title="Welcome · pose"; entry.description="A minimal saved sequence."; }
    if(entry.scenePath.EndsWith("AuthoringShowcase.unity")) { entry.title="Invitation · authoring showcase"; entry.description="Speech, wardrobe, camera and shared contact motion."; }
    if(entry.scenePath.EndsWith("AuthoringCompanion.unity")) { entry.title="Encore · movement and magic"; entry.description="Walking, seating, full-body action and effects."; }
   }
   EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
   var scenes=EditorBuildSettings.scenes.ToList();
   foreach(var entry in catalog.entries) if(!scenes.Any(s=>s.path==entry.scenePath)) scenes.Add(new EditorBuildSettingsScene(entry.scenePath,true));
   EditorBuildSettings.scenes=scenes.ToArray();
   Debug.Log("AUTHORING_CONTENT_SAVED: speech "+lineA.length+" / "+lineB.length+"s; authored motion "+motion.name);
  }
 }
}