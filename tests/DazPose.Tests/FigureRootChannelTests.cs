using System.Text.Json;
using DazPose.Core;

namespace DazPose.Tests;

public sealed class FigureRootChannelTests
{
    [Fact]
    public void PropertyUrlParserDistinguishesNamedNodeControlAndSelectedFigureRoot()
    {
        var namedBone = DazPropertyUrlParser.Parse("name://@selection/lThighBend:?rotation/x/value");
        Assert.Equal("lThighBend", namedBone.TargetNodeName);
        Assert.Null(namedBone.ControlId);
        Assert.False(namedBone.IsSelectedFigureRoot);
        Assert.False(namedBone.IsFigureControlAddress);

        var control = DazPropertyUrlParser.Parse("name://@selection#pCTRLWhatever:?value/value");
        Assert.Null(control.TargetNodeName);
        Assert.Equal("pCTRLWhatever", control.ControlId);
        Assert.False(control.IsSelectedFigureRoot);
        Assert.True(control.IsFigureControlAddress);

        var figureRoot = DazPropertyUrlParser.Parse("name://@selection:?rotation/x/value");
        Assert.Null(figureRoot.TargetNodeName);
        Assert.Null(figureRoot.ControlId);
        Assert.True(figureRoot.IsSelectedFigureRoot);
        Assert.False(figureRoot.IsFigureControlAddress);
    }

    [LocalVintageGlamourFixtureFact]
    public void VintageGlamourPoseConvertsAndReportsThreeNeutralTargetlessFigureRotations()
    {
        var figure = FixtureData.LoadFigure();
        using var poseDocument = DsonFileReader.ReadJson(FixtureData.VintageGlamourPosePath);
        Assert.Equal("preset_pose", poseDocument.RootElement.GetProperty("asset_info").GetProperty("type").GetString());

        var pose = DazPoseParser.Parse(FixtureData.VintageGlamourPosePath, poseDocument, figure);
        Assert.Equal(369, pose.Channels.Count);
        Assert.Equal(294, pose.Channels.Count(channel => channel.TargetBone is not null));
        Assert.Equal(97, pose.SkeletalTargetCount);
        Assert.Equal(97, pose.ResolvedSkeletalTargetCount);

        var targetlessRotations = pose.Channels
            .Where(channel => channel.ParsedUrl.IsSelectedFigureRoot && channel.ParsedUrl.Property == "rotation")
            .ToArray();
        Assert.Equal(3, targetlessRotations.Length);
        Assert.All(targetlessRotations, channel => Assert.False(channel.IsSupportedSkeletalChannel));
        Assert.All(targetlessRotations, channel => Assert.Contains(channel, pose.NeutralUnsupportedChannels));
        Assert.Empty(targetlessRotations.Intersect(pose.NonNeutralUnsupportedChannels));
        PoseConversionService.ValidatePose(pose);

        var output = FixtureData.NewTempDirectory();
        try
        {
            var result = PoseConversionService.Convert(FixtureData.FigurePath, FixtureData.VintageGlamourPosePath, output);
            var ignoredRootChannels = result.Pose.NeutralUnsupportedChannels
                .Where(channel => channel.ParsedUrl.IsSelectedFigureRoot).Select(channel => channel.Url).ToArray();
            Assert.Equal(3, ignoredRootChannels.Length);
            Assert.True(File.Exists(result.JsonPath));
            Assert.True(File.Exists(result.BvhPath));
            Assert.True(File.Exists(result.ReportPath));

            var report = File.ReadAllText(result.ReportPath);
            var ignored = JsonDocument.Parse(File.ReadAllText(result.JsonPath));
            using (ignored)
            {
                var canonicalIgnoredUrls = ignored.RootElement.GetProperty("ignoredNeutralChannels").EnumerateArray()
                    .Select(item => item.GetString()).ToArray();
                foreach (var url in ignoredRootChannels)
                {
                    Assert.Contains(url, report, StringComparison.Ordinal);
                    Assert.Contains(url, canonicalIgnoredUrls);
                }
            }
        }
        finally { FixtureData.DeleteTempDirectory(output); }
    }

    [Fact]
    public void NeutralTargetlessFigureRootRotationIsUnsupportedButAllowed()
    {
        var pose = ParseSyntheticTargetlessTransform("rotation", "y", 0f);
        var channel = Assert.Single(pose.Channels);

        Assert.True(channel.ParsedUrl.IsSelectedFigureRoot);
        Assert.False(channel.IsSupportedSkeletalChannel);
        Assert.Contains(channel, pose.NeutralUnsupportedChannels);
        Assert.Empty(pose.NonNeutralUnsupportedChannels);
        PoseConversionService.ValidatePose(pose);
    }

