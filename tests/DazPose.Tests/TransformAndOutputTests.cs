using DazPose.Core;
using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace DazPose.Tests;

public sealed class TransformAndOutputTests
{
    private static readonly string[] Orders = ["XYZ", "YZX", "ZYX", "ZXY", "XZY", "YXZ"];

    [Theory]
    [MemberData(nameof(RotationOrders))]
    public void EulerConversionRoundTripsEachRotationOrder(string order)
    {
        var source = new Vector3(31.5f, -22.25f, 47.75f);
        var quaternion = DazTransformEvaluator.EulerToQuaternion(source, order);
        var result = DazTransformEvaluator.QuaternionToEuler(quaternion, order);
        var reconstructed = DazTransformEvaluator.EulerToQuaternion(result, order);
        AssertQuaternionEquivalent(quaternion, reconstructed);
    }

    public static IEnumerable<object[]> RotationOrders => Orders.Select(order => new object[] { order });

    [Fact]
    public void SingleAxisRotationIsIndependentOfOtherOrderPositionsAndCombinedOrderMatters()
    {
        var xOnly = Orders.Select(order => DazTransformEvaluator.EulerToQuaternion(new Vector3(35, 0, 0), order)).ToArray();
        foreach (var quaternion in xOnly.Skip(1)) AssertQuaternionEquivalent(xOnly[0], quaternion);
        var xyz = DazTransformEvaluator.EulerToQuaternion(new Vector3(30, 40, 50), "XYZ");
        var zyx = DazTransformEvaluator.EulerToQuaternion(new Vector3(30, 40, 50), "ZYX");
        Assert.True(MathF.Abs(Quaternion.Dot(xyz, zyx)) < 0.9999f);
    }

    [Fact]
    public void DazCentimetersConvertToUnityAndBvhMeters()
    {
        Assert.Equal(1f, DazPoseUnits.CentimetersToMeters(100f), 6);
        Assert.Equal(new Vector3(1, -0.25f, 0.5f), DazPoseUnits.CentimetersToMeters(new Vector3(100, -25, 50)));
    }

    [Fact]
    public void EvaluatorAppliesOrientationConjugationSeparatelyFromPoseEulerOrder()
    {
        var figureRoot = new DazBoneDefinition("Figure", "Figure", "Figure", "figure", null, "XYZ", true,
            Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.One, 1);
        var bone = new DazBoneDefinition("joint", "joint", "Joint", "bone", "Figure", "XZY", false,
            Vector3.Zero, Vector3.UnitY, new Vector3(30, -10, 5), Vector3.Zero, Vector3.Zero, Vector3.One, 1);
        var figure = new DazFigureDefinition
        {
            FilePath = "synthetic.dsf", AssetId = "", Nodes = [figureRoot, bone],
            NodesById = new Dictionary<string, DazBoneDefinition> { ["Figure"] = figureRoot, ["joint"] = bone },
            NodesByName = new Dictionary<string, DazBoneDefinition> { ["Figure"] = figureRoot, ["joint"] = bone }
        };
        var poseRotation = new Vector3(15, 25, -5);
        var channels = new[] { ("x", poseRotation.X), ("y", poseRotation.Y), ("z", poseRotation.Z) }
            .Select(item => new DazPoseChannel(
                $"name://@selection/joint:?rotation/{item.Item1}/value",
                new DazPropertyUrl("name", "@selection/joint", "joint", null, "rotation", item.Item1, "value"),
                [new DazPoseKey(0, item.Item2)], bone, false)).ToArray();
        var pose = new DazPose.Core.DazPose { FilePath = "synthetic.duf", AssetId = "", Channels = channels };
        var evaluation = DazTransformEvaluator.Evaluate(figure, pose);
        var orientation = DazTransformEvaluator.EulerToQuaternion(bone.Orientation, "XYZ");
        var rotation = DazTransformEvaluator.EulerToQuaternion(poseRotation, bone.RotationOrder);
        var expected = Quaternion.Normalize(orientation * rotation * Quaternion.Inverse(orientation));
        AssertQuaternionEquivalent(expected, evaluation.BonesById["joint"].EvaluatedLocalRotation);
    }

