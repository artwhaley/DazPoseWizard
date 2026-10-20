using System;
using System.Collections.Generic;

namespace DazPose.Performer
{
    public readonly struct WardrobeFitSelectionFact
    {
        public int VisibleMask { get; }
        public bool IsValidated { get; }
        public bool HasRequiredBindings { get; }
        public WardrobeFitSelectionFact(int visibleMask, bool isValidated, bool hasRequiredBindings)
        {
            WardrobeLayerState.ValidateMask(visibleMask);
            VisibleMask = visibleMask; IsValidated = isValidated; HasRequiredBindings = hasRequiredBindings;
        }
    }

    /// <summary>Side-effect-free fit choice from captured metadata; it never creates or mutates a renderer.</summary>
    public readonly struct WardrobePreparedSelection
    {
        public WardrobeSelection Selection { get; }
        public int VisibleMask { get; }
        public int FitIndex { get; }
        public string Error { get; }
        public bool IsValid => Selection != null && FitIndex >= 0 && Error == null;
        internal WardrobePreparedSelection(WardrobeSelection selection, int visibleMask, int fitIndex, string error)
        { Selection = selection; VisibleMask = visibleMask; FitIndex = fitIndex; Error = error; }
    }

    public static class WardrobeSelectionPreparer
    {
        public static WardrobePreparedSelection Prepare(WardrobeSelection selection, string expectedGeneration,
            int populatedMask, IReadOnlyList<WardrobeFitSelectionFact> fitFacts)
        {
            if (selection == null) return new WardrobePreparedSelection(null, 0, -1, "Selection is missing.");
            if (!string.Equals(selection.GenerationId, expectedGeneration ?? string.Empty, StringComparison.Ordinal))
                return new WardrobePreparedSelection(selection, 0, -1, "Selection belongs to a different wardrobe generation.");
            WardrobeLayerState.ValidateMask(populatedMask);
            int visible = selection.IsUnclothed ? 0 : WardrobeLayerState.VisibleMask(populatedMask, selection.LayerCeiling);
            if (fitFacts == null) return new WardrobePreparedSelection(selection, visible, -1, "Fit metadata is missing.");
            for (int i = 0; i < fitFacts.Count; ++i)
                if (fitFacts[i].VisibleMask == visible)
                    return fitFacts[i].IsValidated && fitFacts[i].HasRequiredBindings
                        ? new WardrobePreparedSelection(selection, visible, i, null)
                        : new WardrobePreparedSelection(selection, visible, -1, "The matching fit is not validated or is missing required bindings.");
            return new WardrobePreparedSelection(selection, visible, -1, "No fit exists for visible mask " + visible + ".");
        }
    }
}
