using DazPose.Game;
using DazPose.Story;

namespace DazPose.Player.Tests;

public sealed class ProfileEditSessionTests
{
    [Fact]
    public void DiscardAfterManyChangesLeavesCurrentProfileAndStorageUntouched()
    {
        using var directory = new TemporaryProfileDirectory();
        var service = new PlayerProfileService(directory.Path);
        var original = service.Current;

        using (var edit = new PlayerProfileEditSession(service))
        {
            edit.SetFrequency("unknown-kink", KinkFrequency.VeryFrequent);
            edit.SetSentiment("unknown-kink", KinkSentiment.Punishment);
            edit.SetEquipmentOwnedAndWilling("unknown-item", true);
            edit.SetReward("unknown-item", true);
            edit.SetPunishment("unknown-item", true);
            edit.Discard();
        }

        Assert.Same(original, service.Current);
        Assert.False(File.Exists(service.StoragePath));
    }

    [Fact]
    public void ApplyCommitsOnceAndDuplicateApplyDoesNotReplaceCurrentAgain()
    {
        using var directory = new TemporaryProfileDirectory();
        var service = new PlayerProfileService(directory.Path);
        using var edit = new PlayerProfileEditSession(service);
        edit.SetFrequency("future-kink", KinkFrequency.Frequent);

        Assert.True(edit.Commit());
        var applied = service.Current;
        Assert.NotSame(edit.Draft, applied);
        Assert.False(edit.Commit());
        Assert.Same(applied, service.Current);
        Assert.Equal(KinkFrequency.Frequent, Assert.Single(service.Current.kinks).frequency);
    }

    [Fact]
    public void NoOpApplyPreservesCurrentReferenceAndDoesNotCreateStorage()
    {
        using var directory = new TemporaryProfileDirectory();
        var service = new PlayerProfileService(directory.Path);
        var original = service.Current;
        using var edit = new PlayerProfileEditSession(service);

        Assert.True(edit.Commit());

        Assert.Same(original, service.Current);
        Assert.False(File.Exists(service.StoragePath));
    }

    [Fact]
    public void SubmissionScoreUsesFrequencyOnlyAndNoFrequencyPreservesSentiment()
    {
        using var directory = new TemporaryProfileDirectory();
        var service = new PlayerProfileService(directory.Path);
        service.Commit(new PlayerProfile
        {
            favor = 50f,
            kinks = new[]
            {
                new KinkPreference { id = "one", frequency = KinkFrequency.Normal, sentiment = KinkSentiment.Loved },
                new KinkPreference { id = "two", frequency = KinkFrequency.Frequent, sentiment = KinkSentiment.Punishment }
            }
        });
        var catalog = new KinkCatalog
        {
            entries = new[]
            {
                new KinkDefinition { id = "one", displayName = "One" },
                new KinkDefinition { id = "two", displayName = "Two" },
                new KinkDefinition { id = "three", displayName = "Three" }
            }
        };

        using var edit = new PlayerProfileEditSession(service, catalog);
        edit.SetFrequency("one", KinkFrequency.No);

        Assert.Equal(KinkSentiment.Loved, edit.GetKink("one").sentiment);
        Assert.True(edit.Commit());
        Assert.Equal(33, service.Current.submissionScore);
        Assert.Equal(33, PlayerProfileService.CalculateSubmissionScore(service.Current, catalog));
        Assert.Equal(KinkSentiment.Loved, service.Current.kinks.Single(k => k.id == "one").sentiment);
    }

    [Fact]
    public void TurningOwnedEquipmentOffPreservesRewardAndPunishmentChoices()
    {
        using var directory = new TemporaryProfileDirectory();
        var service = new PlayerProfileService(directory.Path);
        service.Commit(ProfileTestSupport.PopulatedProfile());

        using var edit = new PlayerProfileEditSession(service);
        edit.SetEquipmentOwnedAndWilling("known-item", false);
        Assert.True(edit.Commit());

        var item = Assert.Single(service.Current.equipment);
        Assert.False(item.ownedAndWilling);
        Assert.True(item.useAsReward);
        Assert.True(item.useAsPunishment);
    }
}
