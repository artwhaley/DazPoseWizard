using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Editor.Studio
{
    /// <summary>One real-scene preview owner. No Undo recording, dirty clearing or prefab override replacement.</summary>
    public sealed class NativePreviewLease : IDisposable
    {
        static NativePreviewLease active;
        readonly PreviewPropertyLedger ledger=new PreviewPropertyLedger();
        readonly AnimationModeDriver driver;
        PlayableGraph samplingGraph;
        readonly string originalScenePath;
        readonly IDisposable writers;
        readonly HashSet<UnityEngine.Object> ownedObjects=new HashSet<UnityEngine.Object>();
        readonly List<MaterialPropertyBlock> propertyBlockBaseline=new List<MaterialPropertyBlock>();
        readonly List<Material> materials=new List<Material>();
        Action projection;
        bool disposed, applying, staged, saveSuspended;
        int resumeTicks;
        double nextConflictCheck;
        public string Failure { get; private set; }
        public NativeStageContext Context { get; }
        public string Status { get; private set; }="Acquired";
        public bool IsStaged => staged;
        public bool IsDisposed => disposed;
        public event Action Restored;

        public NativePreviewLease(NativeStageContext context)
        {
            NativeStageContext.RequireEditMode();
            if(NativeDirectingG1Window.HasShift)throw new InvalidOperationException("Click Restore Camera Rig in G1 Camera Proof before starting G2.");
            if(active!=null)throw new InvalidOperationException("Stop the other native preview before starting this one.");
            if(AnimationMode.InAnimationMode())throw new InvalidOperationException("Close Animation Window/Timeline preview before directing.");
            Context=context ?? throw new ArgumentNullException(nameof(context));
            originalScenePath=context.Scene.path;
            // Snapshot BEFORE any writer suppression or pose sample. The body is included even when not staged.
            Capture(context.Lara.transform);Capture(context.Body.transform);Capture(context.PlayerRoot);
            driver=ScriptableObject.CreateInstance<AnimationModeDriver>();driver.hideFlags=HideFlags.HideAndDontSave;
            writers=NativePreviewWriterGuard.Acquire(context.Lara.transform);
            active=this;
            AssemblyReloadEvents.beforeAssemblyReload+=Dispose;
            EditorApplication.quitting+=Dispose;
            EditorApplication.playModeStateChanged+=PlayChanged;
            EditorApplication.update+=Update;
            EditorSceneManager.sceneClosing+=SceneClosing;
            EditorSceneManager.activeSceneChangedInEditMode+=SceneChanged;
            EditorSceneManager.sceneSaving+=SceneSaving;
            EditorSceneManager.sceneSaved+=SceneSaved;
            Undo.undoRedoPerformed+=UndoRedo;
            Undo.postprocessModifications+=AuthorModified;
            PrefabUtility.prefabInstanceApplying+=PrefabApplying;
        }
        void Capture(Transform root)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(!ownedObjects.Add(t))continue;
                ownedObjects.Add(t.gameObject);
                string path=NativeStageContext.ObjectPath(t);
                ledger.Capture(path+".position",()=>t.localPosition,v=>{if(t!=null)t.localPosition=v;});
                ledger.Capture(path+".rotation",()=>t.localRotation,v=>{if(t!=null)t.localRotation=v;});
                ledger.Capture(path+".scale",()=>t.localScale,v=>{if(t!=null)t.localScale=v;});
                // Active state is observed but never deliberately changed. Clip active curves are rejected.
                ledger.Capture(path+".active",()=>t.gameObject.activeSelf,v=>{if(t!=null && t.gameObject.activeSelf!=v)t.gameObject.SetActive(v);});
            }
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if(!ownedObjects.Add(renderer))continue;
                string path=NativeStageContext.ObjectPath(renderer.transform);
                ledger.Capture(path+".enabled",()=>renderer.enabled,v=>{if(renderer!=null)renderer.enabled=v;});
                ledger.Capture(path+".forceRenderingOff",()=>renderer.forceRenderingOff,v=>{if(renderer!=null)renderer.forceRenderingOff=v;});
                var refs=renderer.sharedMaterials;
                for(int i=0;i<refs.Length;i++)
                {
                    int slot=i;
                    ledger.Capture(path+".material["+slot+"]",()=>renderer.sharedMaterials.ElementAtOrDefault(slot),v=>{
                        if(renderer==null)return;var now=renderer.sharedMaterials;
                        if(slot<now.Length){now[slot]=v;renderer.sharedMaterials=now;}
                    });
                    var perSlot=new MaterialPropertyBlock();renderer.GetPropertyBlock(perSlot,slot);propertyBlockBaseline.Add(perSlot);
                }
                var global=new MaterialPropertyBlock();renderer.GetPropertyBlock(global);propertyBlockBaseline.Add(global);
                // This adapter deliberately does not write MPBs. Retain snapshots, but never overwrite an author's block.
                if(renderer is SkinnedMeshRenderer skin && skin.sharedMesh!=null)
                {
                    ledger.Capture(path+".updateWhenOffscreen",()=>skin.updateWhenOffscreen,v=>{if(skin!=null)skin.updateWhenOffscreen=v;});
                    var mesh=skin.sharedMesh;
                    for(int i=0;i<mesh.blendShapeCount;i++)
                    {
                        int index=i;float original=skin.GetBlendShapeWeight(index);
                        ledger.Capture(path+".blend["+i+"]",()=>skin!=null && skin.sharedMesh==mesh?skin.GetBlendShapeWeight(index):original,
                            v=>{if(skin!=null && skin.sharedMesh==mesh)skin.SetBlendShapeWeight(index,v);});
                    }
                }
            }
        }
        public void Apply(Action immutableProjection)
        {
            if(disposed)throw new ObjectDisposedException(nameof(NativePreviewLease));
            projection=immutableProjection ?? throw new ArgumentNullException(nameof(immutableProjection));
            if(saveSuspended)return;
            if(applying)throw new InvalidOperationException("Reentrant native projection refused.");
            string[] conflicts=staged?ledger.Conflicts():Array.Empty<string>();
            if(conflicts.Length>0)
            {
                string message="External preview-property edit preserved; directing stopped: "+string.Join(", ",conflicts.Take(6));
                Failure=message;Dispose();throw new InvalidOperationException(message);
            }
            applying=true;
            try
            {
                RestoreBaseline();
                NativeStageContext.Resolve(Context.Host);
                if(AnimationMode.InAnimationMode())throw new InvalidOperationException("Another animation owner interrupted native directing.");
                AnimationMode.StartAnimationMode(driver);
                if(!AnimationMode.InAnimationMode(driver))throw new InvalidOperationException("Could not acquire this preview's AnimationModeDriver.");
                // Record partial writes even if sampling/adapter fails, so exceptional cleanup has ownership.
                try { projection(); }
                finally { ledger.RecordApplied();staged=true; }
                Status="Staged in real Scene/Game views";
                NativeStageContext.RefreshViews();
            }
            catch(Exception error) { Failure=error.Message;Dispose();throw; }
            finally { applying=false; }
        }
        internal void Own(Material material) { materials.Add(material); }
        public void Sample(AnimationClip clip)
        {
            if(!applying || !AnimationMode.InAnimationMode(driver))throw new InvalidOperationException("Sampling requires this active lease.");
            if(clip==null)throw new InvalidOperationException("Assign a real PerformerPose clip; no neutral fallback is synthesized.");
            if(AnimationUtility.GetObjectReferenceCurveBindings(clip).Length!=0)
                throw new InvalidOperationException("INCOMPLETE: object-reference animation curves require a separate property adapter.");
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))
            {
                bool supported=binding.type==typeof(Transform) || binding.type==typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape.",StringComparison.Ordinal);
                if(!supported)throw new InvalidOperationException("INCOMPLETE: unsupported clip property "+binding.path+":"+binding.propertyName);
                var target=string.IsNullOrEmpty(binding.path)?Context.Lara.transform:Context.Lara.transform.Find(binding.path);
                if(target==Context.Host.laraFocus || Context.Host.locations.Any(l=>l.placement==target || l.focus==target))
                    throw new InvalidOperationException("Pose clip targets an authored anchor; sampling refused.");
            }
            var root=Context.Lara.transform;
            Vector3 position=root.position,scale=root.localScale;Quaternion rotation=root.rotation;
            var animator=Context.Lara.ConfiguredAnimator;
            if(!animator.isActiveAndEnabled)throw new InvalidOperationException("Lara\'s saved Animator must already be active and enabled; native preview does not activate the actor.");
            if(animator.hasBoundPlayables)throw new InvalidOperationException("Another PlayableGraph owns Lara's Animator. Stop that preview first.");
            // Saved Lara intentionally has no RuntimeAnimatorController. SampleAnimationClip's controllerless
            // Animator path is unsafe; use a private manual graph, as the runtime Generic rig does.
            samplingGraph=PlayableGraph.Create("DazPose Native Preview Sampling");
            samplingGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable=AnimationClipPlayable.Create(samplingGraph,clip);
            playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);playable.SetTime(0);
            var output=AnimationPlayableOutput.Create(samplingGraph,"Lara pose",animator);output.SetSourcePlayable(playable);
            samplingGraph.Play();
            AnimationMode.BeginSampling();
            try { AnimationMode.SamplePlayableGraph(samplingGraph,0,0); }
            finally { AnimationMode.EndSampling();root.SetPositionAndRotation(position,rotation);root.localScale=scale; }
        }
        void RestoreBaseline()
        {
            if(!staged && !AnimationMode.InAnimationMode(driver))return;
            ledger.PrepareRestore();
            try { if(AnimationMode.InAnimationMode(driver))AnimationMode.StopAnimationMode(driver); }
            finally
            {
                // AnimationMode always stops FIRST. The ledger handles non-animation properties and external edits.
                try { if(samplingGraph.IsValid())samplingGraph.Destroy(); }
                finally
                {
                    try { ledger.Restore(); }
                    finally
                    {
                        staged=false;
                        foreach(var material in materials)if(material!=null)UnityEngine.Object.DestroyImmediate(material);
                        materials.Clear();Restored?.Invoke();
                    }
                }
            }
        }
        void SuspendForSave()
        {
            if(disposed)return;
            saveSuspended=true;resumeTicks=3;
            RestoreBaseline();Status="Baseline restored before serialization; awaiting save completion";
        }
        internal static string[] BeforeAssetSave(string[] paths)
        {
            // Asset saves may include prefab Apply/Save As; suspend for ALL paths. Never filter normal saves.
            active?.SuspendForSave();return paths;
        }
        void SceneSaving(Scene scene,string path) { SuspendForSave(); }
        void SceneSaved(Scene scene) { if(saveSuspended)resumeTicks=2; }
        void Update()
        {
            if(disposed || applying)return;
            try
            {
                if(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || Context.Host==null
                    || Context.Scene!=SceneManager.GetActiveScene() || Context.Scene.path!=originalScenePath || PrefabStageUtility.GetCurrentPrefabStage()!=null)
                {Dispose();return;}
                if(staged && EditorApplication.timeSinceStartup>=nextConflictCheck)
                {
                    nextConflictCheck=EditorApplication.timeSinceStartup+.1;
                    var conflicts=ledger.Conflicts();
                    if(conflicts.Length!=0)throw new InvalidOperationException("External preview-property writer/edit detected; preserved and stopped: "+string.Join(", ",conflicts.Take(6)));
                }
                if(staged && !AnimationMode.InAnimationMode(driver))throw new InvalidOperationException("Owned AnimationMode was interrupted; preview stopped.");
                if(saveSuspended && --resumeTicks<=0 && !EditorApplication.isUpdating)
                {
                    // Callbacks and serialization complete synchronously before this editor update, including failed/cancelled save.
                    saveSuspended=false;if(projection!=null)Apply(projection);
                }
            }
            catch(Exception error){Failure=error.Message;Dispose();Debug.LogException(error);}
        }
        void UndoRedo() { if(!disposed && !saveSuspended && projection!=null)Apply(projection); }
        UndoPropertyModification[] AuthorModified(UndoPropertyModification[] changes)
        {
            if(!applying && changes.Any(m=>ownedObjects.Contains(m.currentValue.target)))
            {
                // Authoring actor/bone fields while posed cannot be safely merged into AnimationMode's Undo snapshot.
                // Restore only owned fields, retaining detected external values, then leave the edit to Unity.
                Failure="Actor edit interrupted directing; authored values preserved";Dispose();
            }
            return changes;
        }
        void PrefabApplying(GameObject instance) { Dispose(); }
        void PlayChanged(PlayModeStateChange state) { if(state==PlayModeStateChange.ExitingEditMode)Dispose(); }
        void SceneClosing(Scene scene,bool removing) { Dispose(); }
        void SceneChanged(Scene before,Scene after) { Dispose(); }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            try { RestoreBaseline(); }
            finally
            {
                writers.Dispose();if(driver!=null)UnityEngine.Object.DestroyImmediate(driver);
                if(active==this)active=null;
                AssemblyReloadEvents.beforeAssemblyReload-=Dispose;EditorApplication.quitting-=Dispose;
                EditorApplication.playModeStateChanged-=PlayChanged;EditorApplication.update-=Update;
                EditorSceneManager.sceneClosing-=SceneClosing;EditorSceneManager.activeSceneChangedInEditMode-=SceneChanged;
                EditorSceneManager.sceneSaving-=SceneSaving;EditorSceneManager.sceneSaved-=SceneSaved;
                Undo.undoRedoPerformed-=UndoRedo;Undo.postprocessModifications-=AuthorModified;
                PrefabUtility.prefabInstanceApplying-=PrefabApplying;
                Status=Failure ?? "Stopped; preview properties restored";
                NativeStageContext.RefreshViews();
            }
        }
    }
    public sealed class NativePreviewSaveGuard : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)=>NativePreviewLease.BeforeAssetSave(paths);
    }
}
