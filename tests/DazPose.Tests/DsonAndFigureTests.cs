using DazPose.Core;
using System.Text.Json;

namespace DazPose.Tests;

public sealed class DsonAndFigureTests
{
    [LocalDazFixtureFact]
    public void DsonReaderLoadsPlainFigureAndGzipPose()
    {
        using var figure = DsonFileReader.ReadJson(FixtureData.FigurePath);
        using var pose = DsonFileReader.ReadJson(FixtureData.PosePath);
        Assert.Equal("figure", figure.RootElement.GetProperty("asset_info").GetProperty("type").GetString());
        Assert.Equal("preset_pose", pose.RootElement.GetProperty("asset_info").GetProperty("type").GetString());
    }

    [LocalDazFixtureFact]
    public void G8FigureCountsAndRepresentativeMetadataMatchGroundTruth()
    {
        var figure = FixtureData.LoadFigure();
        Assert.Equal(171, figure.Nodes.Count);
        Assert.Equal(170, figure.Bones.Count);
        Assert.Single(figure.FigureRoots);
        Assert.Equal("Genesis 8 Female", figure.FigureLabel);

        var thigh = figure.NodesByName["lThighBend"];
        Assert.Equal("lThigh", thigh.Id);
        Assert.Equal("pelvis", thigh.ParentId);
        Assert.Equal("YZX", thigh.RotationOrder);
        AssertVector(new(1.163925f, 4.855467f, 7.945159f), thigh.Orientation);

        var forearm = figure.NodesByName["lForearmBend"];
        Assert.Equal("lForeArm", forearm.Id);
        Assert.Equal("lShldrTwist", forearm.ParentId);
        Assert.Equal("XZY", forearm.RotationOrder);
        AssertVector(new(1.176365f, -16.39288f, -44.76654f), forearm.Orientation);

        var head = figure.NodesById["head"];
        Assert.Equal("neck_2", head.ParentId);
        Assert.Equal("YZX", head.RotationOrder);
        AssertVector(new(0.6970934f, 0, 0), head.Orientation);
    }

    [LocalDazFixtureFact]
    public void FigureHierarchyParentsAllResolveAndParserRejectsCyclesByConstruction()
    {
        var figure = FixtureData.LoadFigure();
        foreach (var bone in figure.Bones)
            Assert.Contains(bone.ParentId!, figure.NodesById.Keys);
        Assert.Equal(figure.Nodes.Count, figure.NodesById.Values.Select(node => node.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void InvalidJsonErrorIncludesSourceFileName()
    {
        var directory = FixtureData.NewTempDirectory();
        try
        {
            var path = Path.Combine(directory, "bad-fixture.dsf");
            File.WriteAllText(path, "{bad json");
            var error = Assert.Throws<DazConversionException>(() => DsonFileReader.ReadJson(path));
            Assert.Contains("bad-fixture.dsf", error.Message);
        }
        finally { FixtureData.DeleteTempDirectory(directory); }
    }

    private static void AssertVector(System.Numerics.Vector3 expected, System.Numerics.Vector3 actual)
    {
        Assert.InRange(MathF.Abs(expected.X - actual.X), 0, 1e-5f);
        Assert.InRange(MathF.Abs(expected.Y - actual.Y), 0, 1e-5f);
        Assert.InRange(MathF.Abs(expected.Z - actual.Z), 0, 1e-5f);
    }
}
