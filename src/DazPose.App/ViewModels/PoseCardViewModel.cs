using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DazPose.App.Models;

namespace DazPose.App.ViewModels;

public sealed class PoseCardViewModel : INotifyPropertyChanged
{
    private Bitmap? _previewImage;
    private ConversionJobState? _state;
    private IReadOnlyList<ConversionOutput> _outputs = Array.Empty<ConversionOutput>();
    private string? _errorMessage;

    public PoseCardViewModel(PoseLibraryEntry entry) => Entry = entry;
    public event PropertyChangedEventHandler? PropertyChanged;
    public PoseLibraryEntry Entry { get; }
    public string Title => string.IsNullOrWhiteSpace(Entry.DisplayName) ? Entry.FileStem : Entry.DisplayName;
    public string AssetTypeLabel => string.Equals(Entry.AssetType, "preset_shape", StringComparison.OrdinalIgnoreCase)
        ? "DAZ SHAPE" : "DAZ POSE";
    public Bitmap? PreviewImage { get => _previewImage; set { if (Set(ref _previewImage, value)) OnPropertyChanged(nameof(HasNoPreview)); } }
    public bool HasNoPreview => PreviewImage is null;
    public ConversionJobState? State { get => _state; set { if (Set(ref _state, value)) { OnPropertyChanged(nameof(StateLabel)); OnPropertyChanged(nameof(StatusBrush)); OnPropertyChanged(nameof(IsConverted)); OnPropertyChanged(nameof(ThumbnailOpacity)); OnPropertyChanged(nameof(StatusToolTip)); } } }
    public string? ErrorMessage { get => _errorMessage; set { if (Set(ref _errorMessage, value)) OnPropertyChanged(nameof(StatusToolTip)); } }
    public IReadOnlyList<ConversionOutput> Outputs { get => _outputs; set { if (Set(ref _outputs, value)) { OnPropertyChanged(nameof(StateLabel)); OnPropertyChanged(nameof(StatusBrush)); OnPropertyChanged(nameof(IsConverted)); OnPropertyChanged(nameof(ThumbnailOpacity)); OnPropertyChanged(nameof(StatusToolTip)); } } }
    public bool IsConverted => Outputs.Any(output => output.State == ConversionJobState.Converted);
    public double ThumbnailOpacity => IsConverted ? 0.72 : 1.0;
    public string StateLabel => IsConverted ? "✓ Converted" : State switch
    {
        ConversionJobState.Queued => "Queued",
        ConversionJobState.Converting => "Converting",
        ConversionJobState.AwaitingUnity => "Awaiting Unity",
        ConversionJobState.Failed => "⚠ Failed",
        _ => ""
    };
    public IBrush StatusBrush => IsConverted ? Brushes.LightGreen : State switch
    {
        ConversionJobState.Failed => Brushes.IndianRed,
        ConversionJobState.AwaitingUnity => Brushes.LightSkyBlue,
        ConversionJobState.Converting => Brushes.Gold,
        ConversionJobState.Queued => Brushes.Gold,
        _ => Brushes.Transparent
    };
    public string StatusToolTip
    {
        get
        {
            if (IsConverted)
                return "Converted to:" + Environment.NewLine + string.Join(Environment.NewLine, Outputs.Where(output => output.State == ConversionJobState.Converted).Select(output =>
                    $"{output.DestinationRelativeFolder}/{Path.GetFileName(output.AnimPath)}"));
            if (State == ConversionJobState.Failed) return ErrorMessage ?? "Conversion failed.";
            return StateLabel;
        }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
