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

        var evaluated = Evaluate(figurePath, posePath);
        var figure = evaluated.Figure;
        var pose = evaluated.Pose;
        var evaluation = evaluated.Evaluation;
        var restEvaluation = evaluated.RestEvaluation;

        var outputBase = Path.Combine(Path.GetFullPath(outputDirectory), pose.PoseName);
        var jsonPath = outputBase + ".dazpose.json";
        var bvhPath = outputBase + ".bvh";
        var reportPath = outputBase + ".report.txt";
        var warnings = new List<string> { BvhWarning };
        warnings.AddRange(pose.Diagnostics);
        DazPoseExporter.WriteJson(jsonPath, figure, pose, evaluation, restEvaluation, warnings);
        DazPoseExporter.WriteBvh(bvhPath, figure, evaluation);
        File.WriteAllText(reportPath, BuildReport(figure, pose, jsonPath, bvhPath, reportPath, warnings), new UTF8Encoding(false));

        var diagnostics = BuildDiagnostics(pose, includeBvhWarning: true);
        return new ConversionResult
        {
            Figure = figure, Pose = pose, Evaluation = evaluation, RestEvaluation = restEvaluation, JsonPath = jsonPath, BvhPath = bvhPath,
            ReportPath = reportPath, Diagnostics = diagnostics
        };
    }

    /// <summary>
    /// Parses, validates, and evaluates a pose using the same pipeline as <see cref="Convert"/>,
    /// then writes only the canonical pose JSON. Callers should write to a staging path and
    /// publish the finished file atomically into Unity's Assets tree.
    /// </summary>
    public static CanonicalConversionResult ConvertCanonical(string figurePath, string posePath, string canonicalOutputPath)
    {
        if (!File.Exists(figurePath)) throw new DazConversionException($"Figure file does not exist: '{figurePath}'.");
        if (!File.Exists(posePath)) throw new DazConversionException($"Pose file does not exist: '{posePath}'.");
        if (string.IsNullOrWhiteSpace(canonicalOutputPath)) throw new ArgumentException("A canonical output path is required.", nameof(canonicalOutputPath));

        var evaluated = Evaluate(figurePath, posePath);
        var fullOutputPath = Path.GetFullPath(canonicalOutputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath)
            ?? throw new DazConversionException($"Canonical output path has no parent directory: '{canonicalOutputPath}'.");
        Directory.CreateDirectory(outputDirectory);
        DazPoseExporter.WriteJson(fullOutputPath, evaluated.Figure, evaluated.Pose, evaluated.Evaluation,
            evaluated.RestEvaluation, evaluated.Pose.Diagnostics);

        return new CanonicalConversionResult
        {
            Figure = evaluated.Figure,
            Pose = evaluated.Pose,
            Evaluation = evaluated.Evaluation,
            RestEvaluation = evaluated.RestEvaluation,
            JsonPath = fullOutputPath,
            Diagnostics = BuildDiagnostics(evaluated.Pose, includeBvhWarning: false)
        };
    }

    private static EvaluatedConversion Evaluate(string figurePath, string posePath)
    {
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
        return new EvaluatedConversion(figure, pose, evaluation, restEvaluation);
    }

    private static IReadOnlyList<ConversionDiagnostic> BuildDiagnostics(DazPose pose, bool includeBvhWarning)
    {
        var diagnostics = new List<ConversionDiagnostic>
        {
            new("Info", $"Ignored {pose.NeutralUnsupportedChannels.Count} inactive unsupported controls."),
            new("Info", $"Parsed {pose.ActiveFigureControls.Count} active DAZ figure control(s); direct-morph confirmation is pending a Unity reference import.")
        };
        diagnostics.AddRange(pose.Diagnostics.Select(message => new ConversionDiagnostic("Warning", message)));
        if (includeBvhWarning) diagnostics.Add(new ConversionDiagnostic("Warning", BvhWarning));
        return diagnostics;
    }

    private sealed record EvaluatedConversion(DazFigureDefinition Figure, DazPose Pose,
        DazPoseEvaluation Evaluation, DazPoseEvaluation RestEvaluation);

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
            var value = activeUnsupported.Keys.First(key => !activeUnsupported.IsNeutralValue(key.Value)).Value;
            var channelKind = activeUnsupported.ParsedUrl.IsSelectedFigureRoot
                ? "figure-root property"
                : activeUnsupported.ParsedUrl.IsFigureControlAddress ? "figure/control property" : null;
            var channelContext = channelKind is null ? string.Empty : $" ({channelKind})";
            var neutralScaleNote = activeUnsupported.ParsedUrl.Property == "scale" ? " Neutral scale is 1." : string.Empty;
            throw new DazConversionException(
                $"Unsupported non-neutral channel '{activeUnsupported.Url}'{channelContext} has value {value.ToString("G9", CultureInfo.InvariantCulture)}.{neutralScaleNote} Conversion stopped to avoid dropping authored pose data.");
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
            $"Active DAZ figure control count: {pose.ActiveFigureControls.Count}",
            "Active DAZ figure controls are manifest candidates until a Unity reference confirms a direct blendshape.",
            "Canonical format: .dazpose.json (DAZ centimeter coordinates; includes evaluated rest and pose world transforms).",
            "BVH format: approximate interoperability export (linear values converted from centimeters to meters; Genesis joint orientation may be lost).",
            "Inactive unsupported controls:",
            ..unsupportedNames.Select(name => $"  {name}"),
            "Warnings:",
            ..warnings.Select(warning => $"  {warning}"),
            $"Output JSON: {jsonPath}",
            $"Output BVH: {bvhPath}",
            $"Output report: {reportPath}"
        ]);
    }
}
