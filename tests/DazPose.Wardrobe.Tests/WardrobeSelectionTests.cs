using DazPose.Performer;

namespace DazPose.Wardrobe.Tests;

public sealed class WardrobeSelectionTests
{
    static readonly WardrobePresetSelectionFacts Outfit = new("first-outfit", ["first", "starter"], 0b110,
        WardrobeHairAction.Set, "first-outfit/hair-a", (1 << 0) | (1 << 2) | (1 << 6));
    static readonly WardrobePresetSelectionFacts KeepHairOutfit = new("keep-outfit", [], 0b101,
        WardrobeHairAction.Keep, "", (1 << 0) | (1 << 1) | (1 << 5));
    static readonly WardrobePresetSelectionFacts ClearHairOutfit = new("clear-outfit", [], 0b011,
        WardrobeHairAction.Clear, "", (1 << 0) | (1 << 1) | (1 << 3));
    static readonly WardrobePresetSelectionFacts[] Presets = [Outfit, KeepHairOutfit, ClearHairOutfit];
    static readonly string[] Hair = ["first-outfit/hair-a", "keep-outfit/hair-b"];

    [Fact]
    public void AddRemoveRespectEverySparseMaskAndCeiling()
    {
        for (int mask = 0; mask <= WardrobeLayerState.AllLayers; mask++)
        for (int ceiling = -1; ceiling <= WardrobeLayerState.Layer2; ceiling++)
        {
            int visible = WardrobeLayerState.VisibleMask(mask, ceiling);
            int remove = WardrobeLayerState.RemoveHighestCeiling(mask, ceiling);
            int add = WardrobeLayerState.AddLowestCeiling(mask, ceiling);
            Assert.InRange(remove, -1, 2);
            Assert.InRange(add, -1, 2);
            Assert.Equal(visible == 0 ? ceiling : WardrobeLayerState.HighestVisible(visible & ~(1 << WardrobeLayerState.HighestVisible(visible))), remove);
            Assert.Equal(WardrobeLayerState.LowestHidden(mask, ceiling) < 0 ? ceiling : Math.Max(ceiling, WardrobeLayerState.LowestHidden(mask, ceiling)), add);
            Assert.Equal(mask, WardrobeLayerState.VisibleMask(mask, 2));
        }
    }

    [Fact]
    public void SetClothingRetainsHairWhileLegacyPolicySetsOrClearsIt()
    {
        var current = new WardrobeSelection("g", "first-outfit", 1, "keep-outfit/hair-b");
        var set = WardrobeSelectionResolver.Resolve(current, "g", Presets, Hair,
            WardrobeSelectionOperation.SetClothing, "keep-outfit");
        Assert.Equal(WardrobeSelectionDisposition.Changed, set.Disposition);
        Assert.Equal("keep-outfit/hair-b", set.Target.HairKey);
        var legacySet = WardrobeSelectionResolver.Resolve(current, "g", Presets, Hair,
            WardrobeSelectionOperation.LegacyOutfit, "first");
        Assert.Equal("first-outfit/hair-a", legacySet.Target.HairKey);
        var legacyClear = WardrobeSelectionResolver.Resolve(current, "g", Presets, Hair,
            WardrobeSelectionOperation.LegacyOutfit, "clear-outfit");
        Assert.Equal("", legacyClear.Target.HairKey);
    }

    [Fact]
    public void SetHairAndClearHairDoNotChangeClothingOrCeiling()
    {
        var current = new WardrobeSelection("g", "first-outfit", 1, "");
        var set = WardrobeSelectionResolver.Resolve(current, "g", Presets, Hair,
            WardrobeSelectionOperation.SetHair, "keep-outfit/hair-b");
        var clear = WardrobeSelectionResolver.Resolve(set.Target, "g", Presets, Hair,
            WardrobeSelectionOperation.ClearHair);
        Assert.Equal("first-outfit", set.Target.PresetId);
        Assert.Equal(1, set.Target.LayerCeiling);
        Assert.Equal("", clear.Target.HairKey);
        Assert.Equal("first-outfit", clear.Target.PresetId);
        Assert.Equal(1, clear.Target.LayerCeiling);
    }

    [Fact]
    public void NakedRemovalRemembersPresetAndAddRestoresLowestLayer()
    {
        var start = new WardrobeSelection("g", "first-outfit", 2, "first-outfit/hair-a");
        var underwear = WardrobeSelectionResolver.Resolve(start, "g", Presets, Hair, WardrobeSelectionOperation.RemoveLayer);
        var naked = WardrobeSelectionResolver.Resolve(underwear.Target, "g", Presets, Hair, WardrobeSelectionOperation.RemoveLayer);
        var dressed = WardrobeSelectionResolver.Resolve(naked.Target, "g", Presets, Hair, WardrobeSelectionOperation.AddLayer);
        Assert.Equal(-1, naked.Target.LayerCeiling);
        Assert.Equal("first-outfit", naked.Target.PresetId);
        Assert.Equal(1, dressed.Target.LayerCeiling);
    }

