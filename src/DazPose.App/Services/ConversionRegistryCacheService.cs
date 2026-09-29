using DazPose.App.Models;
using Microsoft.Data.Sqlite;

namespace DazPose.App.Services;

/// <summary>Fast local cache of source-to-Unity-output mappings. Project files and status records remain the source of truth.</summary>
public sealed class ConversionRegistryCacheService
{
    private readonly string _databasePath;

    public ConversionRegistryCacheService(string? databasePath = null)
    {
        _databasePath = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DazPoseWizard", "library-index.db");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_databasePath))!);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS conversion_outputs (
                project_root TEXT NOT NULL COLLATE NOCASE,
                source_path TEXT NOT NULL COLLATE NOCASE,
                canonical_import_path TEXT NOT NULL COLLATE NOCASE,
                anim_path TEXT NOT NULL,
                destination_relative_folder TEXT NOT NULL,
                state TEXT NOT NULL,
                error_message TEXT,
                timestamp_utc_ticks INTEGER,
                PRIMARY KEY(project_root, canonical_import_path)
            );
            CREATE INDEX IF NOT EXISTS ix_conversion_outputs_source ON conversion_outputs(project_root, source_path COLLATE NOCASE);
            """;
        command.ExecuteNonQuery();
    }

    public IReadOnlyDictionary<string, IReadOnlyList<ConversionOutput>> Load(string projectRoot)
    {
        var root = NormalizeProjectRoot(projectRoot);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT source_path, canonical_import_path, anim_path, destination_relative_folder, state, error_message, timestamp_utc_ticks
            FROM conversion_outputs WHERE project_root = $root
            ORDER BY destination_relative_folder COLLATE NOCASE
            """;
        command.Parameters.AddWithValue("$root", root);
        using var reader = command.ExecuteReader();
        var result = new Dictionary<string, List<ConversionOutput>>(StringComparer.OrdinalIgnoreCase);
        var assetsRoot = Path.GetFullPath(Path.Combine(root, "Assets"));
        while (reader.Read())
        {
            var sourcePath = reader.GetString(0);
            var canonicalPath = reader.GetString(1);
            var animPath = Path.GetFullPath(reader.GetString(2));
            if (!IsWithin(assetsRoot, animPath)) continue;
            var performerPosePath = Path.ChangeExtension(animPath, ".asset");
            var state = Enum.TryParse<ConversionJobState>(reader.GetString(4), true, out var parsed)
                ? parsed : ConversionJobState.AwaitingUnity;
            if (PerformerPoseAssetInspector.IsUsable(root, animPath, performerPosePath)) state = ConversionJobState.Converted;
            else if (state == ConversionJobState.Converted) state = ConversionJobState.AwaitingUnity;
            DateTimeOffset? timestamp = reader.IsDBNull(6) ? null : new DateTimeOffset(reader.GetInt64(6), TimeSpan.Zero);
            var output = new ConversionOutput(canonicalPath, animPath, performerPosePath, reader.GetString(3), state,
                reader.IsDBNull(5) ? null : reader.GetString(5), timestamp);
            if (!result.TryGetValue(sourcePath, out var sourceOutputs)) result[sourcePath] = sourceOutputs = [];
            sourceOutputs.Add(output);
        }
        return result.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyList<ConversionOutput>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    public void Replace(string projectRoot, IReadOnlyDictionary<string, IReadOnlyList<ConversionOutput>> outputs)
    {
        var root = NormalizeProjectRoot(projectRoot);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM conversion_outputs WHERE project_root = $root";
            delete.Parameters.AddWithValue("$root", root);
            delete.ExecuteNonQuery();
        }
        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO conversion_outputs(project_root, source_path, canonical_import_path, anim_path,
                    destination_relative_folder, state, error_message, timestamp_utc_ticks)
                VALUES($root, $source, $canonical, $anim, $destination, $state, $error, $timestamp)
                ON CONFLICT(project_root, canonical_import_path) DO UPDATE SET
                    source_path=excluded.source_path, anim_path=excluded.anim_path,
                    destination_relative_folder=excluded.destination_relative_folder, state=excluded.state,
                    error_message=excluded.error_message, timestamp_utc_ticks=excluded.timestamp_utc_ticks
                """;
            foreach (var name in new[] { "$root", "$source", "$canonical", "$anim", "$destination", "$state", "$error", "$timestamp" })
                insert.Parameters.Add(new SqliteParameter(name, DBNull.Value));
            foreach (var (source, sourceOutputs) in outputs)
            foreach (var output in sourceOutputs)
            {
                insert.Parameters["$root"].Value = root;
                insert.Parameters["$source"].Value = Path.GetFullPath(source);
                insert.Parameters["$canonical"].Value = output.CanonicalImportPath.Replace('\\', '/');
                insert.Parameters["$anim"].Value = Path.GetFullPath(output.AnimPath);
                insert.Parameters["$destination"].Value = UnityProjectService.NormalizeDestinationRelativeFolder(output.DestinationRelativeFolder);
                insert.Parameters["$state"].Value = output.State.ToString();
                insert.Parameters["$error"].Value = output.ErrorMessage is null ? DBNull.Value : output.ErrorMessage;
                insert.Parameters["$timestamp"].Value = output.Timestamp?.UtcTicks is { } ticks ? ticks : DBNull.Value;
                insert.ExecuteNonQuery();
            }
        }
        transaction.Commit();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout = 5000;";
        command.ExecuteNonQuery();
        return connection;
    }

    private static string NormalizeProjectRoot(string projectRoot)
    {
        var full = Path.GetFullPath(projectRoot);
        var root = Path.GetPathRoot(full) ?? string.Empty;
        return full.Length > root.Length ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : full;
    }

    private static bool IsWithin(string root, string target)
    {
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return target.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            || string.Equals(target, root, StringComparison.OrdinalIgnoreCase);
    }
}
