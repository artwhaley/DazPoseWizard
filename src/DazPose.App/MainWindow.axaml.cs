using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DazPose.App.Models;
using DazPose.App.ViewModels;

namespace DazPose.App;

public sealed partial class MainWindow : Window
{
    private const string DropPrefix = "DazPoseWizard.PoseCards:";
    private readonly MainWindowViewModel _viewModel;
    private PointerPressedEventArgs? _dragPressEvent;
    private PoseCardViewModel? _pressedPose;
    private Avalonia.Point _dragStart;
    private bool _dragStarted;
    private bool _initializing;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        Width = Math.Max(MinWidth, _viewModel.Settings.WindowWidth);
        Height = Math.Max(MinHeight, _viewModel.Settings.WindowHeight);
        DataContext = _viewModel;
        var poseGrid = this.FindControl<ListBox>("PoseGrid")!;
        poseGrid.AddHandler(InputElement.PointerPressedEvent, PoseGridPointerPressed,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        poseGrid.AddHandler(InputElement.PointerMovedEvent, PoseGridPointerMoved,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        poseGrid.AddHandler(InputElement.PointerReleasedEvent, PoseGridPointerReleased,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        _initializing = true;
        this.FindControl<RadioButton>("IncludeChildrenRadio")!.IsChecked = _viewModel.SearchIncludesChildren;
        this.FindControl<RadioButton>("CurrentFolderRadio")!.IsChecked = !_viewModel.SearchIncludesChildren;
        switch (_viewModel.ConversionFilter)
        {
            case ConversionFilter.Unconverted: this.FindControl<RadioButton>("UnconvertedFilter")!.IsChecked = true; break;
            case ConversionFilter.Converted: this.FindControl<RadioButton>("ConvertedFilter")!.IsChecked = true; break;
            default: this.FindControl<RadioButton>("AllFilter")!.IsChecked = true; break;
        }
        _initializing = false;
        Closed += async (_, _) =>
        {
            await _viewModel.SaveWindowBoundsAsync(Width, Height);
            await _viewModel.DisposeAsync();
        };
    }

    private void InitializeComponent() => Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

    private void SearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox) _viewModel.SearchText = textBox.Text ?? string.Empty;
    }

    private void ClearSearchClicked(object? sender, RoutedEventArgs e) => this.FindControl<TextBox>("SearchBox")!.Text = string.Empty;

    private void CurrentFolderChecked(object? sender, RoutedEventArgs e)
    {
        if (!_initializing && sender is RadioButton { IsChecked: true }) _viewModel.SearchIncludesChildren = false;
    }

    private void IncludeChildrenChecked(object? sender, RoutedEventArgs e)
    {
        if (!_initializing && sender is RadioButton { IsChecked: true }) _viewModel.SearchIncludesChildren = true;
    }

    private void AllFilterChecked(object? sender, RoutedEventArgs e)
    {
        if (!_initializing && sender is RadioButton { IsChecked: true }) _viewModel.ConversionFilter = ConversionFilter.All;
    }

    private void UnconvertedFilterChecked(object? sender, RoutedEventArgs e)
    {
        if (!_initializing && sender is RadioButton { IsChecked: true }) _viewModel.ConversionFilter = ConversionFilter.Unconverted;
    }

    private void ConvertedFilterChecked(object? sender, RoutedEventArgs e)
    {
        if (!_initializing && sender is RadioButton { IsChecked: true }) _viewModel.ConversionFilter = ConversionFilter.Converted;
    }

