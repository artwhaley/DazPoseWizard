using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DazPose.App;

public sealed partial class NameDialogWindow : Window
{
    public NameDialogWindow() : this("Create Unity Folder", "Enter a name for the child folder.") { }

    public NameDialogWindow(string title, string prompt)
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        Title = title;
        this.FindControl<TextBlock>("PromptText")!.Text = prompt;
        Opened += (_, _) => this.FindControl<TextBox>("NameBox")!.Focus();
    }

    public string? Value { get; private set; }

    private void CreateClicked(object? sender, RoutedEventArgs e)
    {
        Value = this.FindControl<TextBox>("NameBox")!.Text?.Trim();
        if (string.IsNullOrWhiteSpace(Value)) return;
        Close(true);
    }

    private void CancelClicked(object? sender, RoutedEventArgs e) => Close(false);
}
