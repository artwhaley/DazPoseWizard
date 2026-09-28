using System.Text.Json;
using DazPose.Core;

namespace DazPose.Tests;

internal static class FixtureData
{
    public static string FigurePath => Path.Combine(AppContext.BaseDirectory, "fixtures", "private", "Genesis8Female.dsf");
    public static string PosePath => Path.Combine(AppContext.BaseDirectory, "fixtures", "private", "Cherish Genesis 8 Female 16.duf");

    public static DazFigureDefinition LoadFigure()
    {
        using var json = DsonFileReader.ReadJson(FigurePath);
        return DazFigureParser.Parse(FigurePath, json);
    }

    public static DazPose.Core.DazPose LoadPose(DazFigureDefinition? figure = null)
    {
        using var json = DsonFileReader.ReadJson(PosePath);
        return DazPoseParser.Parse(PosePath, json, figure ?? LoadFigure());
    }

    public static string NewTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"DazPoseTool.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    public static void DeleteTempDirectory(string path)
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(fullPath).StartsWith("DazPoseTool.Tests.", StringComparison.Ordinal))
            throw new InvalidOperationException($"Refusing to delete a path outside the test temp directory: {fullPath}");
        if (Directory.Exists(fullPath)) Directory.Delete(fullPath, recursive: true);
    }
}
