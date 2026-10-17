using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DazPose.Editor {
 public static class StoryFoundationChecks {
  [Serializable] sealed class Report {
   public string status="FOUNDATION COMPILED";
   public string actualPlayback="Edit Mode check; see full-session playback evidence";
   public int assertions,instructions,dialogue,poseAliases,nestingDepth;
   public string[] missingContent;
  }
  static int assertions;
  static void Require(bool valid,string message) {assertions++;if(!valid)throw new InvalidOperationException(message);}
  public static void Run() {
   if(Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit Mode only.");
   assertions=0;StoryFoundationBuilder.Prepare();
   var scene=EditorSceneManager.OpenScene(StorySceneBuilder.ScenePath);
   var sequence=GameSessionSceneSetup.InScene<StoryProgramSequence>(scene).Single();var asset=sequence.CompiledSource;
   Require(asset!=null && asset.IsCompiled && sequence.Program==null,"First Contact must save the compiled .dazstory source, with JSON inactive.");
   Require(AssetDatabase.GetAssetPath(asset)==StoryFoundationBuilder.SourcePath,"Wrong active source asset.");
   var program=asset.program;var content=asset.content;var bindings=sequence.Bindings;
   Require(program.dialogue.Length==19 && content.dialogue.Count(d=>program.dialogue.Any(p=>p.id==d.id))==19,"All 19 fixed transcripts must be represented and bound.");
   foreach(var line in program.dialogue)Require(content.Dialogue(line.transcript)!=null && line.id==StoryDialogue.IdFor(line.transcript),"Dialogue transcript hash/clip binding differs.");
   Require(program.dialogue.Any(d=>d.transcript.Contains("(giggles)")) && program.dialogue.Any(d=>d.transcript.Contains("*giggles*")),"Authored performance cues were removed.");
   string[] poseNames={"SexyVamp","LeaningForward","CrossedArmsAmused","Lounging","Main1","Sassy"};
   Require(content.poses.Select(p=>p.id).OrderBy(p=>p).SequenceEqual(poseNames.OrderBy(p=>p)),"All six story pose aliases must exist without substitutions.");
   Require(content.poses.All(p=>p.posture==(p.id=="Lounging" ? PlayerPosture.Sitting : PlayerPosture.Standing)),"Pose posture metadata differs from the packet.");
   Require(bindings.locations.Select(l=>l.id).OrderBy(n=>n).SequenceEqual(new[]{"lara_start","player_start","camera_start","stepforward","Lounger","MainPosition1","Bedside"}.OrderBy(n=>n)),"Semantic location declarations differ.");
   Require(bindings.RequireLocation("Lounger").seat!=null,"Lounger must provide both placement and seat.");
   Require(!GameSessionSceneSetup.InScene<Transform>(scene).Any(t=>new[]{"FirstPerformanceVoid","PerformanceMarkers","TeleportAcceptance","view_lara","view_lounge","ToyControl"}.Contains(t.name)),"Unused test/camera movement marks remain in the current authored scene.");
   var host=sequence.GetComponent<PerformanceHost>();Require(host.PlayOnStart && host.Sequence==sequence,"Actual saved host does not start the authored sequence.");
   Require(bindings.playerBody==host.Bindings.Player.BodyPresence && bindings.playerBody.GetComponent<DazPose.UnityValidation.Partner.PartnerContactTarget>()!=null,"Prepared male body/contact binding was lost.");
   var intro=program.RequireEntry("Intro","card");var calls=program.instructions.Skip(intro.start).Take(intro.end-intro.start).Where(i=>i.code==StoryCode.Call).Select(i=>i.name).ToArray();
   Require(calls.SequenceEqual(new[]{"Lara.DissolveIn","Camera.Track","Lara.Say","Lara.LocomoteTo","Lara.PersistentPose","Lara.Say","Lara.MagicShader","Wait","Lara.MagicShader","Lara.Say","Lara.DissolveTo","Lara.Say","Lara.PersistentPose","Lara.Say"}),"Intro command order does not match the exact authored card.");
   string nested="Session.Mood += 5\n";for(int i=0;i<32;i++)nested="if true {\n"+nested+"}\n";
   var deep=DazStoryCompiler.Compile("sequence Nested {\n"+nested+"}\n","deep.dazstory");
   var restored=JsonUtility.FromJson<CompiledStoryProgram>(JsonUtility.ToJson(deep));
   Require(restored.instructions.Length==deep.instructions.Length && restored.instructions.Any(i=>i.code==StoryCode.Store),"32 source blocks did not survive flat Unity serialization.");
   var wire=content.Shader("GlowyWireframe");var regular=content.Shader("Regular");
   Require(!ShaderUtil.ShaderHasError(wire.wireframeMaterial.shader),"Wireframe shader import has compilation errors.");
   CheckMaterials(wire,regular,host.Bindings.Performer.DissolveProfile.LaraRuntimeMaterials[0]);
   string sceneId=host.Bindings.SceneId,actorId=host.Bindings.ActorId;int transforms=GameSessionSceneSetup.InScene<Transform>(scene).Length;
   StoryFoundationBuilder.Prepare();scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();host=GameSessionSceneSetup.InScene<PerformanceHost>(scene).Single();
   Require(sceneId==host.Bindings.SceneId && actorId==host.Bindings.ActorId && transforms==GameSessionSceneSetup.InScene<Transform>(scene).Length,"Refresh duplicated objects or changed tuning identities.");
   sequence=host.GetComponent<StoryProgramSequence>();bindings=sequence.Bindings;content=sequence.CompiledSource.content;program=sequence.CompiledSource.program;
   var report=new Report {assertions=assertions,instructions=program.instructions.Length,dialogue=program.dialogue.Length,poseAliases=content.poses.Length,nestingDepth=32,missingContent=content.Diagnostics(program,bindings)};
   Directory.CreateDirectory("TestOutput/StoryFoundation");File.WriteAllText("TestOutput/StoryFoundation/report.json",JsonUtility.ToJson(report,true));
   Debug.Log("D5_5_FOUNDATION_PASS: "+JsonUtility.ToJson(report));
  }
  static void CheckMaterials(PerformerMagicShaderProfile wire,PerformerMagicShaderProfile normal,Material original) {
   var root=new GameObject("Transient material unit check");
   try {
    var first=root.AddComponent<MeshRenderer>();first.sharedMaterials=new[]{original};var presentation=root.AddComponent<PerformerMagicShaderPresentation>();
    presentation.Apply(wire);Require(first.sharedMaterial!=original && first.sharedMaterial.shader==wire.wireframeMaterial.shader,"Wireframe mode did not acquire its own material.");
    presentation.Apply(normal);Require(first.sharedMaterial==original,"Regular did not restore the exact original material.");
    presentation.Apply(wire);
    var child=new GameObject("New equipped surface");child.transform.SetParent(root.transform,false);var second=child.AddComponent<MeshRenderer>();second.sharedMaterials=new[]{original};
    presentation.RefreshRenderers();Require(first.sharedMaterial!=original && second.sharedMaterial!=original,"Refresh did not include both current equipped renderers.");
    using(var refresh=presentation.BeginWardrobeRefresh()) {
     Require(presentation.Mode==PerformerMaterialMode.Regular && first.sharedMaterial==original && second.sharedMaterial==original,"Wardrobe transitions must temporarily own normal materials.");
    }
    Require(presentation.Mode==PerformerMaterialMode.GlowyWireframe && first.sharedMaterial!=original && second.sharedMaterial!=original,"Wardrobe completion did not refresh the requested visual mode.");
    var cancel=presentation.CaptureRestoration();cancel();Require(first.sharedMaterial==original && second.sharedMaterial==original,"Cancellation failed to restore both original surfaces.");
    presentation.Apply(wire);using(var refresh=presentation.BeginWardrobeRefresh()) {presentation.RestoreRegular();}
    Require(presentation.Mode==PerformerMaterialMode.Regular && first.sharedMaterial==original && second.sharedMaterial==original,"Wardrobe refresh resurrected a cancelled material mode.");
   } finally {UnityEngine.Object.DestroyImmediate(root);}
  }
 }
}