    [Fact]
    public void NonNeutralTargetlessFigureRootRotationIsRejectedAsAuthoredData()
    {
        const string url = "name://@selection:?rotation/y/value";
        var pose = ParseSyntheticTargetlessTransform("rotation", "y", 15f);
        var channel = Assert.Single(pose.Channels);
        var error = Assert.Throws<DazConversionException>(() => PoseConversionService.ValidatePose(pose));

        Assert.True(channel.ParsedUrl.IsSelectedFigureRoot);
        Assert.Contains(channel, pose.NonNeutralUnsupportedChannels);
        Assert.Contains("Unsupported non-neutral channel", error.Message, StringComparison.Ordinal);
        Assert.Contains("(figure-root property)", error.Message, StringComparison.Ordinal);
        Assert.Contains(url, error.Message, StringComparison.Ordinal);
        Assert.Contains("15", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("does not address a target node", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0f, true)]
    [InlineData(15f, false)]
    public void TargetlessFigureRootTranslationFollowsNeutralSafetyPolicy(float value, bool shouldValidate)
    {
        const string url = "name://@selection:?translation/x/value";
        var pose = ParseSyntheticTargetlessTransform("translation", "x", value);
        var channel = Assert.Single(pose.Channels);

        Assert.True(channel.ParsedUrl.IsSelectedFigureRoot);
        Assert.False(channel.IsSupportedSkeletalChannel);
        if (shouldValidate)
        {
            Assert.Contains(channel, pose.NeutralUnsupportedChannels);
            PoseConversionService.ValidatePose(pose);
        }
        else
        {
            Assert.Contains(channel, pose.NonNeutralUnsupportedChannels);
            var error = Assert.Throws<DazConversionException>(() => PoseConversionService.ValidatePose(pose));
            Assert.Contains("Unsupported non-neutral channel", error.Message, StringComparison.Ordinal);
            Assert.Contains("(figure-root property)", error.Message, StringComparison.Ordinal);
            Assert.Contains(url, error.Message, StringComparison.Ordinal);
            Assert.Contains("15", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NeutralScaleXUsesOneAsNeutralAndRemainsUnsupported()
    {
        const string url = "name://@selection/upperTeeth:?scale/x/value";
        var pose = ParseSyntheticChannel(url, 1f);
        var channel = Assert.Single(pose.Channels);

        Assert.False(channel.IsSupportedChannel);
        Assert.Equal(1f, channel.NeutralValue);
        Assert.Contains(channel, pose.NeutralUnsupportedChannels);
        Assert.Empty(pose.NonNeutralUnsupportedChannels);
        PoseConversionService.ValidatePose(pose);
    }

    [Fact]
    public void GeneralScaleWithMultipleNeutralKeysIsIgnored()
    {
        const string url = "name://@selection/tongue:?scale/general/value";
        var pose = ParseSyntheticChannel(url, 1f, 1f, 1f);
        var channel = Assert.Single(pose.Channels);

        Assert.False(channel.IsSupportedChannel);
        Assert.Contains(channel, pose.NeutralUnsupportedChannels);
        Assert.Empty(pose.NonNeutralUnsupportedChannels);
        PoseConversionService.ValidatePose(pose);
    }

    [Fact]
    public void ScaleValuesWithinExistingFloatToleranceOfOneAreNeutral()
    {
        const string url = "name://@selection/lowerTeeth:?scale/y/value";
        var pose = ParseSyntheticChannel(url, 0.99999994f);
        var channel = Assert.Single(pose.Channels);

        Assert.True(channel.IsNeutralValue(channel.Keys[0].Value));
        Assert.Contains(channel, pose.NeutralUnsupportedChannels);
        Assert.Empty(pose.NonNeutralUnsupportedChannels);
        PoseConversionService.ValidatePose(pose);
    }

    [Fact]
    public void NonNeutralScaleIsRejectedAndReportsItsNeutralDefault()
    {
        const string url = "name://@selection/upperTeeth:?scale/x/value";
        var pose = ParseSyntheticChannel(url, 1.1f);
        var channel = Assert.Single(pose.Channels);

        Assert.Contains(channel, pose.NonNeutralUnsupportedChannels);
        Assert.Empty(pose.NeutralUnsupportedChannels);
        var error = Assert.Throws<DazConversionException>(() => PoseConversionService.ValidatePose(pose));
        Assert.Contains(url, error.Message, StringComparison.Ordinal);
        Assert.Contains("has value 1.1", error.Message, StringComparison.Ordinal);
        Assert.Contains("Neutral scale is 1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NeutralUnsupportedControlPropertyStillUsesZero()
    {
        const string url = "name://@selection#SomeControl:?min/value";
        var pose = ParseSyntheticChannel(url, 0f);
        var channel = Assert.Single(pose.Channels);

        Assert.False(channel.IsSupportedChannel);
        Assert.Equal(0f, channel.NeutralValue);
        Assert.Contains(channel, pose.NeutralUnsupportedChannels);
        Assert.Empty(pose.NonNeutralUnsupportedChannels);
        PoseConversionService.ValidatePose(pose);
    }

    [Fact]
    public void ActiveUnsupportedControlPropertyStillFailsSafe()
    {
        const string url = "name://@selection#SomeControl:?min/value";
        var pose = ParseSyntheticChannel(url, 0.5f);
        var channel = Assert.Single(pose.Channels);

        Assert.Contains(channel, pose.NonNeutralUnsupportedChannels);
        var error = Assert.Throws<DazConversionException>(() => PoseConversionService.ValidatePose(pose));
        Assert.Contains(url, error.Message, StringComparison.Ordinal);
        Assert.Contains("has value 0.5", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(0.5f, true)]
    public void StageFiveFigureControlValueChannelsRemainSupported(float value, bool isActive)
    {
        const string url = "name://@selection#SomeControl:?value/value";
        var pose = ParseSyntheticChannel(url, value);
        var channel = Assert.Single(pose.Channels);

        Assert.True(channel.IsSupportedFigureControlChannel);
        Assert.Equal(isActive, pose.ActiveFigureControls.Any(control => control.RawControlId == "SomeControl"));
        Assert.DoesNotContain(channel, pose.NonNeutralUnsupportedChannels);
        PoseConversionService.ValidatePose(pose);
    }

    [LocalDazFixtureFact("ST PD Smirk.duf")]
    public void StPdSmirkNeutralScaleChannelsDoNotBlockValidation()
    {
        var figure = FixtureData.LoadFigure();
        using var document = DsonFileReader.ReadJson(FixtureData.StPdSmirkPosePath);
        var pose = DazPoseParser.Parse(FixtureData.StPdSmirkPosePath, document, figure);
        var neutralScaleChannels = pose.Channels
            .Where(channel => channel.ParsedUrl.Property == "scale"
                && channel.Keys.All(key => Math.Abs(key.Value - 1f) <= 1e-7f))
            .ToArray();

        Assert.NotEmpty(neutralScaleChannels);
        Assert.All(neutralScaleChannels, channel => Assert.Contains(channel, pose.NeutralUnsupportedChannels));
        Assert.All(neutralScaleChannels, channel => Assert.DoesNotContain(channel, pose.NonNeutralUnsupportedChannels));

        var output = FixtureData.NewTempDirectory();
        try
        {
            var canonicalPath = Path.Combine(output, "ST PD Smirk.dazpose.json");
            var failure = Record.Exception(() => PoseConversionService.ConvertCanonical(
                FixtureData.FigurePath, FixtureData.StPdSmirkPosePath, canonicalPath));
            if (failure is null)
            {
                Assert.True(File.Exists(canonicalPath));
                return;
            }

            var conversionError = Assert.IsType<DazConversionException>(failure);
            Assert.Contains("Unsupported non-neutral channel", conversionError.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(neutralScaleChannels,
                channel => conversionError.Message.Contains(channel.Url, StringComparison.Ordinal));
            Assert.Contains(pose.NonNeutralUnsupportedChannels,
                channel => conversionError.Message.Contains(channel.Url, StringComparison.Ordinal));
        }
        finally { FixtureData.DeleteTempDirectory(output); }
    }

    private static DazPose.Core.DazPose ParseSyntheticTargetlessTransform(string property, string axis, float value)
    {
        var url = $"name://@selection:?{property}/{axis}/value";
        var jsonText = JsonSerializer.Serialize(new
        {
            scene = new
            {
                animations = new[]
                {
                    new { url, keys = new[] { new[] { 0f, value } } }
                }
            }
        });
        using var document = JsonDocument.Parse(jsonText);
        return DazPoseParser.Parse("synthetic-root-channel.duf", document, CreateMinimalFigure());
    }

    private static DazPose.Core.DazPose ParseSyntheticChannel(string url, params float[] values)
    {
        var keys = values.Select((value, index) => new[] { (float)index, value }).ToArray();
        var jsonText = JsonSerializer.Serialize(new { scene = new { animations = new[] { new { url, keys } } } });
        using var document = JsonDocument.Parse(jsonText);
        return DazPoseParser.Parse("synthetic-unsupported-channel.duf", document, CreateMinimalFigure());
    }

    private static DazFigureDefinition CreateMinimalFigure()
    {
        var root = new DazBoneDefinition("Figure", "Figure", "Figure", "figure", null, "XYZ", true,
            System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero,
            System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero, System.Numerics.Vector3.One, 1f);
        return new DazFigureDefinition
        {
            FilePath = "synthetic-figure.dsf",
            AssetId = string.Empty,
            Nodes = [root],
            NodesById = new Dictionary<string, DazBoneDefinition>(StringComparer.Ordinal) { [root.Id] = root },
            NodesByName = new Dictionary<string, DazBoneDefinition>(StringComparer.Ordinal) { [root.Name] = root }
        };
    }
}
