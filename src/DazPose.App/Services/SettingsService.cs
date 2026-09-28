using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using DazPose.App.Models;

namespace DazPose.App.Services;

public sealed class SettingsService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SaveGates =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SettingsService(string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DazPoseWizard", "settings.json");
    }

    public string SettingsPath { get; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var fullSettingsPath = Path.GetFullPath(SettingsPath);
        var saveGate = SaveGates.GetOrAdd(fullSettingsPath, static _ => new SemaphoreSlim(1, 1));
        await saveGate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(fullSettingsPath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = Path.Combine(directory, $"settings.{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }
                File.Move(temporaryPath, fullSettingsPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
        finally
        {
            saveGate.Release();
        }
    }
}
