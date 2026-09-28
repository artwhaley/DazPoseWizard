using DazPose.Core;
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
        var pose = new DazPose.Core.DazPose { FilePath = "synthetic.duf", AssetId = "", Channels = [] };
        var evaluation = DazTransformEvaluator.Evaluate(figure, pose);
        var orientation = DazTransformEvaluator.EulerToQuaternion(bone.Orientation, "XYZ");
        var rotation = DazTransformEvaluator.EulerToQuaternion(bone.Rotation, bone.RotationOrder);
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
            var bvh = File.ReadAllText(result.BvhPath);
            Assert.Contains("HIERARCHY", bvh);
            Assert.Contains("ROOT hip", bvh);
            Assert.Contains("MOTION", bvh);
            Assert.Contains("Frames: 1", bvh);
            Assert.Contains(PoseConversionService.BvhWarning, File.ReadAllText(result.ReportPath));
            Assert.Equal(97, result.Pose.ResolvedSkeletalTargetCount);
            Assert.Equal(74, result.Pose.NeutralUnsupportedChannels.Count);
        }
        finally { FixtureData.DeleteTempDirectory(output); }
    }

    private static void AssertQuaternionEquivalent(Quaternion expected, Quaternion actual)
        => Assert.InRange(MathF.Max(0, 1 - MathF.Abs(Quaternion.Dot(Quaternion.Normalize(expected), Quaternion.Normalize(actual)))), 0, 1e-5f);
}
