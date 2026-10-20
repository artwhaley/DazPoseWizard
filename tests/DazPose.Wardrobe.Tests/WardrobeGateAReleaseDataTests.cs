using System.Text.Json;
using DazPose.Performer;
using DazPose.UnityValidation;

namespace DazPose.Wardrobe.Tests;

/// <summary>Data captured from Art's actual published generation; uses the production planner, with no Unity process.</summary>
public sealed class WardrobeGateAReleaseDataTests
{
    private sealed class Inputs
    {
        public string Generation { get; set; }
        public string CanonicalRootBone { get; set; }
        public string[] CanonicalBones { get; set; }
        public Package[] Packages { get; set; }
        public Fit[] Fits { get; set; }
    }
    private sealed class Package { public string Preset { get; set; } public Piece[] Pieces { get; set; } }
    private sealed class Piece
    {
        public string Id { get; set; }
        public string Role { get; set; }
        public string[] BoneNames { get; set; }
        public LocalBone[] LocalBones { get; set; }
    }
    private sealed class LocalBone { public string Name { get; set; } public string Parent { get; set; } }
    private sealed class Fit
    {
        public string Preset { get; set; }
        public int Mask { get; set; }
        public int Status { get; set; }
        public string[] Garments { get; set; }
        public string BodyGuid { get; set; }
    }
    private static Inputs Load() => JsonSerializer.Deserialize<Inputs>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "GateAReleaseInputs.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    [Fact]
    public void EveryActualReleasedPiecePassesTheProductionCanonicalAndLocalBonePlanner()
    {
        var input = Load();
        Assert.Equal(173, input.CanonicalBones.Length);
        Assert.Equal(24, input.Packages.Sum(package => package.Pieces.Length));
        foreach (var package in input.Packages)
        foreach (var piece in package.Pieces)
        {
            var locals = piece.LocalBones.Select(bone => new WardrobeLocalBoneFact(bone.Name, bone.Parent)).ToArray();
            Assert.True(WardrobeBoneBindingPlan.TryCreate(input.CanonicalBones, piece.BoneNames, input.CanonicalRootBone,
                locals, out var order, out var error), package.Preset + "/" + piece.Id + ": " + error);
            Assert.Equal(locals.Length, order.Length);
        }
    }

    [Fact]
    public void CorrectedIntermediateFitContainsPantiesAndItsOwnPreparedBody()
    {
        var first = Load().Fits.Where(fit => fit.Preset == "first-outfit").OrderBy(fit => fit.Mask).ToArray();
        Assert.Equal(new[] { 0, 2, 6 }, first.Select(fit => fit.Mask));
        Assert.All(first, fit => Assert.Equal(1, fit.Status));
        Assert.Empty(first[0].Garments);
        Assert.Equal(new[] { "CharlenesCloset_Panty_1981" }, first[1].Garments);
        Assert.Equal(new[] { "CharlenesCloset_Panty_1981", "PeekaBoo_16572" }, first[2].Garments);
        Assert.Equal(3, first.Select(fit => fit.BodyGuid).Distinct().Count());
        int lowerCeiling = WardrobeLayerState.RemoveHighestCeiling(6, 2);
        Assert.Equal(1, lowerCeiling);
        Assert.Equal(2, WardrobeLayerState.VisibleMask(6, lowerCeiling));
    }

    [Fact]
    public void ActualPublishedGenerationRecoversAndHairControlsUseOnlyReleasedAssets()
    {
        var input = Load();
        Assert.True(WardrobeAppearanceDeliveryWorkflow.CanRecover(true, input.Generation, input.Generation, false));
        var hairs = input.Packages.SelectMany(package => package.Pieces.Where(piece => piece.Role == "hair")
            .Select(piece => package.Preset + "/" + piece.Id)).ToArray();
        Assert.Equal(new[] { "first-outfit/EmikoHair_59875" }, hairs);
        Assert.Equal(7, input.Fits.Length);
        Assert.All(input.Fits, fit => Assert.Equal(1, fit.Status));
    }
}
