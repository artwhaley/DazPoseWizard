using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Linq;
using DazPose.App.Models;

namespace DazPose.App.Services;

public sealed class ConversionRegistryService
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly UnityProjectService _projectService;

    public ConversionRegistryService(UnityProjectService projectService) => _projectService = projectService;

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public async Task WriteStatusAsync(AppSettings settings, BrowserJobStatus status, CancellationToken cancellationToken = default)
    {
        var directory = GetStatusDirectory(settings);
        Directory.CreateDirectory(directory);
        var path = GetStatusPath(settings, status.CanonicalImportPath);
        var stagedPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(stagedPath, JsonSerializer.Serialize(status, JsonOptions), new UTF8Encoding(false), cancellationToken);
            File.Move(stagedPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(stagedPath)) File.Delete(stagedPath);
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<ConversionOutput>> Reconcile(AppSettings settings)
    {
        if (!_projectService.LooksLikeUnityProject(settings.UnityProjectRoot))
            return new Dictionary<string, IReadOnlyList<ConversionOutput>>(StringComparer.OrdinalIgnoreCase);

        var projectRoot = Path.GetFullPath(settings.UnityProjectRoot);
        var statuses = ReadStatuses(settings);
        var outputs = new Dictionary<string, List<ConversionOutput>>(StringComparer.OrdinalIgnoreCase);
        var seenCanonicalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var enumerationOptions = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var kind in Enum.GetValues<PerformerAssetKind>())
        {
            var importAssetRoot = UnityProjectService.ImportRoot(settings, kind);
            var outputRoot = UnityProjectService.OutputRoot(settings, kind);
            var importRoot = _projectService.ResolveAssetPath(projectRoot, importAssetRoot);
            if (!Directory.Exists(importRoot)) continue;
            foreach (var canonicalPath in Directory.EnumerateFiles(importRoot, "*.dazpose.json", enumerationOptions))
            {
                try
                {
                    var relativeCanonical = Path.GetRelativePath(projectRoot, canonicalPath).Replace('\\', '/');
                    seenCanonicalPaths.Add(relativeCanonical);
                    var relativeImportPath = Path.GetRelativePath(importRoot, canonicalPath).Replace('\\', '/');
                    var sourcePath = ReadSourcePosePath(canonicalPath);
                    if (string.IsNullOrWhiteSpace(sourcePath)) continue;
                    var destinationFolder = Path.GetDirectoryName(relativeImportPath)?.Replace('\\', '/') ?? string.Empty;
                    var outputName = GetAnimName(canonicalPath);
                    statuses.TryGetValue(relativeCanonical, out var status);
                    if (status is not null && status.SchemaVersion >= 2 && status.AssetKind != kind) continue;
                    var outputAssetPath = !string.IsNullOrWhiteSpace(status?.ExpectedAnimPath)
                        ? _projectService.NormalizeAssetRelativePath(status.ExpectedAnimPath)
                        : UnityProjectService.JoinAssetPath(
                            UnityProjectService.JoinAssetPath(outputRoot, UnityProjectService.NormalizeDestinationRelativeFolder(destinationFolder)),
                            outputName);
                    var wrapperAssetPath = ResolveExpectedWrapperPath(outputAssetPath, status);
                    AddOutput(outputs, kind, relativeCanonical, sourcePath, outputAssetPath, wrapperAssetPath,
                        destinationFolder, status, projectRoot);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException)
                {
                    // A malformed canonical file or stale status cannot stop the rest of the registry refresh.
                }
            }
        }

        foreach (var status in statuses.Values)
        {
            try
            {
                var relativeCanonical = _projectService.NormalizeAssetRelativePath(status.CanonicalImportPath);
                if (seenCanonicalPaths.Contains(relativeCanonical)) continue;
                var canonicalDiskPath = _projectService.ResolveAssetPath(projectRoot, relativeCanonical);
                if (File.Exists(canonicalDiskPath)) continue;
                if (string.IsNullOrWhiteSpace(status.SourcePosePath)) continue;
                var kind = status.SchemaVersion >= 2 ? status.AssetKind : PerformerAssetKind.Pose;
                var expectedAssetPath = !string.IsNullOrWhiteSpace(status.ExpectedAnimPath)
                    ? _projectService.NormalizeAssetRelativePath(status.ExpectedAnimPath)
                    : UnityProjectService.JoinAssetPath(
                        UnityProjectService.JoinAssetPath(UnityProjectService.OutputRoot(settings, kind),
                            UnityProjectService.NormalizeDestinationRelativeFolder(status.DestinationRelativeFolder)), GetAnimName(relativeCanonical));
                var expectedWrapperPath = ResolveExpectedWrapperPath(expectedAssetPath, status);
                AddOutput(outputs, kind, relativeCanonical, status.SourcePosePath, expectedAssetPath, expectedWrapperPath,
                    status.DestinationRelativeFolder, status, projectRoot);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException) { }
        }

        return outputs.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyList<ConversionOutput>)pair.Value.OrderBy(item => item.DestinationRelativeFolder, StringComparer.OrdinalIgnoreCase).ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    public string GetStatusDirectory(AppSettings settings) => Path.Combine(Path.GetFullPath(settings.UnityProjectRoot), ".dazposewizard", "status");

    public string GetStatusPath(AppSettings settings, string canonicalImportPath)
    {
        var stablePath = canonicalImportPath.Replace('\\', '/').ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stablePath))).ToLowerInvariant();
        return Path.Combine(GetStatusDirectory(settings), hash + ".json");
    }

    private Dictionary<string, BrowserJobStatus> ReadStatuses(AppSettings settings)
    {
        var statuses = new Dictionary<string, BrowserJobStatus>(StringComparer.OrdinalIgnoreCase);
        var directory = GetStatusDirectory(settings);
        if (!Directory.Exists(directory)) return statuses;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var status = JsonSerializer.Deserialize<BrowserJobStatus>(File.ReadAllText(path), JsonOptions);
                if (status is not null && !string.IsNullOrWhiteSpace(status.CanonicalImportPath))
                    statuses[status.CanonicalImportPath.Replace('\\', '/')] = status;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        }
        return statuses;
    }

    private static ConversionJobState DetermineState(bool outputUsable, BrowserJobStatus? status)
    {
        if (outputUsable) return ConversionJobState.Converted;
        if (status is null) return ConversionJobState.AwaitingUnity;
        if (string.Equals(status.State, "Failed", StringComparison.OrdinalIgnoreCase)) return ConversionJobState.Failed;
        if (string.Equals(status.State, "Processing", StringComparison.OrdinalIgnoreCase)) return ConversionJobState.Converting;
        return ConversionJobState.AwaitingUnity;
    }

    private void AddOutput(Dictionary<string, List<ConversionOutput>> outputs, PerformerAssetKind kind, string relativeCanonical, string sourcePath,
        string outputAssetPath, string wrapperAssetPath, string destinationFolder,
        BrowserJobStatus? status, string projectRoot)
    {
        var safeOutputAssetPath = _projectService.NormalizeAssetRelativePath(outputAssetPath);
        var outputDiskPath = _projectService.ResolveAssetPath(projectRoot, safeOutputAssetPath);
        var safeWrapperPath = _projectService.NormalizeAssetRelativePath(wrapperAssetPath);
        var wrapperDiskPath = _projectService.ResolveAssetPath(projectRoot, safeWrapperPath);
        var outputUsable = PerformerPoseAssetInspector.IsUsable(projectRoot, outputDiskPath, wrapperDiskPath, kind);
        var state = DetermineState(outputUsable, status);
        var output = new ConversionOutput(kind, relativeCanonical, outputDiskPath, wrapperDiskPath,
            destinationFolder, state, status?.ErrorMessage, status?.Timestamp);
        var normalizedSource = Path.GetFullPath(sourcePath);
        if (!outputs.TryGetValue(normalizedSource, out var sourceOutputs)) outputs[normalizedSource] = sourceOutputs = [];
        sourceOutputs.RemoveAll(item => string.Equals(item.CanonicalImportPath, relativeCanonical, StringComparison.OrdinalIgnoreCase));
        sourceOutputs.Add(output);
    }

    private string ResolveExpectedWrapperPath(string expectedAnimPath, BrowserJobStatus? status)
    {
        if (!string.IsNullOrWhiteSpace(status?.ExpectedWrapperAssetPath))
            return _projectService.NormalizeAssetRelativePath(status.ExpectedWrapperAssetPath);
        if (!string.IsNullOrWhiteSpace(status?.ExpectedPerformerPosePath))
            return _projectService.NormalizeAssetRelativePath(status.ExpectedPerformerPosePath);
        return Path.ChangeExtension(_projectService.NormalizeAssetRelativePath(expectedAnimPath), ".asset")
            .Replace('\\', '/');
    }

    private static string GetAnimName(string canonicalPath)
    {
        const string extension = ".dazpose.json";
        var name = Path.GetFileName(canonicalPath);
        return name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? name[..^extension.Length] + ".anim"
            : name + ".anim";
    }

    private static string? ReadSourcePosePath(string canonicalPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(canonicalPath));
        if (document.RootElement.TryGetProperty("source", out var source)
            && source.TryGetProperty("poseFile", out var poseFile)
            && poseFile.ValueKind == JsonValueKind.String)
            return poseFile.GetString();
        return null;
    }
}

