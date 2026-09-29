using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DazPose.App.Models;

namespace DazPose.App.Services;

public sealed class ConversionRegistryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private readonly UnityProjectService _projectService;

    public ConversionRegistryService(UnityProjectService projectService) => _projectService = projectService;

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
        var importRoot = _projectService.ResolveAssetPath(projectRoot, settings.CanonicalImportRoot);
        var statuses = ReadStatuses(settings);
        var outputs = new Dictionary<string, List<ConversionOutput>>(StringComparer.OrdinalIgnoreCase);
        var seenCanonicalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var enumerationOptions = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        if (Directory.Exists(importRoot))
        {
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
                    var outputAssetPath = !string.IsNullOrWhiteSpace(status?.ExpectedAnimPath)
                        ? _projectService.NormalizeAssetRelativePath(status.ExpectedAnimPath)
                        : UnityProjectService.JoinAssetPath(
                            UnityProjectService.JoinAssetPath(settings.FinalPoseAssetRoot, UnityProjectService.NormalizeDestinationRelativeFolder(destinationFolder)),
                            outputName);
                    var performerPoseAssetPath = ResolveExpectedPerformerPosePath(outputAssetPath, status);
                    AddOutput(outputs, relativeCanonical, sourcePath, outputAssetPath, performerPoseAssetPath,
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
                var expectedAssetPath = !string.IsNullOrWhiteSpace(status.ExpectedAnimPath)
                    ? _projectService.NormalizeAssetRelativePath(status.ExpectedAnimPath)
                    : UnityProjectService.JoinAssetPath(
                        UnityProjectService.JoinAssetPath(settings.FinalPoseAssetRoot,
                            UnityProjectService.NormalizeDestinationRelativeFolder(status.DestinationRelativeFolder)), GetAnimName(relativeCanonical));
                var expectedPerformerPosePath = ResolveExpectedPerformerPosePath(expectedAssetPath, status);
                AddOutput(outputs, relativeCanonical, status.SourcePosePath, expectedAssetPath, expectedPerformerPosePath,
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

    private void AddOutput(Dictionary<string, List<ConversionOutput>> outputs, string relativeCanonical, string sourcePath,
        string outputAssetPath, string performerPoseAssetPath, string destinationFolder,
        BrowserJobStatus? status, string projectRoot)
    {
        var safeOutputAssetPath = _projectService.NormalizeAssetRelativePath(outputAssetPath);
        var outputDiskPath = _projectService.ResolveAssetPath(projectRoot, safeOutputAssetPath);
        var safePerformerPosePath = _projectService.NormalizeAssetRelativePath(performerPoseAssetPath);
        var performerPoseDiskPath = _projectService.ResolveAssetPath(projectRoot, safePerformerPosePath);
        var outputUsable = PerformerPoseAssetInspector.IsUsable(projectRoot, outputDiskPath, performerPoseDiskPath);
        var state = DetermineState(outputUsable, status);
        var output = new ConversionOutput(relativeCanonical, outputDiskPath, performerPoseDiskPath,
            destinationFolder, state, status?.ErrorMessage, status?.Timestamp);
        var normalizedSource = Path.GetFullPath(sourcePath);
        if (!outputs.TryGetValue(normalizedSource, out var sourceOutputs)) outputs[normalizedSource] = sourceOutputs = [];
        sourceOutputs.RemoveAll(item => string.Equals(item.CanonicalImportPath, relativeCanonical, StringComparison.OrdinalIgnoreCase));
        sourceOutputs.Add(output);
    }

    private string ResolveExpectedPerformerPosePath(string expectedAnimPath, BrowserJobStatus? status)
    {
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

/// <summary>Checks the Unity-authored PerformerPose YAML without requiring Unity to be open.</summary>
public static class PerformerPoseAssetInspector
{
    private static readonly Regex MetaGuidPattern = new(@"(?m)^guid:\s*([0-9a-f]{32})\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsUsable(string projectRoot, string animationClipPath, string performerPoseAssetPath)
    {
        try
        {
            if (!File.Exists(animationClipPath) || !File.Exists(performerPoseAssetPath)) return false;
            var clipGuid = ReadMetaGuid(animationClipPath + ".meta");
            var poseGuid = ReadMetaGuid(performerPoseAssetPath + ".meta");
            if (clipGuid is null || poseGuid is null) return false;

            var yaml = File.ReadAllText(performerPoseAssetPath);
            if (!HasReference(yaml, "clip", clipGuid)) return false;

            var scriptMeta = Path.Combine(projectRoot, "Assets", "DazPose", "Runtime", "Performer", "PerformerPose.cs.meta");
            if (File.Exists(scriptMeta))
            {
                var scriptGuid = ReadMetaGuid(scriptMeta);
                if (scriptGuid is null || !HasReference(yaml, "m_Script", scriptGuid)) return false;
            }

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
}

public sealed record ResolvedPoseOutput(string BaseName, string CanonicalPath, string CanonicalAssetPath,
    string AnimPath, string AnimAssetPath, string PerformerPosePath, string PerformerPoseAssetPath,
    string DestinationRelativeFolder);

public sealed class PoseOutputNamingService(UnityProjectService projectService)
{
    private readonly object _reservationLock = new();
    private readonly Dictionary<string, string> _reservations = new(StringComparer.OrdinalIgnoreCase);

    public ResolvedPoseOutput Resolve(AppSettings settings, string sourcePosePath, string destinationRelativeFolder)
    {
        var source = Path.GetFullPath(sourcePosePath);
        var destination = UnityProjectService.NormalizeDestinationRelativeFolder(destinationRelativeFolder);
        projectService.ValidateAssetRoots(settings.FinalPoseAssetRoot, settings.CanonicalImportRoot);
        var sourceFolder = Path.GetFileName(Path.GetDirectoryName(source) ?? string.Empty);
        var sourceStem = Path.GetFileNameWithoutExtension(source);
        var plainBase = SanitizeFileStem($"{sourceFolder} - {sourceStem}");
        var importFolderRelative = UnityProjectService.JoinAssetPath(settings.CanonicalImportRoot, destination);
            var finalFolderRelative = UnityProjectService.JoinAssetPath(settings.FinalPoseAssetRoot, destination);
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
            var performerPoseAssetPath = UnityProjectService.JoinAssetPath(finalFolderRelative, selectedBase + ".asset");
            var resolved = new ResolvedPoseOutput(selectedBase,
                Path.Combine(importFolder, selectedBase + ".dazpose.json"), canonicalAssetPath,
                Path.Combine(finalFolder, selectedBase + ".anim"), animAssetPath,
                Path.Combine(finalFolder, selectedBase + ".asset"), performerPoseAssetPath, destination);
            _reservations[Path.GetFullPath(resolved.CanonicalPath)] = source;
            return resolved;
        }
    }

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
