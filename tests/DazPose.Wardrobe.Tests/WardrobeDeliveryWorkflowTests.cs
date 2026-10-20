using DazPose.UnityValidation;

namespace DazPose.Wardrobe.Tests;

public sealed class WardrobeDeliveryWorkflowTests
{
    private const string PublishedGeneration = "d0b34840249bc9e812337938d1214c5f5cfb79f958dbd764afe2b6bca12cce0a";

    [Fact]
    public void PublishedCandidateWithMatchingConfigurationRecoversAfterAuthoringLinkWasCleared()
    {
        Assert.True(WardrobeAppearanceDeliveryWorkflow.CanRecover(true, PublishedGeneration, PublishedGeneration, false));
        Assert.False(WardrobeAppearanceDeliveryWorkflow.CanRecover(true, PublishedGeneration, "old-generation", true));
        Assert.False(WardrobeAppearanceDeliveryWorkflow.CanRecover(true, "", "", true));
    }

    [Fact]
    public void EditableCandidateRequiresItsOwnConfiguration()
    {
        Assert.True(WardrobeAppearanceDeliveryWorkflow.CanRecover(false, "", "draft-hash", true));
        Assert.False(WardrobeAppearanceDeliveryWorkflow.CanRecover(false, "", "draft-hash", false));
    }

    [Fact]
    public void NewCandidatePublishesBeforeGeneratingAssetsAndSavingScene()
    {
        var calls = new List<string>(); bool published = false;
        WardrobeAppearanceDeliveryWorkflow.Resume(() => calls.Add("recover"), () => published,
            () => { calls.Add("publish"); published = true; }, () => calls.Add("prefabs"), () => calls.Add("scene"), null);
        Assert.Equal(new[] { "recover", "publish", "prefabs", "scene" }, calls);
    }

    [Fact]
    public void ExistingPublishedReleaseSkipsPublicationOnEveryResume()
    {
        int publications = 0, recoveries = 0, prefabs = 0, scenes = 0;
        for (int attempt = 0; attempt < 3; attempt++)
            WardrobeAppearanceDeliveryWorkflow.Resume(() => recoveries++, () => true, () => publications++, () => prefabs++, () => scenes++, null);
        Assert.Equal(0, publications); Assert.Equal(3, recoveries); Assert.Equal(3, prefabs); Assert.Equal(3, scenes);
    }

    [Theory]
    [InlineData("prefabs")]
    [InlineData("scene")]
    public void FailureAfterPublicationRetainsReleasedStateAndRetryDoesNotRepublish(string fault)
    {
        bool published = false, fail = true; int publications = 0, scenes = 0;
        Action run = () => WardrobeAppearanceDeliveryWorkflow.Resume(() => { }, () => published,
            () => { publications++; published = true; },
            () => { if (fail && fault == "prefabs") throw new Exception("injected"); },
            () => { scenes++; if (fail && fault == "scene") throw new Exception("injected"); }, null);
        var error = Assert.Throws<InvalidOperationException>(run);
        Assert.Contains("stopped", error.Message);
        Assert.True(published); Assert.Equal(1, publications);
        if (fault == "prefabs") Assert.Equal(0, scenes);
        fail = false; run(); Assert.Equal(1, publications);
    }

    [Fact]
    public void FailedPreparationOrPublicationCannotReachPrefabOrSceneMutation()
    {
        int mutations = 0;
        Assert.Throws<InvalidOperationException>(() => WardrobeAppearanceDeliveryWorkflow.Resume(
            () => throw new Exception("incomplete input"), () => false, () => mutations++, () => mutations++, () => mutations++, null));
        Assert.Equal(0, mutations);
        Assert.Throws<InvalidOperationException>(() => WardrobeAppearanceDeliveryWorkflow.Resume(
            () => { }, () => false, () => { }, () => mutations++, () => mutations++, null));
        Assert.Equal(0, mutations);
    }
}