    [Fact]
    public void ExplicitUnclothedDropsRememberedOutfitAndCannotAdd()
    {
        var current = new WardrobeSelection("g", "first-outfit", -1, "first-outfit/hair-a");
        var cleared = WardrobeSelectionResolver.Resolve(current, "g", Presets, Hair,
            WardrobeSelectionOperation.SetClothing, "unclothed");
        var add = WardrobeSelectionResolver.Resolve(cleared.Target, "g", Presets, Hair, WardrobeSelectionOperation.AddLayer);
        Assert.True(cleared.Target.IsUnclothed);
        Assert.Equal(WardrobeSelectionFailure.NoOutfitSelected, add.Failure);
    }

    [Fact]
    public void RepeatingFullyDressedPresetAppliesRequiredDefaultHairBeforeNoOp()
    {
        var current = new WardrobeSelection("g", "first-outfit", 2, "keep-outfit/hair-b");
        var decision = WardrobeSelectionResolver.Resolve(current, "g", Presets, Hair,
            WardrobeSelectionOperation.LegacyOutfit, "first-outfit");
        Assert.Equal(WardrobeSelectionDisposition.Changed, decision.Disposition);
        Assert.Equal("first-outfit/hair-a", decision.Target.HairKey);
        var same = WardrobeSelectionResolver.Resolve(decision.Target, "g", Presets, Hair,
            WardrobeSelectionOperation.LegacyOutfit, "first-outfit");
        Assert.Equal(WardrobeSelectionDisposition.NoChange, same.Disposition);
    }

    [Fact]
    public void UnknownAndAmbiguousAliasesFailBeforeProducingTarget()
    {
        var collision = new WardrobePresetSelectionFacts("third", ["first"], 1, WardrobeHairAction.Keep, "", 1 << 1);
        var ambiguous = WardrobeSelectionResolver.Resolve(null, "g", [Outfit, collision], Hair,
            WardrobeSelectionOperation.LegacyOutfit, "first");
        var unknown = WardrobeSelectionResolver.Resolve(null, "g", Presets, Hair,
            WardrobeSelectionOperation.LegacyOutfit, "missing");
        Assert.Equal(WardrobeSelectionFailure.AmbiguousAlias, ambiguous.Failure);
        Assert.Equal(WardrobeSelectionFailure.UnknownPreset, unknown.Failure);
        Assert.Null(ambiguous.Target);
    }

    [Fact]
    public void PreparationIsPureAndRejectsMissingFitOrWrongGeneration()
    {
        var selection = new WardrobeSelection("g", "first-outfit", 2, "first-outfit/hair-a");
        var fits = new[] { new WardrobeFitSelectionFact(6, true, true) };
        var prepared = WardrobeSelectionPreparer.Prepare(selection, "g", Outfit.PopulatedMask, fits);
        Assert.True(prepared.IsValid);
        Assert.Equal(0, prepared.FitIndex);
        Assert.Equal(2, selection.LayerCeiling);
        Assert.False(WardrobeSelectionPreparer.Prepare(selection, "other", Outfit.PopulatedMask, fits).IsValid);
        Assert.False(WardrobeSelectionPreparer.Prepare(selection, "g", Outfit.PopulatedMask, []).IsValid);
    }

    [Fact]
    public void ProductionAtomicCommitRollsBackEveryAttemptedStageInReverseOrder()
    {
        for (int failureStage = 0; failureStage < 4; failureStage++)
        {
            var state = new[] { 10, 20, 30, 40 };
            var rollbackOrder = new List<int>();
            var steps = new WardrobeAtomicCommit.Step[4];
            for (int index = 0; index < steps.Length; index++)
            {
                int captured = index;
                int prior = state[index];
                steps[index] = new WardrobeAtomicCommit.Step("stage " + index, () =>
                {
                    state[captured] = -captured;
                    if (captured == failureStage) throw new InvalidOperationException("injected " + captured);
                }, () => { rollbackOrder.Add(captured); state[captured] = prior; });
            }
            Assert.ThrowsAny<Exception>(() => WardrobeAtomicCommit.Execute(steps));
            Assert.Equal(new[] { 10, 20, 30, 40 }, state);
            Assert.Equal(Enumerable.Range(0, failureStage + 1).Reverse(), rollbackOrder);
        }
    }

    [Fact]
    public void CacheBoundsUnpinnedResourcesAndNeverEvictsActiveOrPinnedEntries()
    {
        var disposed = new List<string>();
        using var cache = new WardrobeLeaseCache<string>(2, disposed.Add);
        cache.GetOrCreate("a", () => "a"); cache.SetActive("a");
        cache.GetOrCreate("b", () => "b"); cache.GetOrCreate("c", () => "c");
        using (cache.Pin("b"))
        {
            cache.GetOrCreate("d", () => "d");
            Assert.True(cache.TryGet("b", out _));
            cache.GetOrCreate("e", () => "e");
            Assert.True(cache.TryGet("b", out _));
            Assert.True(cache.TryGet("a", out _));
            Assert.Equal(2, cache.UnpinnedCount);
        }
        Assert.Equal(2, cache.UnpinnedCount);
        Assert.Contains("c", disposed);
    }

