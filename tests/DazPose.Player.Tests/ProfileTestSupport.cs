using DazPose.Game;
using DazPose.Story;

namespace DazPose.Player.Tests;

internal sealed class TemporaryProfileDirectory : IDisposable
{
    private const string Prefix = "DazPose.Player.Tests-";
    public string Path { get; } = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N")));

    public TemporaryProfileDirectory() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        string tempRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath())
            .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        string target = System.IO.Path.GetFullPath(Path);
        string prefix = tempRoot + System.IO.Path.DirectorySeparatorChar;
        if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !System.IO.Path.GetFileName(target).StartsWith(Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to delete outside this test's unique temp directory.");
        if (Directory.Exists(target)) Directory.Delete(target, true);
    }
}

internal static class ProfileTestSupport
{
    public static PlayerProfile PopulatedProfile() => new PlayerProfile
    {
        favor = 50f,
        submissionScore = 25,
        kinks = new[] { new KinkPreference { id = "known-kink", frequency = KinkFrequency.Normal, sentiment = KinkSentiment.Loved } },
        equipment = new[] { new EquipmentPreference { id = "known-item", ownedAndWilling = true, useAsReward = true, useAsPunishment = true } },
        facts = new[] { new PersistentStoryFact { key = "voice", valueType = "string", stringValue = "calm" } },
        assignments = new[] { new AssignmentInstance
        {
            instanceId = "assignment-1", definitionId = "story-1",
            issuedAtUtc = "2026-10-18T12:00:00Z", status = AssignmentStatus.Pending,
            parameters = new[] { new StoryArgument { name = "tone", valueType = "string", stringValue = "soft" } }
        } },
        sessionHistory = new[] { new SessionHistorySummary
        { sessionId = "session-1", endedAtUtc = "2026-10-18T12:00:00Z", summary = "Completed." } }
    };
}
