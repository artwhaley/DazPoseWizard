using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Passive references joining one actor wardrobe owner to its generated rig assets.</summary>
    [DisallowMultipleComponent]
    public sealed class WardrobeRigBinding : MonoBehaviour
    {
        [SerializeField] private WardrobeRigManifest manifest;
        [SerializeField] private Transform clothingRoot;
        [SerializeField] private Transform hairRoot;
        public WardrobeRigManifest Manifest => manifest;
        public Transform ClothingRoot => clothingRoot;
        public Transform HairRoot => hairRoot;
        public void Configure(WardrobeRigManifest value, Transform clothes, Transform hair)
        { manifest = value; clothingRoot = clothes; hairRoot = hair; }
        public WardrobeRigManifest BindOwner(PerformerWardrobe owner, SkinnedMeshRenderer body) =>
            WardrobeAttachmentBinder.ValidateOwner(owner, body, manifest);
        public WardrobeAttachmentLease BindClothing(PerformerWardrobe owner, SkinnedMeshRenderer body, string presetId)
        {
            var rig = BindOwner(owner, body);
            if (!rig.TryGetClothing(presetId, out var descriptor)) throw new System.InvalidOperationException("Clothing prefab is missing from the rig manifest: " + presetId);
            return WardrobeAttachmentBinder.Bind(descriptor, body, clothingRoot, rig.GenerationId, rig.CharacterSignature);
        }
        public WardrobeAttachmentLease BindHair(PerformerWardrobe owner, SkinnedMeshRenderer body, string stableHairKey)
        {
            var rig = BindOwner(owner, body);
            if (!rig.TryGetHair(stableHairKey, out var descriptor)) throw new System.InvalidOperationException("Hair prefab is missing from the rig manifest: " + stableHairKey);
            return WardrobeAttachmentBinder.Bind(descriptor, body, hairRoot, rig.GenerationId, rig.CharacterSignature);
        }
    }
}
