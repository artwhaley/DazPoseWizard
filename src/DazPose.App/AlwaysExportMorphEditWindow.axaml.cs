using Avalonia.Controls;
using Avalonia.Interactivity;
using DazPose.App.Services;

namespace DazPose.App;

public sealed partial class AlwaysExportMorphEditWindow : Window
{
    private sealed record CategoryChoice(string Value, string Label);
    private static readonly CategoryChoice[] CategoryChoices =
    [
        new("Breathing", "Breathing"),
        new("Blink", "Blink"),
        new("BodyCustomization", "Body Customization"),
        new("LipSync", "Lip Sync"),
        new("Manual", "Manual")
    ];

    public RequiredMorphManifestItem? Result { get; private set; }

    public AlwaysExportMorphEditWindow() : this(new RequiredMorphManifestItem { Category = "Manual" }) { }

    public AlwaysExportMorphEditWindow(RequiredMorphManifestItem item)
    {
        InitializeComponent();
        CategoryBox.ItemsSource = CategoryChoices;
        NameText.Text = item.Name;
        CategoryBox.SelectedItem = CategoryChoices.FirstOrDefault(choice =>
            string.Equals(choice.Value, item.Category, StringComparison.Ordinal)) ?? CategoryChoices[^1];
        PurposeText.Text = item.Purpose ?? string.Empty;
        EnabledCheck.IsChecked = item.AlwaysExport;
        NameText.SelectAll();
        NameText.Focus();
    }

    private void InitializeComponent() => Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

    private void SaveClicked(object? sender, RoutedEventArgs e)
    {
        var name = NameText.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorText.Text = "Enter the exact imported blendshape name.";
            return;
        }
        if (CategoryBox.SelectedItem is not CategoryChoice category)
        {
            ErrorText.Text = "Choose a category.";
            return;
        }
        Result = new RequiredMorphManifestItem
        {
            Name = name,
            Category = category.Value,
            Purpose = PurposeText.Text ?? string.Empty,
            AlwaysExport = EnabledCheck.IsChecked == true,
            State = RequiredMorphState.Candidate
        };
        Close(true);
    }

    private void CancelClicked(object? sender, RoutedEventArgs e) => Close(false);
}
