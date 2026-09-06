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

// Manages destination folders, target cards, image moves, shortcuts, and card ordering.
public partial class MainWindow
{
    private async void AddDestinationClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Add sorting folder", AllowMultiple = false });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        if (_settings.Destinations.Any(item => IsWithin(item.Path, path)))
        {
            // The picker may have created this subfolder moments ago. It is already
            // covered by an existing root, so refresh that root instead of adding a
            // duplicate sorting location.
            var wasAlreadyDisplayed = _targets.Any(target => PathEquals(target.Path, path));
            await RefreshDestinationTreesAsync();
            StatusText.Text = wasAlreadyDisplayed
                ? "That folder is already available in the destination tray"
                : "Folder added to the existing destination root";
            return;
        }
        // If a parent is added after one of its children, the child remains available
        // through the parent and must not survive as a second overlapping root.
        _settings.Destinations.RemoveAll(item => IsWithin(path, item.Path));
        var colors = new[] { "#EF8F5A", "#5AA7EF", "#70C99A", "#D28BEF", "#E3C255", "#ED7090" };
        _settings.Destinations.Add(new DestinationRoot { Path = path, Name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } name ? name : path, Color = colors[_settings.Destinations.Count % colors.Length] });
        await RefreshDestinationTreesAsync();
        SaveSettings();
    }

    private async Task RefreshDestinationTreesAsync()
    {
        // Filesystem changes rebuild the folder models, but must never disturb the
        // exact card order that the user is currently looking at.
        var liveOrder = UniquePaths(_targets.Select(target => target.Path));
        NormalizeDestinationRoots();
        var roots = new List<FolderNode>();
        _targets.Clear();
        foreach (var destination in _settings.Destinations.ToArray())
        {
            if (!Directory.Exists(destination.Path))
            {
                continue;
            }

            try
            {
                var tree = await _files.BuildTreeAsync(destination.Path, true);
                roots.Add(tree);
                Flatten(tree, destination, 0);
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
            }
        }
        ApplyDestinationOrder(liveOrder.Count > 0 ? liveOrder : _settings.DestinationOrder);
        _settings.DestinationOrder = UniquePaths(_targets.Select(target => target.Path));
        _destinationTreeRoots.Clear();
        _destinationTreeRoots.AddRange(roots);
        DestinationTree.ItemsSource = roots;
        DestinationCountText.Text = _targets.Count.ToString();
        ApplyShortcuts();
        UpdateDestinationCardWidths(_destinationCardsWidth);
        if (!_settings.ShortcutDefaultsInitialized && _targets.Count > 0)
        {
            for (var index = 0; index < Math.Min(9, _targets.Count); index++)
            {
                _settings.Shortcuts[index + 1] = _targets[index].Path;
            }

            _settings.ShortcutDefaultsInitialized = true;
            ApplyShortcuts();
            SaveSettings();
        }
    }

    private void Flatten(FolderNode node, DestinationRoot root, int depth)
    {
        // Overlapping roots from older settings must never produce duplicate cards.
        if (_targets.Any(target => PathEquals(target.Path, node.Path)))
        {
            return;
        }

        var savedColor = _settings.DestinationColors.FirstOrDefault(pair => PathEquals(pair.Key, node.Path)).Value;
        var hasCustomColor = !string.IsNullOrWhiteSpace(savedColor);
        _targets.Add(new DestinationTarget { Path = node.Path, RootPath = root.Path, Name = node.Name, Color = hasCustomColor ? savedColor! : root.Color, BorderColor = hasCustomColor ? savedColor! : "#33383C", Depth = depth, CardWidth = CalculateDestinationCardWidth(_destinationCardsWidth) });
        foreach (var child in node.Children)
        {
            Flatten(child, root, depth + 1);
        }
    }

    private void ApplyShortcuts()
    {
        foreach (var target in _targets)
        {
            var slot = _settings.Shortcuts.FirstOrDefault(pair => PathEquals(pair.Value, target.Path)).Key;
            target.Shortcut = slot is >= 1 and <= 9 ? $"F{slot}" : "—";
        }
    }

    private void ApplyDestinationOrder(IReadOnlyList<string> preferredOrder)
    {
        if (preferredOrder.Count == 0 || _targets.Count < 2)
        {
            return;
        }

        var ordered = _targets
            .Select((target, scanIndex) => new
            {
                Target = target,
                ScanIndex = scanIndex,
                SavedIndex = FindPathIndex(preferredOrder, target.Path)
            })
            .OrderBy(item => item.SavedIndex < 0 ? int.MaxValue : item.SavedIndex)
            .ThenBy(item => item.ScanIndex)
            .Select(item => item.Target)
            .ToArray();
        _targets.Clear();
        foreach (var target in ordered)
        {
            _targets.Add(target);
        }
    }

    private void NormalizeDestinationRoots()
    {
        for (var index = _settings.Destinations.Count - 1; index >= 0; index--)
        {
            var candidate = _settings.Destinations[index];
            var coveredByAnotherRoot = _settings.Destinations.Where((_, otherIndex) => otherIndex != index)
                .Any(other => IsWithin(other.Path, candidate.Path) &&
                    (!PathEquals(other.Path, candidate.Path) || _settings.Destinations.IndexOf(other) < index));
            if (coveredByAnotherRoot)
            {
                _settings.Destinations.RemoveAt(index);
            }
        }
    }

    private static int FindPathIndex(IReadOnlyList<string> paths, string path)
    {
        for (var index = 0; index < paths.Count; index++)
        {
            if (PathEquals(paths[index], path))
            {
                return index;
            }
        }

        return -1;
    }

    private static List<string> UniquePaths(IEnumerable<string> paths)
    {
        var unique = new List<string>();
        foreach (var path in paths)
        {
            if (!unique.Any(existing => PathEquals(existing, path)))
            {
                unique.Add(path);
            }
        }

        return unique;
    }

    private async Task UpdateDestinationCountAsync(string folderPath)
    {
        try
        {
            var node = FindDestinationFolder(_destinationTreeRoots, folderPath);
            if (node is not null)
            {
                node.ImageCount = await _files.CountImagesAsync(folderPath);
            }
        }
        catch (IOException)
        {
            /* A later full tree refresh will reconcile unavailable folders. */
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static FolderNode? FindDestinationFolder(IEnumerable<FolderNode> nodes, string path)
    {
        foreach (var node in nodes)
        {
            if (PathEquals(node.Path, path))
            {
                return node;
            }

            var found = FindDestinationFolder(node.Children, path);
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }

    private void ChangeDestinationColorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { CommandParameter: DestinationTarget target, Tag: string color })
        {
            return;
        }

        foreach (var path in _settings.DestinationColors.Keys.Where(path => PathEquals(path, target.Path)).ToArray())
        {
            _settings.DestinationColors.Remove(path);
        }

        _settings.DestinationColors[target.Path] = color;
        target.Color = color;
        target.BorderColor = color;
        SaveSettings();
        StatusText.Text = $"Color updated for {target.Name}";
    }

    private void SaveDestinationOrder()
    {
        _settings.DestinationOrder = UniquePaths(_targets.Select(target => target.Path));
        SaveSettings();
    }

    private void ShortcutBadgeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DestinationTarget target } button)
        {
            return;
        }

        e.Handled = true;
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(9) };
        panel.Children.Add(new TextBlock { Text = $"Shortcut for {target.Name}", FontWeight = FontWeight.SemiBold, FontSize = 11 });
        var slots = new WrapPanel { Width = 225 };
        for (var slot = 1; slot <= 9; slot++)
        {
            var captured = slot;
            var used = _settings.Shortcuts.TryGetValue(slot, out var assigned) ? Path.GetFileName(assigned) : "Free";
            var option = new Button { Content = $"F{slot}  {used}", Width = 72, Height = 32, Margin = new Thickness(1.5), FontSize = 9 };
            option.Click += (_, _) => { AssignShortcut(target.Path, captured); FlyoutBase.GetAttachedFlyout(button)?.Hide(); };
            slots.Children.Add(option);
        }
        panel.Children.Add(slots);
        var clear = new Button { Content = "Remove shortcut", HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch, FontSize = 9 };
        clear.Click += (_, _) => { AssignShortcut(target.Path, null); FlyoutBase.GetAttachedFlyout(button)?.Hide(); };
        panel.Children.Add(clear);
        var flyout = new Flyout { Content = panel, Placement = PlacementMode.BottomEdgeAlignedLeft };
        FlyoutBase.SetAttachedFlyout(button, flyout);
        FlyoutBase.ShowAttachedFlyout(button);
    }

    private void AssignShortcut(string path, int? slot)
    {
        foreach (var key in _settings.Shortcuts.Where(pair => PathEquals(pair.Value, path)).Select(pair => pair.Key).ToArray())
        {
            _settings.Shortcuts.Remove(key);
        }

        if (slot is not null)
        {
            _settings.Shortcuts[slot.Value] = path;
        }

        ApplyShortcuts();
        SaveSettings();
        StatusText.Text = slot is null ? $"Shortcut removed from {Path.GetFileName(path)}" : $"{Path.GetFileName(path)} assigned to F{slot}";
    }

    private async void ClearDestinationsClick(object? sender, RoutedEventArgs e)
    {
        if (_settings.Destinations.Count == 0 || !await ConfirmAsync("Clear sorting folders?", "No files or folders will be deleted."))
        {
            return;
        }

        _settings.Destinations.Clear();
        _settings.Shortcuts.Clear();
        _settings.DestinationColors.Clear();
        _settings.DestinationOrder.Clear();
        _settings.ShortcutDefaultsInitialized = true;
        await RefreshDestinationTreesAsync();
        SaveSettings();
        StatusText.Text = "Sorting folders cleared";
    }

    private void SortDestinationsClick(object? sender, RoutedEventArgs e)
    {
        var sorted = _targets.OrderBy(target => target.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(target => target.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        _targets.Clear();
        foreach (var target in sorted)
        {
            _targets.Add(target);
        }

        SaveDestinationOrder();
        StatusText.Text = "Destination tray sorted A–Z";
    }

    private async void NewSubfolderClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.CommandParameter is not string path)
        {
            return;
        }

        var name = await AskTextAsync("New subfolder", "Folder name", "");
        if (name is null)
        {
            return;
        }

        try
        {
            FileService.CreateFolder(path, name);
            await RefreshAllTreesAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void RenameFolderClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.CommandParameter is not string path)
        {
            return;
        }

        var name = await AskTextAsync("Rename folder", "New name", Path.GetFileName(path));
        if (name is null)
        {
            return;
        }

        try
        {
            FileService.RenameFolder(path, name);
            await RefreshAllTreesAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void DeleteFolderClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.CommandParameter is not string path || !await ConfirmAsync("Delete empty folder?", path))
        {
            return;
        }

        try
        {
            FileService.DeleteEmptyFolder(path);
            await RefreshAllTreesAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void RemoveDestinationClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.CommandParameter is not string path)
        {
            return;
        }

        var root = _settings.Destinations.FirstOrDefault(item => IsWithin(item.Path, path));
        if (root is null)
        {
            return;
        }

        if (!await ConfirmAsync($"Remove {root.Name} from sorting folders?", "No files will be deleted."))
        {
            return;
        }

        _settings.Destinations.Remove(root);
        foreach (var key in _settings.Shortcuts.Where(item => IsWithin(root.Path, item.Value)).Select(item => item.Key).ToArray())
        {
            _settings.Shortcuts.Remove(key);
        }

        foreach (var colorPath in _settings.DestinationColors.Keys.Where(colorPath => IsWithin(root.Path, colorPath)).ToArray())
        {
            _settings.DestinationColors.Remove(colorPath);
        }

        _settings.DestinationOrder.RemoveAll(orderedPath => IsWithin(root.Path, orderedPath));
        await RefreshDestinationTreesAsync();
        SaveSettings();
    }

    private async Task RefreshAllTreesAsync()
    {
        await RefreshSourceTreeAsync();
        await RefreshDestinationTreesAsync();
    }

    private async Task MoveCurrentAsync(string destination)
    {
        var current = Current;
        if (current is null || _fileOperationInProgress)
        {
            return;
        }

        _fileOperationInProgress = true;
        StatusText.Text = $"Moving to {Path.GetFileName(destination)}…";
        try
        {
            var outcome = await _files.MoveAsync(current.Path, destination, (CollisionStrategy)Math.Clamp(CollisionBox.SelectedIndex, 0, 2));
            if (outcome.Skipped)
            {
                StatusText.Text = "Move skipped — a file with that name already exists";
                return;
            }
            _images.RemoveAt(_index);
            UpdateSourceCount();
            if (_index >= _images.Count)
            {
                _index = Math.Max(0, _images.Count - 1);
            }

            StatusText.Text = $"Moved to {Path.GetFileName(destination)} · Ctrl+Z to undo";
            await ShowCurrentAsync();
            await RefreshSourceTreeAsync();
            await UpdateDestinationCountAsync(destination);
        }
        catch (Exception ex)
        {
            if (!File.Exists(current.Path))
            {
                RemoveMissingSourceImage(current);
                await ShowCurrentAsync();
            }
            else
            {
                ShowError(ex);
            }
        }
        finally
        {
            _fileOperationInProgress = false;
        }
    }

    private async void UndoClick(object? sender, RoutedEventArgs e)
    {
        if (_sourcePath is null || _fileOperationInProgress)
        {
            return;
        }

        _fileOperationInProgress = true;
        try
        {
            var result = await _files.UndoAsync();
            await LoadSourceCoreAsync(_sourcePath, result.Destination);
            await RefreshSourceTreeAsync();
            var previousDestination = Path.GetDirectoryName(result.Source);
            if (previousDestination is not null)
            {
                await UpdateDestinationCountAsync(previousDestination);
            }

            StatusText.Text = "Last move undone";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            _fileOperationInProgress = false;
        }
    }

    private void DestinationCardsSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _destinationCardsWidth = e.NewSize.Width;
        UpdateDestinationCardWidths(_destinationCardsWidth);
    }

    private void UpdateDestinationCardWidths(double width)
    {
        if (width <= 0)
        {
            return;
        }

        var cardWidth = CalculateDestinationCardWidth(width);
        foreach (var target in _targets)
        {
            target.CardWidth = cardWidth;
        }
    }

    private static double CalculateDestinationCardWidth(double width)
    {
        const double minimumCardWidth = 184;
        const double horizontalMargin = 8;
        const double scrollbarAllowance = 10;
        if (width <= 0)
        {
            return minimumCardWidth;
        }

        var available = Math.Max(minimumCardWidth + horizontalMargin, width - scrollbarAllowance);
        var columns = Math.Max(1, (int)Math.Floor(available / (minimumCardWidth + horizontalMargin)));
        return Math.Max(minimumCardWidth, available / columns - horizontalMargin);
    }

    private void ToggleTrayClick(object? sender, RoutedEventArgs e)
    {
        _trayOpen = !_trayOpen;
        Workspace.RowDefinitions[2].Height = new GridLength(_trayOpen ? Math.Max(100, _settings.TrayHeight) : 32);
        TrayScroll.IsVisible = _trayOpen;
        TrayChevron.RenderTransform = new RotateTransform(_trayOpen ? 0 : 180);
    }

    private async void DestinationCardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging || e.Source is Button || sender is not Border card ||
            e.GetCurrentPoint(card).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased)
        {
            return;
        }

        if (sender is Border { Tag: string path })
        {
            await MoveCurrentAsync(path);
        }
    }

    private void DestinationCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border card || !e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(control => control is Button))
        {
            return;
        }

        if (card.DataContext is not DestinationTarget target)
        {
            return;
        }

        _reorderingTarget = target;
        _reorderStart = e.GetPosition(this);
        _reorderActive = false;
    }

    private bool HandleDestinationReorderPointerMoved(PointerEventArgs e)
    {
        if (_reorderingTarget is null)
        {
            return false;
        }

        var reorderPoint = e.GetPosition(this);
        if (!_reorderActive && Math.Sqrt(Math.Pow(reorderPoint.X - _reorderStart.X, 2) + Math.Pow(reorderPoint.Y - _reorderStart.Y, 2)) > 5)
        {
            _reorderActive = true;
            ReorderPreviewName.Text = _reorderingTarget.Name;
            ReorderAdorner.IsVisible = true;
            Cursor = DragMoveCursor;
        }

        if (_reorderActive)
        {
            PositionReorderPreview(e.GetPosition(ReorderAdorner));
            SetReorderOver(FindDestinationCard(e));
        }

        return true;
    }

    private bool HandleDestinationReorderPointerReleased(PointerReleasedEventArgs e)
    {
        if (_reorderingTarget is null)
        {
            return false;
        }

        var sourceTarget = _reorderingTarget;
        var wasActive = _reorderActive;
        var destinationCard = wasActive ? FindDestinationCard(e) ?? _reorderOver : null;
        var destinationTarget = destinationCard?.DataContext as DestinationTarget;
        SetReorderOver(null);
        _reorderingTarget = null;
        _reorderActive = false;
        ReorderAdorner.IsVisible = false;
        Cursor = null;
        if (wasActive)
        {
            e.Handled = true;
            if (destinationTarget is not null && !ReferenceEquals(sourceTarget, destinationTarget))
            {
                var oldIndex = _targets.IndexOf(sourceTarget);
                var newIndex = _targets.IndexOf(destinationTarget);
                if (oldIndex >= 0 && newIndex >= 0)
                {
                    _targets.Move(oldIndex, newIndex);
                    SaveDestinationOrder();
                    StatusText.Text = $"Moved {sourceTarget.Name} to position {newIndex + 1}";
                }
            }
        }

        return true;
    }

    private void PositionReorderPreview(Point point)
    {
        const double offset = 15;
        var left = Math.Min(point.X + offset, Math.Max(0, ReorderAdorner.Bounds.Width - ReorderPreview.Width));
        var top = Math.Min(point.Y + offset, Math.Max(0, ReorderAdorner.Bounds.Height - ReorderPreview.Height));
        Canvas.SetLeft(ReorderPreview, Math.Max(0, left));
        Canvas.SetTop(ReorderPreview, Math.Max(0, top));
    }

    private Border? FindDestinationCard(PointerEventArgs e)
    {
        foreach (var card in this.GetVisualDescendants().OfType<Border>().Where(item => item.Classes.Contains("destinationCard")).Reverse())
        {
            if (!card.IsVisible || card.Bounds.Width <= 0 || card.Bounds.Height <= 0)
            {
                continue;
            }

            if (new Rect(card.Bounds.Size).Contains(e.GetPosition(card)))
            {
                return card;
            }
        }
        return null;
    }

    private void SetReorderOver(Border? card)
    {
        if (ReferenceEquals(_reorderOver, card))
        {
            return;
        }

        if (_reorderOver is not null)
        {
            _reorderOver.Classes.Remove("reorderOver");
        }

        _reorderOver = card;
        if (_reorderOver is not null)
        {
            _reorderOver.Classes.Add("reorderOver");
        }
    }
}