/// <summary>Checks the Unity-authored performer wrapper YAML without requiring Unity to be open.</summary>
public static class PerformerPoseAssetInspector
{
    private static readonly Regex MetaGuidPattern = new(@"(?m)^guid:\s*([0-9a-f]{32})\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsUsable(string projectRoot, string animationClipPath, string performerPoseAssetPath,
        PerformerAssetKind kind = PerformerAssetKind.Pose)
    {
        try
        {
            if (!File.Exists(animationClipPath) || !File.Exists(performerPoseAssetPath)) return false;
            var clipGuid = ReadMetaGuid(animationClipPath + ".meta");
            var poseGuid = ReadMetaGuid(performerPoseAssetPath + ".meta");
            if (clipGuid is null || poseGuid is null) return false;

            var yaml = File.ReadAllText(performerPoseAssetPath);
            if (!HasReference(yaml, "clip", clipGuid)) return false;

            var scriptName = kind == PerformerAssetKind.Expression ? "PerformerExpression.cs.meta" : "PerformerPose.cs.meta";
            var scriptMeta = Path.Combine(projectRoot, "Assets", "DazPose", "Runtime", "Performer", scriptName);
            if (!File.Exists(scriptMeta)) return false;
            var scriptGuid = ReadMetaGuid(scriptMeta);
            if (scriptGuid is null || !HasReference(yaml, "m_Script", scriptGuid)) return false;

            if (kind == PerformerAssetKind.Expression)
                return IsUsableExpression(yaml, File.ReadAllText(animationClipPath));

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static string? ReadMetaGuid(string path)
    {
        if (!File.Exists(path)) return null;
        var match = MetaGuidPattern.Match(File.ReadAllText(path));
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool HasReference(string yaml, string fieldName, string guid)
    {
        var fieldPattern = new Regex(@"(?m)^\s*" + Regex.Escape(fieldName)
            + @":\s*\{[^}\r\n]*\bguid:\s*([0-9a-f]{32})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var match = fieldPattern.Match(yaml);
        return match.Success && string.Equals(match.Groups[1].Value, guid, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUsableExpression(string wrapperYaml, string clipYaml)
    {
        var channels = ParseMorphChannels(GetYamlSection(wrapperYaml, "channels"));
        var boneChannels = ParseBoneChannels(GetYamlSection(wrapperYaml, "boneChannels"));
        if (channels is null || boneChannels is null || channels.Count == 0 && boneChannels.Count == 0) return false;

        var expectedMorphs = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var channel in channels)
        {
            var key = channel.Path + "|blendShape." + channel.Name;
            if (expectedMorphs.ContainsKey(key)) return false;
            expectedMorphs.Add(key, channel.Weight);
        }

        var expectedPositions = new Dictionary<string, float[]>(StringComparer.Ordinal);
        var expectedRotations = new Dictionary<string, float[]>(StringComparer.Ordinal);
        var boneDescriptorPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var channel in boneChannels)
        {
            if (!boneDescriptorPaths.Add(channel.Path)) return false;
            var properties = channel.Properties;
            if ((properties & 1) != 0 && !expectedPositions.TryAdd(channel.Path, channel.Position)) return false;
            if ((properties & 2) != 0 && !expectedRotations.TryAdd(channel.Path, channel.Rotation)) return false;
        }

        if (!IsEmptyCurveArray(clipYaml, "m_CompressedRotationCurves")
            || !IsEmptyCurveArray(clipYaml, "m_EulerCurves")
            || !IsEmptyCurveArray(clipYaml, "m_ScaleCurves")
            || !IsEmptyCurveArray(clipYaml, "m_PPtrCurves")) return false;

        var actualPositions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in GetCurveBlocks(GetYamlSection(clipYaml, "m_PositionCurves")))
        {
            var path = GetScalarField(block, "path");
            if (path is null || !actualPositions.Add(path) || !expectedPositions.TryGetValue(path, out var target)
                || !CurveVectorsMatch(block, target, quaternion: false)) return false;
        }
        if (!actualPositions.SetEquals(expectedPositions.Keys)) return false;

        var actualRotations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in GetCurveBlocks(GetYamlSection(clipYaml, "m_RotationCurves")))
        {
            var path = GetScalarField(block, "path");
            if (path is null || !actualRotations.Add(path) || !expectedRotations.TryGetValue(path, out var target)
                || !CurveVectorsMatch(block, target, quaternion: true)) return false;
        }
        if (!actualRotations.SetEquals(expectedRotations.Keys)) return false;

        var actualMorphs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in GetCurveBlocks(GetYamlSection(clipYaml, "m_FloatCurves")))
        {
            var attribute = GetScalarField(block, "attribute");
            var path = GetScalarField(block, "path");
            if (attribute is null || path is null || !attribute.StartsWith("blendShape.", StringComparison.Ordinal)) return false;
            var key = path + "|" + attribute;
            if (!actualMorphs.Add(key) || !expectedMorphs.TryGetValue(key, out var target)
                || !CurveScalarsMatch(block, target)) return false;
        }
        return actualMorphs.SetEquals(expectedMorphs.Keys);
    }

    private static List<MorphChannel>? ParseMorphChannels(string section)
    {
        var channels = new List<MorphChannel>();
        var entries = GetListEntries(section, "rendererPath");
        foreach (var entry in entries)
        {
            var path = GetScalarField(entry, "rendererPath");
            var name = GetScalarField(entry, "blendShapeName");
            var rawWeight = GetScalarField(entry, "targetWeight");
            if (path is null || name is null || string.IsNullOrWhiteSpace(name)
                || IsReservedExpressionMorph(name)
                || !TryFiniteFloat(rawWeight, out var weight)) return null;
            channels.Add(new MorphChannel(path, name, weight));
        }
        return channels;
    }

    private static List<BoneChannel>? ParseBoneChannels(string section)
    {
        var channels = new List<BoneChannel>();
        foreach (var entry in GetListEntries(section, "transformPath"))
        {
            var path = GetScalarField(entry, "transformPath");
            var boneId = GetScalarField(entry, "dazBoneId");
            var rawProperties = GetScalarField(entry, "properties");
            if (path is null || string.IsNullOrWhiteSpace(path) || path.Contains('[') || path.Contains(']')
                || string.IsNullOrWhiteSpace(boneId) || boneId is "head" or "lEye" or "rEye"
                || IsForbiddenGazePath(path)
                || !int.TryParse(rawProperties, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var properties)
                || properties == 0 || (properties & ~3) != 0) return null;

            var position = ParseVector(entry, "targetLocalPosition", 3);
            var rotation = ParseVector(entry, "targetLocalRotation", 4);
            if (position is null || rotation is null
                || (properties & 1) != 0 && !position.All(float.IsFinite)
                || (properties & 2) != 0 && (!rotation.All(float.IsFinite) || rotation.Sum(value => value * value) <= 1e-8f)) return null;
            if ((properties & 2) != 0)
            {
                var norm = MathF.Sqrt(rotation.Sum(value => value * value));
                for (var index = 0; index < rotation.Length; index++) rotation[index] /= norm;
            }
            channels.Add(new BoneChannel(path, boneId, properties, position, rotation));
        }
        return channels;
    }

    private static string GetYamlSection(string yaml, string fieldName)
    {
        var header = Regex.Match(yaml, @"(?m)^[ \t]*" + Regex.Escape(fieldName) + @":(?:[ \t]*\[\])?[ \t]*$",
            RegexOptions.CultureInvariant);
        if (!header.Success) return string.Empty;
        var start = header.Index + header.Length;
        var boundary = Regex.Match(yaml.Substring(start), @"(?m)^[ \t]{2}m_[A-Za-z0-9_]+:", RegexOptions.CultureInvariant);
        return boundary.Success ? yaml.Substring(start, boundary.Index) : yaml.Substring(start);
    }

    private static List<string> GetListEntries(string section, string listItemField)
    {
        var starts = Regex.Matches(section, @"(?m)^[ \t]*-[ \t]*" + Regex.Escape(listItemField) + @":",
            RegexOptions.CultureInvariant);
        var entries = new List<string>(starts.Count);
        for (var index = 0; index < starts.Count; index++)
        {
            var end = index + 1 < starts.Count ? starts[index + 1].Index : section.Length;
            entries.Add(section.Substring(starts[index].Index, end - starts[index].Index));
        }
        return entries;
    }

    private static List<string> GetCurveBlocks(string section)
    {
        var starts = Regex.Matches(section, @"(?m)^[ \t]*-[ \t]*curve:", RegexOptions.CultureInvariant);
        var blocks = new List<string>(starts.Count);
        for (var index = 0; index < starts.Count; index++)
        {
            var end = index + 1 < starts.Count ? starts[index + 1].Index : section.Length;
            blocks.Add(section.Substring(starts[index].Index, end - starts[index].Index));
        }
        return blocks;
    }

    private static string? GetScalarField(string yaml, string fieldName)
    {
        var match = Regex.Match(yaml, @"(?m)^[ \t]*(?:-[ \t]*)?" + Regex.Escape(fieldName) + @":[ \t]*([^\r\n]*)$",
            RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        var value = match.Groups[1].Value.Trim();
        if (value.Length >= 2 && (value[0] == '"' && value[^1] == '"' || value[0] == '\'' && value[^1] == '\''))
            value = value.Substring(1, value.Length - 2);
        return value;
    }

    private static float[]? ParseVector(string yaml, string fieldName, int componentCount)
    {
        var value = GetScalarField(yaml, fieldName);
        if (value is null) return null;
        var components = Regex.Matches(value, @"([xyzw]):\s*([^,}]+)", RegexOptions.CultureInvariant);
        var labels = componentCount == 3 ? "xyz" : "xyzw";
        if (components.Count != componentCount) return null;
        var result = new float[componentCount];
        for (var index = 0; index < componentCount; index++)
        {
            if (!string.Equals(components[index].Groups[1].Value, labels[index].ToString(), StringComparison.Ordinal)
                || !TryFiniteFloat(components[index].Groups[2].Value, out result[index])) return null;
        }
        return result;
    }

    private static bool CurveVectorsMatch(string block, float[] expected, bool quaternion)
    {
        var values = Regex.Matches(block,
            @"(?m)^[ \t]*value:[ \t]*\{x:\s*([^,]+),\s*y:\s*([^,]+),\s*z:\s*([^,}]+)" + (quaternion ? @",\s*w:\s*([^}]+)" : string.Empty) + @"\}",
            RegexOptions.CultureInvariant);
        if (values.Count == 0) return false;
        foreach (Match value in values)
        {
            var parsed = new float[expected.Length];
            for (var index = 0; index < expected.Length; index++)
                if (!TryFiniteFloat(value.Groups[index + 1].Value, out parsed[index])) return false;
            if (quaternion)
            {
                var norm = MathF.Sqrt(parsed.Sum(component => component * component));
                if (norm <= 1e-8f) return false;
                var dot = Math.Abs(parsed.Select((component, index) => component * expected[index] / norm).Sum());
                if (Math.Abs(1f - dot) > 0.002f) return false;
            }
            else if (parsed.Where((component, index) => Math.Abs(component - expected[index]) > 0.001f).Any()) return false;
        }
        return true;
    }

    private static bool CurveScalarsMatch(string block, float expected)
    {
        var values = Regex.Matches(block, @"(?m)^[ \t]*value:[ \t]*([^\r\n]+)$", RegexOptions.CultureInvariant);
        return values.Count > 0 && values.Cast<Match>().All(value =>
            TryFiniteFloat(value.Groups[1].Value, out var parsed) && Math.Abs(parsed - expected) <= 0.001f);
    }

    private static bool IsEmptyCurveArray(string yaml, string fieldName)
        => Regex.IsMatch(yaml, @"(?m)^[ \t]*" + Regex.Escape(fieldName) + @":[ \t]*\[\][ \t]*$",
            RegexOptions.CultureInvariant);

    private static bool TryFiniteFloat(string? value, out float result)
        => float.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out result) && float.IsFinite(result);

    private static bool IsReservedExpressionMorph(string name)
        => name is "Breathe" or "EX_Breathe" or "Genesis8Female__EX_Breathe"
            or "BreatheBelly" or "EX_BreatheBelly" or "Genesis8Female__EX_BreatheBelly"
            or "eCTRLEyesClosedL" or "eCTRLEyesClosedR"
            or "Genesis8Female__eCTRLEyesClosedL" or "Genesis8Female__eCTRLEyesClosedR";

    private static bool IsForbiddenGazePath(string path)
    {
        var separator = path.LastIndexOf('/');
        var finalSegment = separator >= 0 ? path.Substring(separator + 1) : path;
        return finalSegment is "head" or "lEye" or "rEye";
    }

    private sealed record MorphChannel(string Path, string Name, float Weight);
    private sealed record BoneChannel(string Path, string BoneId, int Properties, float[] Position, float[] Rotation);
}

public sealed record ResolvedPoseOutput(PerformerAssetKind Kind, string BaseName, string CanonicalPath, string CanonicalAssetPath,
    string AnimPath, string AnimAssetPath, string WrapperPath, string WrapperAssetPath,
    string DestinationRelativeFolder);

public sealed class PoseOutputNamingService(UnityProjectService projectService)
{
    private readonly object _reservationLock = new();
    private readonly Dictionary<string, string> _reservations = new(StringComparer.OrdinalIgnoreCase);

