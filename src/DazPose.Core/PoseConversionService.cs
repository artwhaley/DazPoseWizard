using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DazPose.Core;

public static class PoseConversionService
{
    public const string BvhWarning = "BVH is an interoperability preview and may lose Genesis joint-orientation/bone-roll information.";

    public static ConversionResult Convert(string figurePath, string posePath, string outputDirectory)
    {
        if (!File.Exists(figurePath)) throw new DazConversionException($"Figure file does not exist: '{figurePath}'.");
        if (!File.Exists(posePath)) throw new DazConversionException($"Pose file does not exist: '{posePath}'.");
        if (!Directory.Exists(outputDirectory)) throw new DazConversionException($"Output folder does not exist: '{outputDirectory}'.");

        using var figureDocument = DsonFileReader.ReadJson(figurePath);
        using var poseDocument = DsonFileReader.ReadJson(posePath);
        var figure = DazFigureParser.Parse(figurePath, figureDocument);
        var pose = DazPoseParser.Parse(posePath, poseDocument, figure);
        ValidatePose(pose);
        var evaluation = DazTransformEvaluator.Evaluate(figure, pose);
        var restEvaluation = DazTransformEvaluator.Evaluate(figure, new DazPose
        {
            FilePath = pose.FilePath,
            AssetId = pose.AssetId,
            Channels = Array.Empty<DazPoseChannel>()
        });

        var outputBase = Path.Combine(Path.GetFullPath(outputDirectory), pose.PoseName);
        var jsonPath = outputBase + ".dazpose.json";
        var bvhPath = outputBase + ".bvh";
        var reportPath = outputBase + ".report.txt";
        var warnings = new List<string> { BvhWarning };
        warnings.AddRange(pose.Diagnostics);
        DazPoseExporter.WriteJson(jsonPath, figure, pose, evaluation, restEvaluation, warnings);
        DazPoseExporter.WriteBvh(bvhPath, figure, evaluation);
        File.WriteAllText(reportPath, BuildReport(figure, pose, jsonPath, bvhPath, reportPath, warnings), new UTF8Encoding(false));

        var diagnostics = new List<ConversionDiagnostic>
        {
            new("Info", $"Ignored {pose.NeutralUnsupportedChannels.Count} neutral unsupported channels.")
        };
        diagnostics.AddRange(pose.Diagnostics.Select(message => new ConversionDiagnostic("Warning", message)));
        diagnostics.Add(new ConversionDiagnostic("Warning", BvhWarning));
        return new ConversionResult
        {
            Figure = figure, Pose = pose, Evaluation = evaluation, RestEvaluation = restEvaluation, JsonPath = jsonPath, BvhPath = bvhPath,
            ReportPath = reportPath, Diagnostics = diagnostics
        };
    }

    public static void ValidatePose(DazPose pose)
    {
        var skeletal = pose.Channels.Where(channel => channel.IsSupportedSkeletalChannel).ToArray();
        if (skeletal.Any(channel => channel.Keys.Count != 1))
            throw new DazConversionException("This preset contains animation data. V0 supports static poses only.");
        if (skeletal.Select(channel => channel.Keys[0].Time).Distinct().Skip(1).Any())
            throw new DazConversionException("This preset contains animation data. V0 supports static poses only.");

        var activeUnsupported = pose.NonNeutralUnsupportedChannels.FirstOrDefault();
        if (activeUnsupported is not null)
        {
            var value = activeUnsupported.Keys.First(key => Math.Abs(key.Value) > 1e-7f).Value;
            var channelKind = activeUnsupported.ParsedUrl.IsSelectedFigureRoot
                ? "figure-root property"
                : activeUnsupported.ParsedUrl.IsFigureControlAddress ? "figure/control property" : null;
            var channelContext = channelKind is null ? string.Empty : $" ({channelKind})";
            throw new DazConversionException(
                $"Unsupported non-neutral channel '{activeUnsupported.Url}'{channelContext} has value {value.ToString("G9", CultureInfo.InvariantCulture)}. Conversion stopped to avoid dropping authored pose data.");
        }
    }

    private static string BuildReport(DazFigureDefinition figure, DazPose pose, string jsonPath, string bvhPath,
        string reportPath, IReadOnlyList<string> warnings)
    {
        var unsupportedNames = pose.NeutralUnsupportedChannels.Select(channel => channel.Url).OrderBy(url => url, StringComparer.Ordinal);
        return string.Join(Environment.NewLine,
        [
            "DazPoseTool conversion report",
            $"Conversion timestamp: {DateTimeOffset.Now:O}",
            $"Figure input: {figure.FilePath}",
            $"Pose input: {pose.FilePath}",
            $"Figure asset ID: {figure.AssetId}",
            $"Pose asset ID: {pose.AssetId}",
            $"Figure label: {figure.FigureLabel}",
            $"Figure node count: {figure.Nodes.Count}",
            $"Bone count: {figure.Bones.Count}",
            $"Pose channel count: {pose.Channels.Count}",
            $"Target node count: {pose.SkeletalTargetCount}",
            $"Resolved skeletal target count: {pose.ResolvedSkeletalTargetCount}",
            "Unresolved skeletal target count: 0",
            $"Unsupported neutral channel count: {pose.NeutralUnsupportedChannels.Count}",
            $"Unsupported non-neutral channel count: {pose.NonNeutralUnsupportedChannels.Count}",
            "Canonical format: .dazpose.json (DAZ centimeter coordinates; includes evaluated rest and pose world transforms).",
            "BVH format: approximate interoperability export (linear values converted from centimeters to meters; Genesis joint orientation may be lost).",
            "Unsupported neutral channels:",
            ..unsupportedNames.Select(name => $"  {name}"),
            "Warnings:",
            ..warnings.Select(warning => $"  {warning}"),
            $"Output JSON: {jsonPath}",
            $"Output BVH: {bvhPath}",
            $"Output report: {reportPath}"
        ]);
    }
}
