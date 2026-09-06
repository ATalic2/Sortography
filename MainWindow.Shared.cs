using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using AnimatedImage.Avalonia;

namespace Sortography;

// Provides dialogs, settings persistence, error display, and shared path helpers.
public partial class MainWindow
{
    private async Task<string?> AskTextAsync(string title, string label, string initial)
    {
        var input = new TextBox { Text = initial, Width = 300 };
        var dialog = new Window { Title = title, Width = 360, Height = 155, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var ok = new Button { Content = "OK", Width = 72 };
        var cancel = new Button { Content = "Cancel", Width = 72 };
        ok.Click += (_, _) => dialog.Close(input.Text);
        cancel.Click += (_, _) => dialog.Close(null);
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 9, Children = { new TextBlock { Text = label }, input, new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 7, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right, Children = { ok, cancel } } } };
        return await dialog.ShowDialog<string?>(this);
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new Window { Title = title, Width = 390, Height = 145, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var yes = new Button { Content = "Continue", Width = 85 };
        var no = new Button { Content = "Cancel", Width = 75 };
        yes.Click += (_, _) => dialog.Close(true);
        no.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 12, Children = { new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 7, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right, Children = { yes, no } } } };
        return await dialog.ShowDialog<bool>(this);
    }

    private void SaveSettings()
    {
        if (_trayOpen)
        {
            _settings.TrayHeight = Workspace.RowDefinitions[2].Height.Value;
        }

        if (_leftPaneOpen)
        {
            _settings.LeftWidth = Workspace.ColumnDefinitions[0].Width.Value;
        }

        if (_rightPaneOpen)
        {
            _settings.RightWidth = Workspace.ColumnDefinitions[4].Width.Value;
        }

        try
        {
            _settingsService.Save(_settings);
        }
        catch
        {
        }
    }

    private void ShowError(Exception ex) => StatusText.Text = ex.Message;

    private static bool PathEquals(string? a, string? b) => a is not null && b is not null && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool IsWithin(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative == "." || (!relative.StartsWith("..") && !Path.IsPathRooted(relative));
    }
}

