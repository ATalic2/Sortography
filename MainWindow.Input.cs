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

// Routes window-level keyboard and pointer input across the active UI features.
public partial class MainWindow
{
    private void GlobalPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _sourceTreeKeyboardActive = e.Source is Visual source &&
            source.GetSelfAndVisualAncestors().Any(control => ReferenceEquals(control, SourcePane));

        // Clicking anywhere in the source pane makes it the keyboard-navigation context,
        // including its header and empty space—not only an individual tree row.
        if (_sourceTreeKeyboardActive)
        {
            SourceTree.Focus();
        }
    }

    private async void WindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Z)
        {
            UndoClick(null, new RoutedEventArgs());
            e.Handled = true;
            return;
        }
        if (e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (e.KeyModifiers == KeyModifiers.None && e.Key is >= Key.F1 and <= Key.F9)
        {
            var slot = e.Key - Key.F1 + 1;
            if (_settings.Shortcuts.TryGetValue(slot, out var path))
            {
                e.Handled = true;
                await MoveCurrentAsync(path);
            }
            return;
        }
        var editingControlFocused = e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(control => control is TextBox or ComboBox);
        if (editingControlFocused)
        {
            return;
        }

        if (e.Key == Key.Left)
        {
            e.Handled = true;
            await ChangeIndexAsync(-1);
            return;
        }
        if (e.Key == Key.Right)
        {
            e.Handled = true;
            await ChangeIndexAsync(1);
            return;
        }
        if (e.Key is Key.Up or Key.Down && !_sourceTreeKeyboardActive &&
            e.Source is Visual keySource && keySource.GetSelfAndVisualAncestors().Any(control => ReferenceEquals(control, SourceTree)))
        {
            // The tree can retain focus after a click on the non-focusable preview surface.
            // Block that stale focus from moving rows unless the source pane was clicked last.
            e.Handled = true;
            return;
        }
        if (e.Key is Key.Add or Key.OemPlus)
        {
            SetZoom(_zoom + .15);
        }

        if (e.Key is Key.Subtract or Key.OemMinus)
        {
            SetZoom(_zoom - .15);
        }
    }

    private void GlobalPointerMoved(object? sender, PointerEventArgs e)
    {
        if (HandlePreviewPanPointerMoved(e))
        {
            return;
        }

        if (HandleDestinationReorderPointerMoved(e))
        {
            return;
        }

        HandleImageDragPointerMoved(e);
    }

    private async void GlobalPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (HandlePreviewPanPointerReleased(e))
        {
            return;
        }

        if (HandleDestinationReorderPointerReleased(e))
        {
            return;
        }

        await HandleImageDragPointerReleasedAsync(e);
    }
}

