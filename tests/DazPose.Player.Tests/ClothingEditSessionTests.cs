using DazPose.Game;
using DazPose.Story;

namespace DazPose.Player.Tests;

public sealed class ClothingEditSessionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(6)]
    public void CancelAtZeroTwoOrSixItemsLeavesWorldStateUnchanged(int initialCount)
    {
        var world = WorldWithClothingCount(initialCount);
        var originalClothing = PlayerWorldState.ClothingSlots.ToDictionary(slot => slot, world.GetClothing);
        var other = OtherState(world);

        using (var edit = new ClothingEditSession(world))
        {
            foreach (string slot in edit.Slots) edit.SetClothing(slot, !edit.GetClothing(slot));
            edit.Discard();
        }

        foreach (string slot in PlayerWorldState.ClothingSlots)
            Assert.Equal(originalClothing[slot], world.GetClothing(slot));
        Assert.Equal(other, OtherState(world));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(6)]
    public void ApplyAtZeroTwoOrSixItemsChangesOnlyClothingSlots(int initialCount)
    {
        var world = WorldWithClothingCount(initialCount);
        var other = OtherState(world);
        var expected = PlayerWorldState.ClothingSlots
            .Select((slot, index) => (slot, value: index % 2 == 0))
            .ToDictionary(item => item.slot, item => item.value);

        using var edit = new ClothingEditSession(world);
        foreach (string slot in edit.Slots) edit.SetClothing(slot, expected[slot]);
        Assert.True(edit.Commit());

        foreach (string slot in PlayerWorldState.ClothingSlots)
            Assert.Equal(expected[slot], world.GetClothing(slot));
        Assert.Equal(other, OtherState(world));
        Assert.Equal(expected.Values.Count(value => value), world.ClothingCount);
    }

    [Fact]
    public void UnknownSlotIsRejectedWithoutChangingWorld()
    {
        var world = WorldWithClothingCount(2);
        bool[] before = PlayerWorldState.ClothingSlots.Select(world.GetClothing).ToArray();
        using var edit = new ClothingEditSession(world);

        Assert.Throws<ArgumentException>(() => edit.SetClothing("hat", true));
        Assert.Throws<ArgumentException>(() => edit.GetClothing("hat"));
        Assert.Equal(before, PlayerWorldState.ClothingSlots.Select(world.GetClothing));
    }

    private static PlayerWorldState WorldWithClothingCount(int count)
    {
        var world = new PlayerWorldState
        {
            posture = PlayerPosture.Kneeling,
            hands = PlayerHands.Restricted,
            ankles = PlayerAnkles.Free,
            oral = PlayerOccupancy.Occupied,
            anal = PlayerOccupancy.Free,
            genital = PlayerOccupancy.Occupied
        };
        for (int i = 0; i < count; i++) world.SetClothing(PlayerWorldState.ClothingSlots[i], true);
        return world;
    }

    private static (PlayerPosture posture, PlayerHands hands, PlayerAnkles ankles,
        PlayerOccupancy oral, PlayerOccupancy anal, PlayerOccupancy genital) OtherState(PlayerWorldState world) =>
        (world.posture, world.hands, world.ankles, world.oral, world.anal, world.genital);
}
