using System;
using System.Collections.Generic;
using System.Linq;

namespace DazPose.Performer
{
    /// <summary>Pure, deterministic reduction of wardrobe commands into a complete target selection.</summary>
    public static class WardrobeSelectionResolver
    {
        public static WardrobeSelectionDecision Resolve(WardrobeSelection current, string generationId,
            IReadOnlyList<WardrobePresetSelectionFacts> presets, IReadOnlyCollection<string> hairKeys,
            WardrobeSelectionOperation operation, string idOrHairKey = null)
        {
            if (current != null && !string.Equals(current.GenerationId, generationId ?? string.Empty, StringComparison.Ordinal))
                return Fail(WardrobeSelectionFailure.WrongGeneration, "Current selection belongs to another wardrobe generation.");

            WardrobePresetSelectionFacts selected = null;
            if (current != null && !current.IsUnclothed)
                selected = (presets ?? Array.Empty<WardrobePresetSelectionFacts>()).FirstOrDefault(p => p.Id == current.PresetId);
            if (current != null && !current.IsUnclothed && selected == null)
                return Fail(WardrobeSelectionFailure.UnknownPreset, "Current preset is not present in this wardrobe generation.");

            string presetId = current?.PresetId ?? string.Empty;
            int ceiling = current?.LayerCeiling ?? WardrobeLayerState.EmptyCeiling;
            string hair = current?.HairKey ?? string.Empty;
            switch (operation)
            {
                case WardrobeSelectionOperation.LegacyOutfit:
                case WardrobeSelectionOperation.SetClothing:
                    if (string.Equals((idOrHairKey ?? string.Empty).Trim(), "unclothed", StringComparison.OrdinalIgnoreCase))
                    {
                        presetId = string.Empty; ceiling = WardrobeLayerState.EmptyCeiling;
                    }
                    else
                    {
                        if (!TryResolve(idOrHairKey, presets, out var target, out var failure))
                            return Fail(failure, "Wardrobe preset could not be resolved: " + (idOrHairKey ?? "<empty>"));
                        presetId = target.Id;
                        ceiling = 2;
                        if (operation == WardrobeSelectionOperation.LegacyOutfit)
                        {
                            if (target.HairAction == WardrobeHairAction.Set) hair = target.DefaultHairKey;
                            else if (target.HairAction == WardrobeHairAction.Clear) hair = string.Empty;
                        }
                    }
                    break;
                case WardrobeSelectionOperation.SetHair:
                case WardrobeSelectionOperation.ClearHair:
                    hair = operation == WardrobeSelectionOperation.ClearHair || IsClearHair(idOrHairKey) ? string.Empty : (idOrHairKey ?? string.Empty).Trim();
                    if (hair.Length > 0 && !(hairKeys ?? Array.Empty<string>()).Contains(hair, StringComparer.Ordinal))
                        return Fail(WardrobeSelectionFailure.UnknownHair, "Hair key is not available in this wardrobe generation: " + hair);
                    break;
                case WardrobeSelectionOperation.AddLayer:
                case WardrobeSelectionOperation.RemoveLayer:
                    if (selected == null) return Fail(WardrobeSelectionFailure.NoOutfitSelected, "No outfit is selected.");
                    ceiling = operation == WardrobeSelectionOperation.AddLayer
                        ? WardrobeLayerState.AddLowestCeiling(selected.PopulatedMask, ceiling)
                        : WardrobeLayerState.RemoveHighestCeiling(selected.PopulatedMask, ceiling);
                    break;
                case WardrobeSelectionOperation.ExactRecall:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));
            }

            var destination = Find(presets, presetId);
            int visible = destination == null ? 0 : WardrobeLayerState.VisibleMask(destination.PopulatedMask, ceiling);
            if (destination != null && (destination.ValidatedFitMask & (1 << visible)) == 0)
                return Fail(WardrobeSelectionFailure.NoFitForTarget, "No validated fit exists for visible mask " + visible + ".");

            var result = new WardrobeSelection(generationId, presetId, ceiling, hair);
            bool same = result.Equals(current);
            bool fullyDressedSamePreset = operation == WardrobeSelectionOperation.LegacyOutfit && destination != null &&
                current != null && current.PresetId == destination.Id && visible == destination.PopulatedMask && same;
            if (same || fullyDressedSamePreset)
                return new WardrobeSelectionDecision(WardrobeSelectionDisposition.NoChange, current,
                    WardrobeSelectionFailure.None, fullyDressedSamePreset ? "AlreadyEquipped" : "NoChange", visible);
            return new WardrobeSelectionDecision(WardrobeSelectionDisposition.Changed, result, WardrobeSelectionFailure.None, null, visible);
        }

        private static bool TryResolve(string idOrAlias, IReadOnlyList<WardrobePresetSelectionFacts> presets,
            out WardrobePresetSelectionFacts result, out WardrobeSelectionFailure failure)
        {
            string key = Normalize(idOrAlias);
            var matches = (presets ?? Array.Empty<WardrobePresetSelectionFacts>()).Where(p =>
                Normalize(p.Id) == key || p.Aliases.Any(a => Normalize(a) == key)).Distinct().ToArray();
            result = matches.Length == 1 ? matches[0] : null;
            failure = matches.Length > 1 ? WardrobeSelectionFailure.AmbiguousAlias : WardrobeSelectionFailure.UnknownPreset;
            return result != null;
        }
        private static WardrobePresetSelectionFacts Find(IReadOnlyList<WardrobePresetSelectionFacts> presets, string id) =>
            string.IsNullOrEmpty(id) ? null : (presets ?? Array.Empty<WardrobePresetSelectionFacts>()).FirstOrDefault(p => p.Id == id);
        private static bool IsClearHair(string key) => string.IsNullOrWhiteSpace(key) ||
            string.Equals(key.Trim(), "none", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key.Trim(), "clear", StringComparison.OrdinalIgnoreCase);
        private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();
        private static WardrobeSelectionDecision Fail(WardrobeSelectionFailure failure, string message) =>
            new WardrobeSelectionDecision(WardrobeSelectionDisposition.Failed, null, failure, message, 0);
    }
}
