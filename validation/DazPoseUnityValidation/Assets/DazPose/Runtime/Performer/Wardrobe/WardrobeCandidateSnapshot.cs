using System;
using System.Collections.Generic;
using System.Linq;

namespace DazPose.Performer
{
    /// <summary>Resolved authoring choices; callers receive defensive arrays, never mutable preset parts.</summary>
    public sealed class WardrobeCandidateSnapshot
    {
        private readonly WardrobePreset.Part[] _parts;
        private readonly WardrobeConfiguration.MaterialOverride[] _materials;
        public string PresetId { get; }
        public int ConfigurationRevision { get; }
        public int PopulatedMask { get; }
        public float StandingLift { get; }
        public float FootShrink { get; }
        public WardrobeConfiguration.MaterialOverride[] MaterialOverrides => CopyMaterials(_materials);
        public WardrobePreset.Part[] Parts => Copy(_parts);
        public int[] ReachableMasks
        {
            get
            {
                var masks = new List<int>();
                int ceiling = 2;
                while (true)
                {
                    int visible = WardrobeLayerState.VisibleMask(PopulatedMask, ceiling);
                    if (!masks.Contains(visible)) masks.Add(visible);
                    if (visible == 0) return masks.ToArray();
                    ceiling = WardrobeLayerState.RemoveHighestCeiling(PopulatedMask, ceiling);
                }
            }
        }

        private WardrobeCandidateSnapshot(WardrobePreset preset, WardrobeConfiguration config, WardrobePreset.Part[] parts)
        {
            PresetId = preset.PresetId;
            ConfigurationRevision = config != null ? config.Revision : preset.ConfigurationRevision;
            _parts = Copy(parts);
            _materials = config != null ? config.MaterialOverrides.Where(m => m != null && m.presetId == preset.PresetId).ToArray() : Array.Empty<WardrobeConfiguration.MaterialOverride>();
            float lift = preset.Footwear != null ? preset.Footwear.standingHeight : 0;
            float shrink = preset.Footwear != null ? preset.Footwear.footShrink : 0;
            var footwear = config != null ? config.FootwearOverrides.FirstOrDefault(f => f != null && f.presetId == preset.PresetId) : null;
            if (footwear != null)
            {
                if (footwear.standingHeightOverride) lift = UnityEngine.Mathf.Clamp(footwear.standingHeight, 0, .3f);
                if (footwear.footShrinkOverride) shrink = UnityEngine.Mathf.Clamp(footwear.footShrink, 0, .1f);
            }
            StandingLift = lift; FootShrink = shrink;
            PopulatedMask = parts.Aggregate(0, (mask, part) => mask | (1 << part.layer));
        }

        public static WardrobeCandidateSnapshot Resolve(WardrobePreset preset, WardrobeConfiguration configuration)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (!preset.Validate(out string error)) throw new InvalidOperationException(error);
            var parts = preset.Parts;
            var rows = configuration != null ? configuration.Assignments : Array.Empty<WardrobeConfiguration.PieceAssignment>();
            var materialKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in configuration != null ? configuration.MaterialOverrides : Array.Empty<WardrobeConfiguration.MaterialOverride>())
            {
                if (row == null || row.presetId != preset.PresetId) continue;
                var piece = preset.Package.pieces.FirstOrDefault(p => p.id == row.sourcePieceId);
                if (piece == null || !int.TryParse(row.materialSlotId, out int slot) || slot < 0 || slot >= piece.materials.Length)
                    throw new InvalidOperationException("Needs Repair: invalid material slot " + preset.PresetId + "/" + row.sourcePieceId + "/" + row.materialSlotId);
                if (!materialKeys.Add(row.sourcePieceId + "/" + row.materialSlotId)) throw new InvalidOperationException("Duplicate material override: " + row.sourcePieceId + "/" + row.materialSlotId);
            }
            if (configuration != null && configuration.FootwearOverrides.Count(f => f != null && f.presetId == preset.PresetId) > 1)
                throw new InvalidOperationException("Duplicate footwear tuning: " + preset.PresetId);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows.Where(row => row != null && row.presetId == preset.PresetId))
            {
                if (!seen.Add(row.sourcePieceId)) throw new InvalidOperationException("Duplicate assignment: " + preset.PresetId + "/" + row.sourcePieceId);
                var part = parts.FirstOrDefault(part => part.sourcePieceId == row.sourcePieceId);
                if (part == null) throw new InvalidOperationException("Needs Repair: source piece no longer exists: " + preset.PresetId + "/" + row.sourcePieceId);
                if (row.layer < 0 || row.layer > 2) throw new InvalidOperationException("Layer must be Base, 1 or 2: " + row.sourcePieceId);
                part.layer = row.layer;
            }
            foreach (var part in parts)
            foreach (var ownerId in part.requiredOwnerPieceIds)
            {
                var owner = parts.First(p => p.sourcePieceId == ownerId);
                if (owner.layer > part.layer) throw new InvalidOperationException("Dependent garment would outlive its owner: " + part.sourcePieceId + " requires " + ownerId);
            }
            return new WardrobeCandidateSnapshot(preset, configuration, parts);
        }

        private static WardrobePreset.Part[] Copy(WardrobePreset.Part[] parts) => parts.Select(part => new WardrobePreset.Part
        {
            sourcePieceId = part.sourcePieceId, layer = part.layer,
            requiredOwnerPieceIds = (string[])part.requiredOwnerPieceIds.Clone()
        }).ToArray();
        private static WardrobeConfiguration.MaterialOverride[] CopyMaterials(WardrobeConfiguration.MaterialOverride[] rows) => rows.Select(row =>
            new WardrobeConfiguration.MaterialOverride { presetId = row.presetId, sourcePieceId = row.sourcePieceId,
                materialSlotId = row.materialSlotId, material = row.material, opacityOverride = row.opacityOverride, opacity = row.opacity }).ToArray();
    }
}
