using DazPose.Game;
using DazPose.Story;
using Newtonsoft.Json.Linq;

namespace DazPose.Player.Tests;

public sealed class ProfilePersistenceTests
{
    [Fact]
    public void UnknownCatalogIdAndUnknownRootAndNestedJsonSurviveRepeatedServiceSaves()
    {
        using var directory = new TemporaryProfileDirectory();
        var path = System.IO.Path.Combine(directory.Path, "player-profile.json");
        File.WriteAllText(path, """
        {
          "version": 1,
          "favor": 50,
          "submissionScore": 0,
          "kinks": [{"id":"known-kink","frequency":1,"sentiment":0,"customKink":{"source":"older-app"}}],
          "equipment": [{"id":"known-item","ownedAndWilling":true,"useAsReward":false,"useAsPunishment":false,"customNested":[1,2]}],
          "facts": [{"key":"fact","valueType":"string","stringValue":"kept","customFact":true}],
          "assignments": [{"instanceId":"assignment-1","definitionId":"unknown-to-current-catalog","issuedAtUtc":"2026-10-18T12:00:00Z","status":0,
            "parameters":[{"name":"value","valueType":"string","stringValue":"kept","customParameter":{"deep":"yes"}}],
            "customAssignment":{"version":"future"}}],
          "sessionHistory": [{"sessionId":"session-1","endedAtUtc":"2026-10-18T12:00:00Z","summary":"kept","customHistory":"future"}],
          "futureRoot":{"nested":{"preserve":true}}
        }
        """);

        var kinkCatalog = new KinkCatalog
        { entries = new[] { new KinkDefinition { id = "known-kink", displayName = "Known" } } };
        var service = new PlayerProfileService(directory.Path);
        using (var edit = new PlayerProfileEditSession(service, kinkCatalog))
        {
            edit.SetFrequency("future-kink", KinkFrequency.Frequent);
            Assert.True(edit.Commit());
        }

        var reloaded = new PlayerProfileService(directory.Path);
        using (var edit = new PlayerProfileEditSession(reloaded, kinkCatalog))
        {
            edit.SetSentiment("future-kink", KinkSentiment.Loved);
            Assert.True(edit.Commit());
        }

        JObject saved = JObject.Parse(File.ReadAllText(path));
        Assert.True((bool)saved["futureRoot"]["nested"]["preserve"]);
        Assert.Equal("older-app", (string)saved["kinks"].Single(x => (string)x["id"] == "known-kink")["customKink"]["source"]);
        Assert.Equal(new[] { 1, 2 }, saved["equipment"][0]["customNested"].Values<int>());
        Assert.True((bool)saved["facts"][0]["customFact"]);
        Assert.Equal("future", (string)saved["assignments"][0]["customAssignment"]["version"]);
        Assert.Equal("yes", (string)saved["assignments"][0]["parameters"][0]["customParameter"]["deep"]);
        Assert.Equal("future", (string)saved["sessionHistory"][0]["customHistory"]);
        Assert.Equal("unknown-to-current-catalog", (string)saved["assignments"][0]["definitionId"]);
        Assert.Equal((int)KinkFrequency.Frequent, (int)saved["kinks"].Single(x => (string)x["id"] == "future-kink")["frequency"]);
        Assert.Equal((int)KinkSentiment.Loved, (int)saved["kinks"].Single(x => (string)x["id"] == "future-kink")["sentiment"]);
    }

    [Fact]
    public void FailedTemporaryWriteKeepsCurrentAndFileAndAllowsRetry()
    {
        using var directory = new TemporaryProfileDirectory();
        var service = new PlayerProfileService(directory.Path);
        service.Commit(ProfileTestSupport.PopulatedProfile());
        var currentBefore = service.Current;
        byte[] fileBefore = File.ReadAllBytes(service.StoragePath);
        string tempPath = service.StoragePath + ".tmp";
        Directory.CreateDirectory(tempPath);

        using var edit = new PlayerProfileEditSession(service);
        edit.SetFrequency("known-kink", KinkFrequency.VeryFrequent);
        Assert.ThrowsAny<Exception>(() => edit.Commit());
        Assert.Same(currentBefore, service.Current);
        Assert.Equal(fileBefore, File.ReadAllBytes(service.StoragePath));
        Assert.NotNull(service.LastSaveDiagnostic);

        Directory.Delete(tempPath);
        Assert.True(edit.Commit());
        Assert.Equal(KinkFrequency.VeryFrequent, service.Current.kinks.Single().frequency);
        Assert.Null(service.LastSaveDiagnostic);
    }

    [Fact]
    public void ConcurrentCommitRejectsStaleEditWithoutOverwritingNewerData()
    {
        using var directory = new TemporaryProfileDirectory();
        var service = new PlayerProfileService(directory.Path);
        service.Commit(ProfileTestSupport.PopulatedProfile());

        using var stale = new PlayerProfileEditSession(service);
        stale.SetFrequency("known-kink", KinkFrequency.VeryFrequent);
        service.Update(profile => profile.favor = 63f);
        var newer = service.Current;
        byte[] newerFile = File.ReadAllBytes(service.StoragePath);

        var error = Assert.Throws<InvalidOperationException>(() => stale.Commit());
        Assert.Contains("profile changed", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(newer, service.Current);
        Assert.Equal(63f, service.Current.favor);
        Assert.Equal(newerFile, File.ReadAllBytes(service.StoragePath));
        Assert.Equal(63f, new PlayerProfileService(directory.Path).Current.favor);
    }

    [Fact]
    public void ProfileApplyPreservesFavorFactsAssignmentsAndHistory()
    {
        using var directory = new TemporaryProfileDirectory();
        var service = new PlayerProfileService(directory.Path);
        service.Commit(ProfileTestSupport.PopulatedProfile());

        using var edit = new PlayerProfileEditSession(service);
        edit.SetSentiment("known-kink", KinkSentiment.Neutral);
        Assert.True(edit.Commit());

        var saved = service.Current;
        Assert.Equal(50f, saved.favor);
        Assert.Equal("voice", Assert.Single(saved.facts).key);
        Assert.Equal("calm", saved.facts[0].stringValue);
        var assignment = Assert.Single(saved.assignments);
        Assert.Equal("assignment-1", assignment.instanceId);
        Assert.Equal("story-1", assignment.definitionId);
        Assert.Equal(AssignmentStatus.Pending, assignment.status);
        Assert.Equal("soft", Assert.Single(assignment.parameters).stringValue);
        var history = Assert.Single(saved.sessionHistory);
        Assert.Equal("session-1", history.sessionId);
        Assert.Equal("Completed.", history.summary);
    }
}
