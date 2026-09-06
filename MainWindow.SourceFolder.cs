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

// Manages source-folder selection, scanning, tree navigation, and source image state.
public partial class MainWindow
{
    private void ToggleLeftPaneClick(object? sender, RoutedEventArgs e)
    {
        _leftPaneOpen = !_leftPaneOpen;
        if (_leftPaneOpen)
        {
            Workspace.ColumnDefinitions[0].Width = new GridLength(Math.Max(180, _settings.LeftWidth));
            Workspace.ColumnDefinitions[1].Width = new GridLength(5);
            LeftPaneToggle.Content = "‹";
        }
        else
        {
            _settings.LeftWidth = Workspace.ColumnDefinitions[0].Width.Value;
            Workspace.ColumnDefinitions[0].Width = new GridLength(0);
            Workspace.ColumnDefinitions[1].Width = new GridLength(0);
            LeftPaneToggle.Content = "›";
        }
    }

    private void RemoveMissingSourceImage(ImageItem missing)
    {
        var missingIndex = _images.FindIndex(image => PathEquals(image.Path, missing.Path));
        if (missingIndex >= 0)
        {
            _images.RemoveAt(missingIndex);
            if (missingIndex < _index)
            {
                _index--;
            }

            if (_index >= _images.Count)
            {
                _index = Math.Max(0, _images.Count - 1);
            }
        }

        if (_selectedSourceFolder is not null)
        {
            foreach (var node in _selectedSourceFolder.SourceChildren.OfType<ImageNode>()
                         .Where(node => PathEquals(node.Path, missing.Path)).ToArray())
            {
                _selectedSourceFolder.SourceChildren.Remove(node);
            }

            _selectedSourceFolder.ImageCount = _images.Count;
        }

        UpdateSourceCount();
        UpdateSourceImageHighlight();
        StatusText.Text = $"The source image no longer exists — removed {missing.Name} from the list";
    }

    private async void OpenSourceClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose source folder", AllowMultiple = false });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        _sourceRoot = _sourcePath = path;
        await LoadSourceAsync(path);
        await RefreshSourceTreeAsync();
    }

    private async Task LoadSourceAsync(string path, string? preferred = null)
    {
        if (_fileOperationInProgress)
        {
            return;
        }

        _fileOperationInProgress = true;
        try
        {
            await LoadSourceCoreAsync(path, preferred);
        }
        finally
        {
            _fileOperationInProgress = false;
        }
    }

    private async Task LoadSourceCoreAsync(string path, string? preferred = null)
    {
        LoadingOverlay.IsVisible = true;
        EmptyState.IsVisible = false;
        PreviewImage.IsVisible = false;
        LoadingText.Text = $"Loading images from {Path.GetFileName(path)}…";
        StatusText.Text = LoadingText.Text;
        try
        {
            var found = await _files.ScanImagesAsync(path);
            _images.Clear();
            _images.AddRange(found);
            UpdateSourceCount();
            _index = preferred is null ? 0 : Math.Max(0, _images.FindIndex(item => PathEquals(item.Path, preferred)));
            if (_index >= _images.Count)
            {
                _index = Math.Max(0, _images.Count - 1);
            }

            StatusText.Text = _images.Count > 0 ? $"{_images.Count} images in the selected folder" : "No supported images directly in this folder";
            await ShowCurrentAsync();
        }
        catch (Exception ex)
        {
            _images.Clear();
            UpdateSourceCount();
            ShowError(ex);
            await ShowCurrentAsync();
        }
        finally
        {
            LoadingOverlay.IsVisible = false;
        }
    }

    private async Task RefreshSourceTreeAsync()
    {
        if (_sourceRoot is null)
        {
            return;
        }

        try
        {
            var tree = await _files.BuildTreeAsync(_sourceRoot);
            tree.IsExpanded = true;
            SourceTree.ItemsSource = new[] { tree };
            var selected = FindSourceFolder(tree, _sourcePath ?? _sourceRoot) ?? tree;
            selected.IsExpanded = true;
            PopulateSourceImages(selected);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private FolderNode? FindSourceFolder(FolderNode node, string path)
    {
        if (PathEquals(node.Path, path))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var found = FindSourceFolder(child, path);
            if (found is null)
            {
                continue;
            }

            node.IsExpanded = true;
            return found;
        }
        return null;
    }

    private void PopulateSourceImages(FolderNode folder)
    {
        if (_selectedSourceFolder is not null)
        {
            _selectedSourceFolder.IsSourceSelected = false;
            foreach (var image in _selectedSourceFolder.SourceChildren.OfType<ImageNode>().ToArray())
            {
                _selectedSourceFolder.SourceChildren.Remove(image);
            }
        }
        _selectedSourceFolder = folder;
        folder.IsSourceSelected = true;
        foreach (var image in _images)
        {
            folder.SourceChildren.Add(new ImageNode { Path = image.Path, Name = image.Name });
        }

        UpdateSourceImageHighlight();
    }

    private void UpdateSourceImageHighlight()
    {
        if (_selectedSourceFolder is null)
        {
            return;
        }

        var currentPath = Current?.Path;
        foreach (var image in _selectedSourceFolder.SourceChildren.OfType<ImageNode>())
        {
            image.IsCurrent = currentPath is not null && PathEquals(image.Path, currentPath);
        }
    }

    private async void SourceTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SourceTree.SelectedItem is ImageNode image)
        {
            var imageIndex = _images.FindIndex(item => PathEquals(item.Path, image.Path));
            if (imageIndex >= 0 && imageIndex != _index)
            {
                _index = imageIndex;
                await ShowCurrentAsync();
            }
            return;
        }
        if (SourceTree.SelectedItem is not FolderNode node)
        {
            return;
        }
        // Its image children are already populated. Recreating them while TreeView is
        // processing an Up/Down selection change invalidates its row containers.
        if (PathEquals(_sourcePath, node.Path))
        {
            return;
        }

        _sourcePath = node.Path;
        await LoadSourceAsync(node.Path);
        node.IsExpanded = true;
        PopulateSourceImages(node);
    }

    private void UpdateSourceCount() => SourceCountText.Text = _images.Count.ToString();
}

