using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DazPose.Core;

namespace DazPose.App.Services;

public sealed class RequiredMorphManifestService
{
    public const string ManifestRelativePath = ".dazposewizard/required-morphs.json";
    public const string ExportRulesFileName = "DazPoseWizard-MorphExportRules.csv";
    public static readonly IReadOnlyList<string> AlwaysExportCategories =
        ["Breathing", "Blink", "BodyCustomization", "LipSync", "Manual"];

    private static readonly ConcurrentDictionary<string, object> ProjectLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public int AddCandidateControls(string projectRoot, IEnumerable<DazFigureControlValue> controls,
        string sourcePath, string? contentRoot = null)
    {
        ArgumentNullException.ThrowIfNull(controls);
        var incoming = controls
            .Where(control => control is not null && Math.Abs(control.Value) > 1e-7f
                && !string.IsNullOrWhiteSpace(control.DecodedControlName))
            .GroupBy(control => control.DecodedControlName, StringComparer.Ordinal)
            .Select(group => group.OrderBy(control => control.RawControlId, StringComparer.Ordinal).First())
            .OrderBy(control => control.DecodedControlName, StringComparer.Ordinal)
            .ToArray();
        if (incoming.Length == 0) return 0;

        var project = GetProjectRoot(projectRoot);
        lock (GetProjectLock(project))
        {
            var (manifestPath, _) = GetPaths(project);
            var manifest = ReadManifest(manifestPath);
            var items = NormalizeItems(manifest.Items);
            var byName = items.ToDictionary(item => item.Name, StringComparer.Ordinal);
            var newlyRequired = 0;
            foreach (var control in incoming)
            {
                if (byName.TryGetValue(control.DecodedControlName, out var existing))
                {
                    if (!existing.RequiredByContent) newlyRequired++;
                    existing.RequiredByContent = true;
                    if (string.IsNullOrEmpty(existing.RawControlId)) existing.RawControlId = control.RawControlId ?? string.Empty;
                    if (existing.State == RequiredMorphState.Unresolved) existing.State = RequiredMorphState.Candidate;
                    if (string.IsNullOrEmpty(existing.FirstSeenIn)) existing.FirstSeenIn = DescribeSourcePath(sourcePath, contentRoot);
                    continue;
                }

                var item = new RequiredMorphManifestItem
                {
                    RawControlId = control.RawControlId ?? string.Empty,
                    Name = control.DecodedControlName,
                    State = RequiredMorphState.Candidate,
                    FirstSeenIn = DescribeSourcePath(sourcePath, contentRoot),
                    RequiredByContent = true
                };
                items.Add(item);
                byName.Add(item.Name, item);
                newlyRequired++;
            }

            manifest.SchemaVersion = 2;
            manifest.Items = NormalizeItems(items);
            WriteJson(manifestPath, manifest);
            return newlyRequired;
        }
    }

    public IReadOnlyList<RequiredMorphManifestItem> GetAlwaysExportEntries(string projectRoot)
    {
        var project = GetProjectRoot(projectRoot);
        lock (GetProjectLock(project))
        {
            var (manifestPath, _) = GetPaths(project);
            var manifest = ReadManifest(manifestPath);
            return NormalizeItems(manifest.Items)
                .Where(item => item.Category is not null || item.RequiredByContent)
                .Select(Clone)
                .ToArray();
        }
    }

