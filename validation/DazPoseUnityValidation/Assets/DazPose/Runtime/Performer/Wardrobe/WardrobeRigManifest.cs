using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "DazPose/Wardrobe/Rig Manifest")]
    public sealed class WardrobeRigManifest : ScriptableObject
    {
        [Serializable] public sealed class ClothingEntry { public string presetId; public WardrobeAttachmentDescriptor prefab; }
        [Serializable] public sealed class HairEntry { public string stableHairKey; public WardrobeAttachmentDescriptor prefab; }
        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private string generationId, catalogIdentity, characterSignature;
        [SerializeField] private ClothingEntry[] clothing = Array.Empty<ClothingEntry>();
        [SerializeField] private HairEntry[] hair = Array.Empty<HairEntry>();
        public int SchemaVersion => schemaVersion;
        public string GenerationId => generationId;
        public string CatalogIdentity => catalogIdentity;
        public string CharacterSignature => characterSignature;
        public ClothingEntry[] Clothing => clothing == null ? Array.Empty<ClothingEntry>() : (ClothingEntry[])clothing.Clone();
        public HairEntry[] Hair => hair == null ? Array.Empty<HairEntry>() : (HairEntry[])hair.Clone();
        public bool TryGetClothing(string presetId, out WardrobeAttachmentDescriptor descriptor)
        {
            var entry = (clothing ?? Array.Empty<ClothingEntry>()).SingleOrDefault(x => x != null && x.presetId == presetId);
            descriptor = entry != null ? entry.prefab : null; return descriptor != null;
        }
        public bool TryGetHair(string stableKey, out WardrobeAttachmentDescriptor descriptor)
        {
            var entry = (hair ?? Array.Empty<HairEntry>()).SingleOrDefault(x => x != null && x.stableHairKey == stableKey);
            descriptor = entry != null ? entry.prefab : null; return descriptor != null;
        }
#if UNITY_EDITOR
        public void ConfigureGenerated(string generation, string identity, string signature,
            ClothingEntry[] clothingEntries, HairEntry[] hairEntries)
        {
            generationId = generation; catalogIdentity = identity; characterSignature = signature;
            clothing = clothingEntries == null ? Array.Empty<ClothingEntry>() : (ClothingEntry[])clothingEntries.Clone();
            hair = hairEntries == null ? Array.Empty<HairEntry>() : (HairEntry[])hairEntries.Clone();
            schemaVersion = 1;
        }
#endif
        public bool Validate(WardrobeCatalog catalog, out string error)
        {
            error = null;
            if (schemaVersion != 1 || catalog == null || string.IsNullOrWhiteSpace(generationId) ||
                string.IsNullOrWhiteSpace(characterSignature))
            { error = "Wardrobe rig manifest identity or schema is invalid."; return false; }
            if (!catalog.Validate(out error)) return false;
            if (!string.Equals(generationId, catalog.ReleasedGenerationId, StringComparison.Ordinal) ||
                !string.Equals(characterSignature, catalog.Presets.FirstOrDefault(p => p != null)?.CharacterSignature, StringComparison.Ordinal))
            { error = "Wardrobe rig manifest generation/signature does not match the released catalog."; return false; }
            var presetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in clothing ?? Array.Empty<ClothingEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.presetId) || !presetIds.Add(entry.presetId) ||
                    entry.prefab == null || entry.prefab.AttachmentRole != WardrobeAttachmentRole.Clothing ||
                    !WardrobeRigManifestRules.MatchesIdentity(generationId, characterSignature, entry.prefab.GenerationId, entry.prefab.CharacterSignature, out error) ||
                    entry.prefab.PresetId != entry.presetId || !entry.prefab.Validate(out error))
                { error = "Invalid or duplicate clothing prefab row: " + (entry?.presetId ?? "<null>") + (error == null ? string.Empty : " / " + error); return false; }
                var preset = catalog.Presets.First(p => p.PresetId == entry.presetId);
                var expected = preset.Package.pieces.Where(p => !string.Equals(p.role, "hair", StringComparison.OrdinalIgnoreCase)).Select(p => p.id);
                var actual = entry.prefab.Parts.Select(p => p.stablePieceId);
                if (!WardrobeRigManifestRules.HasExactUniqueKeys(expected, actual, out error) || entry.prefab.Parts.Any(p => string.Equals(p.role, "hair", StringComparison.OrdinalIgnoreCase)))
                { error = "Clothing prefab must contain each non-hair package piece exactly once: " + entry.presetId; return false; }
            }
            var hairKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in hair ?? Array.Empty<HairEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.stableHairKey) || !hairKeys.Add(entry.stableHairKey) ||
                    entry.prefab == null || entry.prefab.AttachmentRole != WardrobeAttachmentRole.Hair ||
                    !WardrobeRigManifestRules.MatchesIdentity(generationId, characterSignature, entry.prefab.GenerationId, entry.prefab.CharacterSignature, out error) ||
                    entry.prefab.StableKey != entry.stableHairKey || !entry.prefab.Validate(out error))
                { error = "Invalid or duplicate hair prefab row: " + (entry?.stableHairKey ?? "<null>") + (error == null ? string.Empty : " / " + error); return false; }
                if (entry.prefab.Parts.Any(p => !string.Equals(p.role, "hair", StringComparison.OrdinalIgnoreCase)) || entry.prefab.Parts.Length != 1)
                { error = "Each hair prefab must contain exactly its single stable hair package piece: " + entry.stableHairKey; return false; }
            }
            if (!WardrobeRigManifestRules.HasExactUniqueKeys(catalog.Presets.Select(p => p.PresetId), presetIds, out error))
            { error = "Rig manifest must contain exactly one clothing prefab for each catalog preset."; return false; }
            var expectedHair = new HashSet<string>(catalog.Presets.SelectMany(p => p.Package.pieces
                .Where(piece => string.Equals(piece.role, "hair", StringComparison.OrdinalIgnoreCase))
                .Select(piece => p.PresetId + "/" + piece.id)), StringComparer.Ordinal);
            if (!WardrobeRigManifestRules.HasExactUniqueKeys(expectedHair, hairKeys, out error))
            { error = "Rig manifest must contain each independently switchable catalog hair key exactly once."; return false; }
            error = null; return true;
        }
    }
}
