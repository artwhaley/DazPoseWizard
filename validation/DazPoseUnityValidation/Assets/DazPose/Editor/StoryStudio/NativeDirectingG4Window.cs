using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DazPose.Editor.Studio {
 /// <summary>Art-operated evidence collector for the ACTUAL Studio; never fixed camera/pose choreography.</summary>
 public sealed class NativeDirectingG4Window : EditorWindow {
  const string Scratch="Assets/Production/Story/Cards/NativeG4Scratch.dazstory";
  const string Index="Assets/Resources/DazPose/GlobalStoryCards.asset";
  [Serializable] sealed class SaveObservation {
   public string path,sha;public bool baselineRestored;public NativeDirectingG1Window.Difference[] forbidden;
  }
  [Serializable] sealed class Report {
   public string result,revision,codeHash,unity,project,scene,exception,indexBeforeScratch,indexWithScratch,indexAfterRemoval;
   public string[] cases,confirmed;public NativeDirectingG1Window.Snapshot baseline,current;
   public NativeDirectingG1Window.Difference[] differences;public NativeStudioObservation[] visits;
   public SaveObservation[] saves;public bool forcedCleanEquivalent,stopDirtyPreserved,scratchDiscovered,scratchRemoved;
  }
  static readonly string[] Cases={
   "Direct early/late cue selection including Resolution Sassy: responsive without story traversal",
   "Dialogue with identical visual state: zero additional pose samples or lease rebuilds",
   "Rapid source selections: final row/inspector/shot match; no stale callbacks",
   "Standing/seated pose replacement: same marks/camera; Undo/Redo; sparse pose baseline",
   "Release-only Move/Rotate: smooth handles, correct release shot; optional Live drag",
   "StartAt authored full rotation; Track actual Lara focus after pose; no invented aim",
   "Default SitAt baked root and custom seated hip alignment; Approach independent",
   "Skipped conditional/loop: no variables/choices/RNG/waits; local uncertainty visible",
   "Dynamic global cards selectable/editable; missing incoming fields labelled; explicit assignments resolve",
   "Ctrl+S and forced clean save resume newest shot; no preview fields serialized",
   "Unsaved authored mark survives Stop; saved marks/assignments survive reopen",
   "Close/source/scene/reload restore baseline; ownership conflicts stop safely",
   "Wardrobe/heels/blendshapes/materials and prefab overrides preserved",
   "Scratch card discovered, ambiguity visible, direct cue editing; removal restores isolated index"
  };
  [SerializeField] bool[] confirmed=new bool[14];
  NativeStageContext context;[SerializeField] NativeDirectingFixture.FixtureIdentity fixture;
  [SerializeField] NativeDirectingG1Window.Snapshot baseline;
  [SerializeField] string baselineText,status="Stop G3, then Begin G4 before starting Studio.",failure,folder,indexBefore,indexAdded,indexRemoved;
  [SerializeField] bool cleanEquivalent,dirtyPreserved,scratchDiscovered,scratchRemoved;
  [SerializeField] int forcedClean;
  [SerializeField] List<SaveObservation> saves=new List<SaveObservation>();
  [SerializeField] List<NativeStudioObservation> visits=new List<NativeStudioObservation>();
  Vector2 scroll;
  SceneDirectorStudioWindow Studio=>Resources.FindObjectsOfTypeAll<SceneDirectorStudioWindow>().FirstOrDefault();
  [MenuItem("DazPose/Story/Native Directing Diagnostics/G4 Full Regression Export")]
  public static void Open(){var w=GetWindow<NativeDirectingG4Window>("G4 Full Regression");w.minSize=new Vector2(480,460);w.Show();}
  void OnEnable(){EditorApplication.delayCall+=RestoreRecording;EditorSceneManager.sceneSaved+=Saved;SceneDirectorStudioWindow.DiagnosticObserved+=Observed;}
  void RestoreRecording(){if(this!=null&&baseline!=null)try{context=NativeStageContext.Resolve(NativeStageContext.FindHost());}catch(Exception e){status="Recording retained; reopen disposable scene before export: "+e.Message;}}
  void OnDisable(){EditorApplication.delayCall-=RestoreRecording;EditorSceneManager.sceneSaved-=Saved;SceneDirectorStudioWindow.DiagnosticObserved-=Observed;}
  void Begin() {
   fixture=NativeDirectingFixture.Require();
   if(AnimationMode.InAnimationMode()||Studio!=null&&Studio.IsDirecting)throw new InvalidOperationException("Stop G2/G3/Studio before Begin G4.");
   context=NativeStageContext.Resolve(NativeStageContext.FindHost());
   if(context.Scene.isDirty)throw new InvalidOperationException("Save your deliberate fixture edits before Begin G4.");
   folder=null;indexBefore=indexAdded=indexRemoved=null;scratchDiscovered=scratchRemoved=false;
   baseline=NativeDirectingG1Window.Capture(context);baselineText=File.ReadAllText(context.Scene.path);
   saves.Clear();visits.Clear();failure=null;cleanEquivalent=false;dirtyPreserved=false;forcedClean=0;confirmed=new bool[Cases.Length];
   Persist();SceneDirectorStudioWindow.Open();status="G4 recording actual Studio. Click source cues there. This panel never chooses framing.";
  }
  void Observed(NativeStudioObservation value) {
   if(baseline==null)return;
   visits.Add(value);
   if(folder!=null)File.AppendAllText(Path.Combine(folder,"visits.jsonl"),JsonUtility.ToJson(value)+"\n");
  }
  [Serializable] sealed class OverrideRow {public string target="",propertyPath="",value="",objectReference="";}
  [Serializable] sealed class Overrides {public OverrideRow[] properties=Array.Empty<OverrideRow>();}
  string WithoutAuthoredFocus(string json) {
   var source=PrefabUtility.GetCorrespondingObjectFromSource(context.Host.laraFocus);
   if(source==null)return json;
   string id=GlobalObjectId.GetGlobalObjectIdSlow(source).ToString();
   var data=JsonUtility.FromJson<Overrides>(json);
   data.properties=data.properties.Where(p=>p.target!=id||!(p.propertyPath.StartsWith("m_LocalPosition.",StringComparison.Ordinal)||p.propertyPath.StartsWith("m_LocalRotation.",StringComparison.Ordinal)||p.propertyPath.StartsWith("m_LocalScale.",StringComparison.Ordinal)||p.propertyPath.StartsWith("m_LocalEulerAnglesHint.",StringComparison.Ordinal))).ToArray();
   return JsonUtility.ToJson(data);
  }
  bool Forbidden(NativeDirectingG1Window.Difference difference) {
   if(difference.id==GlobalObjectId.GetGlobalObjectIdSlow(context.Host.laraFocus).ToString())return false; // authored head-local target
   if(!difference.id.StartsWith("prefab:",StringComparison.Ordinal))return true;
   bool actorPrefab=new[]{context.Lara.gameObject,context.Body.gameObject,context.PlayerRoot.gameObject}.Any(g=>{
    var prefab=PrefabUtility.GetOutermostPrefabInstanceRoot(g);return prefab!=null&&difference.id=="prefab:"+GlobalObjectId.GetGlobalObjectIdSlow(prefab);
   });
   if(!actorPrefab)return false;
   if(difference.before=="<absent>"||difference.after=="<absent>")return true;
   return WithoutAuthoredFocus(difference.before)!=WithoutAuthoredFocus(difference.after);
  }
  void Saved(Scene scene) {
   if(baseline==null||context?.Host==null||scene!=context.Scene)return;
   try{var now=NativeDirectingG1Window.Capture(context);var differences=NativeDirectingG1Window.Compare(baseline,now).Where(Forbidden).ToArray();
    saves.Add(new SaveObservation{path=scene.path,sha=now.sceneSha256,baselineRestored=differences.Length==0,forbidden=differences});
    if(differences.Length>0){failure="Saved preview-owned fields differ from baseline; stop and report G4 FAIL.";Studio?.StopDirecting();}
    Persist();
   }catch(Exception e){failure=e.ToString();Persist();}
  }
  void ForceSave() {
   NativeDirectingFixture.Require();if(baseline==null||Studio==null||!Studio.IsDirecting)throw new InvalidOperationException("Begin G4, then select a source cue in Studio first.");
   bool clean=!context.Scene.isDirty;
   if(!EditorSceneManager.SaveScene(context.Scene))throw new InvalidOperationException("Disposable save cancelled or failed.");
   if(clean&&forcedClean==0){forcedClean++;cleanEquivalent=NativeSceneSaveComparison.Equivalent(baselineText,File.ReadAllText(context.Scene.path));
    if(!cleanEquivalent)failure="Forced clean scene serialization changed meaningful YAML; report G4 FAIL.";}
   Persist();status="Saved through the normal preview suspension. Return to Studio and check the same shot resumes.";
  }
  void Stop() {
   bool dirty=context!=null&&context.Host!=null&&context.Scene.isDirty;
   Studio?.StopDirecting();
   dirtyPreserved=dirty&&context.Scene.isDirty;Persist();status="Stopped Studio. Deliberate authored edits remain; inspect baseline and reopen before Export.";
  }
  void AddScratch() {
   fixture=NativeDirectingFixture.Require();if(baseline==null)throw new InvalidOperationException("Begin G4 first.");
   if(File.Exists(Scratch))throw new InvalidOperationException("Scratch already exists; inspect/remove that fixture card first.");
   Stop();indexBefore=NativeDirectingFixture.HashFile(Index);
   File.WriteAllText(Scratch,"card NativeG4Scratch\ntags [Warmup]\nprogress 20\nweight 1\nrepeat unlimited\ncooldown 1\nrequires World.Posture == Sitting\n{\n await Lara.SitAt(Lounger)\n await Lara.PersistentSitPose(G4ScratchPose)\n if Session.Mood > 0 { await Lara.PersistentSitPose(G4ScratchPose) }\n wait 1s\n await Lara.SitAt(Lounger)\n await Lara.PersistentSitPose(G4ScratchPose)\n}\n");
   AssetDatabase.ImportAsset(Scratch,ImportAssetOptions.ForceSynchronousImport);
   var bindings=SourceBindingReconciler.ReconcileAsset(Scratch,AssetDatabase.AssetPathToGUID(Scratch));
   var sequence=context.Scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StoryProgramSequence>(true)).Single(s=>s.Bindings==context.Host);
   var pose=sequence.CompiledSource.content.Pose("Lounging");bindings.poses.Single().value=pose.value;bindings.poses.Single().posture=pose.posture;
   EditorUtility.SetDirty(bindings);AssetDatabase.SaveAssetIfDirty(bindings);AssetDatabase.ImportAsset(Scratch,ImportAssetOptions.ForceSynchronousImport);
   GlobalStoryCardIndexBuilder.Reindex();indexAdded=NativeDirectingFixture.HashFile(Index);
   scratchDiscovered=Resources.Load<GlobalStoryCardCatalog>("DazPose/GlobalStoryCards").sources.Any(s=>AssetDatabase.GetAssetPath(s)==Scratch);
   Persist();status="Scratch globally published. In Studio select NativeG4Scratch directly in the cue list. SitAt establishes its seat; the wait after the skipped condition warns about ambiguous pose/placement; the following SitAt and pose resolve it.";
  }
  void RemoveScratch() {
   NativeDirectingFixture.Require();Stop();if(indexBefore==null)throw new InvalidOperationException("No scratch baseline recorded in this recording. Preserve files and report before manual cleanup.");
   if(File.Exists(Scratch))AssetDatabase.DeleteAsset(Scratch);
   string companion=SourceBindingReconciler.CompanionPath(Scratch);if(File.Exists(companion))AssetDatabase.DeleteAsset(companion);
   GlobalStoryCardIndexBuilder.Reindex();indexRemoved=NativeDirectingFixture.HashFile(Index);scratchRemoved=indexRemoved==indexBefore;Persist();
   status="Scratch removed. Isolated index baseline hash restored: "+scratchRemoved+". Restart Studio after the import/revision change.";
  }
  void Persist() {
   if(baseline==null)return;
   if(folder==null){folder=Path.Combine(NativeDirectingFixture.ProjectPath,"Temp/NativeDirectingDiagnostics","G4_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"));Directory.CreateDirectory(folder);}
   File.WriteAllText(Path.Combine(folder,"baseline-scene.unity.txt"),baselineText);
   Export(false);
  }
  void Export(bool updateContext) {
   if(baseline==null)throw new InvalidOperationException("Begin G4 first.");
   if(updateContext||context?.Host==null)context=NativeStageContext.Resolve(NativeStageContext.FindHost());
   var now=NativeDirectingG1Window.Capture(context);var differences=NativeDirectingG1Window.Compare(baseline,now);
   bool restored=!differences.Any(Forbidden),stopped=Studio==null||!Studio.IsDirecting;
   bool failed=failure!=null||saves.Any(s=>!s.baselineRestored);
   string result=failed?"FAIL":stopped&&restored&&confirmed.All(x=>x)&&cleanEquivalent&&dirtyPreserved&&saves.Count>=2&&scratchDiscovered&&scratchRemoved?"PASS":"PARTIAL";
   var report=new Report{result=result,revision=fixture.sourceRevision,codeHash=fixture.codeHash,unity=Application.unityVersion,project=NativeDirectingFixture.ProjectPath,scene=context.Scene.path,
    exception=failure,cases=Cases,confirmed=Cases.Where((_,i)=>confirmed[i]).ToArray(),baseline=baseline,current=now,differences=differences,visits=visits.ToArray(),saves=saves.ToArray(),forcedCleanEquivalent=cleanEquivalent,stopDirtyPreserved=dirtyPreserved,
    indexBeforeScratch=indexBefore,indexWithScratch=indexAdded,indexAfterRemoval=indexRemoved,scratchDiscovered=scratchDiscovered,scratchRemoved=scratchRemoved};
   File.WriteAllText(Path.Combine(folder,"report.json"),JsonUtility.ToJson(report,true));
   File.WriteAllText(Path.Combine(folder,"report.txt"),"G4 "+result+"\nActual Studio, no diagnostic framing overrides. Art operates Unity.\nSource: "+fixture.sourceRevision+"\nCode SHA256: "+fixture.codeHash+"\nStopped/restored: "+stopped+"/"+restored+"\nClean forced save: "+cleanEquivalent+"; saves: "+saves.Count+"\nUnsaved dirty edit preserved: "+dirtyPreserved+"\nConfirmed: "+confirmed.Count(x=>x)+"/"+Cases.Length+"\nScratch discovered / removed index restored: "+scratchDiscovered+" / "+scratchRemoved+"\nException: "+failure+"\n\n"+string.Join("\n",Cases.Select((c,i)=>(confirmed[i]?"PASS (Art): ":"NOT RUN: ")+c))+"\n\nExact snapshots, visits, camera composition, visual index timings and differences: report.json\n");
   File.WriteAllText(Path.Combine(folder,"current-scene.unity.txt"),File.ReadAllText(context.Scene.path));
  }
  void Guard(Action action){try{action();}catch(Exception e){status=e.Message;Debug.LogException(e);}Repaint();}
  void OnGUI() {
   scroll=EditorGUILayout.BeginScrollView(scroll);EditorGUILayout.LabelField("G4 — ACTUAL Studio acceptance",EditorStyles.boldLabel);
   EditorGUILayout.HelpBox("Use the existing isolated disposable FirstContact scene. This panel records Studio and saves; it never supplies camera targets or runs Play. Screenshots are not required. Checks remain NOT RUN until you confirm them.",MessageType.Info);
   if(GUILayout.Button("1. Begin G4 — capture baseline, open Studio"))Guard(Begin);
   if(GUILayout.Button("Open actual Scene Director Studio"))SceneDirectorStudioWindow.Open();
   if(GUILayout.Button("Force SaveScene — disposable scene only"))Guard(ForceSave);
   if(GUILayout.Button("Stop Studio — preserve unsaved authored mark"))Guard(Stop);
   if(GUILayout.Button("Add isolated scratch global card"))Guard(AddScratch);
   if(GUILayout.Button("Remove scratch and check isolated index baseline"))Guard(RemoveScratch);
   for(int i=0;i<Cases.Length;i++)confirmed[i]=EditorGUILayout.ToggleLeft("Art: "+Cases[i],confirmed[i]);
   if(GUILayout.Button("Export G4 report"))Guard(()=>{Persist();Export(true);status="Report: "+folder;});
   if(folder!=null){EditorGUILayout.SelectableLabel(folder,EditorStyles.textField,GUILayout.Height(42));if(GUILayout.Button("Show report folder"))EditorUtility.RevealInFinder(folder);}
   EditorGUILayout.HelpBox(status,MessageType.Info);EditorGUILayout.EndScrollView();
  }
 }
}
