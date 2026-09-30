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

    public void ValidateAssetRoots(params string[] roots)
    {
        var normalized = roots.Select(NormalizeAssetRelativePath).Select(path => path.TrimEnd('/')).ToArray();
        for (var i = 0; i < normalized.Length; i++)
        for (var j = i + 1; j < normalized.Length; j++)
            if (string.Equals(normalized[i], normalized[j], StringComparison.OrdinalIgnoreCase)
                || normalized[i].StartsWith(normalized[j] + "/", StringComparison.OrdinalIgnoreCase)
                || normalized[j].StartsWith(normalized[i] + "/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("All pose and expression import/output roots must be separate, non-overlapping folders inside Assets.");
    }

    public async Task WriteBridgeConfigurationAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (!LooksLikeUnityProject(settings.UnityProjectRoot))
            throw new InvalidOperationException("Select a Unity project containing Assets and ProjectSettings or Packages.");

        var importRoot = NormalizeAssetRelativePath(settings.CanonicalImportRoot);
        var outputRoot = NormalizeAssetRelativePath(settings.FinalPoseAssetRoot);
        var expressionImportRoot = NormalizeAssetRelativePath(settings.CanonicalExpressionImportRoot);
        var expressionOutputRoot = NormalizeAssetRelativePath(settings.FinalExpressionAssetRoot);
        ValidateAssetRoots(outputRoot, importRoot, expressionOutputRoot, expressionImportRoot);
        foreach (var root in new[] { importRoot, outputRoot, expressionImportRoot, expressionOutputRoot })
            Directory.CreateDirectory(ResolveAssetPath(settings.UnityProjectRoot, root));
        Directory.CreateDirectory(Path.Combine(Path.GetFullPath(settings.UnityProjectRoot), ".dazposewizard", "status"));

        var bridgePath = Path.Combine(Path.GetFullPath(settings.UnityProjectRoot), "DazPoseWizard.project.json");
        var stagedPath = bridgePath + $".{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(new ProjectBridgeConfiguration
        {
            SchemaVersion = 2,
            PoseImportRoot = importRoot,
            PoseOutputRoot = outputRoot,
            ExpressionImportRoot = expressionImportRoot,
            ExpressionOutputRoot = expressionOutputRoot
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

    public async Task<string> CreateDestinationFolderAsync(AppSettings settings, PerformerAssetKind kind, string parentRelativeFolder,
        string requestedName, CancellationToken cancellationToken = default)
    {
        if (!LooksLikeUnityProject(settings.UnityProjectRoot))
            throw new InvalidOperationException("Select a valid Unity project before creating destination folders.");
        ValidateConfiguredRoots(settings);
        var folderName = SanitizeFolderName(requestedName);
        var parent = NormalizeDestinationRelativeFolder(parentRelativeFolder);
        var child = parent.Length == 0 ? folderName : $"{parent}/{folderName}";
        var outputPath = ResolveAssetPath(settings.UnityProjectRoot, JoinAssetPath(OutputRoot(settings, kind), child));
        var importPath = ResolveAssetPath(settings.UnityProjectRoot, JoinAssetPath(ImportRoot(settings, kind), child));
        Directory.CreateDirectory(outputPath);
        Directory.CreateDirectory(importPath);
        await WriteBridgeConfigurationAsync(settings, cancellationToken);
        return child;
    }

    public Task<string> CreateDestinationFolderAsync(AppSettings settings, string parentRelativeFolder, string requestedName,
        CancellationToken cancellationToken = default) => CreateDestinationFolderAsync(settings, PerformerAssetKind.Pose,
            parentRelativeFolder, requestedName, cancellationToken);

    public IReadOnlyList<FolderNode> LoadDestinationTree(AppSettings settings, PerformerAssetKind kind)
    {
        if (!LooksLikeUnityProject(settings.UnityProjectRoot)) return Array.Empty<FolderNode>();
        var root = ResolveAssetPath(settings.UnityProjectRoot, OutputRoot(settings, kind));
        Directory.CreateDirectory(root);
        var result = new List<FolderNode>();
        foreach (var child in EnumerateDirectories(root))
            result.Add(BuildNode(child, root));
        return result;
    }

    public IReadOnlyList<FolderNode> LoadDestinationTree(AppSettings settings) => LoadDestinationTree(settings, PerformerAssetKind.Pose);

    public void ValidateConfiguredRoots(AppSettings settings) => ValidateAssetRoots(
        settings.FinalPoseAssetRoot, settings.CanonicalImportRoot,
        settings.FinalExpressionAssetRoot, settings.CanonicalExpressionImportRoot);

    public static string ImportRoot(AppSettings settings, PerformerAssetKind kind) =>
        kind == PerformerAssetKind.Expression ? settings.CanonicalExpressionImportRoot : settings.CanonicalImportRoot;

    public static string OutputRoot(AppSettings settings, PerformerAssetKind kind) =>
        kind == PerformerAssetKind.Expression ? settings.FinalExpressionAssetRoot : settings.FinalPoseAssetRoot;

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
    public string PoseImportRoot { get; set; } = string.Empty;
    public string PoseOutputRoot { get; set; } = string.Empty;
    public string ExpressionImportRoot { get; set; } = string.Empty;
    public string ExpressionOutputRoot { get; set; } = string.Empty;
}
