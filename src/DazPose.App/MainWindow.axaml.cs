using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
        ConvertButton.IsEnabled = !string.IsNullOrWhiteSpace(FigurePathBox.Text)
            && !string.IsNullOrWhiteSpace(PosePathBox.Text)
            && !string.IsNullOrWhiteSpace(OutputPathBox.Text)
            && !_isConverting;
        OpenOutputButton.IsEnabled = !string.IsNullOrWhiteSpace(OutputPathBox.Text)
            && Directory.Exists(OutputPathBox.Text);
    }

    private async void BrowseFigure(object? sender, RoutedEventArgs e)
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
        if (files.Count > 0) FigurePathBox.Text = files[0].Path.LocalPath;
    }

    private async void BrowsePose(object? sender, RoutedEventArgs e)
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
        if (files.Count > 0) PosePathBox.Text = files[0].Path.LocalPath;
    }

    private async void BrowseOutput(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select an Output Folder",
            AllowMultiple = false
        });
        if (folders.Count > 0) OutputPathBox.Text = folders[0].Path.LocalPath;
    }

    private async void ConvertClicked(object? sender, RoutedEventArgs e)
    {
        if (_isConverting) return;
        _isConverting = true;
        UpdateButtons();
        DiagnosticsText.Text = "Converting...";
        try
        {
            var figurePath = FigurePathBox.Text!.Trim();
            var posePath = PosePathBox.Text!.Trim();
            var outputPath = OutputPathBox.Text!.Trim();
            var result = await Task.Run(() => PoseConversionService.Convert(figurePath, posePath, outputPath));
            DiagnosticsText.Text = SuccessSummary(result);
        }
        catch (DazConversionException ex)
        {
            DiagnosticsText.Text = $"Conversion failed.\n\n{ex.Message}";
        }
        catch (Exception ex)
        {
            DiagnosticsText.Text = $"Conversion failed unexpectedly.\n\n{ex.Message}\n\nTechnical details:\n{ex}";
        }
        finally
        {
            _isConverting = false;
            UpdateButtons();
        }
    }

    private void OpenOutputFolder(object? sender, RoutedEventArgs e)
    {
        var path = OutputPathBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            DiagnosticsText.Text = "The selected output folder does not exist.";
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DiagnosticsText.Text = $"Could not open the output folder.\n\n{ex.Message}";
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
