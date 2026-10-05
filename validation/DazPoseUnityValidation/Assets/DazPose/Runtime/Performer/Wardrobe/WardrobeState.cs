using System;

namespace DazPose.Performer
{
    /// <summary>Committed wardrobe observation. Arrays are copied on input and output.</summary>
    public sealed class WardrobeState
    {
        private readonly string[][] layerPieceIds;
        private readonly string[][] layerDisplayNames;

        public string OutfitId { get; }
        public string DisplayName { get; }
        public int ConfigurationRevision { get; }
        public bool IsNaked { get; }
        public bool IsChanging { get; }
        public int LayerCeiling { get; }
        public int PopulatedMask { get; }
        public int VisibleMask { get; }
        public int? HighestVisibleLayer { get; }
        public bool CanRemoveLayer { get; }
        public bool CanAddLayer { get; }
        public bool CurrentFootwearPoseActive { get; }
        public string EffectiveFootwearId { get; }
        public string EffectiveHairId { get; }
        public string[][] LayerPieceIds => Copy(layerPieceIds);
        public string[][] LayerDisplayNames => Copy(layerDisplayNames);

        public WardrobeState(string outfitId, string displayName, int revision, bool isNaked,
            bool isChanging, int layerCeiling, int populatedMask, int visibleMask,
            string effectiveFootwearId, string effectiveHairId, string[][] ids, string[][] names)
            : this(outfitId, displayName, revision, isNaked, isChanging, layerCeiling, populatedMask, visibleMask,
                effectiveFootwearId, false, effectiveHairId, ids, names) { }

        public WardrobeState(string outfitId, string displayName, int revision, bool isNaked,
            bool isChanging, int layerCeiling, int populatedMask, int visibleMask,
            string effectiveFootwearId, bool footwearPoseActive, string effectiveHairId, string[][] ids, string[][] names)
        {
            WardrobeLayerState.ValidateCeiling(layerCeiling);
            WardrobeLayerState.ValidateMask(populatedMask);
            WardrobeLayerState.ValidateMask(visibleMask);
            if ((visibleMask & ~populatedMask) != 0) throw new ArgumentException("Visible mask includes an empty layer.");
            OutfitId = outfitId; DisplayName = displayName; ConfigurationRevision = revision;
            IsNaked = isNaked; IsChanging = isChanging; LayerCeiling = layerCeiling;
            PopulatedMask = populatedMask; VisibleMask = visibleMask;
            int highest = WardrobeLayerState.HighestVisible(visibleMask);
            HighestVisibleLayer = highest < 0 ? (int?)null : highest;
            CanRemoveLayer = highest >= 0;
            CanAddLayer = WardrobeLayerState.LowestHidden(populatedMask, layerCeiling) >= 0;
            CurrentFootwearPoseActive = footwearPoseActive;
            EffectiveFootwearId = string.IsNullOrWhiteSpace(effectiveFootwearId) ? "barefoot" : effectiveFootwearId;
            EffectiveHairId = string.IsNullOrWhiteSpace(effectiveHairId) ? "none" : effectiveHairId;
            layerPieceIds = Copy(ids); layerDisplayNames = Copy(names);
        }

        private static string[][] Copy(string[][] source)
        {
            if (source == null) return Array.Empty<string[]>();
            var result = new string[source.Length][];
            for (int i = 0; i < source.Length; ++i)
                result[i] = source[i] == null ? Array.Empty<string>() : (string[])source[i].Clone();
            return result;
        }
    }
}
