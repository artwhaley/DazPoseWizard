using UnityEngine;

namespace DazPose.Performer
{
    public enum WardrobeFitStatus { Candidate, Validated, NeedsFit, Invalid }

    [CreateAssetMenu(menuName = "DAZ Pose/Wardrobe/Fit State")]
    public sealed class WardrobeFitState : ScriptableObject
    {
        [SerializeField, Range(0, 7)] private int visibleMask;
        [SerializeField] private string characterSignature;
        [SerializeField] private WardrobeFitStatus status;
        [SerializeField] private string[] issueCodes = System.Array.Empty<string>();
        [SerializeField] private Mesh bodyMesh;
        [SerializeField] private Mesh[] attachmentMeshes = System.Array.Empty<Mesh>();
        [SerializeField] private PerformerSurfaceBindingAsset surfaceBindings;
        [SerializeField] private PerformerDissolveProfile dissolveProfile;
        [SerializeField] private WardrobeCoverageState coverage;
        [SerializeField] private WardrobeFootwearState footwear;

        public int VisibleMask => visibleMask;
        public string CharacterSignature => characterSignature;
        public WardrobeFitStatus Status => status;
        public string[] IssueCodes => (string[])issueCodes.Clone();
        public Mesh BodyMesh => bodyMesh;
        public Mesh[] AttachmentMeshes => (Mesh[])attachmentMeshes.Clone();
        public PerformerSurfaceBindingAsset SurfaceBindings => surfaceBindings;
        public PerformerDissolveProfile DissolveProfile => dissolveProfile;
        public WardrobeCoverageState Coverage => coverage;
        public WardrobeFootwearState Footwear => footwear;

#if UNITY_EDITOR
        public void ConfigureGenerated(int mask, string signature, WardrobeFitStatus fitStatus,
            Mesh body, Mesh[] attachments, PerformerSurfaceBindingAsset bindings,
            PerformerDissolveProfile profile, string[] coveredPieceIds, bool shoesActive,
            PerformerFootwearProfile shoeProfile)
        {
            visibleMask = mask; characterSignature = signature; status = fitStatus;
            issueCodes = System.Array.Empty<string>(); bodyMesh = body;
            attachmentMeshes = attachments ?? System.Array.Empty<Mesh>();
            surfaceBindings = bindings; dissolveProfile = profile;
            coverage = new WardrobeCoverageState(coveredPieceIds);
            footwear = new WardrobeFootwearState(shoesActive,
                shoesActive && shoeProfile != null ? shoeProfile.standingHeight : 0f,
                shoesActive && shoeProfile != null ? shoeProfile.footShrink : 0f,
                shoesActive && shoeProfile != null ? shoeProfile.sourceName : "barefoot");
        }

        public void ConfigureCandidate(int mask, string signature, string[] issues,
            Mesh diagnosticBody, Mesh[] attachments, PerformerSurfaceBindingAsset bindings,
            PerformerDissolveProfile profile, string[] coveredPieceIds,
            bool shoesActive, bool footPoseActive, PerformerFootwearProfile shoeProfile)
            => ConfigureCandidate(mask, signature, issues, diagnosticBody, attachments, bindings, profile,
                coveredPieceIds, shoesActive, footPoseActive, shoeProfile,
                shoeProfile != null ? shoeProfile.standingHeight : 0f,
                shoeProfile != null ? shoeProfile.footShrink : 0f);

        public void ConfigureCandidate(int mask, string signature, string[] issues,
            Mesh diagnosticBody, Mesh[] attachments, PerformerSurfaceBindingAsset bindings,
            PerformerDissolveProfile profile, string[] coveredPieceIds,
            bool shoesActive, bool footPoseActive, PerformerFootwearProfile shoeProfile,
            float standingLift, float footShrink)
        {
            visibleMask = mask; characterSignature = signature;
            issueCodes = issues ?? System.Array.Empty<string>();
            status = issueCodes.Length == 0 ? WardrobeFitStatus.Validated : WardrobeFitStatus.NeedsFit;
            bodyMesh = diagnosticBody; attachmentMeshes = attachments ?? System.Array.Empty<Mesh>();
            surfaceBindings = bindings; dissolveProfile = profile;
            coverage = new WardrobeCoverageState(coveredPieceIds);
            footwear = new WardrobeFootwearState(shoesActive, footPoseActive,
                footPoseActive ? standingLift : 0f,
                footPoseActive ? footShrink : 0f,
                !footPoseActive ? "barefoot" : shoeProfile != null ? shoeProfile.sourceName : "bent-foot pose only");
        }
#endif
    }

    [System.Serializable] public sealed class WardrobeCoverageState
    {
        [SerializeField] private string[] visiblePieceIds = System.Array.Empty<string>();
        public string[] VisiblePieceIds => (string[])visiblePieceIds.Clone();
        public WardrobeCoverageState() { }
        public WardrobeCoverageState(string[] values) { visiblePieceIds = values ?? System.Array.Empty<string>(); }
    }

    [System.Serializable] public sealed class WardrobeFootwearState
    {
        [SerializeField] private bool footwearActive;
        [SerializeField] private bool bentFootPoseActive;
        [SerializeField] private float standingLift;
        [SerializeField] private float footShrink;
        [SerializeField] private string supportProfileId;
        public bool FootwearActive => footwearActive;
        public bool BentFootPoseActive => bentFootPoseActive;
        public float StandingLift => standingLift;
        public float FootShrink => footShrink;
        public string SupportProfileId => supportProfileId;
        public WardrobeFootwearState() { }
        public WardrobeFootwearState(bool active, float lift, float shrink, string id)
            : this(active, active, lift, shrink, id) { }
        public WardrobeFootwearState(bool active, bool poseActive, float lift, float shrink, string id)
        { footwearActive = active; bentFootPoseActive = poseActive; standingLift = lift; footShrink = shrink; supportProfileId = id; }
    }
}
