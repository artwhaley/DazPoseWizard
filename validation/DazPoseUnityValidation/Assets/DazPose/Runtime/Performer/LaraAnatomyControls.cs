using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Owns only the added anatomy channels; facial animation remains with existing owners.</summary>
    [DisallowMultipleComponent]
    public sealed class LaraAnatomyControls : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer body;
        [SerializeField, Range(0,1)] private float capturedOpening;
        [SerializeField, Range(0,1)] private float nipples;
        [Tooltip("Mesh adjustment relative to the baked Daz value. Daz joint-center/end-point ERC is not applied yet.")]
        [SerializeField, Range(0,1)] private float laraBreasts = .5555556f;
        [SerializeField, HideInInspector] private float bakedLaraBreasts = .5555556f;
        private int openingIndex = -1, nippleIndex = -1, breastIndex = -1;
        private Mesh resolvedMesh;
        public void Configure(SkinnedMeshRenderer renderer, float bakedValue)
        {
            body = renderer; resolvedMesh = null;
            bakedLaraBreasts = bakedValue; laraBreasts = bakedValue;
        }
        public float BakedLaraBreasts => bakedLaraBreasts;
        public float CapturedOpening { get => capturedOpening; set => capturedOpening = Mathf.Clamp01(value); }
        public float Nipples { get => nipples; set => nipples = Mathf.Clamp01(value); }
        public float LaraBreastsMeshPreview { get => laraBreasts; set => laraBreasts = Mathf.Clamp01(value); }
        /// <summary>Rebind to a replacement compatible body mesh without resetting the artist's current controls.</summary>
        public void RebindPreservingValues(SkinnedMeshRenderer renderer)
        {
            body = renderer;
            resolvedMesh = null;
            Apply();
        }
        private void LateUpdate() => Apply();
        public bool IsReady => body != null && body.sharedMesh != null
            && body.sharedMesh.GetBlendShapeIndex("CapturedOpening") >= 0
            && body.sharedMesh.GetBlendShapeIndex("Genesis8Female__PBMNipples") >= 0
            && body.sharedMesh.GetBlendShapeIndex("LaraBreastsAdjustment") >= 0;
        public void Apply()
        {
            if (body == null || body.sharedMesh == null) return;
            if (resolvedMesh != body.sharedMesh)
            {
                resolvedMesh = body.sharedMesh;
                openingIndex = resolvedMesh.GetBlendShapeIndex("CapturedOpening");
                nippleIndex = resolvedMesh.GetBlendShapeIndex("Genesis8Female__PBMNipples");
                breastIndex = resolvedMesh.GetBlendShapeIndex("LaraBreastsAdjustment");
            }
            if (openingIndex >= 0) body.SetBlendShapeWeight(openingIndex, capturedOpening*100);
            if (nippleIndex >= 0) body.SetBlendShapeWeight(nippleIndex, nipples*100);
            if (breastIndex >= 0) body.SetBlendShapeWeight(breastIndex, (laraBreasts-bakedLaraBreasts)*100);
        }
    }
}
