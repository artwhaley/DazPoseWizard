using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DazPose.App.Services;

namespace DazPose.App;

public sealed partial class AlwaysExportMorphsWindow : Window
{
    public ObservableCollection<AlwaysExportMorphRow> Entries { get; } = [];
    public bool Saved { get; private set; }

    public AlwaysExportMorphsWindow() : this(Array.Empty<RequiredMorphManifestItem>()) { }

    public AlwaysExportMorphsWindow(IEnumerable<RequiredMorphManifestItem> entries)
    {
        InitializeComponent();
        DataContext = this;
        foreach (var item in entries.OrderBy(item => item.Category is null
                         ? int.MaxValue : Array.IndexOf(RequiredMorphManifestService.AlwaysExportCategories.ToArray(), item.Category))
                     .ThenBy(item => item.Name, StringComparer.Ordinal))
            Entries.Add(new AlwaysExportMorphRow(item));
    }

    private void InitializeComponent() => Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

    public IReadOnlyList<RequiredMorphManifestItem> GetEntries() => Entries.Select(row => row.ToManifestItem()).ToArray();

    private async void AddClicked(object? sender, RoutedEventArgs e)
    {
        ClearError();
        var seed = new RequiredMorphManifestItem { Category = "Manual", AlwaysExport = true };
        var editor = new AlwaysExportMorphEditWindow(seed);
        if (!await editor.ShowDialog<bool>(this) || editor.Result is not { } result) return;
        if (Entries.Any(row => string.Equals(row.Name, result.Name, StringComparison.Ordinal)))
        {
            ShowError($"The exact morph name '{result.Name}' is already in the list. Edit that entry instead.");
            return;
        }
        Entries.Add(new AlwaysExportMorphRow(result));
        MorphList.SelectedItem = Entries[^1];
    }

    private async void EditClicked(object? sender, RoutedEventArgs e)
    {
        ClearError();
        if (MorphList.SelectedItem is not AlwaysExportMorphRow selected) return;
        var editor = new AlwaysExportMorphEditWindow(selected.ToManifestItem());
        if (!await editor.ShowDialog<bool>(this) || editor.Result is not { } result) return;
        if (Entries.Any(row => !ReferenceEquals(row, selected)
            && string.Equals(row.Name, result.Name, StringComparison.Ordinal)))
        {
            ShowError($"The exact morph name '{result.Name}' is already in the list.");
            return;
        }
        selected.Update(result);
    }

    private void RemoveClicked(object? sender, RoutedEventArgs e)
    {
        ClearError();
        if (MorphList.SelectedItem is not AlwaysExportMorphRow selected) return;
        if (selected.Category is null)
        {
            ShowError("This morph is required by converted content; there is no always-export pin to remove.");
            return;
        }
        Entries.Remove(selected);
    }

    private void SaveClicked(object? sender, RoutedEventArgs e)
    {
        ClearError();
        if (Entries.Any(row => string.IsNullOrWhiteSpace(row.Name)))
        {
            ShowError("Every pin must have an exact morph name.");
            return;
        }
        Saved = true;
        Close(true);
    }

    private void CancelClicked(object? sender, RoutedEventArgs e) => Close(false);

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }

    private void ClearError()
    {
        ErrorText.Text = string.Empty;
        ErrorText.IsVisible = false;
    }
}

public sealed class AlwaysExportMorphRow : System.ComponentModel.INotifyPropertyChanged
{
    private bool _alwaysExport;
    private string _name;
    private string? _category;
    private string _purpose;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public string Name { get => _name; set => Set(ref _name, value); }
    public string? Category
    {
        get => _category;
        set
        {
            if (EqualityComparer<string?>.Default.Equals(_category, value)) return;
            _category = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Category)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(CategoryDisplay)));
        }
    }
    public string CategoryDisplay => Category switch
    {
        null => "(content only)",
        "BodyCustomization" => "Body Customization",
        "LipSync" => "Lip Sync",
        _ => Category
    };
    public string Purpose { get => _purpose; set => Set(ref _purpose, value); }
    public bool AlwaysExport
    {
        get => _alwaysExport;
        set
        {
            if (value && _category is null) Category = "Manual";
            Set(ref _alwaysExport, value);
        }
    }
    public bool RequiredByContent { get; private set; }
    public string Provenance => RequiredByContent ? "Required by converted content" : string.Empty;
    public string RawControlId { get; private set; }
    public RequiredMorphState State { get; private set; }
    public string FirstSeenIn { get; private set; }

    public AlwaysExportMorphRow(RequiredMorphManifestItem item)
    {
        _alwaysExport = item.AlwaysExport;
        _name = item.Name;
        _category = item.Category;
        _purpose = item.Purpose ?? string.Empty;
        RequiredByContent = item.RequiredByContent;
        RawControlId = item.RawControlId;
        State = item.State;
        FirstSeenIn = item.FirstSeenIn;
    }

    public RequiredMorphManifestItem ToManifestItem() => new()
    {
        Name = Name,
        Category = Category,
        Purpose = Purpose,
        AlwaysExport = AlwaysExport,
        RequiredByContent = RequiredByContent,
        RawControlId = RawControlId,
        State = State,
        FirstSeenIn = FirstSeenIn
    };

    public void Update(RequiredMorphManifestItem item)
    {
        Name = item.Name;
        Category = item.Category ?? "Manual";
        Purpose = item.Purpose ?? string.Empty;
        AlwaysExport = item.AlwaysExport;
        RequiredByContent = item.RequiredByContent;
        RawControlId = item.RawControlId;
        State = item.State;
        FirstSeenIn = item.FirstSeenIn;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Provenance)));
    }

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
