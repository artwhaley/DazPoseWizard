using System;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>First outfit proof on the existing rig; maintains desired anatomy state.</summary>
    [ExecuteAlways, DefaultExecutionOrder(10000), DisallowMultipleComponent]
    public sealed class LaraFirstOutfit : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer body, hair, dress, panties;
        [SerializeField] private bool showHair = true, showDress = true, showPanties = true;
        private int[][] bodyIndices;
        private SkinnedMeshRenderer[] attachments;
        private Mesh resolvedBody;
        private MaterialPropertyBlock properties;
        public SkinnedMeshRenderer Hair => hair;
        public SkinnedMeshRenderer Dress => dress;
        public SkinnedMeshRenderer Panties => panties;
        public bool ShowHair { get => showHair; set { showHair = value; Apply(); } }
        public bool ShowDress { get => showDress; set { showDress = value; Apply(); } }
        public bool ShowPanties { get => showPanties; set { showPanties = value; Apply(); } }
        public void Configure(SkinnedMeshRenderer source,SkinnedMeshRenderer hairRenderer,SkinnedMeshRenderer dressRenderer,SkinnedMeshRenderer pantiesRenderer)
        {
            body=source; hair=hairRenderer; dress=dressRenderer; panties=pantiesRenderer;
            resolvedBody=null; attachments=null; Apply();
        }
        private void OnEnable() => Apply();
        private void OnValidate() { resolvedBody=null;attachments=null;Apply(); }
        private void LateUpdate() => Apply();
        public void Apply()
        {
            if (body == null || body.sharedMesh == null || hair == null || dress == null || panties == null) return;
            attachments ??= new[] { hair,dress,panties };
            if (resolvedBody != body.sharedMesh || bodyIndices == null)
            {
                resolvedBody=body.sharedMesh; bodyIndices=new int[attachments.Length][];
                for(int p=0;p<attachments.Length;p++)
                {
                    var mesh=attachments[p].sharedMesh;
                    bodyIndices[p]=new int[mesh.blendShapeCount];
                    for(int s=0;s<mesh.blendShapeCount;s++) bodyIndices[p][s]=resolvedBody.GetBlendShapeIndex(mesh.GetBlendShapeName(s));
                }
            }
            properties ??= new MaterialPropertyBlock(); body.GetPropertyBlock(properties);
            properties.SetFloat("_WardrobeCoverageEnabled",showPanties ? 1 : 0);
            properties.SetFloat("_WardrobeDressCoverageEnabled",showDress ? 1 : 0);
            body.SetPropertyBlock(properties);
            // Imported attachments may already use UV3 for ordinary texture data.
            // The authored coverage channel belongs exclusively to Lara's body.
            properties.SetFloat("_WardrobeCoverageEnabled",0);
            properties.SetFloat("_WardrobeDressCoverageEnabled",0);
            // Skinned bounds are expressed relative to rootBone, not in mesh space.
            // All attachments use the body's root, so its envelope is already correct.
            var bounds=body.localBounds;bounds.Expand(.4f);
            var worldBounds=body.bounds;worldBounds.Expand(.4f);
            for(int p=0;p<attachments.Length;p++)
            {
                var renderer=attachments[p]; renderer.enabled=p==0?showHair:p==1?showDress:showPanties;
                renderer.updateWhenOffscreen=true;
                renderer.allowOcclusionWhenDynamic=false;
                renderer.localBounds=bounds;
                // Refresh the shared world envelope after animation as well. Unity's
                // automatic skinned AABB update can otherwise replace local bounds.
                renderer.bounds=worldBounds;
                renderer.forceRenderingOff=body.forceRenderingOff;
                renderer.SetPropertyBlock(properties);
                for(int s=0;s<bodyIndices[p].Length;s++)
                    if(bodyIndices[p][s]>=0) renderer.SetBlendShapeWeight(s,body.GetBlendShapeWeight(bodyIndices[p][s]));
            }
        }
        public bool Validate(out string reason)
        {
            if(body == null || hair == null || dress == null || panties == null) { reason="First outfit renderers are missing."; return false; }
            foreach(var renderer in new[] { hair,dress,panties })
            {
                if(renderer.transform.localToWorldMatrix != body.transform.localToWorldMatrix) { reason="Attachment dissolve coordinates differ from the body."; return false; }
                if(renderer.sharedMesh == null || renderer.sharedMaterials.Length != renderer.sharedMesh.subMeshCount) { reason="Attachment mesh/material slots differ."; return false; }
                foreach(var material in renderer.sharedMaterials)
                    foreach(string property in new[] { "_DissolveEnabled","_DissolveProgress","_DissolveBoundsMin","_DissolveBoundsSize","_DissolveFieldParams","_DissolveEdgeWidth","_DissolveEdgeColor","_DissolveEdgeEmission" })
                        if(material == null || !material.HasProperty(property)) { reason="Attachment dissolve contract missing: " + property; return false; }
            }
            reason=null; return true;
        }
    }
}