    private void SourceFolderChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is TreeView treeView) _viewModel.SelectSourceFolder(treeView.SelectedItem as FolderNode);
    }

    private void DestinationFolderChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is TreeView treeView) _viewModel.SelectDestinationFolder(treeView.SelectedItem as FolderNode);
    }

    private async void RefreshClicked(object? sender, RoutedEventArgs e) => await _viewModel.RefreshEverythingAsync();

    private async void SettingsClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_viewModel.Settings);
        if (await dialog.ShowDialog<bool>(this) && dialog.Settings is { } settings)
        {
            try { await _viewModel.ApplySettingsAsync(settings); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                or ArgumentException or Microsoft.Data.Sqlite.SqliteException)
            {
                // Keep the main browser open and surface setup errors in its status strip.
                _viewModel.ReportStatus($"Settings were saved, but project setup did not finish: {ex.Message}");
            }
        }
    }

    private void ExitClicked(object? sender, RoutedEventArgs e) => Close();

    private async void LoadMoreClicked(object? sender, RoutedEventArgs e) => await _viewModel.LoadMoreAsync();

    private async void LoadPreviousClicked(object? sender, RoutedEventArgs e) => await _viewModel.LoadPreviousPageAsync();

    private async void CreateFolderClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new NameDialogWindow("Create Unity Destination Folder", "Create a child folder in both the final pose root and the matching canonical import root.");
        if (!await dialog.ShowDialog<bool>(this) || string.IsNullOrWhiteSpace(dialog.Value)) return;
        try { await _viewModel.CreateDestinationFolderAsync(dialog.Value, _viewModel.SelectedDestinationFolder); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            SetStatusMessage($"Could not create the Unity destination folder: {ex.Message}");
        }
    }

    private async void QueueSummaryClicked(object? sender, RoutedEventArgs e) =>
        await _viewModel.SetQueueDrawerOpenAsync(!_viewModel.IsQueueDrawerOpen);

    private async void RetryFailedClicked(object? sender, RoutedEventArgs e) => await _viewModel.RetryFailedAsync();

    private void ClearQueueClicked(object? sender, RoutedEventArgs e) => _viewModel.ClearCompleted();

    private async void PoseGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _dragPressEvent = e;
        _dragStarted = false;
        _dragStart = e.GetPosition(this);
        _pressedPose = FindPoseCard(e.Source as Control);
        await Task.CompletedTask;
    }

    private async void PoseGridPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStarted || _pressedPose is null || _dragPressEvent is null
            || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(this);
        if (Math.Abs(point.X - _dragStart.X) < 8 && Math.Abs(point.Y - _dragStart.Y) < 8) return;

        var selected = this.FindControl<ListBox>("PoseGrid")!.SelectedItems?.OfType<PoseCardViewModel>().ToList() ?? [];
        if (!selected.Contains(_pressedPose)) selected = [_pressedPose];
        if (selected.Count == 0) return;
        var payload = DropPrefix + JsonSerializer.Serialize(selected.Select(card => card.Entry.SourcePath).Distinct(StringComparer.OrdinalIgnoreCase));
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.CreateText(payload));
        _dragStarted = true;
        _viewModel.ReportStatus($"Dragging {selected.Count:N0} pose{(selected.Count == 1 ? "" : "s")} — drop on a Unity destination folder.");
        try
        {
            var effect = await DragDrop.DoDragDropAsync(_dragPressEvent, transfer, DragDropEffects.Copy);
            if (effect == DragDropEffects.None)
                _viewModel.ReportStatus("Drag cancelled. Drag a pose card onto a folder in Unity Pose Assets.");
        }
        finally
        {
            _pressedPose = null;
            _dragPressEvent = null;
            _dragStarted = false;
        }
    }

    private void PoseGridPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragStarted)
        {
            _pressedPose = null;
            _dragPressEvent = null;
        }
    }

    private void PoseGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.A && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            this.FindControl<ListBox>("PoseGrid")!.SelectAll();
            e.Handled = true;
        }
    }

    private void DestinationDragOver(object? sender, DragEventArgs e)
    {
        var text = e.DataTransfer.TryGetText();
        if (text?.StartsWith(DropPrefix, StringComparison.Ordinal) == true && TryParseDrop(text, out _))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void DestinationDrop(object? sender, DragEventArgs e)
    {
        var text = e.DataTransfer.TryGetText();
        if (text is null || !TryParseDrop(text, out var paths)) return;
        var tree = this.FindControl<TreeView>("DestinationTree")!;
        var destination = FindFolderNode(e.Source as Control, tree) ?? _viewModel.SelectedDestinationFolder;
        if (destination is null) return;
        _viewModel.SelectDestinationFolder(destination);
        var selectedCards = paths.Select(path => _viewModel.PoseCards.FirstOrDefault(card =>
            string.Equals(card.Entry.SourcePath, path, StringComparison.OrdinalIgnoreCase)))
            .Where(card => card is not null).Cast<PoseCardViewModel>().ToArray();
        if (selectedCards.Length == 0)
        {
            SetStatusMessage("The dragged pose selection is no longer visible. Select the poses again and retry the drop.");
            return;
        }
        e.Handled = true;
        await _viewModel.EnqueueAsync(selectedCards, destination);
    }

    private static bool TryParseDrop(string payload, out string[] paths)
    {
        paths = [];
        if (!payload.StartsWith(DropPrefix, StringComparison.Ordinal)) return false;
        try
        {
            paths = JsonSerializer.Deserialize<string[]>(payload[DropPrefix.Length..]) ?? [];
            return paths.Length > 0 && paths.All(path => path.EndsWith(".duf", StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException) { return false; }
    }

    private static PoseCardViewModel? FindPoseCard(Control? source)
    {
        while (source is not null)
        {
            if (source is ListBoxItem { DataContext: PoseCardViewModel card }) return card;
            source = source.GetVisualParent() as Control;
        }
        return null;
    }

    private static FolderNode? FindFolderNode(Control? source, TreeView tree)
    {
        while (source is not null && source != tree)
        {
            if (source is TreeViewItem { DataContext: FolderNode node }) return node;
            source = source.GetVisualParent() as Control;
        }
        return null;
    }

    private void SetStatusMessage(string message)
        => _viewModel.ReportStatus(message);
}
