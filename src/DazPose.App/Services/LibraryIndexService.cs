using System.Text.Json;
using System.Text.RegularExpressions;
using DazPose.App.Models;
using DazPose.Core;
using Microsoft.Data.Sqlite;

namespace DazPose.App.Services;

/// <summary>Persistent, metadata-only index for read-only DAZ content libraries.</summary>
public sealed class LibraryIndexService
{
    private const int FolderScanVersion = 2;
    private readonly string _databasePath;
    private readonly object _databaseWriteLock = new();

    public LibraryIndexService(string? databasePath = null)
    {
        _databasePath = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DazPoseWizard", "library-index.db");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_databasePath))!);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS poses (
                source_path TEXT PRIMARY KEY COLLATE NOCASE,
                source_root TEXT NOT NULL COLLATE NOCASE,
                source_folder_path TEXT NOT NULL,
                relative_folder_path TEXT NOT NULL,
                immediate_folder_name TEXT NOT NULL,
                file_stem TEXT NOT NULL,
                display_name TEXT NOT NULL,
                asset_id TEXT NOT NULL,
                asset_type TEXT NOT NULL,
                figure_generation TEXT NOT NULL DEFAULT '',
                preview_image_path TEXT,
                file_size INTEGER NOT NULL,
                modified_utc_ticks INTEGER NOT NULL,
                preview_modified_utc_ticks INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_poses_root_folder ON poses(source_root, relative_folder_path COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS ix_poses_root_name ON poses(source_root, file_stem COLLATE NOCASE, display_name COLLATE NOCASE);
            CREATE TABLE IF NOT EXISTS scanned_folders (
                source_root TEXT NOT NULL COLLATE NOCASE,
                relative_folder_path TEXT NOT NULL COLLATE NOCASE,
                directory_modified_utc_ticks INTEGER NOT NULL,
                scan_version INTEGER NOT NULL,
                scanned_utc_ticks INTEGER NOT NULL,
                PRIMARY KEY(source_root, relative_folder_path)
            );
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            """;
        command.ExecuteNonQuery();
        EnsureColumn(connection, "poses", "figure_generation", "TEXT NOT NULL DEFAULT ''");
        using (var index = connection.CreateCommand())
        {
            index.CommandText = "CREATE INDEX IF NOT EXISTS ix_poses_root_figure ON poses(source_root, figure_generation COLLATE NOCASE);";
            index.ExecuteNonQuery();
        }
        BackfillFigureGenerations(connection);
    }

    public Task<IReadOnlyList<PoseLibraryEntry>> GetAllAsync(string sourceRoot, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<PoseLibraryEntry>>(() => Query(sourceRoot, relativeFolder: null, includeChildren: true, searchText: null, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<PoseLibraryEntry>> SearchAsync(string sourceRoot, string? relativeFolder, bool includeChildren,
        string? searchText, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<PoseLibraryEntry>>(() => Query(sourceRoot, relativeFolder, includeChildren, searchText, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<string>> GetFoldersAsync(string sourceRoot, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<string>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DISTINCT relative_folder_path FROM poses WHERE source_root = $root ORDER BY relative_folder_path COLLATE NOCASE";
            command.Parameters.AddWithValue("$root", NormalizeRoot(sourceRoot));
            using var reader = command.ExecuteReader();
            var folders = new List<string>();
            while (reader.Read()) folders.Add(reader.GetString(0));
            return folders;
        }, cancellationToken);

    public Task<LibraryScanResult> ScanAsync(string sourceRoot, IProgress<LibraryScanProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(sourceRoot, progress, cancellationToken), cancellationToken);

    public Task<LibraryScanResult> ScanFolderAsync(string sourceRoot, string? relativeFolder, bool force = false,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ScanFolder(sourceRoot, relativeFolder, force, cancellationToken), cancellationToken);

    private LibraryScanResult ScanFolder(string sourceRoot, string? relativeFolder, bool force, CancellationToken cancellationToken)
    {
        var root = NormalizeRoot(sourceRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"DAZ content folder does not exist: '{root}'.");
        var relative = NormalizeRelativeFolder(relativeFolder);
        var folderPath = ResolveFolder(root, relative);
        if (!Directory.Exists(folderPath)) throw new DirectoryNotFoundException($"DAZ library folder does not exist: '{folderPath}'.");

        var directoryModifiedTicks = Directory.GetLastWriteTimeUtc(folderPath).Ticks;
        if (!force && IsFolderScanCurrent(root, relative, directoryModifiedTicks))
            return new LibraryScanResult(0, 0, 0, 0, 0);

        var cached = ReadCachedMetadata(root, relative);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changes = new List<IndexedPose>();
        var removals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = 0;
        var indexed = 0;
        var ignored = 0;
        var errors = 0;

        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(folderPath, "*.duf", SearchOption.TopDirectoryOnly).ToArray(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            throw new IOException($"Could not read DAZ library folder '{folderPath}'.", ex);
        }

        foreach (var sourcePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath;
            FileInfo fileInfo;
            try
            {
                fullPath = Path.GetFullPath(sourcePath);
                fileInfo = new FileInfo(fullPath);
                if (!fileInfo.Exists) continue;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                errors++;
                continue;
            }

            seen.Add(fullPath);
            visited++;
            var modifiedTicks = fileInfo.LastWriteTimeUtc.Ticks;
            var previewPath = FindPreviewPath(fullPath);
            var previewTicks = GetModifiedTicks(previewPath);
            if (cached.TryGetValue(fullPath, out var old)
                && old.FileSize == fileInfo.Length
                && old.ModifiedUtcTicks == modifiedTicks
                && string.Equals(old.PreviewImagePath, previewPath, StringComparison.OrdinalIgnoreCase)
                && old.PreviewModifiedUtcTicks == previewTicks)
            {
                indexed++;
                continue;
            }

            try
            {
                using var document = DsonFileReader.ReadJson(fullPath);
                if (!TryReadPoseMetadata(document.RootElement, out var assetId, out var displayName))
                {
                    if (cached.ContainsKey(fullPath)) removals.Add(fullPath);
                    ignored++;
                    continue;
                }

                indexed++;
                changes.Add(CreateIndexedPose(root, fullPath, fileInfo, previewPath, previewTicks, assetId, displayName));
            }
            catch (Exception ex) when (ex is DazConversionException or IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                if (cached.ContainsKey(fullPath)) removals.Add(fullPath);
                errors++;
            }
        }

        foreach (var existingPath in cached.Keys)
            if (!seen.Contains(existingPath)) removals.Add(existingPath);

        cancellationToken.ThrowIfCancellationRequested();
        ApplyChanges(root, changes, removals);
        if (errors == 0) MarkFolderScanned(root, relative, Directory.GetLastWriteTimeUtc(folderPath).Ticks);
        return new LibraryScanResult(visited, changes.Count, removals.Count, ignored, errors);
    }

    private LibraryScanResult Scan(string sourceRoot, IProgress<LibraryScanProgress>? progress, CancellationToken cancellationToken)
    {
        var root = NormalizeRoot(sourceRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"DAZ content folder does not exist: '{root}'.");

        var cached = ReadCachedMetadata(root);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const int writeBatchSize = 64;
        var changed = new List<IndexedPose>(writeBatchSize);
        var changedCount = 0;
        var removals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = 0;
        var indexed = 0;
        var ignored = 0;
        var errors = 0;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var sourcePath in EnumerateDufFiles(root, options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath;
            FileInfo fileInfo;
            try
            {
                fullPath = Path.GetFullPath(sourcePath);
                fileInfo = new FileInfo(fullPath);
                if (!fileInfo.Exists) continue;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                errors++;
                continue;
            }

            seen.Add(fullPath);
            var modifiedTicks = fileInfo.LastWriteTimeUtc.Ticks;
            var previewPath = FindPreviewPath(fullPath);
            var previewTicks = GetModifiedTicks(previewPath);
            if (cached.TryGetValue(fullPath, out var old)
                && old.FileSize == fileInfo.Length
                && old.ModifiedUtcTicks == modifiedTicks
                && string.Equals(old.PreviewImagePath, previewPath, StringComparison.OrdinalIgnoreCase)
                && old.PreviewModifiedUtcTicks == previewTicks)
            {
                visited++;
                indexed++;
                if (visited % 128 == 0) progress?.Report(new LibraryScanProgress(visited, indexed, ignored, errors, fullPath));
                continue;
            }

            try
            {
                using var document = DsonFileReader.ReadJson(fullPath);
                if (!TryReadPoseMetadata(document.RootElement, out var assetId, out var displayName))
                {
                    if (cached.ContainsKey(fullPath)) removals.Add(fullPath);
                    ignored++;
                }
                else
                {
                    indexed++;
                    changed.Add(CreateIndexedPose(root, fullPath, fileInfo, previewPath, previewTicks, assetId, displayName));
                }
            }
            catch (Exception ex) when (ex is DazConversionException or IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                if (cached.ContainsKey(fullPath)) removals.Add(fullPath);
                errors++;
            }

            visited++;
            if (changed.Count >= writeBatchSize)
            {
                ApplyChanges(root, changed, Array.Empty<string>());
                changedCount += changed.Count;
                changed.Clear();
            }
            if (visited % 64 == 0) progress?.Report(new LibraryScanProgress(visited, indexed, ignored, errors, fullPath));
        }

        foreach (var existingPath in cached.Keys)
            if (!seen.Contains(existingPath)) removals.Add(existingPath);

        cancellationToken.ThrowIfCancellationRequested();
        ApplyChanges(root, changed, removals);
        changedCount += changed.Count;
        progress?.Report(new LibraryScanProgress(visited, indexed, ignored, errors, null));
        return new LibraryScanResult(visited, changedCount, removals.Count, ignored, errors);
    }

    private static IEnumerable<string> EnumerateDufFiles(string root, EnumerationOptions options)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*.duf", SearchOption.TopDirectoryOnly)) yield return file;

        IEnumerable<string> topLevelDirectories;
        try
        {
            topLevelDirectories = Directory.EnumerateDirectories(root)
                .Where(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0)
                .OrderBy(path => ScanPriority(Path.GetFileName(path)))
                .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            yield break;
        }

        foreach (var directory in topLevelDirectories)
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(directory, "*.duf", options); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException) { continue; }
            using var enumerator = files.GetEnumerator();
            while (true)
            {
                string current;
                try
                {
                    if (!enumerator.MoveNext()) break;
                    current = enumerator.Current;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException) { break; }
                yield return current;
            }
        }
    }

    private static int ScanPriority(string directoryName) => directoryName.ToLowerInvariant() switch
    {
        "poses" => 0,
        "people" => 1,
        _ => 2
    };

    private IReadOnlyList<PoseLibraryEntry> Query(string sourceRoot, string? relativeFolder, bool includeChildren,
        string? searchText, CancellationToken cancellationToken)
    {
        var root = NormalizeRoot(sourceRoot);
        var folder = (relativeFolder ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var clauses = new List<string> { "source_root = $root" };
        command.Parameters.AddWithValue("$root", root);
        if (folder.Length == 0 && !includeChildren)
        {
            clauses.Add("relative_folder_path = ''");
        }
        else if (folder.Length > 0)
        {
            if (includeChildren)
            {
                clauses.Add("(relative_folder_path = $folder COLLATE NOCASE OR relative_folder_path LIKE $children ESCAPE '\\')");
                command.Parameters.AddWithValue("$folder", folder);
                command.Parameters.AddWithValue("$children", EscapeLike(folder) + "/%");
            }
            else
            {
                clauses.Add("relative_folder_path = $folder COLLATE NOCASE");
                command.Parameters.AddWithValue("$folder", folder);
            }
        }
        command.CommandText = $"SELECT source_path, source_folder_path, relative_folder_path, immediate_folder_name, file_stem, display_name, asset_id, asset_type, preview_image_path, file_size, modified_utc_ticks, figure_generation FROM poses WHERE {string.Join(" AND ", clauses)} ORDER BY display_name COLLATE NOCASE, source_path COLLATE NOCASE";
        using var reader = command.ExecuteReader();
        var entries = new List<PoseLibraryEntry>();
        var terms = (searchText ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = new PoseLibraryEntry(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetInt64(9), reader.GetInt64(10))
            {
                RelativePath = Path.GetRelativePath(root, reader.GetString(0)).Replace('\\', '/'),
                FigureGeneration = string.IsNullOrWhiteSpace(reader.GetString(11)) ? FigureGenerations.Other : reader.GetString(11)
            };
            var searchValues = new[] { entry.FileStem, entry.DisplayName, entry.ImmediateFolderName, entry.RelativeFolderPath, entry.RelativePath, entry.AssetId };
            if (terms.Length == 0 || terms.All(term => searchValues.Any(value => value.Contains(term, StringComparison.OrdinalIgnoreCase))))
                entries.Add(entry);
        }
        return entries;
    }

    private Dictionary<string, CachedMetadata> ReadCachedMetadata(string root, string? relativeFolder = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT source_path, file_size, modified_utc_ticks, preview_image_path, preview_modified_utc_ticks FROM poses WHERE source_root = $root";
        command.Parameters.AddWithValue("$root", root);
        if (relativeFolder is not null)
        {
            command.CommandText += " AND relative_folder_path = $folder COLLATE NOCASE";
            command.Parameters.AddWithValue("$folder", relativeFolder);
        }
        using var reader = command.ExecuteReader();
        var cache = new Dictionary<string, CachedMetadata>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
            cache[reader.GetString(0)] = new CachedMetadata(reader.GetInt64(1), reader.GetInt64(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetInt64(4));
        return cache;
    }

    private void ApplyChanges(string root, IReadOnlyCollection<IndexedPose> changes, IReadOnlyCollection<string> removals)
    {
        lock (_databaseWriteLock) ApplyChangesLocked(root, changes, removals);
    }

    private void ApplyChangesLocked(string root, IReadOnlyCollection<IndexedPose> changes, IReadOnlyCollection<string> removals)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText = """
                INSERT INTO poses(source_path, source_root, source_folder_path, relative_folder_path, immediate_folder_name,
                    file_stem, display_name, asset_id, asset_type, figure_generation, preview_image_path, file_size, modified_utc_ticks, preview_modified_utc_ticks)
                VALUES($path, $root, $folder, $relative, $immediate, $stem, $display, $assetId, $assetType, $figure, $preview, $size, $modified, $previewModified)
                ON CONFLICT(source_path) DO UPDATE SET source_root=excluded.source_root, source_folder_path=excluded.source_folder_path,
                    relative_folder_path=excluded.relative_folder_path, immediate_folder_name=excluded.immediate_folder_name,
                    file_stem=excluded.file_stem, display_name=excluded.display_name, asset_id=excluded.asset_id,
                    asset_type=excluded.asset_type, figure_generation=excluded.figure_generation,
                    preview_image_path=excluded.preview_image_path, file_size=excluded.file_size,
                    modified_utc_ticks=excluded.modified_utc_ticks, preview_modified_utc_ticks=excluded.preview_modified_utc_ticks;
                """;
            foreach (var parameter in new[] { "$path", "$root", "$folder", "$relative", "$immediate", "$stem", "$display", "$assetId", "$assetType", "$figure", "$preview", "$size", "$modified", "$previewModified" })
                upsert.Parameters.Add(new SqliteParameter(parameter, DBNull.Value));
            foreach (var item in changes)
            {
                upsert.Parameters["$path"].Value = item.Entry.SourcePath;
                upsert.Parameters["$root"].Value = item.SourceRoot;
                upsert.Parameters["$folder"].Value = item.Entry.SourceFolderPath;
                upsert.Parameters["$relative"].Value = item.Entry.RelativeFolderPath;
                upsert.Parameters["$immediate"].Value = item.Entry.ImmediateFolderName;
                upsert.Parameters["$stem"].Value = item.Entry.FileStem;
                upsert.Parameters["$display"].Value = item.Entry.DisplayName;
                upsert.Parameters["$assetId"].Value = item.Entry.AssetId;
                upsert.Parameters["$assetType"].Value = item.Entry.AssetType;
                upsert.Parameters["$figure"].Value = item.Entry.FigureGeneration;
                upsert.Parameters["$preview"].Value = item.Entry.PreviewImagePath is null ? DBNull.Value : item.Entry.PreviewImagePath;
                upsert.Parameters["$size"].Value = item.Entry.FileSize;
                upsert.Parameters["$modified"].Value = item.Entry.ModifiedUtcTicks;
                upsert.Parameters["$previewModified"].Value = item.PreviewModifiedUtcTicks;
                upsert.ExecuteNonQuery();
            }
        }
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM poses WHERE source_path = $path AND source_root = $root";
            var pathParameter = delete.Parameters.Add("$path", SqliteType.Text);
            delete.Parameters.AddWithValue("$root", root);
            foreach (var path in removals)
            {
                pathParameter.Value = path;
                delete.ExecuteNonQuery();
            }
        }
        transaction.Commit();
    }

    private static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
    {
        using var query = connection.CreateCommand();
        query.CommandText = $"PRAGMA table_info({table});";
        using var reader = query.ExecuteReader();
        while (reader.Read())
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
        reader.Close();
        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
    }

    private static void BackfillFigureGenerations(SqliteConnection connection)
    {
        var updates = new List<(string Path, string Figure)>();
        using (var query = connection.CreateCommand())
        {
            query.CommandText = "SELECT source_path, relative_folder_path, file_stem, display_name, asset_id FROM poses WHERE figure_generation = ''";
            using var reader = query.ExecuteReader();
            while (reader.Read())
                updates.Add((reader.GetString(0), InferFigureGeneration(
                    reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(0))));
        }
        if (updates.Count == 0) return;
        using var transaction = connection.BeginTransaction();
        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE poses SET figure_generation = $figure WHERE source_path = $path";
        var figureParameter = update.Parameters.Add("$figure", SqliteType.Text);
        var pathParameter = update.Parameters.Add("$path", SqliteType.Text);
        foreach (var item in updates)
        {
            figureParameter.Value = item.Figure;
            pathParameter.Value = item.Path;
            update.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private bool IsFolderScanCurrent(string root, string relativeFolder, long directoryModifiedTicks)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT 1 FROM scanned_folders
            WHERE source_root = $root AND relative_folder_path = $folder COLLATE NOCASE
              AND directory_modified_utc_ticks = $modified AND scan_version = $version
            """;
        command.Parameters.AddWithValue("$root", root);
        command.Parameters.AddWithValue("$folder", relativeFolder);
        command.Parameters.AddWithValue("$modified", directoryModifiedTicks);
        command.Parameters.AddWithValue("$version", FolderScanVersion);
        return command.ExecuteScalar() is not null;
    }

    private void MarkFolderScanned(string root, string relativeFolder, long directoryModifiedTicks)
    {
        lock (_databaseWriteLock)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO scanned_folders(source_root, relative_folder_path, directory_modified_utc_ticks, scan_version, scanned_utc_ticks)
                VALUES($root, $folder, $modified, $version, $scanned)
                ON CONFLICT(source_root, relative_folder_path) DO UPDATE SET
                    directory_modified_utc_ticks=excluded.directory_modified_utc_ticks,
                    scan_version=excluded.scan_version,
                    scanned_utc_ticks=excluded.scanned_utc_ticks;
                """;
            command.Parameters.AddWithValue("$root", root);
            command.Parameters.AddWithValue("$folder", relativeFolder);
            command.Parameters.AddWithValue("$modified", directoryModifiedTicks);
            command.Parameters.AddWithValue("$version", FolderScanVersion);
            command.Parameters.AddWithValue("$scanned", DateTime.UtcNow.Ticks);
            command.ExecuteNonQuery();
        }
    }

    private static IndexedPose CreateIndexedPose(string root, string fullPath, FileInfo fileInfo,
        string? previewPath, long previewTicks, string assetId, string displayName)
    {
        var folder = Path.GetDirectoryName(fullPath)!;
        var relativeFolder = Path.GetRelativePath(root, folder).Replace('\\', '/');
        if (relativeFolder == ".") relativeFolder = string.Empty;
        var fileStem = Path.GetFileNameWithoutExtension(fullPath);
        var entry = new PoseLibraryEntry(
            fullPath,
            folder,
            relativeFolder,
            Path.GetFileName(folder),
            fileStem,
            displayName,
            assetId,
            "preset_pose",
            previewPath,
            fileInfo.Length,
            fileInfo.LastWriteTimeUtc.Ticks)
        {
            FigureGeneration = InferFigureGeneration(relativeFolder, fileStem, displayName, assetId, fullPath)
        };
        return new IndexedPose(entry, previewTicks, root);
    }

    private static string InferFigureGeneration(params string?[] values)
    {
        var text = string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        try { text = Uri.UnescapeDataString(text); }
        catch (UriFormatException) { }

        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (Regex.IsMatch(text, @"GENESIS\s*8(?:[\s._-]*1)\s*FEMALE|(?<![A-Z0-9])(?:G8[\s._-]*1F|G81F)(?![A-Z0-9])", options))
            return FigureGenerations.G81Female;
        if (Regex.IsMatch(text, @"GENESIS\s*8(?:[\s._-]*1)\s*MALE|(?<![A-Z0-9])(?:G8[\s._-]*1M|G81M)(?![A-Z0-9])", options))
            return FigureGenerations.G81Male;
        if (Regex.IsMatch(text, @"GENESIS\s*8\s*FEMALE|(?<![A-Z0-9])G8F(?![A-Z0-9])", options))
            return FigureGenerations.G8Female;
        if (Regex.IsMatch(text, @"GENESIS\s*8\s*MALE|(?<![A-Z0-9])G8M(?![A-Z0-9])", options))
            return FigureGenerations.G8Male;
        if (Regex.IsMatch(text, @"GENESIS\s*9|(?<![A-Z0-9])G9(?:F|M)?(?![A-Z0-9])", options))
            return FigureGenerations.Genesis9;
        return FigureGenerations.Other;
    }

    private static string NormalizeRelativeFolder(string? relativeFolder) =>
        (relativeFolder ?? string.Empty).Trim().Replace('\\', '/').Trim('/');

    private static string ResolveFolder(string root, string relativeFolder)
    {
        var folderPath = relativeFolder.Length == 0
            ? root
            : Path.GetFullPath(Path.Combine(root, relativeFolder.Replace('/', Path.DirectorySeparatorChar)));
        var check = Path.GetRelativePath(root, folderPath);
        if (Path.IsPathRooted(check) || check == ".."
            || check.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || check.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
            throw new InvalidOperationException("The selected folder is outside the configured DAZ content root.");
        return folderPath;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout = 5000;";
        command.ExecuteNonQuery();
        return connection;
    }

    private static bool TryReadPoseMetadata(JsonElement root, out string assetId, out string displayName)
    {
        assetId = string.Empty;
        displayName = string.Empty;
        if (!root.TryGetProperty("asset_info", out var info) || info.ValueKind != JsonValueKind.Object) return false;
        if (!info.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String
            || !string.Equals(type.GetString(), "preset_pose", StringComparison.OrdinalIgnoreCase)) return false;
        assetId = ReadString(info, "id") ?? string.Empty;
        displayName = ReadString(info, "label") ?? ReadString(info, "name")
            ?? ReadString(root, "label") ?? ReadString(root, "name") ?? string.Empty;
        return true;
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? FindPreviewPath(string sourcePath)
    {
        var candidates = new[]
        {
            Path.ChangeExtension(sourcePath, ".tip.png"),
            sourcePath + ".tip.png",
            sourcePath + ".png",
            Path.ChangeExtension(sourcePath, ".png"),
            sourcePath + ".jpg",
            Path.ChangeExtension(sourcePath, ".jpg"),
            sourcePath + ".jpeg",
            Path.ChangeExtension(sourcePath, ".jpeg")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static long GetModifiedTicks(string? path)
    {
        try { return path is not null ? File.GetLastWriteTimeUtc(path).Ticks : 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 0; }
    }

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    private static string NormalizeRoot(string root)
    {
        var fullPath = Path.GetFullPath(root);
        var pathRoot = Path.GetPathRoot(fullPath) ?? string.Empty;
        return fullPath.Length > pathRoot.Length
            ? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : fullPath;
    }
    private sealed record CachedMetadata(long FileSize, long ModifiedUtcTicks, string? PreviewImagePath, long PreviewModifiedUtcTicks);
    private sealed record IndexedPose(PoseLibraryEntry Entry, long PreviewModifiedUtcTicks, string SourceRoot);
}
