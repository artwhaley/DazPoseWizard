using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using DazPose.App.Models;
using DazPose.App.Services;

namespace DazPose.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private const int PageSize = 96;
    private readonly SettingsService _settingsService = new();
    private readonly LibraryIndexService _libraryIndex = new();
    private readonly ConversionRegistryCacheService _registryCache = new();
    private readonly UnityProjectService _projectService = new();
    private readonly RequiredMorphManifestService _requiredMorphs = new();
    private readonly ConversionRegistryService _registry;
    private readonly PoseOutputNamingService _naming;
    private readonly ThumbnailService _thumbnails = new();
    private readonly ConversionQueueService _queue;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly object _registryDebounceLock = new();
    private readonly object _folderIndexLock = new();
    private readonly Dictionary<string, Task> _folderIndexTasks = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _searchDebounce;
    private CancellationTokenSource? _registryDebounce;
    private CancellationTokenSource? _thumbnailCancellation;
    private AppSettings _settings;
    private IReadOnlyList<PoseLibraryEntry> _matchingEntries = Array.Empty<PoseLibraryEntry>();
    private IReadOnlyDictionary<string, IReadOnlyList<ConversionOutput>> _outputs =
        new Dictionary<string, IReadOnlyList<ConversionOutput>>(StringComparer.OrdinalIgnoreCase);
    private FolderNode? _selectedSourceFolder;
    private FolderNode? _selectedDestinationFolder;
    private string _searchText = string.Empty;
    private bool _searchIncludesChildren;
    private bool _showG8Female = true;
    private bool _showG81Female = true;
    private bool _showG8Male = true;
    private bool _showG81Male = true;
    private bool _showGenesis9 = true;
    private bool _showOtherFigures = true;
    private ConversionFilter _conversionFilter;
    private bool _isScanning;
    private bool _isQueueDrawerOpen;
    private string _statusMessage = "Configure the DAZ library and Unity project to begin.";
    private string _scanProgress = string.Empty;
    private bool _hasMorePoses;
    private bool _isEmpty;
    private bool _hasPreviousPoses;
    private int _visibleCount;
    private int _pageOffset;
    private int _filteredCount;
    private int _lastIncrementalRefreshCount;
    private string _sourceTreeRoot = string.Empty;
    private bool _isDisposed;

    public MainWindowViewModel()
    {
        _settings = _settingsService.Load();
        _searchIncludesChildren = _settings.SearchIncludesChildren;
        _showG8Female = _settings.ShowG8Female;
        _showG81Female = _settings.ShowG81Female;
        _showG8Male = _settings.ShowG8Male;
        _showG81Male = _settings.ShowG81Male;
        _showGenesis9 = _settings.ShowGenesis9;
        _showOtherFigures = _settings.ShowOtherFigures;
        _conversionFilter = Enum.TryParse<ConversionFilter>(_settings.ConvertedFilter, true, out var filter) ? filter : ConversionFilter.All;
        _registry = new ConversionRegistryService(_projectService);
        _naming = new PoseOutputNamingService(_projectService);
        _queue = new ConversionQueueService(() => _settings, _projectService, _registry, _naming,
            requiredMorphs: _requiredMorphs);
        _queue.JobChanged += QueueJobChanged;
        _queue.JobsChanged += QueueJobsChanged;
        QueueJobs = new ObservableCollection<ConversionJob>();
        RefreshQueueJobs();
        ResetWatchers();
        _ = InitializeAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<FolderNode> SourceFolders { get; } = [];
    public ObservableCollection<FolderNode> DestinationFolders { get; } = [];
    public ObservableCollection<PoseCardViewModel> PoseCards { get; } = [];
    public ObservableCollection<ConversionJob> QueueJobs { get; }
    public AppSettings Settings => _settings;
    public DazMorphExportRulesResult GenerateDazMorphExportRules()
    {
        if (!_projectService.LooksLikeUnityProject(_settings.UnityProjectRoot))
            throw new InvalidOperationException("Configure a valid Unity project before generating DAZ Morph Export Rules.");
        return _requiredMorphs.GenerateExportRules(_settings.UnityProjectRoot);
    }
    public IReadOnlyList<RequiredMorphManifestItem> GetAlwaysExportMorphEntries()
    {
        if (!_projectService.LooksLikeUnityProject(_settings.UnityProjectRoot))
            throw new InvalidOperationException("Configure a valid Unity project before managing Always-Export Morphs.");
        return _requiredMorphs.GetAlwaysExportEntries(_settings.UnityProjectRoot);
    }
    public void SaveAlwaysExportMorphEntries(IEnumerable<RequiredMorphManifestItem> entries)
    {
        if (!_projectService.LooksLikeUnityProject(_settings.UnityProjectRoot))
            throw new InvalidOperationException("Configure a valid Unity project before managing Always-Export Morphs.");
        _requiredMorphs.SaveAlwaysExportEntries(_settings.UnityProjectRoot, entries);
    }
    public string GetMorphExportRulesDirectory()
    {
        if (!_projectService.LooksLikeUnityProject(_settings.UnityProjectRoot))
            throw new InvalidOperationException("Configure a valid Unity project before opening the DAZ Morph Export Rules folder.");
        return _requiredMorphs.GetProjectDirectory(_settings.UnityProjectRoot);
    }
    public FolderNode? SelectedSourceFolder { get => _selectedSourceFolder; set { if (value is not null && !ReferenceEquals(value, _selectedSourceFolder)) SelectSourceFolder(value); } }
    public FolderNode? SelectedDestinationFolder { get => _selectedDestinationFolder; set { if (value is not null && !ReferenceEquals(value, _selectedDestinationFolder)) SelectDestinationFolder(value); } }
    public string SearchText { get => _searchText; set { if (Set(ref _searchText, value)) { OnPropertyChanged(nameof(EmptyMessage)); DebounceSearch(); } } }
    public bool SearchIncludesChildren { get => _searchIncludesChildren; set { if (Set(ref _searchIncludesChildren, value)) { _settings.SearchIncludesChildren = value; OnPropertyChanged(nameof(IsCurrentFolderScope)); OnPropertyChanged(nameof(IsIncludeChildrenScope)); OnPropertyChanged(nameof(EmptyMessage)); _ = PersistSettingsAsync(); _ = RefreshCardsAsync(); } } }
    public bool IsCurrentFolderScope { get => !SearchIncludesChildren; set { if (value) SearchIncludesChildren = false; } }
    public bool IsIncludeChildrenScope { get => SearchIncludesChildren; set { if (value) SearchIncludesChildren = true; } }
    public bool ShowG8Female { get => _showG8Female; set { if (Set(ref _showG8Female, value)) { _settings.ShowG8Female = value; FigureFilterChanged(); } } }
    public bool ShowG81Female { get => _showG81Female; set { if (Set(ref _showG81Female, value)) { _settings.ShowG81Female = value; FigureFilterChanged(); } } }
    public bool ShowG8Male { get => _showG8Male; set { if (Set(ref _showG8Male, value)) { _settings.ShowG8Male = value; FigureFilterChanged(); } } }
    public bool ShowG81Male { get => _showG81Male; set { if (Set(ref _showG81Male, value)) { _settings.ShowG81Male = value; FigureFilterChanged(); } } }
    public bool ShowGenesis9 { get => _showGenesis9; set { if (Set(ref _showGenesis9, value)) { _settings.ShowGenesis9 = value; FigureFilterChanged(); } } }
    public bool ShowOtherFigures { get => _showOtherFigures; set { if (Set(ref _showOtherFigures, value)) { _settings.ShowOtherFigures = value; FigureFilterChanged(); } } }
    public ConversionFilter ConversionFilter { get => _conversionFilter; set { if (Set(ref _conversionFilter, value)) { _settings.ConvertedFilter = value.ToString(); _pageOffset = 0; _ = PersistSettingsAsync(); _ = ApplyCurrentPageAsync(); } } }
    public bool IsScanning { get => _isScanning; private set { if (Set(ref _isScanning, value)) { OnPropertyChanged(nameof(RefreshButtonText)); OnPropertyChanged(nameof(EmptyMessage)); } } }
    public bool IsQueueDrawerOpen { get => _isQueueDrawerOpen; set => Set(ref _isQueueDrawerOpen, value); }
    public string StatusMessage { get => _statusMessage; private set => Set(ref _statusMessage, value); }
    public string ScanProgress { get => _scanProgress; private set => Set(ref _scanProgress, value); }
    public bool HasMorePoses { get => _hasMorePoses; private set => Set(ref _hasMorePoses, value); }
    public bool IsEmpty { get => _isEmpty; private set => Set(ref _isEmpty, value); }
    public bool HasPreviousPoses { get => _hasPreviousPoses; private set => Set(ref _hasPreviousPoses, value); }
    public int VisibleCount { get => _visibleCount; private set => Set(ref _visibleCount, value); }
    public string PageSummary => _filteredCount == 0 ? "0 poses" : $"{_pageOffset + 1}–{Math.Min(_pageOffset + VisibleCount, _filteredCount)} of {_filteredCount}";
    public string CurrentSourceFolderLabel => SelectedSourceFolder?.RelativePath is { Length: > 0 } path ? path : "DAZ Library";
    public string CurrentDestinationLabel => SelectedDestinationFolder?.RelativePath is { Length: > 0 } path ? path : "Daz Poses";
    public string RefreshButtonText => IsScanning ? "Scanning…" : "Refresh library";
    public string EmptyMessage => IsScanning
        ? "Scanning this library now. Pose cards appear as each batch is indexed."
        : !SearchIncludesChildren
            ? "No poses are directly in this folder. Expand the folder tree, or choose Include Children."
            : string.IsNullOrWhiteSpace(SearchText)
                ? "No DAZ pose or shape presets were found in this folder or its children."
                : "No DAZ presets match this folder and search.";
    public string QueueSummary
    {
        get
        {
            var jobs = _queue.Jobs;
            var converting = jobs.Count(job => job.State == ConversionJobState.Converting);
            var awaiting = jobs.Count(job => job.State == ConversionJobState.AwaitingUnity);
            var failed = jobs.Count(job => job.State == ConversionJobState.Failed);
            var complete = _outputs.Values.SelectMany(value => value).Count(output => output.State == ConversionJobState.Converted);
            return $"{jobs.Count(job => job.State == ConversionJobState.Queued)} queued • {converting} converting • {awaiting} awaiting Unity • {complete} complete • {failed} failed";
        }
    }

    public bool IsConfigured => Directory.Exists(_settings.DazContentRoot)
        && File.Exists(_settings.FigureDefinitionPath)
        && _projectService.LooksLikeUnityProject(_settings.UnityProjectRoot);

    public void SelectSourceFolder(FolderNode? node)
    {
        if (node is null) return;
        if (Set(ref _selectedSourceFolder, node, nameof(SelectedSourceFolder)))
        {
            OnPropertyChanged(nameof(CurrentSourceFolderLabel));
            OnPropertyChanged(nameof(EmptyMessage));
        }
        _settings.LastSelectedSourceFolder = node.RelativePath;
        _ = PersistSettingsAsync();
        _ = RefreshCardsAsync();
        _ = EnsureSourceFolderIndexedAsync(node.RelativePath);
    }

    public void SelectDestinationFolder(FolderNode? node)
    {
        if (node is null) return;
        if (Set(ref _selectedDestinationFolder, node, nameof(SelectedDestinationFolder)))
            OnPropertyChanged(nameof(CurrentDestinationLabel));
    }

    public async Task ApplySettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings.FinalPoseAssetRoot = _projectService.NormalizeAssetRelativePath(settings.FinalPoseAssetRoot);
        settings.CanonicalImportRoot = _projectService.NormalizeAssetRelativePath(settings.CanonicalImportRoot);
        _projectService.ValidateAssetRoots(settings.FinalPoseAssetRoot, settings.CanonicalImportRoot);
        _settings = settings;
        _searchIncludesChildren = settings.SearchIncludesChildren;
        _showG8Female = settings.ShowG8Female;
        _showG81Female = settings.ShowG81Female;
        _showG8Male = settings.ShowG8Male;
        _showG81Male = settings.ShowG81Male;
        _showGenesis9 = settings.ShowGenesis9;
        _showOtherFigures = settings.ShowOtherFigures;
        await _settingsService.SaveAsync(_settings, cancellationToken);
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(IsConfigured));
        OnPropertyChanged(nameof(IsCurrentFolderScope));
        OnPropertyChanged(nameof(IsIncludeChildrenScope));
        OnPropertyChanged(nameof(ShowG8Female));
        OnPropertyChanged(nameof(ShowG81Female));
        OnPropertyChanged(nameof(ShowG8Male));
        OnPropertyChanged(nameof(ShowG81Male));
        OnPropertyChanged(nameof(ShowGenesis9));
        OnPropertyChanged(nameof(ShowOtherFigures));

        _outputs = _projectService.LooksLikeUnityProject(_settings.UnityProjectRoot)
            ? _registryCache.Load(_settings.UnityProjectRoot)
            : new Dictionary<string, IReadOnlyList<ConversionOutput>>(StringComparer.OrdinalIgnoreCase);
        ResetWatchers();

        if (_projectService.LooksLikeUnityProject(_settings.UnityProjectRoot))
        {
            await _projectService.WriteBridgeConfigurationAsync(_settings, cancellationToken);
            LoadDestinationTree();
            ResetWatchers();
        }
        await LoadCachedLibraryAsync(cancellationToken);
        await RefreshRegistryAsync(cancellationToken);
        StatusMessage = IsConfigured ? "Settings saved. The DAZ library is ready to browse." : "Settings saved. Review the library, figure, and Unity project paths.";
    }

    public async Task RefreshLibraryAsync(CancellationToken cancellationToken = default)
    {
        if (IsScanning) return;
        if (!Directory.Exists(_settings.DazContentRoot))
        {
            StatusMessage = "Choose a valid DAZ content root in Settings.";
            return;
        }

        IsScanning = true;
        ScanProgress = "Scanning for pose and shape presets…";
        try
        {
            var progress = new Progress<LibraryScanProgress>(value =>
            {
                ScanProgress = $"Scanned {value.Visited:N0} DUF files • indexed {value.Indexed:N0} poses • {value.Errors:N0} read errors";
                if (value.Indexed >= _lastIncrementalRefreshCount + 64 || value.CurrentPath is null)
                {
                    _lastIncrementalRefreshCount = value.Indexed;
                    _ = RefreshCardsAsync(cancellationToken);
                }
            });
            _lastIncrementalRefreshCount = 0;
            var result = await _libraryIndex.ScanAsync(_settings.DazContentRoot, progress, cancellationToken);
            await LoadCachedLibraryAsync(cancellationToken);
            StatusMessage = $"Library refreshed: {result.AddedOrUpdated:N0} added or updated, {result.Removed:N0} removed, {result.Ignored:N0} unsupported presets skipped, {result.Errors:N0} read errors.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            StatusMessage = $"Could not refresh the DAZ library: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            ScanProgress = string.Empty;
        }
    }

    public async Task RefreshEverythingAsync(CancellationToken cancellationToken = default)
    {
        if (_projectService.LooksLikeUnityProject(_settings.UnityProjectRoot))
        {
            await _projectService.WriteBridgeConfigurationAsync(_settings, cancellationToken);
            ResetWatchers();
        }
        LoadDestinationTree();
        await RefreshLibraryAsync(cancellationToken);
        await RefreshRegistryAsync(cancellationToken);
        StatusMessage = "Library and Unity output status reconciled.";
    }

    public async Task<string> CreateDestinationFolderAsync(string name, FolderNode? parent, CancellationToken cancellationToken = default)
    {
        if (!_projectService.LooksLikeUnityProject(_settings.UnityProjectRoot))
            throw new InvalidOperationException("Configure a Unity project first.");
        var relative = await _projectService.CreateDestinationFolderAsync(_settings, parent?.RelativePath ?? string.Empty, name, cancellationToken);
        LoadDestinationTree();
        var node = FindFolder(DestinationFolders, relative);
        SelectDestinationFolder(node);
        StatusMessage = $"Created {relative} under both the final pose root and canonical import root.";
        return relative;
    }

    public async Task<int> EnqueueAsync(IEnumerable<PoseCardViewModel> cards, FolderNode? destination,
        CancellationToken cancellationToken = default)
    {
        var requests = cards.Select(card => new ConversionRequest(card.Entry.SourcePath, destination?.RelativePath ?? string.Empty)).ToArray();
        var count = await _queue.EnqueueAsync(requests, cancellationToken);
        var destinationName = destination?.RelativePath is { Length: > 0 } relative ? relative : "Daz Poses";
        StatusMessage = count == 0 ? "Those poses are already queued for this destination." : $"{count} pose{(count == 1 ? "" : "s")} queued for {destinationName}.";
        return count;
    }

    public IReadOnlyList<PoseCardViewModel> ResolveSelection(IEnumerable<object> selectedItems) =>
        selectedItems.OfType<PoseCardViewModel>().ToArray();

    public void ReportStatus(string message) => StatusMessage = message;

    public async Task SaveWindowBoundsAsync(double width, double height)
    {
        _settings.WindowWidth = width;
        _settings.WindowHeight = height;
        await PersistSettingsAsync();
    }

    public async Task LoadMoreAsync()
    {
        if (_pageOffset + PageSize < _filteredCount) _pageOffset += PageSize;
        await ApplyCurrentPageAsync();
    }

    public async Task LoadPreviousPageAsync()
    {
        _pageOffset = Math.Max(0, _pageOffset - PageSize);
        await ApplyCurrentPageAsync();
    }

    public async Task RetryFailedAsync() =>
        StatusMessage = $"{await _queue.RetryFailedAsync()} failed conversion(s) queued for retry.";

    public void ClearCompleted()
    {
        var count = _queue.ClearCompleted();
        StatusMessage = $"Cleared {count} completed or failed queue item(s).";
    }

    public async Task SetQueueDrawerOpenAsync(bool open)
    {
        IsQueueDrawerOpen = open;
        if (open) await RefreshRegistryAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            if (_projectService.LooksLikeUnityProject(_settings.UnityProjectRoot))
                _outputs = _registryCache.Load(_settings.UnityProjectRoot);
            await LoadCachedLibraryAsync();
            if (_projectService.LooksLikeUnityProject(_settings.UnityProjectRoot))
            {
                await _projectService.WriteBridgeConfigurationAsync(_settings);
                LoadDestinationTree();
                ResetWatchers();
            }
            await RefreshRegistryAsync();
            if (string.IsNullOrWhiteSpace(_settings.DazContentRoot)) StatusMessage = "Open Settings to choose your DAZ content root, figure definition, and Unity project.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Startup recovery: {ex.Message}";
        }
    }

    private async Task LoadCachedLibraryAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_settings.DazContentRoot)) return;
        BuildSourceTree();
        var lastFolder = ExpandToFolder(SourceFolders.FirstOrDefault(), _settings.LastSelectedSourceFolder);
        SelectSourceFolder(lastFolder ?? SourceFolders.FirstOrDefault());
        await RefreshCardsAsync(cancellationToken);
    }

    private async Task RefreshCardsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.DazContentRoot))
        {
            _matchingEntries = Array.Empty<PoseLibraryEntry>();
            await ApplyCurrentPageAsync();
            return;
        }
        try
        {
            _matchingEntries = await _libraryIndex.SearchAsync(_settings.DazContentRoot,
                SelectedSourceFolder?.RelativePath ?? _settings.LastSelectedSourceFolder,
                SearchIncludesChildren, SearchText, cancellationToken);
            _pageOffset = 0;
            await ApplyCurrentPageAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusMessage = $"Could not search the DAZ preset index: {ex.Message}";
        }
    }

    private Task ApplyCurrentPageAsync()
    {
        var filtered = _matchingEntries.Where(entry =>
        {
            if (!IsFigureVisible(entry.FigureGeneration)) return false;
            var converted = _outputs.TryGetValue(entry.SourcePath, out var outputs)
                && outputs.Any(output => output.State == ConversionJobState.Converted);
            return ConversionFilter switch
            {
                ConversionFilter.Converted => converted,
                ConversionFilter.Unconverted => !converted,
                _ => true
            };
        }).ToArray();
        _filteredCount = filtered.Length;
        _pageOffset = Math.Clamp(_pageOffset, 0, filtered.Length == 0 ? 0 : ((filtered.Length - 1) / PageSize) * PageSize);
        var start = _pageOffset;
        var end = Math.Min(filtered.Length, start + PageSize);
        PoseCards.Clear();
        for (var i = start; i < end; i++)
        {
            var card = new PoseCardViewModel(filtered[i]);
            ApplyStatus(card);
            PoseCards.Add(card);
        }
        VisibleCount = PoseCards.Count;
        HasMorePoses = end < filtered.Length;
        HasPreviousPoses = start > 0;
        IsEmpty = filtered.Length == 0;
        OnPropertyChanged(nameof(CurrentSourceFolderLabel));
        OnPropertyChanged(nameof(PageSummary));
        LoadThumbnails(PoseCards.ToArray());
        return Task.CompletedTask;
    }

    private bool IsFigureVisible(string figureGeneration) => figureGeneration switch
    {
        FigureGenerations.G8Female => ShowG8Female,
        FigureGenerations.G81Female => ShowG81Female,
        FigureGenerations.G8Male => ShowG8Male,
        FigureGenerations.G81Male => ShowG81Male,
        FigureGenerations.Genesis9 => ShowGenesis9,
        _ => ShowOtherFigures
    };

    private void FigureFilterChanged()
    {
        _pageOffset = 0;
        _ = PersistSettingsAsync();
        _ = ApplyCurrentPageAsync();
    }

    private void LoadThumbnails(IReadOnlyList<PoseCardViewModel> cards)
    {
        _thumbnailCancellation?.Cancel();
        _thumbnailCancellation?.Dispose();
        _thumbnailCancellation = new CancellationTokenSource();
        var token = _thumbnailCancellation.Token;
        foreach (var card in cards.Where(card => card.Entry.PreviewImagePath is not null))
            _ = LoadThumbnailAsync(card, token);
    }

    private async Task LoadThumbnailAsync(PoseCardViewModel card, CancellationToken cancellationToken)
    {
        try
        {
            var image = await _thumbnails.LoadAsync(card.Entry.PreviewImagePath, cancellationToken);
            if (image is null || cancellationToken.IsCancellationRequested) return;
            await Dispatcher.UIThread.InvokeAsync(() => card.PreviewImage = image);
        }
        catch (OperationCanceledException) { }
    }

    private void ApplyStatus(PoseCardViewModel card)
    {
        if (_outputs.TryGetValue(card.Entry.SourcePath, out var outputs)) card.Outputs = outputs;
        var latestJob = _queue.Jobs.FirstOrDefault(job => string.Equals(job.Request.SourcePosePath,
            card.Entry.SourcePath, StringComparison.OrdinalIgnoreCase));
        if (latestJob is not null)
        {
            card.State = latestJob.State;
            card.ErrorMessage = latestJob.ErrorMessage;
        }
        else if (outputs is not null && outputs.Count > 0)
        {
            var latestOutput = outputs.OrderByDescending(output => output.Timestamp).First();
            card.State = latestOutput.State;
            card.ErrorMessage = latestOutput.ErrorMessage;
        }
    }

    private async Task RefreshRegistryAsync(CancellationToken cancellationToken = default)
    {
        if (!_projectService.LooksLikeUnityProject(_settings.UnityProjectRoot))
        {
            _outputs = new Dictionary<string, IReadOnlyList<ConversionOutput>>(StringComparer.OrdinalIgnoreCase);
            UpdateCardsStatus();
            return;
        }
        try
        {
            var reconciled = await Task.Run(() => _registry.Reconcile(_settings), cancellationToken);
            _outputs = reconciled;
            _queue.Reconcile(_outputs);
            UpdateCardsStatus();
            OnPropertyChanged(nameof(QueueSummary));
            try { await Task.Run(() => _registryCache.Replace(_settings.UnityProjectRoot, reconciled), cancellationToken); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
            {
                StatusMessage = $"Unity status was refreshed, but the local registry cache could not be updated: {ex.Message}";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            StatusMessage = $"Could not reconcile Unity assets: {ex.Message}";
        }
    }

    private void UpdateCardsStatus()
    {
        foreach (var card in PoseCards) ApplyStatus(card);
        if (ConversionFilter != ConversionFilter.All) _ = ApplyCurrentPageAsync();
    }

    private void BuildSourceTree()
    {
        var rootPath = Path.GetFullPath(_settings.DazContentRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (SourceFolders.Count > 0 && string.Equals(_sourceTreeRoot, rootPath, StringComparison.OrdinalIgnoreCase)) return;
        SourceFolders.Clear();
        _sourceTreeRoot = rootPath;
        var rootNode = CreateSourceFolderNode(rootPath, rootPath);
        SourceFolders.Add(rootNode);
        rootNode.IsExpanded = true;
    }

    private FolderNode CreateSourceFolderNode(string rootPath, string folderPath)
    {
        var relative = Path.GetRelativePath(rootPath, folderPath).Replace('\\', '/');
        if (relative == ".") relative = string.Empty;
        var name = relative.Length == 0
            ? Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : Path.GetFileName(folderPath);
        var node = new FolderNode(name, relative);
        node.SetLazyChildren(() =>
        {
            _ = EnsureSourceFolderIndexedAsync(relative);
            return EnumerateSourceChildren(rootPath, folderPath);
        });
        return node;
    }

    private IReadOnlyList<FolderNode> EnumerateSourceChildren(string rootPath, string folderPath)
    {
        try
        {
            return Directory.EnumerateDirectories(folderPath)
                .Where(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Select(path => CreateSourceFolderNode(rootPath, path))
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return Array.Empty<FolderNode>();
        }
    }

    private Task EnsureSourceFolderIndexedAsync(string? relativeFolder, bool force = false)
    {
        var folder = (relativeFolder ?? string.Empty).Replace('\\', '/').Trim('/');
        var key = force ? $"{folder}|force" : folder;
        Task task;
        lock (_folderIndexLock)
        {
            if (_folderIndexTasks.TryGetValue(key, out var existing)) return existing;
            task = IndexSourceFolderCoreAsync(folder, force);
            _folderIndexTasks[key] = task;
        }
        _ = task.ContinueWith(_ =>
        {
            lock (_folderIndexLock)
                if (_folderIndexTasks.TryGetValue(key, out var current) && ReferenceEquals(current, task))
                    _folderIndexTasks.Remove(key);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return task;
    }

    private async Task IndexSourceFolderCoreAsync(string relativeFolder, bool force)
    {
        if (!Directory.Exists(_settings.DazContentRoot)) return;
        try
        {
            var result = await _libraryIndex.ScanFolderAsync(_settings.DazContentRoot, relativeFolder, force);
            var selectedFolder = SelectedSourceFolder?.RelativePath ?? string.Empty;
            var affectsSelection = string.Equals(selectedFolder, relativeFolder, StringComparison.OrdinalIgnoreCase)
                || SearchIncludesChildren && IsSameOrDescendant(relativeFolder, selectedFolder);
            if (affectsSelection) await RefreshCardsAsync();
            if (result.Visited > 0 && string.Equals(selectedFolder, relativeFolder, StringComparison.OrdinalIgnoreCase))
            {
                var poseCount = Math.Max(0, result.Visited - result.Ignored - result.Errors);
                StatusMessage = $"Indexed {poseCount:N0} pose{(poseCount == 1 ? "" : "s")} in {CurrentSourceFolderLabel}; "
                    + $"{result.Ignored:N0} non-pose files skipped, {result.Errors:N0} read errors.";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            if (string.Equals(SelectedSourceFolder?.RelativePath ?? string.Empty, relativeFolder, StringComparison.OrdinalIgnoreCase))
                StatusMessage = $"Could not index {CurrentSourceFolderLabel}: {ex.Message}";
        }
    }

    private static bool IsSameOrDescendant(string candidate, string parent)
    {
        if (parent.Length == 0) return true;
        return string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static FolderNode? ExpandToFolder(FolderNode? root, string? relativePath)
    {
        if (root is null || string.IsNullOrWhiteSpace(relativePath)) return root;
        var current = root;
        var accumulated = string.Empty;
        foreach (var segment in relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current.IsExpanded = true;
            accumulated = accumulated.Length == 0 ? segment : $"{accumulated}/{segment}";
            var child = current.Children.FirstOrDefault(node =>
                string.Equals(node.RelativePath, accumulated, StringComparison.OrdinalIgnoreCase));
            if (child is null) return root;
            current = child;
        }
        return current;
    }

    private void LoadDestinationTree()
    {
        DestinationFolders.Clear();
        var rootName = Path.GetFileName(_settings.FinalPoseAssetRoot.TrimEnd('/'));
        var root = new FolderNode(string.IsNullOrWhiteSpace(rootName) ? "Daz Poses" : rootName, string.Empty)
        {
            Children = _projectService.LoadDestinationTree(_settings)
        };
        DestinationFolders.Add(root);
        var selected = SelectedDestinationFolder is not null
            ? FindFolder(DestinationFolders, SelectedDestinationFolder.RelativePath)
            : null;
        SelectDestinationFolder(selected ?? root);
    }

    private static FolderNode? FindFolder(IEnumerable<FolderNode> nodes, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return nodes.FirstOrDefault();
        foreach (var node in nodes)
        {
            if (string.Equals(node.RelativePath, path, StringComparison.OrdinalIgnoreCase)) return node;
            var nested = FindFolder(node.Children, path);
            if (nested is not null) return nested;
        }
        return null;
    }

    private void DebounceSearch()
    {
        _searchDebounce?.Cancel();
        _searchDebounce?.Dispose();
        _searchDebounce = new CancellationTokenSource();
        var token = _searchDebounce.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(220, token);
                await Dispatcher.UIThread.InvokeAsync(() => RefreshCardsAsync(token));
            }
            catch (OperationCanceledException) { }
        });
    }

    private void ResetWatchers()
    {
        foreach (var watcher in _watchers) watcher.Dispose();
        _watchers.Clear();
        if (!_projectService.LooksLikeUnityProject(_settings.UnityProjectRoot)) return;
        var paths = new[]
        {
            _registry.GetStatusDirectory(_settings),
            _projectService.ResolveAssetPath(_settings.UnityProjectRoot, _settings.CanonicalImportRoot),
            _projectService.ResolveAssetPath(_settings.UnityProjectRoot, _settings.FinalPoseAssetRoot)
        };
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(path)) continue;
            try
            {
                var watcher = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName
                };
                watcher.Changed += OnUnityFilesChanged;
                watcher.Created += OnUnityFilesChanged;
                watcher.Deleted += OnUnityFilesChanged;
                watcher.Renamed += OnUnityFilesRenamed;
                watcher.Error += OnWatcherError;
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
    }

    private void OnUnityFilesChanged(object sender, FileSystemEventArgs e) => ScheduleRegistryRefresh();
    private void OnUnityFilesRenamed(object sender, RenamedEventArgs e) => ScheduleRegistryRefresh();
    private void OnWatcherError(object sender, ErrorEventArgs e) => ScheduleRegistryRefresh();

    private void ScheduleRegistryRefresh()
    {
        CancellationToken token;
        lock (_registryDebounceLock)
        {
            if (_isDisposed) return;
            _registryDebounce?.Cancel();
            _registryDebounce?.Dispose();
            _registryDebounce = new CancellationTokenSource();
            token = _registryDebounce.Token;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token);
                await Dispatcher.UIThread.InvokeAsync(() => RefreshRegistryAsync(token));
            }
            catch (OperationCanceledException) { }
        });
    }

    private void QueueJobChanged(object? sender, ConversionJob job) => Dispatcher.UIThread.Post(() =>
    {
        RefreshQueueJobs();
        UpdateCardsStatus();
        OnPropertyChanged(nameof(QueueSummary));
    });

    private void QueueJobsChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        RefreshQueueJobs();
        OnPropertyChanged(nameof(QueueSummary));
    });

    private void RefreshQueueJobs()
    {
        var jobs = _queue?.Jobs ?? Array.Empty<ConversionJob>();
        QueueJobs.Clear();
        foreach (var job in jobs) QueueJobs.Add(job);
    }

    private async Task PersistSettingsAsync()
    {
        try { await _settingsService.SaveAsync(_settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusMessage = $"Could not save settings: {ex.Message}"; }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public async ValueTask DisposeAsync()
    {
        lock (_registryDebounceLock) _isDisposed = true;
        _searchDebounce?.Cancel();
        lock (_registryDebounceLock) _registryDebounce?.Cancel();
        _thumbnailCancellation?.Cancel();
        foreach (var watcher in _watchers) watcher.Dispose();
        _thumbnails.Dispose();
        await _queue.DisposeAsync();
    }
}
