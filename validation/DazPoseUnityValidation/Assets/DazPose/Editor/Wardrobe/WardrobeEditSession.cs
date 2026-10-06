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
        private sealed class Snapshot
        {
            public WardrobeConfiguration.PieceAssignment[] Assignments;
            public WardrobeConfiguration.MaterialOverride[] Overrides;
            public WardrobeConfiguration.FootwearOverride[] Footwear;
        }

        private readonly WardrobeConfiguration _source;
        private WardrobeConfiguration.PieceAssignment[] _assignments;
        private WardrobeConfiguration.MaterialOverride[] _overrides;
        private WardrobeConfiguration.FootwearOverride[] _footwear;
        private readonly Stack<Snapshot> _undo = new Stack<Snapshot>();
        private readonly Stack<Snapshot> _redo = new Stack<Snapshot>();
        private Snapshot _saved;

        public bool IsDirty { get; private set; }
        public int EditGeneration { get; private set; }
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
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
            EditGeneration = 0;
            _undo.Clear();
            _redo.Clear();
            _saved = Capture();
        }

        public bool Undo()
        {
            if (_undo.Count == 0) return false;
            _redo.Push(Capture());
            Restore(_undo.Pop());
            EditGeneration++;
            UpdateDirty();
            return true;
        }

        public bool Redo()
        {
            if (_redo.Count == 0) return false;
            _undo.Push(Capture());
            Restore(_redo.Pop());
            EditGeneration++;
            UpdateDirty();
            return true;
        }

        public bool TrySetLayer(string presetId, string pieceId, int layer, out string error)
        {
            if (layer < 0 || layer > 2) { error = "Layer must be Base, 1 or 2."; return false; }
            var row = _assignments.FirstOrDefault(x => x.presetId == presetId && x.sourcePieceId == pieceId);
            if (row == null) { error = "Stable source piece is not in this setup session."; return false; }
            if (row.layer == layer) { error = null; return true; }
            BeforeEdit();
            row.layer = layer;
            Changed();
            error = null;
            return true;
        }

        public void SetOpacity(string presetId, string pieceId, string slotId, float opacity)
        {
            opacity = Mathf.Clamp01(opacity);
            var row = _overrides.FirstOrDefault(x => x.presetId == presetId && x.sourcePieceId == pieceId && x.materialSlotId == slotId);
            if (row != null && row.opacityOverride && Mathf.Abs(row.opacity - opacity) < .0001f) return;
            BeforeEdit();
            if (row == null)
            {
                row = new WardrobeConfiguration.MaterialOverride
                    { presetId = presetId, sourcePieceId = pieceId, materialSlotId = slotId };
                _overrides = _overrides.Concat(new[] { row }).ToArray();
            }
            row.opacityOverride = true;
            row.opacity = opacity;
            Changed();
        }

        public void ClearOpacity(string presetId, string pieceId, string slotId)
        {
            var row = _overrides.FirstOrDefault(x => x.presetId == presetId && x.sourcePieceId == pieceId && x.materialSlotId == slotId);
            if (row == null || !row.opacityOverride) return;
            BeforeEdit();
            row.opacityOverride = false;
            Changed();
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
            height = Mathf.Clamp(height, 0f, .3f); shrink = Mathf.Clamp(shrink, 0f, .1f);
            if (row.standingHeightOverride == setHeight && row.footShrinkOverride == setShrink &&
                Mathf.Abs(row.standingHeight - height) < .0001f && Mathf.Abs(row.footShrink - shrink) < .0001f) return;
            BeforeEdit();
            row.standingHeightOverride = setHeight; row.standingHeight = height;
            row.footShrinkOverride = setShrink; row.footShrink = shrink;
            _footwear = _footwear.Where(x => x.presetId != presetId).Concat(new[] { row }).ToArray();
            Changed();
        }

        public void ClearFootwear(string presetId)
        {
            if (!_footwear.Any(x => x.presetId == presetId)) return;
            BeforeEdit();
            _footwear = _footwear.Where(x => x.presetId != presetId).ToArray();
            Changed();
        }

        public void MarkCommitted()
        {
            IsDirty = false;
            _undo.Clear(); _redo.Clear();
            _saved = Capture();
        }

        private void BeforeEdit() { _undo.Push(Capture()); _redo.Clear(); }
        private void Changed() { EditGeneration++; UpdateDirty(); }
        private void UpdateDirty() { IsDirty = !Equivalent(Capture(), _saved); }
        private Snapshot Capture() => new Snapshot
        { Assignments = Clone(_assignments), Overrides = Clone(_overrides), Footwear = Clone(_footwear) };
        private void Restore(Snapshot state)
        { _assignments = Clone(state.Assignments); _overrides = Clone(state.Overrides); _footwear = Clone(state.Footwear); }

        private static bool Equivalent(Snapshot a, Snapshot b)
        {
            if (a == null || b == null) return false;
            return a.Assignments.Length == b.Assignments.Length && a.Overrides.Length == b.Overrides.Length && a.Footwear.Length == b.Footwear.Length &&
                a.Assignments.Zip(b.Assignments, (x, y) => x.presetId == y.presetId && x.sourcePieceId == y.sourcePieceId && x.layer == y.layer).All(x => x) &&
                a.Overrides.Zip(b.Overrides, (x, y) => x.presetId == y.presetId && x.sourcePieceId == y.sourcePieceId && x.materialSlotId == y.materialSlotId &&
                    x.material == y.material && x.opacityOverride == y.opacityOverride && Mathf.Abs(x.opacity - y.opacity) < .0001f).All(x => x) &&
                a.Footwear.Zip(b.Footwear, (x, y) => x.presetId == y.presetId && x.standingHeightOverride == y.standingHeightOverride &&
                    Mathf.Abs(x.standingHeight - y.standingHeight) < .0001f && x.footShrinkOverride == y.footShrinkOverride &&
                    Mathf.Abs(x.footShrink - y.footShrink) < .0001f).All(x => x);
        }

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
