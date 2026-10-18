using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DazPose.Story.Language;

namespace DazPose.Story.Tests;

public sealed class GlobalCardAdditionalTests
{
    private static CompiledStoryProgram Compile(string text, string file = "additional-test.dazstory") =>
        DazStoryCompiler.Compile(text, file, allowExternalCards: true);

    private static string ExternalCard(string id, string tags, string body = "Session.Mood += 2", string requirement = "") =>
        "card " + id + "\ntags [" + tags + "]\nprogress 10\n"
        + (requirement.Length == 0 ? "" : "requires " + requirement + "\n")
        + "{\n" + body + "\n}\n";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LinkedCardFailureOrCancellationReceivesNoProgressOrHistoryCredit(bool cancel)
    {
        var root = Compile("sequence Test {\nawait Director.Execute(ExternalWork)\n}\n");
        var importedCard = Compile(ExternalCard("ExternalWork", "Warmup", "await Lara.PersistentPose(SexyVamp)"), "global/work.dazstory");
        var linked = StoryProgramLinker.Link(root, new[] { new StoryLinkUnit("wardrobe/work.dazstory", importedCard) });
        var pending = new TaskCompletionSource<StoryData>(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new DirectorTests.Host();
        host.Handler = (command, token) =>
        {
            if (command.Verb != "Lara.PersistentPose") return Task.FromResult(default(StoryData));
            token.Register(() => pending.TrySetCanceled(token));
            return pending.Task;
        };

        using var cancellation = new CancellationTokenSource();
        var machine = new StoryMachine(linked, host);
        Task run = machine.RunAsync("Test", cancellation.Token);
        Assert.False(run.IsCompleted);
        if (cancel)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        }
        else
        {
            pending.SetException(new InvalidOperationException("linked pose failed"));
            await Assert.ThrowsAsync<StorySourceException>(() => run);
        }

        Assert.Equal(0, host.Read("Session.PhaseCompletion").Numeric);
        Assert.Equal(0, machine.Director.History.SelectionCount);
        Assert.Equal(0, machine.Director.History.For("ExternalWork").TimesUsed);
        Assert.Empty(host.Selected);
    }

    [Fact]
    public async Task EmptyTagQuerySelectsPublishedCardsAndExcludesLocalIntro()
    {
        const string rootText =
            "card Intro {\nSession.Mood += 100\n}\n"
            + "sequence Test {\nawait Director.DrawAndExecute(tags: [])\n}\n";
        var root = Compile(rootText);
        var external = Compile(ExternalCard("Remote", "Warmup"));
        var linked = StoryProgramLinker.Link(root, new[] { new StoryLinkUnit("source/cards.dazstory", external) });
        var host = new DirectorTests.Host();

        await new StoryMachine(linked, host, new StorySessionRandom(7)).RunAsync("Test");

        Assert.Equal(new[] { "Remote" }, host.Selected);
        Assert.Equal(52, host.Read("Session.Mood").Numeric);
        Assert.Contains(host.Rejected, reason => reason.StartsWith("Intro: Local card is not published for random selection."));
        Assert.False(linked.cards.Single(card => card.Id == "Intro").Selectable);
    }

    private sealed class PreparedTagHost : DirectorTests.Host, IStoryExecutionHost
    {
        StoryData IStoryExecutionHost.Function(string name, StoryData[] arguments)
        {
            if (name == "Equipment.PreparedHasTag")
            {
                string tag = arguments.Single().String;
                return new StoryData(State["Session.PreparedEquipment"].Items.Any(id => "equipment:" + id == tag));
            }
            return base.Function(name, arguments);
        }
    }

    [Fact]
    public async Task OpenEquipmentTagsParticipateInLinkedSelectionAndPreparedTagRequirements()
    {
        const string customEquipment = "custom-gear_v2";
        const string requirement = "Equipment.PreparedHasTag(\"equipment:" + customEquipment + "\")";
        var root = Compile(
            "sequence Test {\n"
            + "Session.SelectPreparedEquipment(1)\n"
            + "await Director.DrawAndExecute(equipment: [\"" + customEquipment + "\"])\n"
            + "}\n");
        var external = Compile(ExternalCard("GearBeat", "Warmup, equipment:" + customEquipment,
            "Session.Mood += 2", requirement));
        var linked = StoryProgramLinker.Link(root, new[] { new StoryLinkUnit("source/gear.dazstory", external) });
        var prepared = new PreparedTagHost { Equipment = customEquipment };

        await new StoryMachine(linked, prepared, new StorySessionRandom(9)).RunAsync("Test");

        Assert.Equal(new[] { "GearBeat" }, prepared.Selected);
        Assert.Equal(52, prepared.Read("Session.Mood").Numeric);
        Assert.Contains("equipment:" + customEquipment, linked.cards.Single(card => card.Id == "GearBeat").Tags);

        var unprepared = new PreparedTagHost { Equipment = null };
        var unpreparedMachine = new StoryMachine(linked, unprepared, new StorySessionRandom(9));
        await Assert.ThrowsAsync<StorySourceException>(() => unpreparedMachine.RunAsync("Test"));
        Assert.Equal(0, unprepared.Read("Session.PhaseCompletion").Numeric);
        Assert.Equal(0, unpreparedMachine.Director.History.SelectionCount);
        Assert.Contains(unprepared.Rejected, reason => reason.Contains("Required Equipment.PreparedHasTag"));
    }

    [Fact]
    public void RepeatedLinkingDoesNotMutateRootOrImportedPrograms()
    {
        var root = Compile("card Intro {\nSession.Mood += 1\n}\nsequence Test {\nplay PublicCard\n}\n");
        var imported = Compile(ExternalCard("PublicCard", "Warmup"));
        var unit = new StoryLinkUnit("stable/source.dazstory", imported);

        var first = StoryProgramLinker.Link(root, new[] { unit });
        CardDefinition firstCard = first.cards.Single(card => card.Id == "PublicCard");
        firstCard.Id = "corrupted";
        firstCard.Tags[0] = "Corrupted";

        var second = StoryProgramLinker.Link(root, new[] { unit });

        Assert.False(root.linked);
        Assert.False(imported.linked);
        Assert.Single(root.cards);
        Assert.Single(imported.cards);
        Assert.Equal("Intro", root.cards.Single().Id);
        Assert.Equal("PublicCard", imported.cards.Single().Id);
        Assert.Equal(new[] { "Warmup" }, imported.cards.Single().Tags);
        Assert.Equal("PublicCard", second.cards.Single(card => card.Selectable).Id);
        Assert.Equal(new[] { "Warmup" }, second.cards.Single(card => card.Id == "PublicCard").Tags);
        Assert.NotSame(firstCard, second.cards.Single(card => card.Id == "PublicCard"));
        Assert.Equal("stable/source.dazstory", second.cards.Single(card => card.Id == "PublicCard").Source.unit);
    }
}