    [Fact]
    public void NestedPinsReleaseIndependentlyAndDisposeResourcesOnce()
    {
        var disposed = new List<string>();
        var cache = new WardrobeLeaseCache<string>(1, disposed.Add);
        cache.GetOrCreate("snapshot", () => "snapshot");
        var outer = cache.Pin("snapshot");
        var inner = cache.Pin("snapshot");
        outer.Dispose();
        Assert.True(cache.TryGet("snapshot", out _));
        inner.Dispose();
        cache.SetActive(null);
        cache.Dispose(); cache.Dispose();
        Assert.Equal(new[] { "snapshot" }, disposed);
    }

    [Fact]
    public void BinderBonePlannerRejectsAmbiguousMissingAndCyclicMapsBeforeBinding()
    {
        Assert.False(WardrobeBoneBindingPlan.TryCreate(["root", "root"], ["root"], "root", [], out _, out _));
        Assert.False(WardrobeBoneBindingPlan.TryCreate(["root"], ["missing"], "root", [], out _, out _));
        Assert.False(WardrobeBoneBindingPlan.TryCreate(["root"], ["root"], "missing", [], out _, out _));
        Assert.False(WardrobeBoneBindingPlan.TryCreate(["root"], ["root"], "root",
            [new("left", "right"), new("right", "left")], out _, out _));
        Assert.False(WardrobeBoneBindingPlan.TryCreate(["root"], ["root"], "root",
            [new("root", "root")], out _, out _));
    }

    [Fact]
    public void BinderBonePlannerReturnsIndependentLocalOrdersForSeparateAttachments()
    {
        var locals = new[] { new WardrobeLocalBoneFact("finger", "hand"), new WardrobeLocalBoneFact("hand", "root") };
        Assert.True(WardrobeBoneBindingPlan.TryCreate(["root"], ["root", "finger"], "root", locals, out var first, out _));
        Assert.True(WardrobeBoneBindingPlan.TryCreate(["root"], ["root", "finger"], "root", locals, out var second, out _));
        Assert.Equal(new[] { "hand", "finger" }, first);
        Assert.Equal(first, second);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void RigManifestRulesRejectDuplicateMissingUnexpectedAndWrongGenerationRows()
    {
        Assert.True(WardrobeRigManifestRules.HasExactUniqueKeys(["first", "second"], ["second", "first"], out _));
        Assert.False(WardrobeRigManifestRules.HasExactUniqueKeys(["first", "first"], ["first"], out _));
        Assert.False(WardrobeRigManifestRules.HasExactUniqueKeys(["first", "second"], ["first"], out _));
        Assert.False(WardrobeRigManifestRules.HasExactUniqueKeys(["first"], ["first", "unexpected"], out _));
        Assert.True(WardrobeRigManifestRules.MatchesIdentity("generation", "signature", "generation", "signature", out _));
        Assert.False(WardrobeRigManifestRules.MatchesIdentity("generation", "signature", "old", "signature", out _));
        Assert.False(WardrobeRigManifestRules.MatchesIdentity("generation", "signature", "generation", "other", out _));
    }

    [Fact]
    public void RigAssetIdentityPathsAndHashesAreDeterministicAndRefuseMismatchedReuse()
    {
        const string root = "Assets/Production/Wardrobe/Rigs/generation";
        Assert.Equal(root + "/Clothing/first-outfit.prefab",
            WardrobeRigAssetIdentity.PrefabPath(root, WardrobeAttachmentRole.Clothing, "first-outfit"));
        Assert.Equal(root + "/Hair/first-outfit/EmikoHair_59875.prefab",
            WardrobeRigAssetIdentity.PrefabPath(root, WardrobeAttachmentRole.Hair, "first-outfit/EmikoHair_59875"));
        var first = WardrobeRigAssetIdentity.ContentHash("g", "first-outfit", WardrobeAttachmentRole.Clothing, "first-outfit", ["mesh-guid", "bone-a"]);
        var repeat = WardrobeRigAssetIdentity.ContentHash("g", "first-outfit", WardrobeAttachmentRole.Clothing, "first-outfit", ["mesh-guid", "bone-a"]);
        var changed = WardrobeRigAssetIdentity.ContentHash("g", "first-outfit", WardrobeAttachmentRole.Clothing, "first-outfit", ["mesh-guid", "bone-b"]);
        Assert.Equal(first, repeat);
        Assert.True(WardrobeRigAssetIdentity.MayReuseExisting(first, repeat));
        Assert.False(WardrobeRigAssetIdentity.MayReuseExisting(first, changed));
    }
}