    public ResolvedPoseOutput Resolve(AppSettings settings, string sourcePosePath, PerformerAssetKind kind, string destinationRelativeFolder)
    {
        var source = Path.GetFullPath(sourcePosePath);
        var destination = UnityProjectService.NormalizeDestinationRelativeFolder(destinationRelativeFolder);
        projectService.ValidateConfiguredRoots(settings);
        var sourceFolder = Path.GetFileName(Path.GetDirectoryName(source) ?? string.Empty);
        var sourceStem = Path.GetFileNameWithoutExtension(source);
        var plainBase = SanitizeFileStem($"{sourceFolder} - {sourceStem}");
        var importFolderRelative = UnityProjectService.JoinAssetPath(UnityProjectService.ImportRoot(settings, kind), destination);
        var finalFolderRelative = UnityProjectService.JoinAssetPath(UnityProjectService.OutputRoot(settings, kind), destination);
        var importFolder = projectService.ResolveAssetPath(settings.UnityProjectRoot, importFolderRelative);
        var finalFolder = projectService.ResolveAssetPath(settings.UnityProjectRoot, finalFolderRelative);
        Directory.CreateDirectory(importFolder);
        Directory.CreateDirectory(finalFolder);

        lock (_reservationLock)
        {
            var selectedBase = plainBase;
            if (IsCollision(selectedBase, source, importFolder, finalFolder))
            {
                var suffix = SourceHash(source)[..6];
                selectedBase = $"{plainBase} [{suffix}]";
                if (IsCollision(selectedBase, source, importFolder, finalFolder))
                    selectedBase = $"{plainBase} [{SourceHash(source)[..10]}]";
                if (IsCollision(selectedBase, source, importFolder, finalFolder))
                    throw new IOException($"A stable filename collision remains for '{source}'. No existing Unity or canonical asset was overwritten.");
            }

            var canonicalAssetPath = UnityProjectService.JoinAssetPath(importFolderRelative, selectedBase + ".dazpose.json");
            var animAssetPath = UnityProjectService.JoinAssetPath(finalFolderRelative, selectedBase + ".anim");
            var wrapperAssetPath = UnityProjectService.JoinAssetPath(finalFolderRelative, selectedBase + ".asset");
            var resolved = new ResolvedPoseOutput(kind, selectedBase,
                Path.Combine(importFolder, selectedBase + ".dazpose.json"), canonicalAssetPath,
                Path.Combine(finalFolder, selectedBase + ".anim"), animAssetPath,
                Path.Combine(finalFolder, selectedBase + ".asset"), wrapperAssetPath, destination);
            _reservations[Path.GetFullPath(resolved.CanonicalPath)] = source;
            return resolved;
        }
    }

