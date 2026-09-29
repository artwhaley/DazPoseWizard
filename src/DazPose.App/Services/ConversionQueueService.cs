using System.Collections.Concurrent;
using System.Threading.Channels;
using DazPose.App.Models;
using DazPose.Core;

namespace DazPose.App.Services;

public sealed class ConversionQueueService : IAsyncDisposable
{
    private readonly Func<AppSettings> _settingsProvider;
    private readonly UnityProjectService _projectService;
    private readonly ConversionRegistryService _registry;
    private readonly PoseOutputNamingService _naming;
    private readonly RequiredMorphManifestService _requiredMorphs;
    private readonly Channel<ConversionJob> _channel;
    private readonly ConcurrentDictionary<string, ConversionJob> _activeJobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ConversionJob> _jobs = [];
    private readonly object _jobsLock = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task[] _workers;

    public ConversionQueueService(Func<AppSettings> settingsProvider, UnityProjectService projectService,
        ConversionRegistryService registry, PoseOutputNamingService naming, int concurrency = 2, int capacity = 256,
        RequiredMorphManifestService? requiredMorphs = null)
    {
        _settingsProvider = settingsProvider;
        _projectService = projectService;
        _registry = registry;
        _naming = naming;
        _requiredMorphs = requiredMorphs ?? new RequiredMorphManifestService();
        _channel = Channel.CreateBounded<ConversionJob>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = concurrency == 1,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _workers = Enumerable.Range(0, Math.Clamp(concurrency, 1, 4)).Select(_ => Task.Run(WorkerAsync)).ToArray();
    }

    public event EventHandler<ConversionJob>? JobChanged;
    public event EventHandler? JobsChanged;

    public IReadOnlyList<ConversionJob> Jobs
    {
        get { lock (_jobsLock) return _jobs.ToArray(); }
    }

