using System;
using System.Linq;
using UnityEngine;

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "DAZ Pose/Wardrobe/Configuration")]
    public sealed class WardrobeConfiguration : ScriptableObject
    {
        [Serializable] public sealed class PieceAssignment
        {
            public string presetId;
            public string sourcePieceId;
            [Range(0, 2)] public int layer;
        }
        [Serializable] public sealed class MaterialOverride
        {
            public string presetId;
            public string sourcePieceId;
            public string materialSlotId;
            public Material material;
            public bool opacityOverride;
            [Range(0, 1)] public float opacity = 1f;
        }
        [Serializable] public sealed class FootwearOverride
        {
            public string presetId;
            public bool standingHeightOverride;
            [Range(0, .3f)] public float standingHeight;
            public bool footShrinkOverride;
            [Range(0, .1f)] public float footShrink = .05f;
        }
        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private int revision;
        [SerializeField] private string generationHash;
        [SerializeField] private PieceAssignment[] assignments = Array.Empty<PieceAssignment>();
        [SerializeField] private MaterialOverride[] materialOverrides = Array.Empty<MaterialOverride>();
        [SerializeField] private FootwearOverride[] footwearOverrides = Array.Empty<FootwearOverride>();
        [SerializeField] private string[] inputHashes = Array.Empty<string>();

        public int SchemaVersion => schemaVersion;
        public int Revision => revision;
        public string GenerationHash => generationHash;
        public PieceAssignment[] Assignments => Clone(assignments);
        public MaterialOverride[] MaterialOverrides => Clone(materialOverrides);
        public FootwearOverride[] FootwearOverrides => Clone(footwearOverrides);
        public string[] InputHashes => (string[])inputHashes.Clone();

#if UNITY_EDITOR
        public void ConfigureGenerated(PieceAssignment[] rows)
        { assignments = Clone(rows); }
        public void ConfigureGenerated(PieceAssignment[] rows, MaterialOverride[] overrides)
        { assignments = Clone(rows); materialOverrides = Clone(overrides); }
        public void ConfigureGenerated(PieceAssignment[] rows, MaterialOverride[] overrides, FootwearOverride[] shoes)
        { assignments = Clone(rows); materialOverrides = Clone(overrides); footwearOverrides = Clone(shoes); }

        public bool TrySetWorkingAssignments(PieceAssignment[] rows, out string error)
        {
            rows ??= Array.Empty<PieceAssignment>();
            var keys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                if (row == null || !WardrobeCatalog.IsValidId(row.presetId) || string.IsNullOrWhiteSpace(row.sourcePieceId))
                { error = "Every assignment needs a stable preset and source-piece ID."; return false; }
                if (row.layer < 0 || row.layer > 2)
                { error = "Layer assignments must be Base, 1 or 2."; return false; }
                if (!keys.Add(row.presetId + "/" + row.sourcePieceId))
                { error = "A source piece can have only one layer assignment."; return false; }
            }
            assignments = Clone(rows);
            error = null;
            return true;
        }

        public void CommitRevision(string stableGenerationHash, string[] sourceInputHashes)
        {
            revision++;
            generationHash = stableGenerationHash ?? string.Empty;
            inputHashes = sourceInputHashes ?? Array.Empty<string>();
        }

        public void SetMaterialOpacity(string presetId, string pieceId, string slotId, float opacity)
        {
            var rows = Clone(materialOverrides).ToList();
            var row = rows.FirstOrDefault(x => x.presetId == presetId && x.sourcePieceId == pieceId && x.materialSlotId == slotId);
            if (row == null)
            {
                row = new MaterialOverride { presetId = presetId, sourcePieceId = pieceId, materialSlotId = slotId };
                rows.Add(row);
            }
            row.opacityOverride = true;
            row.opacity = Mathf.Clamp01(opacity);
            materialOverrides = rows.ToArray();
        }

        public void ClearMaterialOpacity(string presetId, string pieceId, string slotId)
        {
            var rows = Clone(materialOverrides).ToList();
            var row = rows.FirstOrDefault(x => x.presetId == presetId && x.sourcePieceId == pieceId && x.materialSlotId == slotId);
            if (row == null) return;
            row.opacityOverride = false;
            materialOverrides = rows.ToArray();
        }

        public bool TryGetFootwearTuning(string presetId, PerformerFootwearProfile source,
            out float standingHeight, out float footShrink)
        {
            standingHeight = source != null ? source.standingHeight : 0f;
            footShrink = source != null ? source.footShrink : 0f;
            var row = footwearOverrides.FirstOrDefault(x => x != null && x.presetId == presetId);
            if (row == null) return false;
            if (row.standingHeightOverride) standingHeight = Mathf.Clamp(row.standingHeight, 0f, .3f);
            if (row.footShrinkOverride) footShrink = Mathf.Clamp(row.footShrink, 0f, .1f);
            return row.standingHeightOverride || row.footShrinkOverride;
        }

        public void SetFootwearTuning(string presetId, bool setHeight, float height, bool setShrink, float shrink)
        {
            var rows = Clone(footwearOverrides).ToList();
            var row = rows.FirstOrDefault(x => x.presetId == presetId);
            if (row == null) { row = new FootwearOverride { presetId = presetId }; rows.Add(row); }
            row.standingHeightOverride = setHeight; row.standingHeight = Mathf.Clamp(height, 0f, .3f);
            row.footShrinkOverride = setShrink; row.footShrink = Mathf.Clamp(shrink, 0f, .1f);
            footwearOverrides = rows.ToArray();
        }

        public void ClearFootwearTuning(string presetId)
        {
            footwearOverrides = Clone(footwearOverrides).Where(x => x.presetId != presetId).ToArray();
        }

#endif

        private static PieceAssignment[] Clone(PieceAssignment[] source) => (source ?? Array.Empty<PieceAssignment>())
            .Select(row => row == null ? null : new PieceAssignment
                { presetId = row.presetId, sourcePieceId = row.sourcePieceId, layer = row.layer }).ToArray();
        private static MaterialOverride[] Clone(MaterialOverride[] source) => (source ?? Array.Empty<MaterialOverride>())
            .Select(row => row == null ? null : new MaterialOverride
                { presetId = row.presetId, sourcePieceId = row.sourcePieceId, materialSlotId = row.materialSlotId,
                    material = row.material, opacityOverride = row.opacityOverride, opacity = row.opacity }).ToArray();
        private static FootwearOverride[] Clone(FootwearOverride[] source) => (source ?? Array.Empty<FootwearOverride>())
            .Select(row => row == null ? null : new FootwearOverride { presetId = row.presetId,
                standingHeightOverride = row.standingHeightOverride, standingHeight = row.standingHeight,
                footShrinkOverride = row.footShrinkOverride, footShrink = row.footShrink }).ToArray();
    }
}
