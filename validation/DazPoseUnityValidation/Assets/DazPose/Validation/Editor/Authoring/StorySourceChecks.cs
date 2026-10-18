using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using CrazyMinnow.SALSA;
using DazPose.Game;
using DazPose.UnityValidation.Partner;
using DazPose.Performer;
using DazPose.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DazPose.Editor {
 // Bounded Edit Mode checks for data boundaries only. Never starts a host or enters Play Mode.
 public static class StorySourceChecks {
  [Serializable] sealed class Report {
   public string status="SOURCE COMPLETE — AWAITING USER PLAYBACK";
   public string playMode="NOT RUN";
   public int authoredSteps, savedTargets, savedAssets, assertions;
   public bool roundTrip, atomicStorage, symmetricConflicts, savedScene, idempotentSetup, originalVoidPreserved;
  }
  static bool IsFixture(MonoBehaviour component) {
   if(component==null) return true;
   var type=component.GetType();
   // Historical namespaces do not make these required runtime components test harnesses.
   if(component is PartnerBody || component is PartnerContactTarget || component is DazPose.UnityValidation.DazPoseCharacterState) return false;
   return type.Name.Contains("Acceptance") || type.Name.StartsWith("Remediation",StringComparison.Ordinal)
    || type.Name=="FirstContactPerformance" || type.Name=="FirstPerformanceVoidControls"
    || (type.Namespace ?? "").StartsWith("DazPose.UnityValidation",StringComparison.Ordinal);
  }
  static int assertions;
  static void Require(bool value,string reason) { assertions++; if(!value) throw new InvalidOperationException(reason); }
  static IEnumerable<StoryStep> All(StoryStep[] steps) {
   foreach(var step in steps ?? Array.Empty<StoryStep>()) { yield return step; foreach(var child in All(step.steps)) yield return child; foreach(var child in All(step.elseSteps)) yield return child; }
  }
  static string Hash(string path) { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))); }
  public static void Run() {
   if(File.Exists(StoryFoundationBuilder.SourcePath)) {StoryFoundationChecks.Run();return;}
   if(Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only.");
   assertions=0; var report=new Report(); string before=Hash(GameSessionSceneSetup.VoidPath);
   StorySceneBuilder.BuildOrRefresh();
   var scene=EditorSceneManager.OpenScene(StorySceneBuilder.ScenePath);
   var host=GameSessionSceneSetup.InScene<PerformanceHost>(scene).Single();
   var sequence=host.Sequence as StoryProgramSequence;
   Require(sequence!=null && host.PlayOnStart,"Saved host must own the serialized story and start on Play.");
   var document=StoryProgramSerializer.Parse(sequence.Program.text);
   StoryProgramValidator.Validate(document,sequence.Bindings);
   string canonical=StoryProgramSerializer.ToJson(document);
   var roundtrip=StoryProgramSerializer.Parse(canonical);
   Require(canonical==StoryProgramSerializer.ToJson(roundtrip),"Canonical JSON did not round-trip.");
   var steps=All(document.steps).ToArray(); report.authoredSteps=steps.Length;
   Require(steps.Any(s=>s.id=="remove-commit-underwear") && steps.Any(s=>s.op=="story.checkpoint"),"Deepest authored clothing branch or checkpoint was lost.");
   try { StoryProgramSerializer.Parse("{\"id\":\"missing-header\"}"); throw new InvalidOperationException("Missing schema accepted."); } catch(StoryProgramException) { assertions++; }
   try { StoryProgramSerializer.Parse("{\"schema\":\"dazpose.story\",\"version\":1,\"id\":\"duplicate\",\"steps\":[{\"id\":\"x\",\"op\":\"wait.seconds\",\"steps\":[{\"id\":\"x\",\"op\":\"wait.seconds\"}]}]}"); throw new InvalidOperationException("Duplicate ID accepted."); } catch(StoryProgramException) { assertions++; }
   Require(steps.Single(s=>s.id=="remove-prompt-underwear").args.Single(a=>a.name=="confirm").stringValue=="Ready", "Deepest prompt arguments were lost.");
   string deep="{\"id\":\"leaf\",\"op\":\"wait.seconds\"}";
   for(int i=0;i<12;i++) deep="{\"id\":\"deep-"+i+"\",\"op\":\"flow.repeat\",\"number\":1,\"steps\":["+deep+"]}";
   try { StoryProgramSerializer.Parse("{\"schema\":\"dazpose.story\",\"version\":1,\"id\":\"too-deep\",\"steps\":["+deep+"]}"); throw new InvalidOperationException("Deep JSON was silently accepted."); }
   catch(StoryProgramException) { assertions++; }
   report.roundTrip=true;
   string proof=Path.GetFullPath("TestOutput/StorySourceChecks/"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(proof);
   var profiles=new PlayerProfileService(proof);
   profiles.Update(p=>p.favor=63);
   string saved=File.ReadAllText(profiles.StoragePath);
   Require(new PlayerProfileService(proof).Current.favor==63,"Profile did not persist.");
   Directory.CreateDirectory(profiles.StoragePath+".tmp");
   try { profiles.Update(p=>p.favor=22); throw new InvalidOperationException("Blocked temporary write should fail."); }
   catch(IOException) { assertions++; } catch(UnauthorizedAccessException) { assertions++; }
   Require(profiles.Current.favor==63 && File.ReadAllText(profiles.StoragePath)==saved && profiles.LastSaveDiagnostic!=null,"Failed commit altered the last valid profile.");
   string bad=Path.Combine(proof,"malformed"); Directory.CreateDirectory(bad);
   File.WriteAllText(Path.Combine(bad,"player-profile.json"),"malformed");
   var fallback=new PlayerProfileService(bad);
   Require(fallback.LastLoadDiagnostic!=null && File.ReadAllText(fallback.StoragePath)=="malformed","Corrupt input was not retained with a diagnostic.");
   fallback.Update(p=>p.favor=51);
   Require(Directory.GetFiles(bad,"*.corrupt-*").Length==1,"Explicit recovery commit lost the corrupt backup.");
   report.atomicStorage=true;
   var rope=new EquipmentDefinition { id="rope",identityTags=new[]{"equipment:rope"} };
   var future=new EquipmentDefinition { id="future",identityTags=new[]{"equipment:future"},excludesTags=new[]{"equipment:rope"} };
   Require(!EquipmentCompatibility.AreCompatible(rope,future) && !EquipmentCompatibility.AreCompatible(future,rope),"Future item's exclusions must be symmetric.");
   Require(EquipmentCompatibility.AreCompatible(rope,new EquipmentDefinition { id="ice",identityTags=new[]{"equipment:ice"} }),"No invented conflict for unrelated items.");
   report.symmetricConflicts=true;
   Require(sequence.Bindings.playerBody==host.Bindings.Player.BodyPresence,"Story body and scoped player body disagree.");
   sequence.Bindings.playerBody.ValidateConfiguration();
   var bodyRoot=sequence.Bindings.playerBody.gameObject;
   Require(bodyRoot.GetComponent<PerformerSalsaLipSync>()==null && bodyRoot.GetComponent<Salsa>()==null
    && bodyRoot.GetComponent<QueueProcessor>()==null,"Player visual body retained orphaned speech/lip-sync components.");
   var male=bodyRoot.GetComponent<PartnerBody>(); var contact=bodyRoot.GetComponent<PartnerContactTarget>();
   Require(male!=null && contact!=null && (host.Bindings.ContactTarget as PartnerContactTarget)==contact,"Player body must use the prepared male and be the saved session contact target.");
   Require(contact.IsReadyForContact(false,out string contactReason),"Prepared player contact is invalid: "+contactReason);
   Require(contact.Profile==AssetDatabase.LoadAssetAtPath<PartnerContactProfile>("Assets/Production/Partners/PreparedContact.asset"),"Player body lost the original measured contact profile.");
   Require(sequence.Bindings.playerBody.StartsHidden && sequence.Bindings.playerBody.SurfaceCount==bodyRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,"Player visibility must cover every male body surface and start hidden.");
   Require(!GameSessionSceneSetup.InScene<Transform>(scene).Any(t=>t.name=="FirstPerformanceVoid" || t.name=="PerformanceMarkers" || t.name=="TeleportAcceptance" || t.name=="ToyControl"),"Legacy stage or test markers leaked into the production scene.");
   Require(!GameSessionSceneSetup.InScene<MonoBehaviour>(scene).Any(IsFixture),"Validation component leaked into the production scene: "+string.Join(", ",GameSessionSceneSetup.InScene<MonoBehaviour>(scene).Where(IsFixture).Select(c=>c==null ? "Missing script" : c.GetType().FullName+" on "+c.name)));
   Require(sequence.Bindings.targets.All(b=>b.value!=null) && sequence.Bindings.targets.Count(b=>b.value.parent!=null && b.value.parent.name=="Authoring Marks")==4,"Story must bind exactly four dedicated scene poses plus the two live head targets.");
   var lipSync=host.Bindings.Performer.GetComponent<PerformerSalsaLipSync>();
   string lipSyncFailure="Missing performer lip-sync bridge.";
   Require(lipSync!=null && lipSync.ValidateConfiguration(out lipSyncFailure),"Speaking Lara lip-sync setup is invalid: "+lipSyncFailure);
   Require(host.Bindings.Performer.StartsHidden,"Saved Lara must start hidden.");
   report.savedTargets=sequence.Bindings.targets.Length; report.savedAssets=sequence.Bindings.assets.Length;
   int count=GameSessionSceneSetup.InScene<Transform>(scene).Length;
   string sceneId=host.Bindings.SceneId,actorId=host.Bindings.ActorId;
   StorySceneBuilder.BuildOrRefresh();
   scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
   host=GameSessionSceneSetup.InScene<PerformanceHost>(scene).Single();
   Require(count==GameSessionSceneSetup.InScene<Transform>(scene).Length && sceneId==host.Bindings.SceneId && actorId==host.Bindings.ActorId,"Refresh duplicated objects or changed tuning identity.");
   Require(GameSessionSceneSetup.InScene<StoryPromptService>(scene).Length==1,"Scene must save exactly one story prompt service.");
   report.idempotentSetup=true; report.savedScene=true;
   report.originalVoidPreserved=before==Hash(GameSessionSceneSetup.VoidPath);
   Require(report.originalVoidPreserved,"Original Void changed.");
   Require(!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode,"Play Mode must not run.");
   // Exercise the actual production menu's new-scene path in Edit Mode, including the saved template.
   const string temporary="Assets/__ProductionShapeCheck.unity";
   try {
    var fresh=GameSessionSceneSetup.CopyEnvironment(temporary);
    var freshScene=fresh.gameObject.scene;
    Require(fresh.Bindings.Player.BodyPresence!=null && fresh.Bindings.ContactTarget is PartnerContactTarget,"New production scene lacks the prepared male player/contact binding.");
    Require(!GameSessionSceneSetup.InScene<MonoBehaviour>(freshScene).Any(IsFixture) && !GameSessionSceneSetup.InScene<Transform>(freshScene).Any(t=>t.name=="PerformanceMarkers" || t.name=="FirstPerformanceVoid"),"New production scene inherited test fixtures.");
    fresh.Bindings.Player.BodyPresence.ValidateConfiguration();
    Require(GameSessionSceneSetup.InScene<PerformerSeat>(freshScene).Length==1,"Clean production template lost the authored seat.");
   } finally { EditorSceneManager.OpenScene(StorySceneBuilder.ScenePath); AssetDatabase.DeleteAsset(temporary); }
   var template=EditorSceneManager.OpenScene(ProductionSceneEnvironment.TemplatePath,UnityEditor.SceneManagement.OpenSceneMode.Additive);
   try {
    Require(template.GetRootGameObjects().Select(r=>r.name).OrderBy(n=>n).SequenceEqual(new[]{"Environment","Player","SceneVolumes"}),"Production environment must contain only authored art, volumes and the player view.");
    Require(!GameSessionSceneSetup.InScene<MonoBehaviour>(template).Any(IsFixture),"Template contains a validation controller.");
   } finally { EditorSceneManager.CloseScene(template,true); }
   report.assertions=assertions;
   Directory.CreateDirectory("TestOutput/StorySourceChecks");
   File.WriteAllText("TestOutput/StorySourceChecks/report.json",JsonUtility.ToJson(report,true));
   Debug.Log("D1_D5_STATIC_PASS: "+JsonUtility.ToJson(report));
  }
 }
}