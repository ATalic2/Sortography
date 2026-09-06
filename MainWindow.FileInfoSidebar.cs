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

// Manages the file-information sidebar and its displayed metadata.
public partial class MainWindow
{
    private void ToggleRightPaneClick(object? sender, RoutedEventArgs e)
    {
        _rightPaneOpen = !_rightPaneOpen;
        if (_rightPaneOpen)
        {
            Workspace.ColumnDefinitions[4].Width = new GridLength(Math.Max(180, _settings.RightWidth));
            Workspace.ColumnDefinitions[3].Width = new GridLength(5);
            RightPaneToggle.Content = "›";
        }
        else
        {
            _settings.RightWidth = Workspace.ColumnDefinitions[4].Width.Value;
            Workspace.ColumnDefinitions[4].Width = new GridLength(0);
            Workspace.ColumnDefinitions[3].Width = new GridLength(0);
            RightPaneToggle.Content = "‹";
        }
    }

    private void ShowFileInfo(ImageItem image)
    {
        PropertyName.Text = image.Name;
        PropertySize.Text = FormatBytes(image.Size);
        PropertyModified.Text = image.Modified.ToString("g");
        PropertyPath.Text = image.Path;
    }

    private void ShowFileDimensions(int width, int height)
    {
        PropertyDimensions.Text = $"{width} × {height}";
    }

    private void ClearFileInfo()
    {
        PropertyName.Text = PropertyDimensions.Text = PropertySize.Text = PropertyModified.Text = PropertyPath.Text = "—";
    }

    private static string FormatBytes(long value) => value < 1024 * 1024 ? $"{value / 1024d:0.0} KB" : $"{value / 1024d / 1024d:0.0} MB";
}

