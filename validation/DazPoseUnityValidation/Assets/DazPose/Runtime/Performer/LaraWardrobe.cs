using System;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Shared-rig wardrobe inventory. Coverage decisions remain per outfit.</summary>
    [ExecuteAlways, DefaultExecutionOrder(10000), DisallowMultipleComponent]
    public sealed class LaraWardrobe : MonoBehaviour
    {
        [Serializable] public sealed class Piece
        {
            public string name;
            public SkinnedMeshRenderer renderer;
            public bool shell;
            public bool visible = true;
            public bool persistentHair;
            public int coverageChannel = -1;
            public float[] materialOpacities = Array.Empty<float>();
        }
        [SerializeField] private SkinnedMeshRenderer body;
        [SerializeField] private Piece[] pieces = Array.Empty<Piece>();
        [SerializeField] private bool showClothes = true, showGlossShells = true;
        [SerializeField, Min(0)] private float boundsPadding = .35f;
        private Mesh resolvedBody;
        private int[][] shapeIndices;
        private MaterialPropertyBlock block;
        private MaterialPropertyBlock opacityBlock;
        public SkinnedMeshRenderer Body => body;
        public Piece[] Pieces => pieces;
        public bool ShowClothes { get => showClothes; set { showClothes=value; Apply(); } }
        public bool ShowGlossShells { get => showGlossShells; set { showGlossShells=value; Apply(); } }
        public void Configure(SkinnedMeshRenderer source, Piece[] inventory)
        { body=source;pieces=inventory;resolvedBody=null;shapeIndices=null;Apply(); }
        private void OnEnable() => Apply();
        private void OnValidate() { resolvedBody=null;shapeIndices=null;Apply(); }
        private void LateUpdate() => Apply();
        public void Apply()
        {
            if(body==null || body.sharedMesh==null)return;
            if(resolvedBody!=body.sharedMesh || shapeIndices==null || shapeIndices.Length!=pieces.Length)
            {
                resolvedBody=body.sharedMesh;shapeIndices=new int[pieces.Length][];
                for(int p=0;p<pieces.Length;p++)
                {
                    var mesh=pieces[p].renderer?.sharedMesh;
                    shapeIndices[p]=new int[mesh!=null ? mesh.blendShapeCount : 0];
                    for(int s=0;s<shapeIndices[p].Length;s++)shapeIndices[p][s]=resolvedBody.GetBlendShapeIndex(mesh.GetBlendShapeName(s));
                }
            }
            block ??= new MaterialPropertyBlock();body.GetPropertyBlock(block);
            float frontCoverage=0,envelopeCoverage=0;
            if(showClothes)foreach(var piece in pieces)if(!piece.persistentHair && piece.visible && (!piece.shell || showGlossShells))
            {if(piece.coverageChannel==0)frontCoverage=1;if(piece.coverageChannel==1)envelopeCoverage=1;}
            block.SetFloat("_WardrobeCoverageEnabled",frontCoverage);block.SetFloat("_WardrobeDressCoverageEnabled",envelopeCoverage);
            body.SetPropertyBlock(block);
            // Body-only coverage must never sample an attachment's texture UV channel.
            block.SetFloat("_WardrobeCoverageEnabled",0);block.SetFloat("_WardrobeDressCoverageEnabled",0);
            var local=body.localBounds;local.Expand(boundsPadding*2);
            var world=body.bounds;world.Expand(boundsPadding*2);
            for(int p=0;p<pieces.Length;p++)
            {
                var piece=pieces[p];var renderer=piece.renderer;if(renderer==null)continue;
                renderer.enabled=(piece.persistentHair ? piece.visible : showClothes && piece.visible) && (!piece.shell || showGlossShells);
                renderer.updateWhenOffscreen=true;renderer.allowOcclusionWhenDynamic=false;
                renderer.localBounds=local;renderer.bounds=world;renderer.forceRenderingOff=body.forceRenderingOff;
                renderer.SetPropertyBlock(block);
                ApplyMaterialOpacity(piece, renderer);
                for(int s=0;s<shapeIndices[p].Length;s++)if(shapeIndices[p][s]>=0)
                    renderer.SetBlendShapeWeight(s,body.GetBlendShapeWeight(shapeIndices[p][s]));
            }
        }
        private void ApplyMaterialOpacity(Piece piece, SkinnedMeshRenderer renderer)
        {
            var materials = renderer.sharedMaterials;
            for (int slot = 0; slot < materials.Length; ++slot)
            {
                float opacity = piece.materialOpacities != null && slot < piece.materialOpacities.Length
                    ? piece.materialOpacities[slot] : -1f;
                if (opacity < 0)
                {
                    renderer.SetPropertyBlock(null, slot);
                    continue;
                }
                var material = materials[slot];
                if (material == null) continue;
                string property = material.HasProperty("_BaseColor") ? "_BaseColor" :
                    material.HasProperty("_Color") ? "_Color" : null;
                if (property == null) continue;
                opacityBlock ??= new MaterialPropertyBlock();
                renderer.GetPropertyBlock(opacityBlock, slot);
                var color = material.GetColor(property); color.a = Mathf.Clamp01(opacity);
                opacityBlock.SetColor(property, color);
                renderer.SetPropertyBlock(opacityBlock, slot);
            }
        }
        public bool Validate(out string reason)
        {
            if(body==null || pieces.Length==0) { reason="Wardrobe body/inventory is missing.";return false; }
            foreach(var piece in pieces)
            {
                var renderer=piece.renderer;
                if(renderer==null || renderer.sharedMesh==null || renderer.sharedMesh.subMeshCount!=renderer.sharedMaterials.Length)
                { reason="Wardrobe mesh/material slots differ: "+piece.name;return false; }
                if(renderer.transform.localToWorldMatrix!=body.transform.localToWorldMatrix)
                { reason="Wardrobe dissolve coordinates differ: "+piece.name;return false; }
                foreach(var material in renderer.sharedMaterials)
                    foreach(string property in new[] { "_DissolveEnabled","_DissolveProgress","_DissolveBoundsMin","_DissolveBoundsSize","_DissolveFieldParams","_DissolveEdgeWidth","_DissolveEdgeColor","_DissolveEdgeEmission" })
                        if(material==null || !material.HasProperty(property)) { reason="Wardrobe dissolve contract missing: "+piece.name+"/"+property;return false; }
            }
            reason=null;return true;
        }
    }
}