    public void SaveAlwaysExportEntries(string projectRoot, IEnumerable<RequiredMorphManifestItem> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var pins = entries.Select(Clone).ToArray();
        foreach (var pin in pins)
        {
            ValidateMorphName(pin.Name);
            if (pin.Category is null)
            {
                if (pin.AlwaysExport)
                    throw new ArgumentException($"'{pin.Name}' needs a category before it can be enabled as an always-export pin.", nameof(entries));
            }
            else if (!AlwaysExportCategories.Contains(pin.Category, StringComparer.Ordinal))
                throw new ArgumentException($"'{pin.Name}' must have a supported always-export category.", nameof(entries));
            pin.RawControlId ??= string.Empty;
            pin.Purpose ??= string.Empty;
            pin.FirstSeenIn ??= string.Empty;
            pin.State = Enum.IsDefined(pin.State) ? pin.State : RequiredMorphState.Candidate;
        }
        var duplicate = pins.GroupBy(pin => pin.Name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"The exact morph name '{duplicate.Key}' appears more than once.", nameof(entries));

        var project = GetProjectRoot(projectRoot);
        lock (GetProjectLock(project))
        {
            var (manifestPath, _) = GetPaths(project);
            var manifest = ReadManifest(manifestPath);
            var byName = NormalizeItems(manifest.Items).ToDictionary(item => item.Name, StringComparer.Ordinal);
            var submittedPins = pins.Where(pin => pin.Category is not null)
                .ToDictionary(pin => pin.Name, StringComparer.Ordinal);

            foreach (var item in byName.Values.ToArray())
            {
                if (submittedPins.ContainsKey(item.Name)) continue;
                if (item.RequiredByContent)
                {
                    item.AlwaysExport = false;
                    item.Category = null;
                    item.Purpose = null;
                }
                else
                {
                    byName.Remove(item.Name);
                }
            }

            foreach (var pin in submittedPins.Values)
            {
                if (byName.TryGetValue(pin.Name, out var existing))
                {
                    existing.AlwaysExport = pin.AlwaysExport;
                    existing.Category = pin.Category;
                    existing.Purpose = pin.Purpose;
                }
                else
                {
                    pin.RequiredByContent = false;
                    byName.Add(pin.Name, pin);
                }
            }

            manifest.SchemaVersion = 2;
            manifest.Items = NormalizeItems(byName.Values);
            WriteJson(manifestPath, manifest);
        }
    }

