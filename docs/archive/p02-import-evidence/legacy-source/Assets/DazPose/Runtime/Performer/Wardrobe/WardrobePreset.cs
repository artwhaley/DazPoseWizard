using System;
using System.Linq;
using UnityEngine;

namespace DazPose.Performer
{
    public enum WardrobeHairAction { Keep, Set, Clear }

    [CreateAssetMenu(menuName = "DAZ Pose/Wardrobe/Preset")]
    public sealed class WardrobePreset : ScriptableObject
    {
        [Serializable] public sealed class Part
        {
            public string sourcePieceId;
            [Range(0, 2)] public int layer;
            public string[] requiredOwnerPieceIds = Array.Empty<string>();
        }

        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private string presetId;
        [SerializeField] private string displayName;
        [SerializeField] private string[] aliases = Array.Empty<string>();
        [SerializeField] private string characterSignature;
        [SerializeField] private int configurationRevision;
        [SerializeField] private WardrobeOutfitDefinition package;
        [SerializeField] private Part[] parts = Array.Empty<Part>();
        [SerializeField] private WardrobeHairAction hairAction = WardrobeHairAction.Keep;
        [SerializeField] private string hairId;
        [SerializeField] private GameObject hairPrefab;
        [SerializeField] private PerformerFootwearProfile footwear;
        [SerializeField] private WardrobeFitState[] fitStates = Array.Empty<WardrobeFitState>();

        public int SchemaVersion => schemaVersion;
        public string PresetId => presetId;
        public string DisplayName => displayName;
        public string[] Aliases => (string[])aliases.Clone();
        public string CharacterSignature => characterSignature;
        public int ConfigurationRevision => configurationRevision;
        public WardrobeOutfitDefinition Package => package;
        public Part[] Parts => CloneParts(parts);
        public WardrobeHairAction HairAction => hairAction;
        public string HairId => hairId;
        public GameObject HairPrefab => hairPrefab;
        public PerformerFootwearProfile Footwear => footwear;
        public WardrobeFitState[] FitStates => (WardrobeFitState[])fitStates.Clone();

#if UNITY_EDITOR
        public void ConfigureGenerated(string id, string name, string signature, int revision,
            WardrobeOutfitDefinition sourcePackage, Part[] generatedParts, string[] generatedAliases,
            WardrobeHairAction generatedHairAction, string generatedHairId, PerformerFootwearProfile generatedFootwear)
        {
            presetId = id; displayName = name; characterSignature = signature;
            configurationRevision = revision; package = sourcePackage;
            parts = CloneParts(generatedParts); aliases = generatedAliases != null ? (string[])generatedAliases.Clone() : Array.Empty<string>();
            hairAction = generatedHairAction; hairId = generatedHairId; hairPrefab = null;
            footwear = generatedFootwear;
        }

        public void SetGeneratedFitStates(WardrobeFitState[] values) => fitStates = values != null ? (WardrobeFitState[])values.Clone() : Array.Empty<WardrobeFitState>();
#endif

        private static Part[] CloneParts(Part[] source) => (source ?? Array.Empty<Part>()).Select(part => part == null ? null :
            new Part { sourcePieceId = part.sourcePieceId, layer = part.layer,
                requiredOwnerPieceIds = part.requiredOwnerPieceIds != null ? (string[])part.requiredOwnerPieceIds.Clone() : Array.Empty<string>() }).ToArray();

        public int PopulatedMask
        {
            get
            {
                int mask = 0;
                foreach (var part in parts)
                {
                    if (part == null) continue;
                    WardrobeLayerState.ValidateCeiling(part.layer);
                    if (!string.IsNullOrWhiteSpace(part.sourcePieceId)) mask |= 1 << part.layer;
                }
                return mask;
            }
        }

        public bool Validate(out string error)
        {
            if (schemaVersion != 1) { error = "Unsupported preset schema version."; return false; }
            if (!WardrobeCatalog.IsValidId(presetId)) { error = "Preset ID must be lowercase ASCII."; return false; }
            if (string.IsNullOrWhiteSpace(displayName)) { error = "Preset display name is required."; return false; }
            if (string.IsNullOrWhiteSpace(characterSignature)) { error = "Character signature is required."; return false; }
            if (package == null) { error = "Legacy package reference is missing."; return false; }
            if (parts == null || fitStates == null) { error = "Part/fit state list is null."; return false; }
            if (fitStates.Length > 4) { error = "A preset may have at most four distinct fit states."; return false; }
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var part in parts)
            {
                if (part == null || string.IsNullOrWhiteSpace(part.sourcePieceId) || !seen.Add(part.sourcePieceId))
                { error = "Part source IDs must be present and unique."; return false; }
                if (Array.FindIndex(package.pieces, p => p != null && p.id == part.sourcePieceId) < 0)
                { error = "Part ID does not exist in its legacy source package: " + part.sourcePieceId; return false; }
                if (part.layer < 0 || part.layer > 2) { error = "Part layer must be 0, 1 or 2."; return false; }
                foreach (var required in part.requiredOwnerPieceIds ?? Array.Empty<string>())
                    if (!seen.Contains(required) && Array.FindIndex(parts, p => p != null && p.sourcePieceId == required) < 0)
                    { error = "A shell dependency references an unknown source piece: " + required; return false; }
            }
            if (hairAction == WardrobeHairAction.Set &&
                (string.IsNullOrWhiteSpace(hairId) || Array.FindIndex(package.pieces,
                    p => p != null && p.id == hairId && string.Equals(p.role, "hair", StringComparison.OrdinalIgnoreCase)) < 0))
            { error = "Set hair action must reference a hair piece in the source package."; return false; }
            if (hairAction != WardrobeHairAction.Set && hairPrefab != null)
            { error = "Only an explicit Set hair action can carry a hair prefab."; return false; }
            var masks = new System.Collections.Generic.HashSet<int>();
            foreach (var fit in fitStates)
            {
                if (fit == null || !masks.Add(fit.VisibleMask)) { error = "Fit state references must exist and use distinct visible masks."; return false; }
                if (fit.CharacterSignature != characterSignature) { error = "Fit state character signature differs from preset."; return false; }
            }
            foreach (var alias in aliases ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(alias)) { error = "Aliases cannot be empty."; return false; }
            error = null;
            return true;
        }
    }
}