    public ResolvedPoseOutput Resolve(AppSettings settings, string sourcePosePath, string destinationRelativeFolder) =>
        Resolve(settings, sourcePosePath, PerformerAssetKind.Pose, destinationRelativeFolder);

    public void Release(ResolvedPoseOutput output)
    {
        lock (_reservationLock) _reservations.Remove(Path.GetFullPath(output.CanonicalPath));
    }

    public static string SanitizeFileStem(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*").ToHashSet();
        var safe = new string(value.Select(character => invalid.Contains(character) || char.IsControl(character) ? '_' : character).ToArray())
            .Trim().TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(safe)) safe = "Pose";
        var deviceName = safe.Split('.')[0];
        if (new[] { "CON", "PRN", "AUX", "NUL" }.Contains(deviceName, StringComparer.OrdinalIgnoreCase)
            || System.Text.RegularExpressions.Regex.IsMatch(deviceName, "^(COM|LPT)[1-9]$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            safe = $"_{safe}";
        return safe;
    }

    private bool IsCollision(string baseName, string source, string importFolder, string finalFolder)
    {
        var canonical = Path.Combine(importFolder, baseName + ".dazpose.json");
        if (_reservations.TryGetValue(Path.GetFullPath(canonical), out var reservedSource) && !SamePath(reservedSource, source)) return true;
        if (File.Exists(canonical))
        {
            var existingSource = ReadSourcePosePath(canonical);
            if (!SamePath(existingSource, source)) return true;
        }
        var anim = Path.Combine(finalFolder, baseName + ".anim");
        return File.Exists(anim) && !File.Exists(canonical);
    }

    private static string? ReadSourcePosePath(string canonicalPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(canonicalPath));
            if (document.RootElement.TryGetProperty("source", out var source)
                && source.TryGetProperty("poseFile", out var poseFile)
                && poseFile.ValueKind == JsonValueKind.String)
                return poseFile.GetString();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return null;
    }

    private static bool SamePath(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left)) return false;
        try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException) { return false; }
    }

    private static string SourceHash(string source)
    {
        var identity = source.Trim().ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }
}
