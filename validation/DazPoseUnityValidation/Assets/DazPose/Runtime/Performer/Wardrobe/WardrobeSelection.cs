using System;

namespace DazPose.Performer
{
    /// <summary>Immutable, asset-independent wardrobe intent. A negative ceiling means naked, not no selection.</summary>
    public sealed class WardrobeSelection : IEquatable<WardrobeSelection>
    {
        public string GenerationId { get; }
        public string PresetId { get; }
        public int LayerCeiling { get; }
        public string HairKey { get; }
        public bool IsUnclothed => string.IsNullOrEmpty(PresetId);

        public WardrobeSelection(string generationId, string presetId, int layerCeiling, string hairKey)
        {
            WardrobeLayerState.ValidateCeiling(layerCeiling);
            GenerationId = generationId ?? string.Empty;
            PresetId = string.IsNullOrWhiteSpace(presetId) ? string.Empty : presetId.Trim();
            HairKey = string.IsNullOrWhiteSpace(hairKey) ? string.Empty : hairKey.Trim();
            LayerCeiling = layerCeiling;
            if (IsUnclothed && layerCeiling != WardrobeLayerState.EmptyCeiling)
                throw new ArgumentException("An unclothed selection must use the empty ceiling.", nameof(layerCeiling));
        }

        public bool Equals(WardrobeSelection other) => other != null &&
            string.Equals(GenerationId, other.GenerationId, StringComparison.Ordinal) &&
            string.Equals(PresetId, other.PresetId, StringComparison.Ordinal) &&
            LayerCeiling == other.LayerCeiling && string.Equals(HairKey, other.HairKey, StringComparison.Ordinal);
        public override bool Equals(object obj) => Equals(obj as WardrobeSelection);
        public override int GetHashCode() => HashCode.Combine(GenerationId, PresetId, LayerCeiling, HairKey);
    }

    public enum WardrobeSelectionOperation { LegacyOutfit, SetClothing, SetHair, ClearHair, AddLayer, RemoveLayer, ExactRecall }
    public enum WardrobeSelectionDisposition { Changed, NoChange, Failed }
    public enum WardrobeSelectionFailure { None, UnknownPreset, AmbiguousAlias, WrongGeneration, UnknownHair, NoFitForTarget, NoOutfitSelected }

    /// <summary>Pure catalog facts captured once by the Unity catalog adapter.</summary>
    public sealed class WardrobePresetSelectionFacts
    {
        private readonly string[] aliases;
        public string Id { get; }
        public string[] Aliases => (string[])aliases.Clone();
        public int PopulatedMask { get; }
        public WardrobeHairAction HairAction { get; }
        public string DefaultHairKey { get; }
        public int ValidatedFitMask { get; }

        public WardrobePresetSelectionFacts(string id, string[] aliases, int populatedMask,
            WardrobeHairAction hairAction, string defaultHairKey, int validatedFitMask)
        {
            WardrobeLayerState.ValidateMask(populatedMask);
            if ((validatedFitMask & ~0xff) != 0) throw new ArgumentOutOfRangeException(nameof(validatedFitMask));
            Id = id ?? string.Empty;
            this.aliases = aliases == null ? Array.Empty<string>() : (string[])aliases.Clone();
            PopulatedMask = populatedMask;
            HairAction = hairAction;
            DefaultHairKey = defaultHairKey ?? string.Empty;
            ValidatedFitMask = validatedFitMask;
        }
    }

    public sealed class WardrobeSelectionDecision
    {
        public WardrobeSelectionDisposition Disposition { get; }
        public WardrobeSelection Target { get; }
        public WardrobeSelectionFailure Failure { get; }
        public string Message { get; }
        public int VisibleMask { get; }
        internal WardrobeSelectionDecision(WardrobeSelectionDisposition disposition, WardrobeSelection target,
            WardrobeSelectionFailure failure, string message, int visibleMask)
        { Disposition = disposition; Target = target; Failure = failure; Message = message; VisibleMask = visibleMask; }
    }
}
