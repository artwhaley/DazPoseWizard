using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor.Studio
{
    /// <summary>One real-scene preview owner. No Undo recording, dirty clearing or prefab override replacement.</summary>
    public sealed class NativePreviewLease : IDisposable
    {
        static NativePreviewLease active;
        readonly PreviewPropertyLedger ledger=new PreviewPropertyLedger();
        readonly AnimationModeDriver driver;
        internal NativeWardrobeProjection Wardrobe {get;}
        readonly PreviewResourceOwner<NativePoseSampler> sampler=new PreviewResourceOwner<NativePoseSampler>();
        readonly string originalScenePath;
        readonly IDisposable writers;
        readonly HashSet<UnityEngine.Object> ownedObjects=new HashSet<UnityEngine.Object>();
        readonly List<MaterialPropertyBlock> propertyBlockBaseline=new List<MaterialPropertyBlock>();
        readonly Dictionary<(Renderer renderer,int slot,Material source),Material> materials=new Dictionary<(Renderer,int,Material),Material>();
        Action projection;
        bool disposed, applying, restoring, staged, saveSuspended;
        int resumeTicks;
        double nextConflictCheck;
        internal int SampleCount {get;private set;}
        public string Failure { get; private set; }
        public NativeStageContext Context { get; }
        public string Status { get; private set; }="Acquired";
        public bool IsStaged => staged;
        public bool IsSaveSuspended => saveSuspended;
        public bool IsDisposed => disposed;
        public event Action Restored;
        internal void DesiredProjection(Action value)=>projection=value;

        public NativePreviewLease(NativeStageContext context)
        {
            NativeStageContext.RequireEditMode();
            if(NativeDirectingG1Window.HasShift)throw new InvalidOperationException("Click Restore Camera Rig in G1 Camera Proof before starting G2.");
            if(active!=null)throw new InvalidOperationException("Stop the other native preview before starting this one.");
            if(AnimationMode.InAnimationMode())throw new InvalidOperationException("Close Animation Window/Timeline preview before directing.");
            Context=context ?? throw new ArgumentNullException(nameof(context));
            originalScenePath=context.Scene.path;
            Wardrobe=new NativeWardrobeProjection(context);Restored+=Wardrobe.Hide;
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
                // The head-child focus is an authored local offset, never sampled or preview-owned.
                if(t==Context.Host.laraFocus)continue;
                if(!ownedObjects.Add(t))continue;
                ownedObjects.Add(t.gameObject);
                string path=NativeStageContext.ObjectPath(t);
                string group=t==Context.Lara.transform||t==Context.Body.transform||t==Context.PlayerRoot||t==Context.ViewRig?"spatial":"pose";
                ledger.Capture(path+".position",()=>t.localPosition,v=>{if(t!=null)t.localPosition=v;},group:group);
                ledger.Capture(path+".rotation",()=>t.localRotation,v=>{if(t!=null)t.localRotation=v;},group:group);
                ledger.Capture(path+".scale",()=>t.localScale,v=>{if(t!=null)t.localScale=v;},group:group);
                // Active state is observed but never deliberately changed. Clip active curves are rejected.
                ledger.Capture(path+".active",()=>t.gameObject.activeSelf,v=>{if(t!=null && t.gameObject.activeSelf!=v)t.gameObject.SetActive(v);});
            }
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if(!ownedObjects.Add(renderer))continue;
                string path=NativeStageContext.ObjectPath(renderer.transform);
                ledger.Capture(path+".enabled",()=>renderer.enabled,v=>{if(renderer!=null)renderer.enabled=v;},group:"presentation");
                ledger.Capture(path+".forceRenderingOff",()=>renderer.forceRenderingOff,v=>{if(renderer!=null)renderer.forceRenderingOff=v;},group:"presentation");
                var refs=renderer.sharedMaterials;
                for(int i=0;i<refs.Length;i++)
                {
                    int slot=i;
                    ledger.Capture(path+".material["+slot+"]",()=>renderer.sharedMaterials.ElementAtOrDefault(slot),v=>{
                        if(renderer==null)return;var now=renderer.sharedMaterials;
                        if(slot<now.Length){now[slot]=v;renderer.sharedMaterials=now;}
                    },group:"presentation");
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
                            v=>{if(skin!=null && skin.sharedMesh==mesh)skin.SetBlendShapeWeight(index,v);},group:"pose");
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
                NativeStageContext.Resolve(Context.Host,Context.ValidateAllLocations);
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
        // Spatial edits keep AnimationMode, the sampled skeleton, materials and all baselines alive.
        internal void ApplySpatial(Action update) {
            if(disposed)throw new ObjectDisposedException(nameof(NativePreviewLease));
            if(saveSuspended)return;
            if(!staged||!AnimationMode.InAnimationMode(driver))throw new InvalidOperationException("The sampled shot is no longer active.");
            if(applying)throw new InvalidOperationException("Reentrant native spatial update refused.");
            if(ledger.Conflicts("spatial").Length!=0)throw new InvalidOperationException("A preview root was edited outside the directing controls.");
            applying=true;
            try{try{update();}finally{ledger.RecordApplied("spatial");ledger.RecordApplied("pose");}NativeStageContext.RefreshViews();}
            catch(Exception error){Failure=error.Message;Dispose();throw;}
            finally{applying=false;}
        }
        // Deltas retain the graph and independent groups; desired full projection is updated first.
        internal void ApplyAuthoringDelta(Action complete,Action update,bool pose,bool presentation) {
            projection=complete;
            if(saveSuspended)return;
            if(!staged){Apply(complete);return;}
            if(disposed||!AnimationMode.InAnimationMode(driver))throw new InvalidOperationException("Preview ownership interrupted.");
            if(applying)throw new InvalidOperationException("Reentrant authoring projection.");
            var conflicts=ledger.Conflicts();if(conflicts.Length!=0)throw new InvalidOperationException("External preview edit preserved: "+string.Join(", ",conflicts.Take(6)));
            applying=true;
            try {
                if(pose){sampler.WithExisting(s=>s.Suspend());ledger.PrepareRestore("pose");ledger.Restore("pose");}
                if(presentation){ledger.PrepareRestore("presentation");ledger.Restore("presentation");}
                try{update();}finally{ledger.RecordApplied();}
                NativeStageContext.RefreshViews();
            }catch(Exception e){Failure=e.Message;Dispose();throw;}
            finally{applying=false;}
        }
        // SaveAssetIfDirty on this exact source-binding asset cannot serialize scene preview properties.
        // All ordinary saves, scene saves and prefab operations still use the full suspension boundary.
        static string bindingSavePath;
        internal static void SaveBindings(DazPose.Story.StoryContentBindings content){
            string prior=bindingSavePath;bindingSavePath=AssetDatabase.GetAssetPath(content);
            try{AssetDatabase.SaveAssetIfDirty(content);}finally{bindingSavePath=prior;}
        }
        internal Material MaterialFor(Renderer renderer,int slot,Material source) {
            var key=(renderer,slot,source);
            if(!materials.TryGetValue(key,out var clone)||clone==null){clone=new Material(source){hideFlags=HideFlags.HideAndDontSave};materials[key]=clone;}
            return clone;
        }
        public void Sample(AnimationClip clip,float seconds=0)
        {
            if(!applying || !AnimationMode.InAnimationMode(driver))throw new InvalidOperationException("Sampling requires this active lease.");
            ValidateClip(clip);
            var root=Context.Lara.transform;
            Vector3 position=root.position,scale=root.localScale;Quaternion rotation=root.rotation;
            try { sampler.GetOrCreate(()=>new NativePoseSampler(Context.Lara.ConfiguredAnimator)).Sample(clip,seconds);SampleCount++; }
            finally { root.SetPositionAndRotation(position,rotation);root.localScale=scale; }

        }
        internal void ResetPoseToBaseline(){
            if(!applying)throw new InvalidOperationException("Pose reset requires owned projection.");
            sampler.WithExisting(value=>value.Suspend());ledger.Restore("pose");
        }
        readonly Dictionary<AnimationClip,int> validatedClips=new Dictionary<AnimationClip,int>();
        internal void InvalidateClipValidation()=>validatedClips.Clear();
        internal void ValidateClip(AnimationClip clip)
        {
            if(clip==null)throw new InvalidOperationException("Assign a real PerformerPose clip; no neutral fallback is synthesized.");
            int revision=EditorUtility.GetDirtyCount(clip);
            if(validatedClips.TryGetValue(clip,out var seen)&&seen==revision)return;
            if(clip.empty||clip.legacy)throw new InvalidOperationException("Assign a prepared nonempty Generic pose clip.");
            if(AnimationUtility.GetObjectReferenceCurveBindings(clip).Length!=0)
                throw new InvalidOperationException("INCOMPLETE: object-reference animation curves require a separate property adapter.");
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))
            {
                bool supported=binding.type==typeof(Transform) || binding.type==typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape.",StringComparison.Ordinal);
                if(!supported)throw new InvalidOperationException("INCOMPLETE: unsupported clip property "+binding.path+":"+binding.propertyName);
                var target=string.IsNullOrEmpty(binding.path)?Context.Lara.transform:Context.Lara.transform.Find(binding.path);
                if(target==null)throw new InvalidOperationException("Clip '"+clip.name+"' targets missing Lara rig path '"+binding.path+"'. Use the source-bound clip prepared for this saved rig.");
                if(target==Context.Host.laraFocus || Context.Host.locations.Any(l=>l.placement==target || l.focus!=null && l.focus==target))
                    throw new InvalidOperationException("Pose clip targets an authored anchor; sampling refused.");
            }
            if(validatedClips.Count>=64)validatedClips.Clear();validatedClips[clip]=revision;
        }
        void RestoreBaseline()
        {
            if(!staged && !AnimationMode.InAnimationMode(driver))return;
            restoring=true;
            try
            {
                ledger.PrepareRestore();
                try { if(AnimationMode.InAnimationMode(driver))AnimationMode.StopAnimationMode(driver); }
                finally
                {
                    // AnimationMode always stops FIRST. The ledger handles non-animation properties and external edits.
                    try { sampler.WithExisting(value=>value.Suspend()); }
                    finally
                    {
                        try { ledger.Restore(); }
                        finally
                        {
                            staged=false;
                            // Detached by ledger restoration. Keep owned instances for the next cue/save resume.
                            Restored?.Invoke();
                        }
                    }
                }
            }
            finally { restoring=false; }

        }
        void SuspendForSave()
        {
            if(disposed)return;
            saveSuspended=true;resumeTicks=3;
            RestoreBaseline();Status="Baseline restored before serialization; awaiting save completion";
        }
        internal static string[] BeforeAssetSave(string[] paths)
        {
            // Only a scoped, exact typed-binding asset save avoids suspension. All normal/prefab saves suspend.
            if(string.IsNullOrEmpty(bindingSavePath)||paths.Any(path=>path!=bindingSavePath))active?.SuspendForSave();return paths;
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
                if(staged && !NativePreviewInteraction.IsDragging && EditorApplication.timeSinceStartup>=nextConflictCheck)
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
        void UndoRedo() { if(!disposed && !saveSuspended && projection!=null){
            if(SpatialUndo!=null)SpatialUndo();else Apply(projection);
        } }
        internal Action SpatialUndo;
        UndoPropertyModification[] AuthorModified(UndoPropertyModification[] changes)
        {
            if(!disposed && !applying && !restoring && !saveSuspended && changes.Any(m=>ownedObjects.Contains(m.currentValue.target)))
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
                try { Wardrobe.Dispose();sampler.Dispose(); }
                finally
                {
                    foreach(var material in materials.Values)if(material!=null)UnityEngine.Object.DestroyImmediate(material);
                    materials.Clear();writers.Dispose();if(driver!=null)UnityEngine.Object.DestroyImmediate(driver);
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
    }
    public sealed class NativePreviewSaveGuard : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)=>NativePreviewLease.BeforeAssetSave(paths);
    }
}
