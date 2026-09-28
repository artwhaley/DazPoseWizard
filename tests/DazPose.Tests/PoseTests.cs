using DazPose.Core;
using System.Numerics;

namespace DazPose.Tests;

public sealed class PoseTests
{
    [LocalDazFixtureFact]
    public void CherishPoseHasExpectedStaticChannelCountsAndNeutralUnsupportedChannels()
    {
        var figure = FixtureData.LoadFigure();
        var pose = FixtureData.LoadPose(figure);

        Assert.Equal(368, pose.Channels.Count);
        Assert.All(pose.Channels, channel => Assert.Single(channel.Keys));
        Assert.All(pose.Channels.SelectMany(channel => channel.Keys), key => Assert.Equal(0, key.Time));
        Assert.Equal(291, pose.Channels.Count(channel => channel.ParsedUrl.Property == "rotation"));
        Assert.Equal(3, pose.Channels.Count(channel => channel.ParsedUrl.Property == "translation"));
        Assert.Equal(74, pose.Channels.Count(channel => channel.ParsedUrl.Property == "value" && channel.ParsedUrl.LeafProperty == "value"));
        Assert.Equal(97, pose.SkeletalTargetCount);
        Assert.Equal(97, pose.ResolvedSkeletalTargetCount);
        Assert.Equal(74, pose.NeutralUnsupportedChannels.Count);
        Assert.Empty(pose.NonNeutralUnsupportedChannels);
        Assert.All(pose.Channels.Where(channel => channel.TargetBone is not null), channel => Assert.False(channel.UsedIdFallback));
    }

    [LocalDazFixtureFact]
    public void NameAddressingDoesNotCollapseIntoNodeIds()
    {
        var figure = FixtureData.LoadFigure();
        Assert.Equal("lThigh", figure.NodesByName["lThighBend"].Id);
        Assert.Equal("lForeArm", figure.NodesByName["lForearmBend"].Id);
        Assert.False(figure.NodesById.ContainsKey("lThighBend"));
        Assert.False(figure.NodesById.ContainsKey("lForearmBend"));

        var pose = FixtureData.LoadPose(figure);
        Assert.Equal("lThigh", pose.Channels.Single(channel => channel.Url.Contains("lThighBend:?rotation/x", StringComparison.Ordinal)).TargetBone!.Id);
    }

    [LocalDazFixtureFact]
    public void RepresentativeCherishValuesArePreserved()
    {
        var pose = FixtureData.LoadPose();
        AssertChannel(pose, "lThighBend", "rotation", "x", -54.68033f);
        AssertChannel(pose, "lThighBend", "rotation", "z", -4.70808f);
        AssertChannel(pose, "lShin", "rotation", "x", 59.23077f);
        AssertChannel(pose, "lFoot", "rotation", "x", 65f);
        AssertChannel(pose, "lFoot", "rotation", "y", 9.816853f);
        AssertChannel(pose, "rThighBend", "rotation", "z", 19.74233f);
        AssertChannel(pose, "head", "rotation", "x", -11.90386f);
        AssertChannel(pose, "head", "rotation", "y", -15.23077f);
        AssertChannel(pose, "lIndex2", "rotation", "z", -89.0736f);
        AssertChannel(pose, "lRing2", "rotation", "z", -109.3465f);
        AssertChannel(pose, "hip", "translation", "y", -2.404064f);
    }

    [LocalDazFixtureFact]
    public void NonNeutralUnsupportedPropertiesAndAnimationAreRejected()
    {
        var basePose = FixtureData.LoadPose();
        var channel = basePose.Channels.First(channel => channel.ParsedUrl.Property == "value");
        var activeChannel = channel with { Keys = [new DazPoseKey(0, 0.25f)] };
        var activePose = new DazPose.Core.DazPose
        {
            FilePath = basePose.FilePath, AssetId = basePose.AssetId,
            Channels = basePose.Channels.Select(item => ReferenceEquals(item, channel) ? activeChannel : item).ToArray()
        };
        var unsupported = Assert.Throws<DazConversionException>(() => PoseConversionService.ValidatePose(activePose));
        Assert.Contains("Unsupported non-neutral channel", unsupported.Message);

        var movingChannel = basePose.Channels.First(channel => channel.IsSupportedSkeletalChannel) with
        {
            Keys = [new DazPoseKey(0, 0), new DazPoseKey(1, 30)]
        };
        var animatedPose = new DazPose.Core.DazPose
        {
            FilePath = basePose.FilePath, AssetId = basePose.AssetId,
            Channels = basePose.Channels.Select(item => item.IsSupportedSkeletalChannel ? movingChannel : item).ToArray()
        };
        var animation = Assert.Throws<DazConversionException>(() => PoseConversionService.ValidatePose(animatedPose));
        Assert.Equal("This preset contains animation data. V0 supports static poses only.", animation.Message);
    }

    private static void AssertChannel(DazPose.Core.DazPose pose, string target, string property, string axis, float expected)
    {
        var channel = Assert.Single(pose.Channels, channel => channel.TargetBone?.Name == target
            && channel.ParsedUrl.Property == property && channel.ParsedUrl.Axis == axis);
        Assert.InRange(MathF.Abs(channel.Keys[0].Value - expected), 0, 1e-5f);
    }
}
