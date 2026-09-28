using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using DazPose.Core;

namespace DazPose.App;

public partial class MainWindow : Window
{
    private bool _isConverting;

    public MainWindow() => InitializeComponent();

    private void InitializeComponent() => Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

    private void PathChanged(object? sender, TextChangedEventArgs e) => UpdateButtons();

    private void UpdateButtons()
    {
        var figurePath = GetControl<TextBox>("FigurePathBox")?.Text;
        var posePath = GetControl<TextBox>("PosePathBox")?.Text;
        var outputPath = GetControl<TextBox>("OutputPathBox")?.Text;

        if (GetControl<Button>("ConvertButton") is { } convertButton)
            convertButton.IsEnabled = !string.IsNullOrWhiteSpace(figurePath)
                && !string.IsNullOrWhiteSpace(posePath)
                && !string.IsNullOrWhiteSpace(outputPath)
                && !_isConverting;
        if (GetControl<Button>("OpenOutputButton") is { } openOutputButton)
            openOutputButton.IsEnabled = !string.IsNullOrWhiteSpace(outputPath)
                && Directory.Exists(outputPath);
    }

    private async void BrowseFigure(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select a DAZ Figure Definition",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("DSON Support File (*.dsf)") { Patterns = ["*.dsf"] },
                    FilePickerFileTypes.All
                ]
            });
            if (files.Count > 0 && GetLocalPath(files[0], "figure file") is { } path)
            {
                if (GetControl<TextBox>("FigurePathBox") is { } figurePathBox)
                    figurePathBox.Text = path;
                else
                    SetDiagnostics("The figure path field could not be found. Restart the app and try again.");
            }
        }
        catch (Exception ex)
        {
            SetDiagnostics($"Could not open the figure file picker.\n\n{ex.Message}");
        }
    }

    private async void BrowsePose(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select a DAZ Pose Preset",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("DSON User File (*.duf)") { Patterns = ["*.duf"] },
                    FilePickerFileTypes.All
                ]
            });
            if (files.Count > 0 && GetLocalPath(files[0], "pose file") is { } path)
            {
                if (GetControl<TextBox>("PosePathBox") is { } posePathBox)
                    posePathBox.Text = path;
                else
                    SetDiagnostics("The pose path field could not be found. Restart the app and try again.");
            }
        }
        catch (Exception ex)
        {
            SetDiagnostics($"Could not open the pose file picker.\n\n{ex.Message}");
        }
    }

    private async void BrowseOutput(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select an Output Folder",
                AllowMultiple = false
            });
            if (folders.Count > 0 && GetLocalPath(folders[0], "output folder") is { } path)
            {
                if (GetControl<TextBox>("OutputPathBox") is { } outputPathBox)
                    outputPathBox.Text = path;
                else
                    SetDiagnostics("The output path field could not be found. Restart the app and try again.");
            }
        }
        catch (Exception ex)
        {
            SetDiagnostics($"Could not open the output folder picker.\n\n{ex.Message}");
        }
    }

    private string? GetLocalPath(IStorageItem? item, string description)
    {
        try
        {
            if (item is null)
            {
                SetDiagnostics($"The picker did not return a {description}.");
                return null;
            }

            if (item.TryGetLocalPath() is { Length: > 0 } path)
                return path;

            SetDiagnostics($"The selected {description} does not provide a local file-system path. Select it from a local folder.");
        }
        catch (Exception ex)
        {
            SetDiagnostics($"Could not get a local path for the selected {description}.\n\n{ex.Message}");
        }

        return null;
    }

    private T? GetControl<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? this.GetVisualDescendants().OfType<T>().FirstOrDefault(control => control.Name == name);

    private void SetDiagnostics(string message)
    {
        if (GetControl<TextBlock>("DiagnosticsText") is { } diagnosticsText)
        {
            diagnosticsText.Text = message;
            return;
        }

        var summary = message.Replace('\r', ' ').Replace('\n', ' ');
        Title = summary.Length > 90 ? $"DazPoseTool V0 — {summary[..90]}…" : $"DazPoseTool V0 — {summary}";
        Debug.WriteLine(message);
    }

    private async void ConvertClicked(object? sender, RoutedEventArgs e)
    {
        if (_isConverting) return;
        var figurePath = GetControl<TextBox>("FigurePathBox")?.Text?.Trim();
        var posePath = GetControl<TextBox>("PosePathBox")?.Text?.Trim();
        var outputPath = GetControl<TextBox>("OutputPathBox")?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(figurePath) || string.IsNullOrWhiteSpace(posePath) || string.IsNullOrWhiteSpace(outputPath))
        {
            SetDiagnostics("Select a figure file, pose preset, and output folder before converting.");
            return;
        }

        _isConverting = true;
        UpdateButtons();
        SetDiagnostics("Converting...");
        try
        {
            var result = await Task.Run(() => PoseConversionService.Convert(figurePath, posePath, outputPath));
            SetDiagnostics(SuccessSummary(result));
        }
        catch (DazConversionException ex)
        {
            SetDiagnostics($"Conversion failed.\n\n{ex.Message}");
        }
        catch (Exception ex)
        {
            SetDiagnostics($"Conversion failed unexpectedly.\n\n{ex.Message}\n\nTechnical details:\n{ex}");
        }
        finally
        {
            _isConverting = false;
            UpdateButtons();
        }
    }

    private void OpenOutputFolder(object? sender, RoutedEventArgs e)
    {
        var path = GetControl<TextBox>("OutputPathBox")?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            SetDiagnostics("The selected output folder does not exist.");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetDiagnostics($"Could not open the output folder.\n\n{ex.Message}");
        }
    }

    private static string SuccessSummary(ConversionResult result)
    {
        var lines = new List<string>
        {
            $"Loaded figure: {result.Figure.FigureLabel}",
            $"Figure nodes: {result.Figure.Nodes.Count}",
            $"Bones: {result.Figure.Bones.Count}",
            "",
            $"Loaded pose: {result.Pose.PoseName}",
            $"Pose channels: {result.Pose.Channels.Count}",
            $"Skeletal targets: {result.Pose.SkeletalTargetCount}",
            $"Resolved skeletal targets: {result.Pose.ResolvedSkeletalTargetCount}",
            "Unresolved skeletal targets: 0",
            $"Ignored neutral unsupported channels: {result.Pose.NeutralUnsupportedChannels.Count}",
            "",
            "Generated:",
            Path.GetFileName(result.JsonPath),
            Path.GetFileName(result.BvhPath),
            Path.GetFileName(result.ReportPath)
        };
        if (result.Diagnostics.Count > 0)
        {
            lines.Add("");
            lines.Add("Diagnostics:");
            lines.AddRange(result.Diagnostics.Select(diagnostic => $"{diagnostic.Severity}: {diagnostic.Message}"));
        }
        lines.Add("");
        lines.Add("Conversion successful.");
        return string.Join(Environment.NewLine, lines);
    }
}