    public async Task<int> EnqueueAsync(IEnumerable<ConversionRequest> requests, CancellationToken cancellationToken = default)
    {
        var added = 0;
        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = new ConversionRequest(Path.GetFullPath(request.SourcePosePath),
                UnityProjectService.NormalizeDestinationRelativeFolder(request.DestinationRelativeFolder));
            var key = JobKey(normalized);
            if (_activeJobs.ContainsKey(key)) continue;

            var job = new ConversionJob(normalized);
            if (!_activeJobs.TryAdd(key, job)) continue;
            lock (_jobsLock) _jobs.Insert(0, job);
            JobsChanged?.Invoke(this, EventArgs.Empty);
            await _channel.Writer.WriteAsync(job, cancellationToken);
            added++;
        }
        return added;
    }

    public async Task<int> RetryFailedAsync(CancellationToken cancellationToken = default)
    {
        var failed = Jobs.Where(job => job.State == ConversionJobState.Failed)
            .Select(job => new ConversionRequest(job.Request.SourcePosePath, job.Request.DestinationRelativeFolder)).ToArray();
        return await EnqueueAsync(failed, cancellationToken);
    }

    public int ClearCompleted()
    {
        int removed;
        lock (_jobsLock)
        {
            var before = _jobs.Count;
            _jobs.RemoveAll(job => job.State is ConversionJobState.Converted or ConversionJobState.Failed);
            removed = before - _jobs.Count;
        }
        if (removed > 0) JobsChanged?.Invoke(this, EventArgs.Empty);
        return removed;
    }

    public void Reconcile(IReadOnlyDictionary<string, IReadOnlyList<ConversionOutput>> outputs)
    {
        foreach (var job in Jobs.Where(job => job.State is ConversionJobState.AwaitingUnity or ConversionJobState.Converted or ConversionJobState.Failed))
        {
            var hasSource = outputs.TryGetValue(Path.GetFullPath(job.Request.SourcePosePath), out var sourceOutputs);
            var match = hasSource ? sourceOutputs!.FirstOrDefault(output => string.Equals(output.DestinationRelativeFolder,
                job.Request.DestinationRelativeFolder, StringComparison.OrdinalIgnoreCase)) : null;
            if (match is not null)
            {
                job.State = match.State;
                job.ErrorMessage = match.ErrorMessage;
                job.AnimPath = match.AnimPath;
                JobChanged?.Invoke(this, job);
            }
            else if (job.State == ConversionJobState.Converted)
            {
                job.State = ConversionJobState.Failed;
                job.ErrorMessage = "The canonical import and its Unity status record are missing. Queue the pose again to rebuild this destination.";
                JobChanged?.Invoke(this, job);
            }
        }
    }

    private async Task WorkerAsync()
    {
        try
        {
            await foreach (var job in _channel.Reader.ReadAllAsync(_shutdown.Token))
            {
                var key = JobKey(job.Request);
                ResolvedPoseOutput? outputReservation = null;
                try
                {
                    job.State = ConversionJobState.Converting;
                    Notify(job);
                    var settings = _settingsProvider();
                    if (!File.Exists(job.Request.SourcePosePath))
                        throw new FileNotFoundException("The source pose no longer exists in the DAZ library.", job.Request.SourcePosePath);
                    if (string.IsNullOrWhiteSpace(settings.FigureDefinitionPath) || !File.Exists(settings.FigureDefinitionPath))
                        throw new InvalidOperationException("Configure a Genesis 8 Female DAZ figure definition before converting poses.");
                    if (!_projectService.LooksLikeUnityProject(settings.UnityProjectRoot))
                        throw new InvalidOperationException("Configure a valid Unity project before converting poses.");

                    await _projectService.WriteBridgeConfigurationAsync(settings, _shutdown.Token);
                    var output = _naming.Resolve(settings, job.Request.SourcePosePath, job.Request.DestinationRelativeFolder);
                    outputReservation = output;
                    job.CanonicalPath = output.CanonicalPath;
                    job.AnimPath = output.AnimPath;
                    var projectRoot = Path.GetFullPath(settings.UnityProjectRoot);
                    var stagingDirectory = Path.Combine(projectRoot, "Library", "DazPoseWizard", "Staging");
                    Directory.CreateDirectory(stagingDirectory);
                    CleanupOldStagingFiles(stagingDirectory);
                    var stagingPath = Path.Combine(stagingDirectory, $"{Guid.NewGuid():N}.stage");

                    try
                    {
                        var conversion = await Task.Run(() => PoseConversionService.ConvertCanonical(settings.FigureDefinitionPath,
                            job.Request.SourcePosePath, stagingPath), _shutdown.Token);
                        _requiredMorphs.AddCandidateControls(projectRoot, conversion.Pose.ActiveFigureControls,
                            job.Request.SourcePosePath, settings.DazContentRoot);
                        await _registry.WriteStatusAsync(settings, new BrowserJobStatus
                        {
                            CanonicalImportPath = output.CanonicalAssetPath,
                            SourcePosePath = job.Request.SourcePosePath,
                            DestinationRelativeFolder = output.DestinationRelativeFolder,
                            ExpectedAnimPath = output.AnimAssetPath,
                            ExpectedPerformerPosePath = output.PerformerPoseAssetPath,
                            State = nameof(ConversionJobState.AwaitingUnity),
                            Timestamp = DateTimeOffset.UtcNow
                        }, _shutdown.Token);
                        Directory.CreateDirectory(Path.GetDirectoryName(output.CanonicalPath)!);
                        File.Move(stagingPath, output.CanonicalPath, overwrite: true);
                    }
                    finally
                    {
                        if (File.Exists(stagingPath)) File.Delete(stagingPath);
                    }

                    job.State = ConversionJobState.AwaitingUnity;
                    Notify(job);
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
                {
                    job.State = ConversionJobState.Failed;
                    job.ErrorMessage = "Conversion was stopped because the application is closing.";
                    Notify(job);
                }
                catch (Exception ex)
                {
                    job.State = ConversionJobState.Failed;
                    job.ErrorMessage = ex.Message;
                    await WriteFailureStatusIfPossibleAsync(job, ex);
                    Notify(job);
                }
                finally
                {
                    if (outputReservation is not null) _naming.Release(outputReservation);
                    _activeJobs.TryRemove(key, out _);
                    JobsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
    }

    private async Task WriteFailureStatusIfPossibleAsync(ConversionJob job, Exception exception)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(job.CanonicalPath) || string.IsNullOrWhiteSpace(job.AnimPath)) return;
            var settings = _settingsProvider();
            var projectRoot = Path.GetFullPath(settings.UnityProjectRoot);
            var canonicalAsset = Path.GetRelativePath(projectRoot, job.CanonicalPath).Replace('\\', '/');
            var animAsset = Path.GetRelativePath(projectRoot, job.AnimPath).Replace('\\', '/');
            await _registry.WriteStatusAsync(settings, new BrowserJobStatus
            {
                CanonicalImportPath = canonicalAsset,
                SourcePosePath = job.Request.SourcePosePath,
                DestinationRelativeFolder = job.Request.DestinationRelativeFolder,
                ExpectedAnimPath = animAsset,
                ExpectedPerformerPosePath = Path.GetRelativePath(projectRoot,
                    Path.ChangeExtension(job.AnimPath, ".asset")).Replace('\\', '/'),
                State = nameof(ConversionJobState.Failed),
                Timestamp = DateTimeOffset.UtcNow,
                ErrorMessage = exception.Message
            }, _shutdown.Token);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or OperationCanceledException) { }
    }

    private void Notify(ConversionJob job)
    {
        JobChanged?.Invoke(this, job);
        JobsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string JobKey(ConversionRequest request) =>
        Path.GetFullPath(request.SourcePosePath).ToUpperInvariant() + "\n" + request.DestinationRelativeFolder.ToUpperInvariant();

    private static void CleanupOldStagingFiles(string directory)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-3);
            foreach (var file in Directory.EnumerateFiles(directory, "*.stage", SearchOption.TopDirectoryOnly))
            {
                try { if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        _shutdown.Cancel();
        try { await Task.WhenAll(_workers); }
        catch (OperationCanceledException) { }
        _shutdown.Dispose();
    }
}
