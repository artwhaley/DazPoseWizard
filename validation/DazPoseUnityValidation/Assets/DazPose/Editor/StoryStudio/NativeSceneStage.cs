using System;
using System.Linq;
using DazPose.Performer;
using DazPose.Player;
using UnityEngine;
namespace DazPose.Editor.Studio
{
    public enum NativeStandingEndpoint { StartHidden, DissolveIn, Locomote, StandingDissolve }
    /// <summary>Immutable logical cue result; authored Transform references remain live for native handle drags.</summary>
    public sealed class NativeStandingProjection
    {
        public Transform LaraMark { get; }
        public Transform CameraMark { get; }
        public Transform PrecedingMark { get; }
        public PerformerPose Pose { get; }
        public NativeStandingEndpoint Endpoint { get; }
        public bool TrackLara { get; }
        public bool HideBody { get; }
        public PerformerMagicShaderProfile Material { get; }
        public NativeStandingProjection(Transform laraMark,Transform cameraMark,PerformerPose pose,NativeStandingEndpoint endpoint,bool trackLara,bool hideBody=false,PerformerMagicShaderProfile material=null,Transform precedingMark=null)
        { LaraMark=laraMark;CameraMark=cameraMark;PrecedingMark=precedingMark;Pose=pose;Endpoint=endpoint;TrackLara=trackLara;HideBody=hideBody;Material=material; }
    }
    /// <summary>Saved actors and PlayerView only. No interpreter, async gameplay, cloned scene, or custom rendering.</summary>
    public sealed class NativeSceneStage : IDisposable
    {
        readonly NativePreviewLease lease;
        public NativeStageContext Context=>lease.Context;
        public bool Ready=>!lease.IsDisposed;
        public string Status=>lease.Status;
        public string Failure=>lease.Failure;
        public NativeSceneStage(NativeStageContext context) { lease=new NativePreviewLease(context); }
        public void Apply(NativeStandingProjection projection)
        {
            if(projection==null)throw new ArgumentNullException(nameof(projection));
            lease.Apply(()=>Stage(projection));
        }
        void Stage(NativeStandingProjection p)
        {
            var c=Context;
            RequireAnchor(p.LaraMark,"Lara placement");RequireAnchor(p.CameraMark,"Camera.StartAt placement");
            var pose=p.Endpoint==NativeStandingEndpoint.StandingDissolve?c.Lara.InitialPose:p.Pose;
            if(pose==null || pose.Clip==null)throw new InvalidOperationException("Assign the configured initial or source-bound persistent PerformerPose clip.");
            // Sample FIRST, isolate root curves, then place actor. Aim reads the resulting head-child focus.
            lease.Sample(pose.Clip);
            bool yawOnly=p.Endpoint==NativeStandingEndpoint.Locomote || p.Endpoint==NativeStandingEndpoint.StandingDissolve;
            Quaternion facing=NativeStandingRules.Facing(p.LaraMark.rotation,p.LaraMark.forward,yawOnly);
            Vector3 position=p.LaraMark.position;
            if(p.Endpoint==NativeStandingEndpoint.Locomote)
                position.y=p.PrecedingMark!=null?p.PrecedingMark.position.y:c.Lara.transform.position.y;
            c.Lara.transform.SetPositionAndRotation(position,facing);
            bool hidden=p.Endpoint==NativeStandingEndpoint.StartHidden;
            foreach(var renderer in c.Lara.GetComponentsInChildren<Renderer>(true))
            {
                // Endpoint dissolve requires no residual dissolve block. We never mutate or discard unknown MPB data.
                EnsureSettledDissolve(renderer);
                renderer.forceRenderingOff=hidden;
                if(renderer is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;
            }
            if(p.HideBody)foreach(var renderer in c.Body.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=true;
            if(p.Material!=null)
            {
                p.Material.ValidateConfiguration();
                if(p.Material.mode==PerformerMaterialMode.GlowyWireframe)
                    foreach(var renderer in c.Lara.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled && r.gameObject.activeInHierarchy && (r is MeshRenderer || r is SkinnedMeshRenderer)))
                    {
                        var refs=renderer.sharedMaterials;
                        for(int i=0;i<refs.Length;i++){var clone=new Material(p.Material.wireframeMaterial){hideFlags=HideFlags.HideAndDontSave};lease.Own(clone);refs[i]=clone;}
                        renderer.sharedMaterials=refs;
                    }
            }
            // PlayerView.ResetPerformanceView endpoint only: position lives on player, rotation on ViewRig.
            c.PlayerRoot.position=p.CameraMark.position;c.ViewRig.rotation=p.CameraMark.rotation;
            if(p.TrackLara)c.ViewRig.rotation=PlayerViewEndpoint.DesiredRotation(c.Host.laraFocus.position,c.Camera.transform.position,c.ViewRig.rotation);
        }
        void RequireAnchor(Transform value,string label)
        {
            if(value==null || value.gameObject.scene!=Context.Scene)throw new InvalidOperationException("Assign "+label+" in the host scene.");
            foreach(var root in new[]{Context.Lara.transform,Context.Body.transform,Context.PlayerRoot})
                if(value==root || value.IsChildOf(root))throw new InvalidOperationException(label+" must be an authored scene anchor outside preview-owned actor/camera hierarchies.");
        }
        static void EnsureSettledDissolve(Renderer renderer)
        {
            var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
            Check(block);
            for(int i=0;i<renderer.sharedMaterials.Length;i++){renderer.GetPropertyBlock(block,i);Check(block);}
        }
        static void Check(MaterialPropertyBlock block)
        { if(block.GetFloat("_DissolveEnabled")!=0 || block.GetFloat("_DissolveProgress")!=0)throw new InvalidOperationException("INCOMPLETE: this renderer has an active authored dissolve property block. Stop its presentation before native endpoint staging."); }
        public void Dispose()=>lease.Dispose();
    }
}
