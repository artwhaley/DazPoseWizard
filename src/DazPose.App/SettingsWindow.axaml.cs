using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DazPose.App.Models;
using DazPose.App.Services;

namespace DazPose.App;

public sealed partial class SettingsWindow : Window
{
    private readonly AppSettings _initialSettings;
    private readonly SettingsService _settingsService = new();
    private readonly UnityProjectService _projectService = new();

    public SettingsWindow() : this(new AppSettings()) { }

    public SettingsWindow(AppSettings settings)
    {
        _initialSettings = settings;
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        this.FindControl<TextBox>("DazRootBox")!.Text = settings.DazContentRoot;
        this.FindControl<TextBox>("FigureDefinitionBox")!.Text = settings.FigureDefinitionPath;
        this.FindControl<TextBox>("UnityProjectBox")!.Text = settings.UnityProjectRoot;
        this.FindControl<TextBox>("OutputRootBox")!.Text = settings.FinalPoseAssetRoot;
        this.FindControl<TextBox>("ImportRootBox")!.Text = settings.CanonicalImportRoot;
    }

    private async void BrowseDazRoot(object? sender, RoutedEventArgs e) => await PickFolder("Select DAZ Content Root", "DazRootBox");

    private async void BrowseUnityProject(object? sender, RoutedEventArgs e) => await PickFolder("Select Unity Project Root", "UnityProjectBox");

    private async void BrowseFigureDefinition(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Genesis 8 Female Figure Definition",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("DSON Support File (*.dsf)") { Patterns = ["*.dsf"] }, FilePickerFileTypes.All]
            });
            if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
                this.FindControl<TextBox>("FigureDefinitionBox")!.Text = path;
        }
        catch (Exception ex) { SetError($"Could not open the figure file picker: {ex.Message}"); }
    }

    private async Task PickFolder(string title, string targetName)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
            if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
                this.FindControl<TextBox>(targetName)!.Text = path;
        }
        catch (Exception ex) { SetError($"Could not open the folder picker: {ex.Message}"); }
    }

    private async void SaveClicked(object? sender, RoutedEventArgs e)
    {
        var settings = new AppSettings
        {
            DazContentRoot = this.FindControl<TextBox>("DazRootBox")!.Text?.Trim() ?? string.Empty,
            FigureDefinitionPath = this.FindControl<TextBox>("FigureDefinitionBox")!.Text?.Trim() ?? string.Empty,
            UnityProjectRoot = this.FindControl<TextBox>("UnityProjectBox")!.Text?.Trim() ?? string.Empty,
            FinalPoseAssetRoot = this.FindControl<TextBox>("OutputRootBox")!.Text?.Trim() ?? string.Empty,
            CanonicalImportRoot = this.FindControl<TextBox>("ImportRootBox")!.Text?.Trim() ?? string.Empty,
            LastSelectedSourceFolder = _initialSettings.LastSelectedSourceFolder,
            SearchIncludesChildren = _initialSettings.SearchIncludesChildren,
            ShowG8Female = _initialSettings.ShowG8Female,
            ShowG81Female = _initialSettings.ShowG81Female,
            ShowG8Male = _initialSettings.ShowG8Male,
            ShowG81Male = _initialSettings.ShowG81Male,
            ShowGenesis9 = _initialSettings.ShowGenesis9,
            ShowOtherFigures = _initialSettings.ShowOtherFigures,
            ConvertedFilter = _initialSettings.ConvertedFilter,
            WindowWidth = _initialSettings.WindowWidth,
            WindowHeight = _initialSettings.WindowHeight
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(settings.DazContentRoot) && !Directory.Exists(settings.DazContentRoot))
                throw new InvalidOperationException("The DAZ content root does not exist.");
            if (!string.IsNullOrWhiteSpace(settings.FigureDefinitionPath)
                && (!File.Exists(settings.FigureDefinitionPath) || !string.Equals(Path.GetExtension(settings.FigureDefinitionPath), ".dsf", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Choose an existing Genesis 8 Female .dsf figure definition.");
            if (!string.IsNullOrWhiteSpace(settings.UnityProjectRoot) && !_projectService.LooksLikeUnityProject(settings.UnityProjectRoot))
                throw new InvalidOperationException("The selected folder does not look like a Unity project (Assets and ProjectSettings or Packages are required).");
            settings.FinalPoseAssetRoot = _projectService.NormalizeAssetRelativePath(settings.FinalPoseAssetRoot);
            settings.CanonicalImportRoot = _projectService.NormalizeAssetRelativePath(settings.CanonicalImportRoot);
            _projectService.ValidateAssetRoots(settings.FinalPoseAssetRoot, settings.CanonicalImportRoot);
            await _settingsService.SaveAsync(settings);
            Settings = settings;
            Close(true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            SetError(ex is IOException or UnauthorizedAccessException
                ? $"Could not save settings: {ex.Message}"
                : ex.Message);
        }
    }

    private void CancelClicked(object? sender, RoutedEventArgs e) => Close(false);

    private void SetError(string message)
    {
        var error = this.FindControl<TextBlock>("ErrorText")!;
        error.Text = message;
        error.IsVisible = true;
    }

    public AppSettings? Settings { get; private set; }
}
