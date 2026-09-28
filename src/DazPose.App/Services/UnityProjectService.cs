using System.Text.Json;
using DazPose.App.Models;

namespace DazPose.App.Services;

public sealed class UnityProjectService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public bool LooksLikeUnityProject(string? projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot) || !Directory.Exists(projectRoot)) return false;
        return Directory.Exists(Path.Combine(projectRoot, "Assets"))
            && (Directory.Exists(Path.Combine(projectRoot, "ProjectSettings"))
                || Directory.Exists(Path.Combine(projectRoot, "Packages")));
    }

    public string ResolveAssetPath(string projectRoot, string assetRelativePath)
    {
        var normalized = NormalizeAssetRelativePath(assetRelativePath);
        var project = Path.GetFullPath(projectRoot);
        var fullPath = Path.GetFullPath(Path.Combine(project, normalized.Replace('/', Path.DirectorySeparatorChar)));
        EnsureWithin(Path.Combine(project, "Assets"), fullPath);
        return fullPath;
    }

    public string NormalizeAssetRelativePath(string assetRelativePath)
    {
        if (string.IsNullOrWhiteSpace(assetRelativePath)) throw new ArgumentException("An Assets-relative path is required.", nameof(assetRelativePath));
        var normalized = assetRelativePath.Trim().Replace('\\', '/').TrimEnd('/');
        if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
            || normalized.Split('/').Any(part => part is "" or "." or ".."))
            throw new ArgumentException("The path must stay inside the Unity Assets folder.", nameof(assetRelativePath));
        return normalized;
    }

    public void ValidateAssetRoots(string outputRoot, string importRoot)
    {
        var output = NormalizeAssetRelativePath(outputRoot).TrimEnd('/');
        var import = NormalizeAssetRelativePath(importRoot).TrimEnd('/');
        var overlaps = string.Equals(output, import, StringComparison.OrdinalIgnoreCase)
            || output.StartsWith(import + "/", StringComparison.OrdinalIgnoreCase)
            || import.StartsWith(output + "/", StringComparison.OrdinalIgnoreCase);
        if (overlaps) throw new ArgumentException("The canonical import root and final pose asset root must be separate, non-overlapping folders inside Assets.");
    }

    public async Task WriteBridgeConfigurationAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (!LooksLikeUnityProject(settings.UnityProjectRoot))
            throw new InvalidOperationException("Select a Unity project containing Assets and ProjectSettings or Packages.");

        var importRoot = NormalizeAssetRelativePath(settings.CanonicalImportRoot);
        var outputRoot = NormalizeAssetRelativePath(settings.FinalPoseAssetRoot);
        ValidateAssetRoots(outputRoot, importRoot);
        var importPath = ResolveAssetPath(settings.UnityProjectRoot, importRoot);
        var outputPath = ResolveAssetPath(settings.UnityProjectRoot, outputRoot);
        Directory.CreateDirectory(importPath);
        Directory.CreateDirectory(outputPath);
        Directory.CreateDirectory(Path.Combine(Path.GetFullPath(settings.UnityProjectRoot), ".dazposewizard", "status"));

        var bridgePath = Path.Combine(Path.GetFullPath(settings.UnityProjectRoot), "DazPoseWizard.project.json");
        var stagedPath = bridgePath + $".{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(new ProjectBridgeConfiguration
        {
            SchemaVersion = 1,
            ImportRoot = importRoot,
            OutputRoot = outputRoot
        }, JsonOptions);
        try
        {
            await File.WriteAllTextAsync(stagedPath, json, new System.Text.UTF8Encoding(false), cancellationToken);
            File.Move(stagedPath, bridgePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(stagedPath)) File.Delete(stagedPath);
        }
    }

    public async Task<string> CreateDestinationFolderAsync(AppSettings settings, string parentRelativeFolder,
        string requestedName, CancellationToken cancellationToken = default)
    {
        if (!LooksLikeUnityProject(settings.UnityProjectRoot))
            throw new InvalidOperationException("Select a valid Unity project before creating destination folders.");
        ValidateAssetRoots(settings.FinalPoseAssetRoot, settings.CanonicalImportRoot);
        var folderName = SanitizeFolderName(requestedName);
        var parent = NormalizeDestinationRelativeFolder(parentRelativeFolder);
        var child = parent.Length == 0 ? folderName : $"{parent}/{folderName}";
        var outputPath = ResolveAssetPath(settings.UnityProjectRoot, JoinAssetPath(settings.FinalPoseAssetRoot, child));
        var importPath = ResolveAssetPath(settings.UnityProjectRoot, JoinAssetPath(settings.CanonicalImportRoot, child));
        Directory.CreateDirectory(outputPath);
        Directory.CreateDirectory(importPath);
        await WriteBridgeConfigurationAsync(settings, cancellationToken);
        return child;
    }

    public IReadOnlyList<FolderNode> LoadDestinationTree(AppSettings settings)
    {
        if (!LooksLikeUnityProject(settings.UnityProjectRoot)) return Array.Empty<FolderNode>();
        var root = ResolveAssetPath(settings.UnityProjectRoot, settings.FinalPoseAssetRoot);
        Directory.CreateDirectory(root);
        var result = new List<FolderNode>();
        foreach (var child in EnumerateDirectories(root))
            result.Add(BuildNode(child, root));
        return result;
    }

    public static string NormalizeDestinationRelativeFolder(string? relativeFolder)
    {
        if (string.IsNullOrWhiteSpace(relativeFolder)) return string.Empty;
        var normalized = relativeFolder.Trim().Replace('\\', '/').Trim('/');
        if (normalized.Split('/').Any(part => part is "" or "." or ".."))
            throw new ArgumentException("Destination folder must be a relative path without traversal segments.", nameof(relativeFolder));
        return normalized;
    }

    public static string SanitizeFolderName(string requestedName)
    {
        if (string.IsNullOrWhiteSpace(requestedName)) throw new ArgumentException("Enter a folder name.", nameof(requestedName));
        var invalid = Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*").ToHashSet();
        var safe = new string(requestedName.Trim().Select(character => invalid.Contains(character) || char.IsControl(character) ? '_' : character).ToArray())
            .Trim().TrimEnd('.', ' ');
        if (safe is "" or "." or "..") throw new ArgumentException("The folder name is empty after removing invalid characters.", nameof(requestedName));
        var deviceName = safe.Split('.')[0];
        if (new[] { "CON", "PRN", "AUX", "NUL" }.Contains(deviceName, StringComparer.OrdinalIgnoreCase)
            || System.Text.RegularExpressions.Regex.IsMatch(deviceName, "^(COM|LPT)[1-9]$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            safe = $"_{safe}";
        return safe;
    }

    public static string JoinAssetPath(string root, string destinationRelativeFolder)
    {
        var normalizedRoot = root.Trim().Replace('\\', '/').TrimEnd('/');
        var relative = NormalizeDestinationRelativeFolder(destinationRelativeFolder);
        return relative.Length == 0 ? normalizedRoot : $"{normalizedRoot}/{relative}";
    }

    private static IEnumerable<string> EnumerateDirectories(string parent)
    {
        try
        {
            return Directory.EnumerateDirectories(parent).Where(path =>
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
        catch (DirectoryNotFoundException) { return Array.Empty<string>(); }
    }

    private static FolderNode BuildNode(string folderPath, string root)
    {
        var relative = Path.GetRelativePath(root, folderPath).Replace('\\', '/');
        var node = new FolderNode(Path.GetFileName(folderPath), relative);
        node.Children = EnumerateDirectories(folderPath).Select(child => BuildNode(child, root)).ToArray();
        return node;
    }

    private static void EnsureWithin(string root, string target)
    {
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var targetFull = Path.GetFullPath(target);
        if (!targetFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(targetFull.TrimEnd(Path.DirectorySeparatorChar), rootFull.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The configured Unity path resolves outside the Assets folder.");
    }
}

public sealed class ProjectBridgeConfiguration
{
    public int SchemaVersion { get; set; }
    public string ImportRoot { get; set; } = string.Empty;
    public string OutputRoot { get; set; } = string.Empty;
}
