using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Performer;
using DazPose.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DazPose.Editor.Studio
{
    /// <summary>Art-operated fixed seating oracles before N06 cue transport. Uses the same real-scene lease as G2.</summary>
    public sealed class NativeDirectingG3Window : EditorWindow
    {
        [Serializable] sealed class SaveRow
        {public string sceneSha256,endpoint;public bool suspended,actorsMatch,actorScaleProbe;public NativeDirectingG1Window.Difference[] differences;}
        [Serializable] sealed class Report
        {
            public string gate="G3",version="N05-G3-v1",nativeResult,timestampUtc,sourceRevision,codeHash,projectPath,scenePath,sourceTrace,exception;
            public string scope="Fixed source-bound seating endpoints. No full interpreter/Director replay, timing, Play, hardware or executor Unity operation.";
            public bool[] seen,accepted;
            public bool liveObserved,saveUndoStopObserved,rotatedScaledChairObserved,actorScaleSampled,approachIndependent,cleanPreviewPreserved,cleanForcedSaveEquivalent,unsavedStopPreserved,restorationMatches;
            public int applications,forcedSaves;
            public NativeSeatObservation[] samples;
            public SaveRow[] saves;
            public NativeDirectingG1Window.Snapshot baseline,current;
            public NativeDirectingG1Window.Difference[] restoredDifferences;
        }
        NativeStageContext context;
        NativeSceneStage stage;
        NativeDirectingFixture.FixtureIdentity fixture;
        StoryProgramSequence sequence;
        PerformerSeat seat;
        NativeSeatedProjection projection;
        NativeSeatObservation latest,collected;
        NativeDirectingG1Window.Snapshot baseline;
        readonly List<NativeSeatObservation> samples=new List<NativeSeatObservation>();
        readonly List<SaveRow> saves=new List<SaveRow>();
        readonly bool[] seen=new bool[3],accepted=new bool[3];
        bool track=true,scaleProbe,liveObserved,saveUndoStopObserved,rotatedScaledChairObserved,actorScaleSampled,approachIndependent,cleanPreviewPreserved,cleanForcedSaveEquivalent,unsavedStopPreserved;
        int mode,applications,forcedSaves;
        string stableKey,approachKey,sourceHash,sourcePath,sourceTrace,baselineText,forcedText,reportPath,failure,status="Start once, then use A / B / C to compare the three real seating endpoints.";
        double nextWatch;
        Vector2 scroll;

        [MenuItem("DazPose/Story/Native Directing Diagnostics/G3 Seating Proof")]
        public static void Open()
        {var w=GetWindow<NativeDirectingG3Window>("G3 Seating Proof");w.minSize=new Vector2(570,640);w.Show();}
        void OnEnable()
        {EditorApplication.update+=Tick;EditorSceneManager.sceneSaved+=Saved;AssemblyReloadEvents.beforeAssemblyReload+=Stop;}
        void OnDisable()
        {EditorApplication.update-=Tick;EditorSceneManager.sceneSaved-=Saved;AssemblyReloadEvents.beforeAssemblyReload-=Stop;Stop();}
        void ResolveSource()
        {
            var rows=context.Scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<StoryProgramSequence>(true)).Where(s=>s.Bindings==context.Host).ToArray();
            if(rows.Length!=1 || rows[0].CompiledSource==null || !rows[0].CompiledSource.IsCompiled || rows[0].CompiledSource.content==null)
                throw new InvalidOperationException("Game Session needs one compiled, source-bound FirstContact sequence. Let imports finish.");
            sequence=rows[0];sourcePath=AssetDatabase.GetAssetPath(sequence.CompiledSource);
            string text=File.ReadAllText(sourcePath);
            sourceTrace=NativeSeatingSourceOracle.Describe(sourcePath,text);
            sourceHash=AssetDatabase.GetAssetDependencyHash(sourcePath).ToString();
            var location=context.Host.RequireLocation("Lounger");seat=location.seat;
            if(seat==null)throw new InvalidOperationException("Assign the saved Lounger location's PerformerSeat; its placement mark alone is not a seat.");
            if(seat.DefaultStyle!=PerformerSeatedStyle.CrossLegs)throw new InvalidOperationException("This fixed FirstContact proof expects the saved Lounger default CrossLegs style. Report an intentional style change before changing this oracle.");
        }
        NativeSeatedProjection MakeProjection()
        {
            var pose=sequence.CompiledSource.content.Pose("Lounging");
            return new NativeSeatedProjection(seat,context.Host.RequireLocation("camera_start").placement,(NativeSeatCommand)mode,
                mode==0?null:pose.value,pose.posture,track,true,scaleProbe?new Vector3(1.1f,.95f,1.2f):Vector3.one);
        }
        void Start()
        {
            if(stage!=null && stage.Ready){mode=0;Apply();return;}
            Stop();fixture=NativeDirectingFixture.Require();
            if(SceneManager.sceneCount!=1)throw new InvalidOperationException("Use only the disposable G1_FirstContact scene for G3.");
            context=NativeStageContext.Resolve(NativeStageContext.FindHost());ResolveSource();
            if(context.Scene.isDirty)throw new InvalidOperationException("Save your outstanding fixture mark edits with Ctrl+S before starting G3's clean-save proof.");
            var cameras=context.Scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Where(c=>c.enabled && c.gameObject.activeInHierarchy && c.targetTexture==null && c.targetDisplay==context.Camera.targetDisplay).ToArray();
            if(cameras.Length!=1 || cameras[0]!=context.Camera)throw new InvalidOperationException("The bound MainCamera must already be the only active camera on its Game display.");
            mode=0;track=true;scaleProbe=false;projection=MakeProjection();NativeSeatEndpoint.Resolve(context,projection);
            if(!EditorUtility.DisplayDialog("G3 seating — isolated scene only","Project: "+NativeDirectingFixture.ProjectPath+"\nScene: "+context.Scene.path+"\n\nSamples actual Lara in three source-bound seating endpoints. Save restores preview properties first, then resumes. Chair edits are yours; temporary actor-scale probe is restored. No Play, rebake or screenshots.","Start G3","Cancel"))return;
            baseline=NativeDirectingG1Window.Capture(context);baselineText=File.ReadAllText(context.Scene.path);forcedText=null;
            Array.Clear(seen,0,3);Array.Clear(accepted,0,3);samples.Clear();saves.Clear();latest=collected=null;
            applications=forcedSaves=0;failure=null;liveObserved=saveUndoStopObserved=rotatedScaledChairObserved=actorScaleSampled=approachIndependent=cleanForcedSaveEquivalent=unsavedStopPreserved=false;
            stage=new NativeSceneStage(context);Apply();
            var now=NativeDirectingG1Window.Capture(context);cleanPreviewPreserved=baseline.sceneDirty==now.sceneDirty && baseline.undoGroup==now.undoGroup;
            if(!cleanPreviewPreserved)throw new InvalidOperationException("G3 preview changed scene dirty state or Undo group. Stop and report FAIL.");
        }
        string StableKey()
        {
            var root=context.Lara.transform;
            string Matrix(Transform t)=>t==null?"none":t.localToWorldMatrix.ToString("R");
            return Matrix(seat.SeatAnchor)+Matrix(context.Host.RequireLocation("camera_start").placement)+Matrix(root.parent)
                +root.localScale.ToString("R")+context.Host.laraFocus.localPosition.ToString("R")+context.Host.laraFocus.localRotation.ToString("R")
                +EditorJsonUtility.ToJson(seat)+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(seat.SeatingProfile));
        }
        string ApproachKey()=>seat.ApproachAnchor.localToWorldMatrix.ToString("R");
        void Apply()
        {
            if(mode==3)
            {
                stage.Apply(new NativeStandingProjection(context.Host.RequireLocation("MainPosition1").placement,context.Host.RequireLocation("camera_start").placement,context.Lara.InitialPose,NativeStandingEndpoint.StandingDissolve,track,true));
                latest=null;status="Standing MainPosition1 endpoint — yaw + configured initialPose. Fixed source endpoint, not chronological transport.";
            }
            else
            {
                projection=MakeProjection();
                try {stage.Apply(projection);}
                finally {if(stage.LastSeating!=null)latest=stage.LastSeating;}
                Collect();status="LIVE: "+new[]{"A — Warmup SitAt(Lounger), default CrossLegs","B — Warmup PersistentSitPose(Lounging), live hip alignment","C — Main seated DissolveTo(Lounger, Lounging), live hip alignment"}[mode]+". Native handles update this endpoint without Save or Start.";
            }
            applications++;stableKey=StableKey();approachKey=ApproachKey();Repaint();
        }
        void Collect()
        {
            if(stage==null || stage.LastSeating==null || ReferenceEquals(collected,stage.LastSeating))return;
            latest=collected=stage.LastSeating;
            seen[(int)projection.Command]=true;actorScaleSampled|=scaleProbe;
            samples.Add(latest);if(samples.Count>256)samples.RemoveAt(0);
        }
        void Tick()
        {
            if(stage==null)return;
            if(!stage.Ready){status=stage.Status;if(stage.Failure!=null)failure=stage.Failure;Repaint();return;}
            if(stage.IsSaveSuspended || EditorApplication.isUpdating || EditorApplication.isCompiling || EditorApplication.timeSinceStartup<nextWatch)return;
            nextWatch=EditorApplication.timeSinceStartup+.04;
            Guard(()=>
            {
                NativeStageContext.Resolve(context.Host);Collect();
                if(AssetDatabase.GetAssetDependencyHash(sourcePath).ToString()!=sourceHash)throw new InvalidOperationException("FirstContact source/bindings changed. Stop and restart this fixed G3 proof.");
                string currentStable=StableKey(),currentApproach=ApproachKey();
                if(currentStable==stableKey && currentApproach==approachKey)return;
                bool approachOnly=currentStable==stableKey && currentApproach!=approachKey && latest!=null && mode<3;
                var previous=latest;Apply();
                if(approachOnly)
                {
                    if(NativeSeatingMath.Error(previous.actorRoot,latest.actorRoot)>.00001f || Quaternion.Angle(previous.actorRotation,latest.actorRotation)>.001f)
                        throw new InvalidOperationException("Approach-only edit moved the settled actor. G3 FAIL.");
                    approachIndependent=true;
                }
            });
        }
        void SelectAnchor(int which)
        {
            Transform target=which==0?seat.SeatAnchor:which==1?seat.ApproachAnchor:which==2?seat.transform:which==3?context.Host.RequireLocation("Lounger").focus:context.Host.RequireLocation("camera_start").placement;
            if(target==null)throw new InvalidOperationException("That authored focus/anchor is not assigned on Lounger.");
            Selection.activeGameObject=target.gameObject;EditorGUIUtility.PingObject(target.gameObject);
            status="Selected "+NativeStageContext.ObjectPath(target)+(which==3?". This fixed cue tracks Lara; the seat focus is not its camera target.":". Use Scene W / E / R handles; G3 keeps the pose live.");
        }
        bool IsActorDifference(NativeDirectingG1Window.Difference d)
        {
            if(!d.id.StartsWith("prefab:",StringComparison.Ordinal))return true;
            foreach(var root in new[]{context.Lara.gameObject,context.Body.gameObject,context.PlayerRoot.gameObject})
            {var prefab=PrefabUtility.GetOutermostPrefabInstanceRoot(root);if(prefab!=null && d.id=="prefab:"+GlobalObjectId.GetGlobalObjectIdSlow(prefab))return true;}
            return false;
        }
        void Saved(Scene scene)
        {
            if(baseline==null || context==null || context.Host==null || context.Scene!=scene)return;
            try
            {
                var now=NativeDirectingG1Window.Capture(context);var diff=NativeDirectingG1Window.Compare(baseline,now).Where(IsActorDifference).ToArray();
                bool suspended=stage==null || !stage.Ready || stage.IsSaveSuspended && !AnimationMode.InAnimationMode();
                saves.Add(new SaveRow{sceneSha256=now.sceneSha256,endpoint=mode==3?"Standing":((NativeSeatCommand)mode).ToString(),actorScaleProbe=mode<3 && scaleProbe,suspended=suspended,actorsMatch=diff.Length==0,differences=diff});
                if(!suspended || diff.Length!=0)throw new InvalidOperationException("G3 save snapshot differs from actor/camera/bone/renderer/prefab baseline. Stop and report FAIL.");
            }
            catch(Exception error){failure=error.ToString();Stop();Debug.LogException(error);}
        }
        void ForceSave()
        {
            NativeDirectingFixture.Require();if(stage==null || !stage.Ready)throw new InvalidOperationException("Start G3 before its forced save.");
            bool clean=!context.Scene.isDirty;
            if(!EditorSceneManager.SaveScene(context.Scene))throw new InvalidOperationException("Disposable save failed/cancelled; baseline restoration was requested.");
            if(clean && !cleanForcedSaveEquivalent)
            {
                forcedText=File.ReadAllText(context.Scene.path);cleanForcedSaveEquivalent=NativeSceneSaveComparison.Equivalent(baselineText,forcedText);
                if(!cleanForcedSaveEquivalent)throw new InvalidOperationException("Clean G3 save changed serialized data beyond empty-string/line-ending normalization. Export the report for repair.");
            }
            forcedSaves++;status="Forced save completed; seated preview resumes automatically. Save observations: "+saves.Count;
        }
        void Stop()
        {
            if(stage==null)return;
            bool dirty=context!=null && context.Host!=null && context.Scene.isDirty;
            Collect();try{stage.Dispose();}finally{stage=null;stableKey=approachKey=null;}
            unsavedStopPreserved|=dirty && context!=null && context.Host!=null && context.Scene.isDirty;
            status="STOPPED: actor, pose, camera and temporary actor scale restored; authored chair/anchor changes retained.";
        }
        void Export()
        {
            if(baseline==null)throw new InvalidOperationException("Start G3 first.");
            if(context==null || context.Host==null)context=NativeStageContext.Resolve(NativeStageContext.FindHost());
            var now=NativeDirectingG1Window.Capture(context);var diff=NativeDirectingG1Window.Compare(baseline,now);bool matches=!diff.Any(IsActorDifference);
            bool stopped=stage==null || !stage.Ready;
            bool allSeatFamiliesSaved=Enumerable.Range(0,3).All(i=>saves.Any(row=>row.endpoint==((NativeSeatCommand)i).ToString() && row.suspended && row.actorsMatch));
            bool scaleSaved=saves.Any(row=>row.actorScaleProbe && row.suspended && row.actorsMatch);
            bool pass=allSeatFamiliesSaved && scaleSaved && seen.All(v=>v) && accepted.All(v=>v) && liveObserved && saveUndoStopObserved && rotatedScaledChairObserved && actorScaleSampled && approachIndependent
                && cleanPreviewPreserved && cleanForcedSaveEquivalent && unsavedStopPreserved && matches && stopped && saves.Count>=2 && forcedSaves>0;
            string result=failure!=null || saves.Any(s=>!s.suspended || !s.actorsMatch)?"FAIL":pass?"PASS":"PARTIAL";
            reportPath=Path.Combine(NativeDirectingFixture.ProjectPath,"Temp/NativeDirectingDiagnostics","G3_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"));Directory.CreateDirectory(reportPath);
            var report=new Report{nativeResult=result,timestampUtc=DateTime.UtcNow.ToString("O"),sourceRevision=fixture.sourceRevision,codeHash=fixture.codeHash,
                projectPath=NativeDirectingFixture.ProjectPath,scenePath=context.Scene.path,sourceTrace=sourceTrace,exception=failure,seen=seen,accepted=accepted,
                liveObserved=liveObserved,saveUndoStopObserved=saveUndoStopObserved,rotatedScaledChairObserved=rotatedScaledChairObserved,actorScaleSampled=actorScaleSampled,approachIndependent=approachIndependent,
                cleanPreviewPreserved=cleanPreviewPreserved,cleanForcedSaveEquivalent=cleanForcedSaveEquivalent,unsavedStopPreserved=unsavedStopPreserved,restorationMatches=matches,
                applications=applications,forcedSaves=forcedSaves,samples=samples.ToArray(),saves=saves.ToArray(),baseline=baseline,current=now,restoredDifferences=diff};
            File.WriteAllText(Path.Combine(reportPath,"report.json"),JsonUtility.ToJson(report,true));File.WriteAllText(Path.Combine(reportPath,"baseline-scene.unity.txt"),baselineText);
            File.WriteAllText(Path.Combine(reportPath,"current-scene.unity.txt"),File.ReadAllText(context.Scene.path));if(forcedText!=null)File.WriteAllText(Path.Combine(reportPath,"clean-forced-save.unity.txt"),forcedText);
            string text=$"G3 {result}\nVersion: N05-G3-v1\nSource: {fixture.sourceRevision}\nCode: {fixture.codeHash}\n{sourceTrace}\nSeen A/B/C: {string.Join("/",seen)}; Art accepted: {string.Join("/",accepted)}\nLive marks: {liveObserved}; save/Undo/Stop/reopen: {saveUndoStopObserved}\nApproach independence: {approachIndependent}; actor-scale probe: {actorScaleSampled}; rotated/scaled chair: {rotatedScaledChairObserved}\nClean preview: {cleanPreviewPreserved}; clean forced-save data: {cleanForcedSaveEquivalent}\nSaved snapshots: {saves.Count}; forced saves: {forcedSaves}; applications: {applications}\nStopped actor/camera baseline: {matches}; unsaved authored dirty retained: {unsavedStopPreserved}\nException: {failure ?? "none"}\nNo screenshots required. Native acceptance is Art's report; no executor Unity operation.\n";
            File.WriteAllText(Path.Combine(reportPath,"report.txt"),text);status="Exported "+result+": "+reportPath;Debug.Log(text+"\n"+reportPath);
        }
        void Guard(Action action)
        {try{action();}catch(Exception error){failure=error.ToString();Stop();status=error.Message;Debug.LogException(error);}Repaint();}
        void Choose(int value){mode=value;Guard(Apply);}
        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);EditorGUILayout.LabelField("G3 v1 — real seating and save",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Same isolated G1_FirstContact scene. Stop G2 first if still active. Fixed source-bound endpoints, no full cue navigation, Play, rebake or screenshots.",MessageType.Info);
            if(GUILayout.Button("1. Start G3 — default SitAt"))Guard(Start);
            using(new EditorGUI.DisabledScope(stage==null || !stage.Ready))
            {
                if(GUILayout.Button("A. Warmup SitAt(Lounger) — default CrossLegs"))Choose(0);
                if(GUILayout.Button("B. Warmup PersistentSitPose(Lounging)"))Choose(1);
                if(GUILayout.Button("C. Main seated DissolveTo(Lounger, Lounging)"))Choose(2);
                if(GUILayout.Button("Standing endpoint — MainPosition1 / initialPose"))Choose(3);
                bool nextTrack=EditorGUILayout.Popup("Camera orientation",track?1:0,new[]{"Use camera_start rotation","Track Lara — overrides mark rotation"})==1;
                bool nextScale=EditorGUILayout.ToggleLeft("Temporary Lara scale probe (X 1.10 / Y 0.95 / Z 1.20; restored on Save/Stop)",scaleProbe);
                if(nextTrack!=track || nextScale!=scaleProbe){track=nextTrack;scaleProbe=nextScale;Guard(Apply);}
                EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Select SeatAnchor"))Guard(()=>SelectAnchor(0));if(GUILayout.Button("Select ApproachAnchor"))Guard(()=>SelectAnchor(1));EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Select chair — rotate/scale"))Guard(()=>SelectAnchor(2));if(GUILayout.Button("Select seat focus"))Guard(()=>SelectAnchor(3));if(GUILayout.Button("Select camera_start"))Guard(()=>SelectAnchor(4));EditorGUILayout.EndHorizontal();
                if(GUILayout.Button("Force SaveScene — disposable scene only"))Guard(ForceSave);
                if(GUILayout.Button("Stop — restore preview, preserve authored chair edits"))Guard(Stop);
            }
            if(latest!=null && mode<3)
            {
                EditorGUILayout.LabelField("Actual hip → SeatAnchor",(latest.hipToSeatMeters*1000).ToString("0.000")+" mm");
                EditorGUILayout.LabelField("Approach / seat yaw difference",latest.facingDisagreementDegrees.ToString("0.0")+" degrees");
                EditorGUILayout.LabelField("Error vs runtime endpoint",(latest.expectedHipResidualMeters*1000).ToString("0.000")+" mm / "+(latest.toleranceMeters*1000).ToString("0.0")+" mm tolerance");
                EditorGUILayout.HelpBox(latest.liveHipAligned?"Lounging aligns the sampled hip to SeatAnchor (1 mm tolerance).":"Default CrossLegs retains its baked placement. Its hip need not equal SeatAnchor; the error is measured against the actual baked endpoint (10 mm tolerance).",MessageType.Info);
                if(!string.IsNullOrEmpty(latest.warning))EditorGUILayout.HelpBox(latest.warning,MessageType.Warning);
            }
            for(int i=0;i<3;i++)accepted[i]=EditorGUILayout.ToggleLeft("Art: "+new[]{"A default CrossLegs looks correct","B persistent Lounging looks correct","C Main seated dissolve looks correct"}[i],accepted[i]);
            liveObserved=EditorGUILayout.ToggleLeft("Art: seat/camera edits update Scene and Game live",liveObserved);
            rotatedScaledChairObserved=EditorGUILayout.ToggleLeft("Art: rotated/scaled chair and temporary Lara scale remain correct",rotatedScaledChairObserved);
            saveUndoStopObserved=EditorGUILayout.ToggleLeft("Art: Save/Undo/Stop/reopen preserve my edits and remove preview values",saveUndoStopObserved);
            if(GUILayout.Button("Export G3 report (after Stop)"))Guard(Export);
            if(stage!=null && stage.Ready)EditorGUILayout.LabelField("Preview",stage.IsSaveSuspended?"Saving baseline — automatic resume pending":"Live — native anchor watcher");
            if(!string.IsNullOrEmpty(reportPath))EditorGUILayout.SelectableLabel(reportPath,GUILayout.Height(36));
            EditorGUILayout.HelpBox(status,failure==null?MessageType.Info:MessageType.Error);EditorGUILayout.EndScrollView();
        }
    }
}
