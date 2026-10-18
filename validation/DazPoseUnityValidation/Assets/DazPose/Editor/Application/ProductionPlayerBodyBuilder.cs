using System;
using System.IO;
using System.Linq;
using DazPose.Editor.ParticleBody;
using DazPose.Performer;
using DazPose.Player;
using DazPose.UnityValidation.Partner;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor {
 public static class ProductionPlayerBodyBuilder {
  public const string PrefabPath="Assets/Production/Player/PlayerBody.prefab";
  const string Folder="Assets/Production/Player/Presentation";
  public static GameObject EnsurePrefab() {
   var saved=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
   if(saved!=null) return saved;
   Directory.CreateDirectory(Folder);
   var source=AssetDatabase.LoadAssetAtPath<GameObject>(GameSessionSceneSetup.PartnerPath);
   var body=(GameObject)PrefabUtility.InstantiatePrefab(source);
   try {
    body.name="Player Body";
    var lara=AssetDatabase.LoadAssetAtPath<GameObject>(GameSessionSceneSetup.LaraPath).GetComponent<SuccubusPerformer>();
    var style=lara.DissolveProfile;
    var parts=body.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select((renderer,index)=>CreateSurface(renderer,index,style)).ToArray();
    body.AddComponent<PlayerBodyPresence>().Configure(parts,true);
    var contact=body.GetComponent<PartnerContactTarget>();
    if(contact==null || !contact.IsReadyForContact(false,out string reason)) throw new InvalidOperationException("Prepared male contact target is invalid.");
    return PrefabUtility.SaveAsPrefabAsset(body,PrefabPath);
   } finally { UnityEngine.Object.DestroyImmediate(body); }
  }
  public static PlayerBodyPresence AddToHost(PerformanceHost host) {
   if(host==null || host.Bindings==null || host.Bindings.Player==null) throw new InvalidOperationException("Initialize one saved Game Session and player first.");
   if(host.Bindings.Player.BodyPresence!=null) return host.Bindings.Player.BodyPresence;
   var scene=host.gameObject.scene;
   var body=(GameObject)PrefabUtility.InstantiatePrefab(EnsurePrefab(),scene);
   Undo.RegisterCreatedObjectUndo(body,"Add prepared male player body"); body.name="Player Body";
   var actors=scene.GetRootGameObjects().SingleOrDefault(r=>r.name=="Actors");
   if(actors==null) {
    actors=new GameObject("Actors"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actors,scene);
    if(host.Bindings.Performer!=null) host.Bindings.Performer.transform.SetParent(actors.transform,true);
   }
   body.transform.SetParent(actors.transform,true);
   var presence=body.GetComponent<PlayerBodyPresence>();
   Undo.RecordObject(host.Bindings.Player,"Bind player body"); host.Bindings.Player.ConfigureBodyPresence(presence);
   PrefabUtility.RecordPrefabInstancePropertyModifications(host.Bindings.Player); EditorUtility.SetDirty(host.Bindings.Player);
   Undo.RecordObject(host.Bindings,"Bind player contact column"); host.Bindings.ConfigureContactTarget(body.GetComponent<PartnerContactTarget>()); EditorUtility.SetDirty(host.Bindings);
   return presence;
  }
  [MenuItem("DazPose/Scene/Add Prepared Player Body")]
  static void AddToCurrentScene() {
   var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
   var hosts=GameSessionSceneSetup.InScene<PerformanceHost>(scene);
   if(hosts.Length!=1) throw new InvalidOperationException("Initialize exactly one Game Session first.");
   Selection.activeGameObject=AddToHost(hosts[0]).gameObject;
   UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
  }
  static PlayerBodyDissolvePart CreateSurface(SkinnedMeshRenderer renderer,int index,PerformerDissolveProfile style) {
   var originals=renderer.sharedMaterials;
   var materials=originals.Select(m=>ConvertMaterial(m)).ToArray();
   string prefix=Folder+"/Surface"+index;
   var profile=UnityEngine.Object.Instantiate(style); profile.name="PlayerBodySurface"+index;
   AssetDatabase.CreateAsset(profile,prefix+"Profile.asset");
   profile.ConfigureConvertedMaterials(style.SssDissolveShader,style.WetDissolveShader,materials,materials.Select(m=>m.shader).ToArray());
   var surface=PerformerSurfaceBindingBaker.Bake(renderer,PerformerSurfaceBindingAsset.RequiredBindingCount,0x504c4159+index,prefix+"Bindings.asset");
   profile.ConfigureParticleBodyAssets(style.ParticleBodyVfxAsset,surface); EditorUtility.SetDirty(profile);
   var particles=renderer.gameObject.AddComponent<PerformerParticleBody>();
   var particleData=new SerializedObject(particles);
   particleData.FindProperty("targetRenderer").objectReferenceValue=renderer;
   particleData.FindProperty("surfaceBindings").objectReferenceValue=surface;
   particleData.FindProperty("visualEffectAsset").objectReferenceValue=style.ParticleBodyVfxAsset;
   particleData.ApplyModifiedPropertiesWithoutUndo();
   var rig=renderer.gameObject.AddComponent<PerformerDissolveRig>();
   var rigData=new SerializedObject(rig);
   rigData.FindProperty("targetRenderer").objectReferenceValue=renderer;
   rigData.FindProperty("particleBody").objectReferenceValue=particles;
   rigData.ApplyModifiedPropertiesWithoutUndo();
   var part=new PlayerBodyDissolvePart { rig=rig,profile=profile,renderer=renderer,visibleMaterials=originals };
   part.Validate(); return part;
  }
  static Material ConvertMaterial(Material original) {
   if(original==null) throw new InvalidOperationException("Prepared male body contains an unassigned material.");
   string path=Folder+"/"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original))+".mat";
   var saved=AssetDatabase.LoadAssetAtPath<Material>(path); if(saved!=null) return saved;
   string family=original.shader.name;
   string graph;
   if(AssetDatabase.GetAssetPath(original.shader)=="Assets/Production/Player/Shaders/PartnerShell.shadergraph") graph="Player Shell Dissolve.shadergraph";
   else if(family=="Daz3D/Wet") graph="Wet Dissolve.shadergraph";
   else if(family=="Daz3D/uDTU HDRP.SSS") graph="uDTU HDRP SSS Dissolve.shadergraph";
   else if(family=="Daz3D/uDTU HDRP.Hair") graph="uDTU HDRP Hair Dissolve.shadergraph";
   else if(family=="Daz3D/uDTU HDRP.Specular" || family=="Daz3D/uDTU HDRP.Metallic") {
    string name=family.EndsWith("Specular",StringComparison.Ordinal) ? "Specular" : "Metallic";
    graph="uDTU HDRP "+name+(original.HasProperty("_SurfaceType") && original.GetFloat("_SurfaceType")>.5f ? " Transparent" : "")+" Dissolve.shadergraph";
   } else throw new InvalidOperationException("No existing dissolve family for player body material "+original.name+": "+family);
   var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/DazPose/Effects/Dissolve/Shaders/"+graph);
   if(shader==null) throw new InvalidOperationException("Missing player body dissolve shader "+graph);
   var converted=new Material(original) { name=original.name+" (player dissolve)",shader=shader };
   AssetDatabase.CreateAsset(converted,path); return converted;
  }
 }
}