    public DazMorphExportRulesResult GenerateExportRules(string projectRoot)
    {
        var project = GetProjectRoot(projectRoot);
        lock (GetProjectLock(project))
        {
            var (manifestPath, csvPath) = GetPaths(project);
            var manifest = ReadManifest(manifestPath);
            manifest.SchemaVersion = 2;
            manifest.Items = NormalizeItems(manifest.Items);
            WriteJson(manifestPath, manifest);

            var pinned = manifest.Items.Where(item => item.Category is not null && item.AlwaysExport)
                .OrderBy(item => CategoryOrder(item.Category), Comparer<int>.Default)
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .ToArray();
            var contentRequired = manifest.Items.Where(item => item.RequiredByContent)
                .OrderBy(item => item.Name, StringComparer.Ordinal)
                .ToArray();
            var exportedNames = pinned.Select(item => item.Name)
                .Concat(contentRequired.Select(item => item.Name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var csv = new StringBuilder();
            foreach (var name in exportedNames) csv.Append(Quote(name)).Append(",\"Export\"\r\n");
            csv.Append("\"Anything\",\"Bake\"\r\n");
            WriteAtomic(csvPath, csv.ToString());
            return new DazMorphExportRulesResult(csvPath, exportedNames.Length, manifestPath,
                pinned.Length, contentRequired.Length);
        }
    }

    public string GetProjectDirectory(string projectRoot)
    {
        var project = GetProjectRoot(projectRoot);
        return Path.Combine(project, ".dazposewizard");
    }

    private static int CategoryOrder(string? category)
    {
        var index = category is null ? -1 : CategoryIndex(category);
        return index < 0 ? int.MaxValue : index;
    }

    private static int CategoryIndex(string value)
    {
        for (var index = 0; index < AlwaysExportCategories.Count; index++)
            if (string.Equals(AlwaysExportCategories[index], value, StringComparison.Ordinal)) return index;
        return -1;
    }

    private static object GetProjectLock(string projectRoot) => ProjectLocks.GetOrAdd(projectRoot, _ => new object());

    private static string GetProjectRoot(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("A Unity project root is required.", nameof(projectRoot));
        return Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static (string ManifestPath, string CsvPath) GetPaths(string projectRoot)
    {
        var directory = Path.Combine(projectRoot, ".dazposewizard");
        return (Path.Combine(directory, "required-morphs.json"), Path.Combine(directory, ExportRulesFileName));
    }

    private static RequiredMorphManifest ReadManifest(string path)
    {
        if (!File.Exists(path)) return new RequiredMorphManifest();
        RequiredMorphManifest? manifest;
        try { manifest = JsonSerializer.Deserialize<RequiredMorphManifest>(File.ReadAllText(path), JsonOptions); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidDataException($"Could not read the project Required Morph Manifest at {path}.", exception);
        }
        if (manifest is null || manifest.SchemaVersion is < 1 or > 2 || manifest.Items is null)
            throw new InvalidDataException($"The project Required Morph Manifest has an unsupported schema: {path}");
        if (manifest.SchemaVersion == 1)
        {
            foreach (var item in manifest.Items.Where(item => item is not null)) item.RequiredByContent = true;
            manifest.SchemaVersion = 2;
        }
        manifest.Items = NormalizeItems(manifest.Items);
        return manifest;
    }

    private static List<RequiredMorphManifestItem> NormalizeItems(IEnumerable<RequiredMorphManifestItem> items)
    {
        return (items ?? Array.Empty<RequiredMorphManifestItem>())
            .Where(item => item is not null && !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name, StringComparer.Ordinal)
            .Select(group =>
            {
                var all = group.ToArray();
                var representative = all.OrderBy(item => item.RawControlId ?? string.Empty, StringComparer.Ordinal).First();
                var pin = all.Where(item => item.Category is not null)
                    .OrderBy(item => CategoryOrder(item.Category)).ThenBy(item => item.Category, StringComparer.Ordinal).FirstOrDefault();
                var state = all.Any(item => item.State == RequiredMorphState.ConfirmedDirectMorph)
                    ? RequiredMorphState.ConfirmedDirectMorph
                    : all.All(item => item.State == RequiredMorphState.Unresolved)
                        ? RequiredMorphState.Unresolved : RequiredMorphState.Candidate;
                return new RequiredMorphManifestItem
                {
                    RawControlId = all.Select(item => item.RawControlId ?? string.Empty)
                        .Where(value => value.Length > 0).OrderBy(value => value, StringComparer.Ordinal).FirstOrDefault() ?? string.Empty,
                    Name = representative.Name,
                    State = state,
                    FirstSeenIn = all.Select(item => item.FirstSeenIn ?? string.Empty)
                        .FirstOrDefault(value => value.Length > 0) ?? string.Empty,
                    RequiredByContent = all.Any(item => item.RequiredByContent),
                    AlwaysExport = pin is not null && all.Where(item => item.Category is not null).Any(item => item.AlwaysExport),
                    Category = pin?.Category,
                    Purpose = pin?.Purpose
                };
            })
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static RequiredMorphManifestItem Clone(RequiredMorphManifestItem item) => new()
    {
        RawControlId = item.RawControlId ?? string.Empty,
        Name = item.Name,
        State = item.State,
        FirstSeenIn = item.FirstSeenIn ?? string.Empty,
        RequiredByContent = item.RequiredByContent,
        AlwaysExport = item.AlwaysExport,
        Category = item.Category,
        Purpose = item.Purpose
    };

    private static string DescribeSourcePath(string sourcePath, string? contentRoot)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) return string.Empty;
        var fullPath = Path.GetFullPath(sourcePath);
        if (!string.IsNullOrWhiteSpace(contentRoot))
        {
            var fullRoot = Path.GetFullPath(contentRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var relative = Path.GetRelativePath(fullRoot, fullPath);
            if (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                return relative.Replace('\\', '/');
        }
        return fullPath;
    }

    private static void ValidateMorphName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A morph name cannot be empty or whitespace.", nameof(name));
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static void WriteJson(string path, RequiredMorphManifest manifest) =>
        WriteAtomic(path, JsonSerializer.Serialize(manifest, JsonOptions));

    private static void WriteAtomic(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, contents, new UTF8Encoding(false));
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}

public enum RequiredMorphState { Candidate, ConfirmedDirectMorph, Unresolved }

public sealed class RequiredMorphManifest
{
    public int SchemaVersion { get; set; } = 2;
    public List<RequiredMorphManifestItem> Items { get; set; } = [];
}

public sealed class RequiredMorphManifestItem
{
    public string RawControlId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public RequiredMorphState State { get; set; } = RequiredMorphState.Candidate;
    public string FirstSeenIn { get; set; } = string.Empty;
    public bool RequiredByContent { get; set; }
    public bool AlwaysExport { get; set; }
    public string? Category { get; set; }
    public string? Purpose { get; set; }
}

public sealed record DazMorphExportRulesResult(string CsvPath, int ExportRuleCount, string ManifestPath,
    int AlwaysExportRuleCount = 0, int ContentRequiredRuleCount = 0);