    [Fact]
    public void EvaluatorAppliesScaleInheritanceAndCompensation()
    {
        var figureRoot = new DazBoneDefinition("Figure", "Figure", "Figure", "figure", null, "XYZ", true,
            Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.One, 1);
        var parent = new DazBoneDefinition("parent", "parent", "Parent", "bone", "Figure", "XYZ", true,
            Vector3.Zero, Vector3.UnitY, Vector3.Zero, Vector3.Zero, Vector3.Zero, new Vector3(2), 1);
        var inherited = new DazBoneDefinition("inherited", "inherited", "Inherited", "bone", "parent", "XYZ", true,
            Vector3.Zero, Vector3.UnitY, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.One, 1);
        var compensated = inherited with { Id = "compensated", Name = "compensated", InheritsScale = false };
        var nodes = new[] { figureRoot, parent, inherited, compensated };
        var figure = new DazFigureDefinition
        {
            FilePath = "synthetic.dsf", AssetId = "", Nodes = nodes,
            NodesById = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal),
            NodesByName = nodes.ToDictionary(node => node.Name, StringComparer.Ordinal)
        };
        var pose = new DazPose.Core.DazPose { FilePath = "synthetic.duf", AssetId = "", Channels = [] };
        var evaluation = DazTransformEvaluator.Evaluate(figure, pose);
        Assert.Equal(2, evaluation.BonesById["inherited"].EvaluatedGlobalScale.X);
        Assert.Equal(1, evaluation.BonesById["compensated"].EvaluatedGlobalScale.X);
    }

    [LocalDazFixtureFact]
    public void ConversionWritesCanonicalJsonBvhAndReport()
    {
        var output = FixtureData.NewTempDirectory();
        try
        {
            var result = PoseConversionService.Convert(FixtureData.FigurePath, FixtureData.PosePath, output);
            Assert.True(File.Exists(result.JsonPath));
            Assert.True(File.Exists(result.BvhPath));
            Assert.True(File.Exists(result.ReportPath));
            Assert.EndsWith("Cherish Genesis 8 Female 16.dazpose.json", result.JsonPath, StringComparison.Ordinal);
            Assert.EndsWith("Cherish Genesis 8 Female 16.bvh", result.BvhPath, StringComparison.Ordinal);

            using var json = JsonDocument.Parse(File.ReadAllText(result.JsonPath));
            Assert.Equal("DazPoseTool", json.RootElement.GetProperty("format").GetString());
            Assert.Equal(170, json.RootElement.GetProperty("bones").GetArrayLength());
            var hip = json.RootElement.GetProperty("bones").EnumerateArray().Single(bone => bone.GetProperty("id").GetString() == "hip");
            Assert.Equal(3, hip.GetProperty("restWorldPositionCm").GetArrayLength());
            Assert.Equal(4, hip.GetProperty("restWorldRotation").GetArrayLength());
            var bvh = File.ReadAllText(result.BvhPath);
            Assert.Contains("HIERARCHY", bvh);
            Assert.Contains("ROOT hip", bvh);
            Assert.Contains("MOTION", bvh);
            Assert.Contains("Frames: 1", bvh);
            var hipOffsetLine = bvh.Split('\n').First(line => line.StartsWith("  OFFSET ", StringComparison.Ordinal));
            var expectedHipOffset = DazPoseUnits.CentimetersToMeters(
                result.Figure.NodesById["hip"].CenterPoint - result.Figure.NodesById[result.Figure.NodesById["hip"].ParentId!].CenterPoint);
            var outputOffset = hipOffsetLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)
                .Select(value => float.Parse(value, CultureInfo.InvariantCulture)).ToArray();
            Assert.Equal(expectedHipOffset.X, outputOffset[0], 5);
            Assert.Equal(expectedHipOffset.Y, outputOffset[1], 5);
            Assert.Equal(expectedHipOffset.Z, outputOffset[2], 5);
            Assert.Contains(PoseConversionService.BvhWarning, File.ReadAllText(result.ReportPath));
            Assert.Equal(97, result.Pose.ResolvedSkeletalTargetCount);
            Assert.Equal(74, result.Pose.FigureControls.Count);
            Assert.Empty(result.Pose.ActiveFigureControls);
            Assert.Empty(result.Pose.NeutralUnsupportedChannels);
        }
        finally { FixtureData.DeleteTempDirectory(output); }
    }

    private static void AssertQuaternionEquivalent(Quaternion expected, Quaternion actual)
        => Assert.InRange(MathF.Max(0, 1 - MathF.Abs(Quaternion.Dot(Quaternion.Normalize(expected), Quaternion.Normalize(actual)))), 0, 1e-5f);
}
