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
    /// <summary>User-operated integrated standing proof. Fixed source-bound endpoints, not full Studio navigation.</summary>
    public sealed class NativeDirectingG2Window : EditorWindow
    {
        [Serializable] sealed class SaveObservation
        {
            public string path,sceneSha256;
            public bool previewSuspended,actorStateMatches;
            public NativeDirectingG1Window.Difference[] differences;
        }
        [Serializable] sealed class Report
        {
            public string gate="G2",diagnosticVersion="N04-G2-v2",result="NOT RUN",timestampUtc,projectPath,scenePath,sourceRevision,codeHash,status,exception;
            public string scope="Fixed source-bound standing fixture; no full cue navigation, gesture audition, seating or runtime execution. Native results require Art confirmation.";
            public string gestureBoundary="Awaited gesture: underlying persistent pose after masked additive layer fades. Non-awaited FirstContact Disapproval: in-progress / indeterminate from end-frame projection; no standalone clip sampling.";
            public bool gameObserved,markDragObserved,undoRedoObserved,reopenObserved,unsavedStopPreserved,cleanPreviewPreserved,restorationMatches,cleanForcedSaveBytesMatch,cleanForcedSaveEquivalent;
            public int forcedSaves,saveCount,restages;
            public NativeDirectingG1Window.Snapshot baseline,current;
            public NativeDirectingG1Window.Difference[] restoredDifferences;
            public SaveObservation[] saves;
        }
        NativeStageContext context;
        NativeSceneStage stage;
        NativeStandingProjection projection;
        NativeDirectingFixture.FixtureIdentity fixture;
        NativeDirectingG1Window.Snapshot baseline;
        readonly List<SaveObservation> saves=new List<SaveObservation>();
        int restages,forcedSaves,endpoint=2;
        bool cleanForcedSaveBytesMatch,cleanForcedSaveEquivalent;
        string baselineSceneText,cleanForcedSavedText;
        bool tracking=false,gameObserved,markDragObserved,undoRedoObserved,reopenObserved,unsavedStopPreserved,cleanPreviewPreserved;
        string watch,status="Click Start standing proof. Use this same scene's real Scene and Game tabs.",failure,reportPath;
        Vector2 scroll;
        double nextWatch;

        [MenuItem("DazPose/Story/Native Directing Diagnostics/G2 Standing and Save Proof")]
        public static void Open()
        {var window=GetWindow<NativeDirectingG2Window>("G2 Standing and Save Proof");window.minSize=new Vector2(540,560);window.Show();}
        void OnEnable()
        {EditorApplication.update+=Tick;EditorSceneManager.sceneSaved+=Saved;AssemblyReloadEvents.beforeAssemblyReload+=Stop;}
        void OnDisable()
        {EditorApplication.update-=Tick;EditorSceneManager.sceneSaved-=Saved;AssemblyReloadEvents.beforeAssemblyReload-=Stop;Stop();}
        void Start()
        {
            if(stage!=null && stage.Ready){ResolveProjection();Apply();return;}
            Stop();fixture=NativeDirectingFixture.Require();
            if(SceneManager.sceneCount!=1)throw new InvalidOperationException("Close other scenes; use only G1_FirstContact for this disposable proof.");
            context=NativeStageContext.Resolve(NativeStageContext.FindHost());
            var camera=context.Camera;
            if(!camera.enabled || !camera.gameObject.activeInHierarchy || camera.targetTexture!=null)
                throw new InvalidOperationException("The existing saved MainCamera must already render to Game. No camera configuration is repaired.");
            var cameras=context.Scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Where(c=>c.enabled && c.gameObject.activeInHierarchy && c.targetTexture==null && c.targetDisplay==camera.targetDisplay).ToArray();
            if(cameras.Length!=1 || cameras[0]!=camera)throw new InvalidOperationException("The bound MainCamera must be the only enabled camera on its Game display.");
            if(context.Scene.isDirty)throw new InvalidOperationException("Save this disposable scene with Ctrl+S before starting G2, so the clean-save comparison has a clean baseline.");
            ResolveProjection(); // Missing source/bindings fail BEFORE acquiring or changing any actor.
            if(!EditorUtility.DisplayDialog("G2 — disposable standing and save proof",
                "Project: "+NativeDirectingFixture.ProjectPath+"\nScene: "+context.Scene.path+"\n\nThis temporarily samples real Lara and moves the real PlayerView rig in Edit Mode. Normal Save restores preview properties before serialization. Only deliberate mark edits should persist. No Play. No screenshot required.","Start standing proof","Cancel"))return;
            baseline=NativeDirectingG1Window.Capture(context);baselineSceneText=File.ReadAllText(context.Scene.path);cleanForcedSavedText=null;cleanForcedSaveBytesMatch=cleanForcedSaveEquivalent=false;saves.Clear();forcedSaves=restages=0;
            gameObserved=markDragObserved=undoRedoObserved=reopenObserved=unsavedStopPreserved=false;
            failure=null;stage=new NativeSceneStage(context);Apply();
            var now=NativeDirectingG1Window.Capture(context);
            cleanPreviewPreserved=now.sceneDirty==baseline.sceneDirty && now.undoGroup==baseline.undoGroup;
            if(!cleanPreviewPreserved)throw new InvalidOperationException("Preview changed scene dirty state or Undo group. Stop and report G2 FAIL.");
        }
        StoryProgramSequence Sequence()
        {
            var rows=context.Scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<StoryProgramSequence>(true)).Where(s=>s.Bindings==context.Host).ToArray();
            if(rows.Length!=1 || rows[0].CompiledSource==null || !rows[0].CompiledSource.IsCompiled || rows[0].CompiledSource.content==null)
                throw new InvalidOperationException("Game Session must have one compiled, source-bound FirstContact StoryProgramSequence. Let import/compile finish.");
            string path=AssetDatabase.GetAssetPath(rows[0].CompiledSource);
            string text=File.ReadAllText(path);
            foreach(string cue in new[]{"Camera.StartAt(camera_start)","Camera.Track(Lara)","Lara.LocomoteTo(stepforward)","Lara.PersistentPose(SexyVamp)"})
                if(!text.Contains(cue))throw new InvalidOperationException("G2 fixed fixture source no longer contains "+cue+". Rebind diagnostic to revised source.");
            return rows[0];
        }
        void ResolveProjection()
        {
            var source=Sequence().CompiledSource;
            var pose=endpoint==2?source.content.Pose("SexyVamp").value:context.Lara.InitialPose;
            if(pose==null || pose.Clip==null)throw new InvalidOperationException("Assign the actual SexyVamp wrapper and Lara InitialPose; no fallback pose is generated.");
            string mark=endpoint==0?"lara_start":endpoint==3?"Lounger":"stepforward";
            var command=endpoint==0?NativeStandingEndpoint.DissolveIn:endpoint==3?NativeStandingEndpoint.StandingDissolve:NativeStandingEndpoint.Locomote;
            projection=new NativeStandingProjection(context.Host.RequireLocation(mark).placement,context.Host.RequireLocation("camera_start").placement,pose,command,tracking,true,precedingMark:context.Host.RequireLocation("lara_start").placement);
        }
        string WatchKey()
        {
            string Key(Transform t)=>t==null?"missing":t.position.ToString("R")+t.rotation.ToString("R")+t.lossyScale.ToString("R");
            return Key(projection.LaraMark)+Key(projection.CameraMark)+context.Host.laraFocus.localPosition.ToString("R")+context.Host.laraFocus.localRotation.ToString("R")+context.Host.laraFocus.localScale.ToString("R")+Key(projection.PrecedingMark);
        }
        void Apply()
        {
            stage.Apply(projection);watch=WatchKey();restages++;
            status="STAGED: "+(endpoint==2?"Intro: LocomoteTo(stepforward) → PersistentPose(SexyVamp)":endpoint==1?"Intro: LocomoteTo(stepforward), configured idle rest":endpoint==0?"Intro: DissolveIn(lara_start), full mark rotation":"Intro: standing DissolveTo(Lounger), initial pose + yaw")
                +". Camera.StartAt(camera_start)"+(tracking?" → Track(Lara)":" (tracking off)")+". Drag selected marks with native handles; Game updates.";
        }
        void Tick()
        {
            if(stage!=null && !stage.Ready)
            {
                if(stage.Failure!=null){failure=stage.Failure;status=stage.Failure;}else status=stage.Status;
                Repaint();return;
            }
            if(stage==null || EditorApplication.timeSinceStartup<nextWatch)return;
            if(stage.IsSaveSuspended || EditorApplication.isUpdating || EditorApplication.isCompiling)
            { Repaint();return; }
            nextWatch=EditorApplication.timeSinceStartup+.04;
            Guard(()=>{NativeStageContext.Resolve(context.Host);if(WatchKey()!=watch)Apply();});
        }
        void Select(string id)
        {
            if(context==null)context=NativeStageContext.Resolve(NativeStageContext.FindHost());
            Selection.activeGameObject=context.Host.RequireLocation(id).placement.gameObject;
            EditorGUIUtility.PingObject(Selection.activeGameObject);
            status="Selected '"+Selection.activeGameObject.name+"' in Hierarchy. In Scene, press W to move or E to rotate. Watch Game while dragging.";
        }
        bool IsActorDifference(NativeDirectingG1Window.Difference difference)
        {
            if(!difference.id.StartsWith("prefab:",StringComparison.Ordinal))return true;
            // Chair/anchor prefab overrides are authored. Actor/body/camera prefab overrides must stay baseline.
            foreach(var root in new[]{context.Lara.gameObject,context.Body.gameObject,context.PlayerRoot.gameObject})
            {
                var prefab=PrefabUtility.GetOutermostPrefabInstanceRoot(root);
                if(prefab!=null && difference.id=="prefab:"+GlobalObjectId.GetGlobalObjectIdSlow(prefab))return true;
            }
            return false;
        }
        void Saved(Scene scene)
        {
            if(baseline==null || context==null || context.Host==null || context.Scene!=scene)return;
            try
            {
                var now=NativeDirectingG1Window.Capture(context);
                var diff=NativeDirectingG1Window.Compare(baseline,now).Where(IsActorDifference).ToArray();
                saves.Add(new SaveObservation{path=scene.path,sceneSha256=now.sceneSha256,previewSuspended=stage==null || !stage.Ready || stage.IsSaveSuspended && !AnimationMode.InAnimationMode(),actorStateMatches=diff.Length==0,differences=diff});
                if(diff.Length!=0){failure="Saved actor/camera snapshot differs from baseline. Stop and report G2 FAIL.";Stop();}
            }
            catch(Exception error){failure=error.ToString();Stop();Debug.LogException(error);}
        }
        void ForceSave()
        {
            NativeDirectingFixture.Require();
            if(stage==null || !stage.Ready)throw new InvalidOperationException("Start the standing proof before forcing its disposable scene save.");
            bool wasClean=!context.Scene.isDirty;
            if(!EditorSceneManager.SaveScene(context.Scene))throw new InvalidOperationException("Disposable scene save cancelled or failed; preview baseline was restored.");
            if(wasClean && forcedSaves==0)
            {
                cleanForcedSavedText=File.ReadAllText(context.Scene.path);
                cleanForcedSaveBytesMatch=NativeDirectingFixture.HashFile(context.Scene.path)==baseline.sceneSha256;
                cleanForcedSaveEquivalent=NativeSceneSaveComparison.Equivalent(baselineSceneText,cleanForcedSavedText);
                if(!cleanForcedSaveEquivalent)throw new InvalidOperationException("Forced save changed scene data beyond empty-string/line-ending normalization. Baseline/saved YAML is retained for export; report G2 FAIL.");
            }
            forcedSaves++;status="Forced serialization complete. Preview resumes on the next editor updates. Save observations: "+saves.Count;
        }
        void Stop()
        {
            if(stage==null)return;
            bool dirty=context!=null && context.Host!=null && context.Scene.isDirty;
            try {stage.Dispose();}
            finally {stage=null;watch=null;}
            unsavedStopPreserved=dirty && context!=null && context.Host!=null && context.Scene.isDirty;
            status="STOPPED: actor/camera baseline restored. Your authored marks and scene dirty state remain. "+(unsavedStopPreserved?"Unsaved authored edit remains dirty.":"");
        }
        void Export()
        {
            if(baseline==null)throw new InvalidOperationException("Run Start standing proof first.");
            if(context==null || context.Host==null)context=NativeStageContext.Resolve(NativeStageContext.FindHost());
            var now=NativeDirectingG1Window.Capture(context);
            var diff=NativeDirectingG1Window.Compare(baseline,now);
            // Deliberate chair overrides appear in full differences, but are not preview-owned properties.
            bool matches=!diff.Any(IsActorDifference);
            bool stopped=stage==null || !stage.Ready;
            bool observed=gameObserved && markDragObserved && undoRedoObserved && reopenObserved;
            string result=failure!=null || saves.Any(s=>!s.previewSuspended || !s.actorStateMatches)?"FAIL":
                stopped && matches && observed && unsavedStopPreserved && cleanPreviewPreserved && cleanForcedSaveEquivalent && forcedSaves>0 && saves.Count>=2?"PASS":"PARTIAL";
            reportPath=Path.Combine(NativeDirectingFixture.ProjectPath,"Temp/NativeDirectingDiagnostics","G2_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"));Directory.CreateDirectory(reportPath);
            var report=new Report{result=result,timestampUtc=DateTime.UtcNow.ToString("O"),projectPath=NativeDirectingFixture.ProjectPath,scenePath=context.Scene.path,sourceRevision=fixture.sourceRevision,codeHash=fixture.codeHash,
                status=status,exception=failure,gameObserved=gameObserved,markDragObserved=markDragObserved,undoRedoObserved=undoRedoObserved,reopenObserved=reopenObserved,unsavedStopPreserved=unsavedStopPreserved,
                cleanPreviewPreserved=cleanPreviewPreserved,cleanForcedSaveBytesMatch=cleanForcedSaveBytesMatch,cleanForcedSaveEquivalent=cleanForcedSaveEquivalent,restorationMatches=matches,forcedSaves=forcedSaves,saveCount=saves.Count,restages=restages,baseline=baseline,current=now,restoredDifferences=diff,saves=saves.ToArray()};
            File.WriteAllText(Path.Combine(reportPath,"report.json"),JsonUtility.ToJson(report,true));
            File.WriteAllText(Path.Combine(reportPath,"baseline-scene.unity.txt"),baselineSceneText);
            File.WriteAllText(Path.Combine(reportPath,"current-scene.unity.txt"),File.ReadAllText(context.Scene.path));
            if(cleanForcedSavedText!=null)File.WriteAllText(Path.Combine(reportPath,"clean-forced-save.unity.txt"),cleanForcedSavedText);
            string text=$"G2 {result}\nSource: {fixture.sourceRevision}\nCode SHA256: {fixture.codeHash}\nScene: {context.Scene.path}\nClean preview/Undo group preserved: {cleanPreviewPreserved}\nClean forced save byte-identical: {cleanForcedSaveBytesMatch}\nClean forced save data equivalent: {cleanForcedSaveEquivalent}\nSaved baseline observations: {saves.Count}; forced saves: {forcedSaves}\nRestored actor/camera matches: {matches}\nStop preserved unsaved authored dirty state: {unsavedStopPreserved}\nArt Game/drag/Undo/reopen: {gameObserved}/{markDragObserved}/{undoRedoObserved}/{reopenObserved}\nException: {failure ?? "none"}\nFull object/prefab differences in report.json. No screenshots required. Native PASS requires Art confirmation.\n";
            File.WriteAllText(Path.Combine(reportPath,"report.txt"),text);
            status="Exported "+result+": "+reportPath;Debug.Log(text+"\n"+reportPath);
        }
        void Guard(Action action)
        {try{action();}catch(Exception error){failure=error.ToString();Stop();status=error.Message;Debug.LogException(error);}Repaint();}
        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("G2 v2 — standing and normal save",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Fixed FirstContact technical fixture, not full Studio cue navigation. Real saved Lara + PlayerView, Edit Mode, no Play. No screenshots required.",MessageType.Info);
            if(GUILayout.Button("1. Start standing proof — SexyVamp")){endpoint=2;tracking=false;Guard(Start);}
            using(new EditorGUI.DisabledScope(stage==null || !stage.Ready))
            {
                int next=EditorGUILayout.Popup("Standing endpoint",endpoint,new[]{"DissolveIn(lara_start)","LocomoteTo(stepforward) — idle rest","PersistentPose(SexyVamp)","Standing DissolveTo(Lounger) — initialPose"});
                bool track=EditorGUILayout.Popup("Camera orientation",tracking?1:0,new[]{"Use camera_start rotation","Track Lara — overrides mark rotation"})==1;
                if(next!=endpoint || track!=tracking){endpoint=next;tracking=track;Guard(()=>{ResolveProjection();Apply();});}
                EditorGUILayout.HelpBox(tracking?"Tracking aims at Lara. camera_start controls position; its rotation is superseded. Choose Use camera_start rotation to edit the shot angle.":"Camera uses camera_start position AND full rotation. Moving or rotating that mark updates the Game shot.",MessageType.Info);
                if(GUILayout.Button("2a. Select stepforward — move/rotate in Scene"))Guard(()=>Select("stepforward"));
                if(GUILayout.Button("2b. Select camera_start — move/rotate in Scene"))Guard(()=>Select("camera_start"));
                EditorGUILayout.HelpBox("First: Ctrl+S without editing, then Force Save. Next: move stepforward, Ctrl+S, Undo/Redo. Finally move a mark without saving and click Stop: scene must stay dirty. You can also edit Lounger's placement to check chair prefab overrides.",MessageType.Info);
                if(GUILayout.Button("3. Force SaveScene — disposable scene only"))Guard(ForceSave);
                if(GUILayout.Button("4. Stop — restore preview, retain my mark edits"))Guard(Stop);
            }
            gameObserved=EditorGUILayout.ToggleLeft("Art: real Game shows standing pose and camera endpoints",gameObserved);
            markDragObserved=EditorGUILayout.ToggleLeft("Art: stepforward/camera_start drags update Game",markDragObserved);
            undoRedoObserved=EditorGUILayout.ToggleLeft("Art: normal Undo/Redo preserves mark edits and restages",undoRedoObserved);
            reopenObserved=EditorGUILayout.ToggleLeft("Art: after Stop + reopen, saved marks persist; actor/camera preview does not",reopenObserved);
            if(GUILayout.Button("5. Export report (after Stop)"))Guard(Export);
            if(!string.IsNullOrEmpty(reportPath))EditorGUILayout.SelectableLabel(reportPath,GUILayout.Height(36));
            if(stage!=null && stage.Ready)EditorGUILayout.LabelField("Preview",stage.IsSaveSuspended?"Saving baseline — automatic resume pending":stage.IsStaged?"Live — watching marks at 25 Hz":"Restaging");
            EditorGUILayout.HelpBox(status,failure==null?MessageType.Info:MessageType.Error);
            EditorGUILayout.EndScrollView();
        }
    }
}
