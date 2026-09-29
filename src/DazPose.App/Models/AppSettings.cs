namespace DazPose.App.Models;

public sealed class AppSettings
{
    public string DazContentRoot { get; set; } = string.Empty;
    public string FigureDefinitionPath { get; set; } = string.Empty;
    public string UnityProjectRoot { get; set; } = string.Empty;
    public string FinalPoseAssetRoot { get; set; } = "Assets/Animations/DazPoses";
    public string CanonicalImportRoot { get; set; } = "Assets/DazPoseImports";
    public string FinalExpressionAssetRoot { get; set; } = "Assets/Animations/DazExpressions";
    public string ExpressionImportRoot { get; set; } = "Assets/DazExpressionImports";
    public string LastSelectedSourceFolder { get; set; } = string.Empty;
    public bool SearchIncludesChildren { get; set; }
    public bool ShowG8Female { get; set; } = true;
    public bool ShowG81Female { get; set; } = true;
    public bool ShowG8Male { get; set; } = true;
    public bool ShowG81Male { get; set; } = true;
    public bool ShowGenesis9 { get; set; } = true;
    public bool ShowOtherFigures { get; set; } = true;
    public string ConvertedFilter { get; set; } = "All";
    public double WindowWidth { get; set; } = 1440;
    public double WindowHeight { get; set; } = 900;
}

public sealed record PoseLibraryEntry(
    string SourcePath,
    string SourceFolderPath,
    string RelativeFolderPath,
    string ImmediateFolderName,
    string FileStem,
    string DisplayName,
    string AssetId,
    string AssetType,
    string? PreviewImagePath,
    long FileSize,
    long ModifiedUtcTicks)
{
    public string RelativePath { get; init; } = string.Empty;
    public string FigureGeneration { get; init; } = FigureGenerations.Other;
}

public static class FigureGenerations
{
    public const string G8Female = "G8F";
    public const string G81Female = "G8.1F";
    public const string G8Male = "G8M";
    public const string G81Male = "G8.1M";
    public const string Genesis9 = "G9";
    public const string Other = "Other";
}

public enum ConversionFilter
{
    All,
    Unconverted,
    Converted
}

public enum ConversionJobState
{
    Queued,
    Converting,
    AwaitingUnity,
    Converted,
    Failed
}

public enum PerformerAssetKind
{
    Pose = 0,
    Expression = 1
}

public sealed class BrowserJobStatus
{
    public int SchemaVersion { get; set; } = 2;
    public PerformerAssetKind AssetKind { get; set; }
    public string CanonicalImportPath { get; set; } = string.Empty;
    public string SourcePosePath { get; set; } = string.Empty;
    public string DestinationRelativeFolder { get; set; } = string.Empty;
    public string ExpectedAnimPath { get; set; } = string.Empty;
    public string ExpectedPerformerPosePath { get; set; } = string.Empty;
    public string ExpectedWrapperAssetPath { get; set; } = string.Empty;
    public string State { get; set; } = nameof(ConversionJobState.AwaitingUnity);
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? ErrorMessage { get; set; }
}

public sealed record ConversionOutput(PerformerAssetKind AssetKind, string CanonicalImportPath, string AnimPath, string WrapperAssetPath,
    string DestinationRelativeFolder,
    ConversionJobState State, string? ErrorMessage, DateTimeOffset? Timestamp);

public sealed record LibraryScanProgress(int Visited, int Indexed, int Ignored, int Errors, string? CurrentPath);

public sealed record LibraryScanResult(int Visited, int AddedOrUpdated, int Removed, int Ignored, int Errors);

public sealed class FolderNode(string name, string relativePath) : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _childrenLoaded = true;
    private IReadOnlyList<FolderNode> _children = Array.Empty<FolderNode>();
    private Func<IReadOnlyList<FolderNode>>? _childrenLoader;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public string Name { get; } = name;
    public string RelativePath { get; } = relativePath;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            if (value) EnsureChildrenLoaded();
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }
    public IReadOnlyList<FolderNode> Children
    {
        get => _children;
        set
        {
            _children = value;
            _childrenLoaded = true;
            _childrenLoader = null;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Children)));
        }
    }

    public void SetLazyChildren(Func<IReadOnlyList<FolderNode>> loader)
    {
        _childrenLoader = loader ?? throw new ArgumentNullException(nameof(loader));
        _childrenLoaded = false;
        // TreeView needs one child to render an expander before the directory is read.
        _children = [new FolderNode("Loading…", $"{RelativePath}/.loading")];
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Children)));
    }

    private void EnsureChildrenLoaded()
    {
        if (_childrenLoaded) return;
        var loader = _childrenLoader;
        _childrenLoaded = true;
        _childrenLoader = null;
        try { _children = loader?.Invoke() ?? Array.Empty<FolderNode>(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            _children = Array.Empty<FolderNode>();
        }
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Children)));
    }
    public override string ToString() => Name;
}

public sealed record ConversionRequest(string SourcePosePath, PerformerAssetKind Kind, string DestinationRelativeFolder)
{
    public ConversionRequest(string sourcePosePath, string destinationRelativeFolder)
        : this(sourcePosePath, PerformerAssetKind.Pose, destinationRelativeFolder) { }
}

public sealed class ConversionJob : System.ComponentModel.INotifyPropertyChanged
{
    private ConversionJobState _state = ConversionJobState.Queued;
    private string? _errorMessage;
    private string? _canonicalPath;
    private string? _animPath;

    public ConversionJob(ConversionRequest request)
    {
        Request = request;
        QueuedAt = DateTimeOffset.Now;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public ConversionRequest Request { get; }
    public string DisplayName => Path.GetFileNameWithoutExtension(Request.SourcePosePath);
    public string KindLabel => Request.Kind.ToString();
    public DateTimeOffset QueuedAt { get; }
    public ConversionJobState State { get => _state; internal set => Set(ref _state, value); }
    public string? ErrorMessage { get => _errorMessage; internal set => Set(ref _errorMessage, value); }
    public string? CanonicalPath { get => _canonicalPath; internal set => Set(ref _canonicalPath, value); }
    public string? AnimPath { get => _animPath; internal set => Set(ref _animPath, value); }

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
