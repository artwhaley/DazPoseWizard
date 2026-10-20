using System;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Generated prefab metadata. The descriptor is passive; the actor owns selection and transitions.</summary>
    [DisallowMultipleComponent]
    public sealed class WardrobeAttachmentDescriptor : MonoBehaviour
    {
        [Serializable] public sealed class LocalBoneRecord
        {
            public string name, parent;
            public Vector3 position, scale;
            public Quaternion rotation = Quaternion.identity;
        }
        [Serializable] public sealed class PartRecord
        {
            public string stablePieceId, stableKey, role, rootBoneName;
            public SkinnedMeshRenderer renderer;
            public int layer, coverageChannel = -1;
            public bool shell, requiresBentFootPose;
            public string[] boneNames = Array.Empty<string>();
            public LocalBoneRecord[] localBones = Array.Empty<LocalBoneRecord>();
        }

        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private WardrobeAttachmentRole attachmentRole;
        [SerializeField] private string generationId, characterSignature, stableKey, presetId, sourceIdentity, contentHash;
        [SerializeField] private PartRecord[] parts = Array.Empty<PartRecord>();
        public int SchemaVersion => schemaVersion;
        public WardrobeAttachmentRole AttachmentRole => attachmentRole;
        public string GenerationId => generationId;
        public string CharacterSignature => characterSignature;
        public string StableKey => stableKey;
        public string PresetId => presetId;
        public string SourceIdentity => sourceIdentity;
        public string ContentHash => contentHash;
        public PartRecord[] Parts => parts == null ? Array.Empty<PartRecord>() : (PartRecord[])parts.Clone();

#if UNITY_EDITOR
        public void ConfigureGenerated(WardrobeAttachmentRole role, string generation, string signature,
            string key, string ownerPresetId, string source, string hash, PartRecord[] generatedParts)
        {
            attachmentRole = role; generationId = generation; characterSignature = signature;
            stableKey = key; presetId = ownerPresetId; sourceIdentity = source; contentHash = hash;
            parts = generatedParts == null ? Array.Empty<PartRecord>() : (PartRecord[])generatedParts.Clone();
            schemaVersion = 1;
        }
#endif

        internal void ConfigureTransient(WardrobeAttachmentRole role, string generation, string signature,
            string key, string ownerPresetId, string source, PartRecord[] generatedParts)
        {
            attachmentRole = role; generationId = generation; characterSignature = signature;
            stableKey = key; presetId = ownerPresetId; sourceIdentity = source; contentHash = "transient";
            parts = generatedParts == null ? Array.Empty<PartRecord>() : (PartRecord[])generatedParts.Clone();
            schemaVersion = 1;
        }

        public bool Validate(out string error)
        {
            if (schemaVersion != 1) { error = "Unsupported wardrobe attachment descriptor schema."; return false; }
            if (string.IsNullOrWhiteSpace(generationId) || string.IsNullOrWhiteSpace(characterSignature) ||
                string.IsNullOrWhiteSpace(stableKey) || parts == null || parts.Length == 0)
            { error = "Wardrobe attachment descriptor identity or parts are missing."; return false; }
            foreach (var part in parts)
            {
                if (part == null || string.IsNullOrWhiteSpace(part.stablePieceId) || part.renderer == null ||
                    part.renderer.sharedMesh == null || part.boneNames == null || part.boneNames.Length != part.renderer.sharedMesh.bindposes.Length)
                { error = "Wardrobe attachment part mesh, renderer or bone map is invalid."; return false; }
                if (part.renderer.sharedMaterials.Length != part.renderer.sharedMesh.subMeshCount)
                { error = "Wardrobe attachment mesh/material slots differ: " + part.stablePieceId; return false; }
            }
            error = null; return true;
        }
    }
}
