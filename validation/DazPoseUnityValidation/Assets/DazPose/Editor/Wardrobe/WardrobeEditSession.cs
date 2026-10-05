using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Performer;
using UnityEngine;

namespace DazPose.UnityValidation
{
    /// <summary>Unsaved setup-window edits. Persistent assets change only on Commit.</summary>
    public sealed class WardrobeEditSession
    {
        private readonly WardrobeConfiguration _source;
        private WardrobeConfiguration.PieceAssignment[] _assignments;
        private WardrobeConfiguration.MaterialOverride[] _overrides;
        private WardrobeConfiguration.FootwearOverride[] _footwear;

        public bool IsDirty { get; private set; }
        public WardrobeConfiguration.PieceAssignment[] Assignments => Clone(_assignments);
        public WardrobeConfiguration.MaterialOverride[] MaterialOverrides => Clone(_overrides);
        public WardrobeConfiguration.FootwearOverride[] FootwearOverrides => Clone(_footwear);

        public WardrobeEditSession(WardrobeConfiguration source, WardrobeCatalog catalog)
        {
            _source = source;
            Reload(catalog);
        }

        public void Reload(WardrobeCatalog catalog)
        {
            _assignments = _source != null ? _source.Assignments : Array.Empty<WardrobeConfiguration.PieceAssignment>();
            _overrides = _source != null ? _source.MaterialOverrides : Array.Empty<WardrobeConfiguration.MaterialOverride>();
            _footwear = _source != null ? _source.FootwearOverrides : Array.Empty<WardrobeConfiguration.FootwearOverride>();
            if (catalog != null)
            {
                var map = _assignments.Where(x => x != null).ToDictionary(Key, x => x, StringComparer.Ordinal);
                foreach (var preset in catalog.Presets)
                foreach (var part in preset.Parts)
                {
                    string key = preset.PresetId + "/" + part.sourcePieceId;
                    if (!map.ContainsKey(key)) map.Add(key, new WardrobeConfiguration.PieceAssignment
                        { presetId = preset.PresetId, sourcePieceId = part.sourcePieceId, layer = part.layer });
                }
                _assignments = map.Values.OrderBy(x => x.presetId, StringComparer.Ordinal)
                    .ThenBy(x => x.sourcePieceId, StringComparer.Ordinal).ToArray();
            }
            IsDirty = false;
        }

        public bool TrySetLayer(string presetId, string pieceId, int layer, out string error)
        {
            if (layer < 0 || layer > 2) { error = "Layer must be Base, 1 or 2."; return false; }
            var row = _assignments.FirstOrDefault(x => x.presetId == presetId && x.sourcePieceId == pieceId);
            if (row == null) { error = "Stable source piece is not in this setup session."; return false; }
            row.layer = layer;
            IsDirty = true;
            error = null;
            return true;
        }

        public void SetOpacity(string presetId, string pieceId, string slotId, float opacity)
        {
            var row = _overrides.FirstOrDefault(x => x.presetId == presetId && x.sourcePieceId == pieceId && x.materialSlotId == slotId);
            if (row == null)
            {
                row = new WardrobeConfiguration.MaterialOverride
                    { presetId = presetId, sourcePieceId = pieceId, materialSlotId = slotId };
                _overrides = _overrides.Concat(new[] { row }).ToArray();
            }
            row.opacityOverride = true;
            row.opacity = Mathf.Clamp01(opacity);
            IsDirty = true;
        }

        public void ClearOpacity(string presetId, string pieceId, string slotId)
        {
            var row = _overrides.FirstOrDefault(x => x.presetId == presetId && x.sourcePieceId == pieceId && x.materialSlotId == slotId);
            if (row == null || !row.opacityOverride) return;
            row.opacityOverride = false;
            IsDirty = true;
        }

        public WardrobeConfiguration.FootwearOverride GetFootwear(string presetId, PerformerFootwearProfile profile)
        {
            var row = _footwear.FirstOrDefault(x => x.presetId == presetId);
            var result = row != null ? Clone(new[] { row })[0] : new WardrobeConfiguration.FootwearOverride { presetId = presetId };
            if (!result.standingHeightOverride) result.standingHeight = profile != null ? profile.standingHeight : 0f;
            if (!result.footShrinkOverride) result.footShrink = profile != null ? profile.footShrink : 0f;
            return result;
        }

        public void SetFootwear(string presetId, bool setHeight, float height, bool setShrink, float shrink)
        {
            var row = GetFootwear(presetId, null);
            row.standingHeightOverride = setHeight; row.standingHeight = Mathf.Clamp(height, 0f, .3f);
            row.footShrinkOverride = setShrink; row.footShrink = Mathf.Clamp(shrink, 0f, .1f);
            _footwear = _footwear.Where(x => x.presetId != presetId).Concat(new[] { row }).ToArray();
            IsDirty = true;
        }

        public void ClearFootwear(string presetId)
        {
            _footwear = _footwear.Where(x => x.presetId != presetId).ToArray();
            IsDirty = true;
        }

        public void MarkCommitted() => IsDirty = false;

        public static string Key(WardrobeConfiguration.PieceAssignment row) => row.presetId + "/" + row.sourcePieceId;
        private static WardrobeConfiguration.PieceAssignment[] Clone(WardrobeConfiguration.PieceAssignment[] rows) =>
            (rows ?? Array.Empty<WardrobeConfiguration.PieceAssignment>()).Select(x => x == null ? null :
                new WardrobeConfiguration.PieceAssignment { presetId = x.presetId, sourcePieceId = x.sourcePieceId, layer = x.layer }).ToArray();
        private static WardrobeConfiguration.MaterialOverride[] Clone(WardrobeConfiguration.MaterialOverride[] rows) =>
            (rows ?? Array.Empty<WardrobeConfiguration.MaterialOverride>()).Select(x => x == null ? null :
                new WardrobeConfiguration.MaterialOverride { presetId = x.presetId, sourcePieceId = x.sourcePieceId,
                    materialSlotId = x.materialSlotId, material = x.material, opacityOverride = x.opacityOverride, opacity = x.opacity }).ToArray();
        private static WardrobeConfiguration.FootwearOverride[] Clone(WardrobeConfiguration.FootwearOverride[] rows) =>
            (rows ?? Array.Empty<WardrobeConfiguration.FootwearOverride>()).Select(x => x == null ? null :
                new WardrobeConfiguration.FootwearOverride { presetId = x.presetId,
                    standingHeightOverride = x.standingHeightOverride, standingHeight = x.standingHeight,
                    footShrinkOverride = x.footShrinkOverride, footShrink = x.footShrink }).ToArray();
    }
}
