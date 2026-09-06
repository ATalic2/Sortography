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

// Manages image display, navigation, zoom, panning, and image drag feedback.
public partial class MainWindow
{
    private void PreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Current is null)
        {
            return;
        }

        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsRightButtonPressed)
        {
            _panningImage = true;
            _panStart = e.GetPosition(this);
            _panStartOffsetX = _zoomOffsetX;
            _panStartOffsetY = _zoomOffsetY;
            PreviewImage.Cursor = DragMoveCursor;
            e.Pointer.Capture(PreviewImage);
            e.Handled = true;
            return;
        }
        if (!properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragging = true;
        _dragActive = false;
        _dragStart = e.GetPosition(this);
    }

    private bool HandlePreviewPanPointerMoved(PointerEventArgs e)
    {
        if (!_panningImage)
        {
            return false;
        }

        var panPoint = e.GetPosition(this);
        _zoomOffsetX = _panStartOffsetX + panPoint.X - _panStart.X;
        _zoomOffsetY = _panStartOffsetY + panPoint.Y - _panStart.Y;
        ApplyZoomTransform();
        e.Handled = true;
        return true;
    }

    private void HandleImageDragPointerMoved(PointerEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (!_dragActive && Math.Sqrt(Math.Pow(point.X - _dragStart.X, 2) + Math.Pow(point.Y - _dragStart.Y, 2)) > 5)
        {
            _dragActive = true;
            DragAdorner.IsVisible = true;
            Cursor = DragMoveCursor;
            PreviewImage.Cursor = DragMoveCursor;
        }

        if (!_dragActive)
        {
            return;
        }

        PositionDragThumbnail(e.GetPosition(DragAdorner));
        SetDragOver(FindDropTarget(e));
    }

    private bool HandlePreviewPanPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_panningImage)
        {
            return false;
        }

        _panningImage = false;
        e.Pointer.Capture(null);
        PreviewImage.Cursor = PreviewHandCursor;
        e.Handled = true;
        return true;
    }

    private async Task HandleImageDragPointerReleasedAsync(PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var wasDragActive = _dragActive;
        var target = _dragActive ? FindDropTarget(e) ?? _dragOver : null;
        var path = target?.Tag as string;
        SetDragOver(null);
        _dragging = false;
        _dragActive = false;
        DragAdorner.IsVisible = false;
        Cursor = null;
        PreviewImage.Cursor = PreviewHandCursor;
        if (wasDragActive)
        {
            e.Handled = true;
        }

        if (path is not null)
        {
            e.Handled = true;
            await MoveCurrentAsync(path);
        }
    }

    private async void PreviewHostPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (Current is null || e.Handled)
        {
            return;
        }

        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(control => control is Button))
        {
            return;
        }

        if (e.GetCurrentPoint(PreviewHost).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased)
        {
            return;
        }

        var direction = e.GetPosition(PreviewHost).X < PreviewHost.Bounds.Width / 2 ? -1 : 1;
        e.Handled = true;
        await ChangeIndexAsync(direction);
    }

    private void PrepareDragThumbnail()
    {
        if (_bitmap is null)
        {
            return;
        }

        const double maximum = 150;
        const double minimum = 72;
        var width = _bitmap.PixelSize.Width;
        var height = _bitmap.PixelSize.Height;
        if (width >= height)
        {
            DragThumbnail.Width = maximum;
            DragThumbnail.Height = Math.Max(minimum, maximum * height / width);
        }
        else
        {
            DragThumbnail.Height = maximum;
            DragThumbnail.Width = Math.Max(minimum, maximum * width / height);
        }
    }

    private void PositionDragThumbnail(Point point)
    {
        const double offset = 18;
        var left = point.X + offset;
        var top = point.Y + offset;
        if (left + DragThumbnail.Width > DragAdorner.Bounds.Width)
        {
            left = point.X - DragThumbnail.Width - offset;
        }

        if (top + DragThumbnail.Height > DragAdorner.Bounds.Height)
        {
            top = point.Y - DragThumbnail.Height - offset;
        }

        Canvas.SetLeft(DragThumbnail, Math.Max(0, left));
        Canvas.SetTop(DragThumbnail, Math.Max(0, top));
    }

    private Border? FindDropTarget(PointerEventArgs e)
    {
        foreach (var target in this.GetVisualDescendants().OfType<Border>().Where(item => item.Classes.Contains("dropTarget")).Reverse())
        {
            if (!target.IsVisible || target.Bounds.Width <= 0 || target.Bounds.Height <= 0)
            {
                continue;
            }

            var pointInsideTarget = e.GetPosition(target);
            if (new Rect(target.Bounds.Size).Contains(pointInsideTarget))
            {
                return target;
            }
        }
        return null;
    }

    private void SetDragOver(Border? target)
    {
        if (ReferenceEquals(_dragOver, target))
        {
            return;
        }

        if (_dragOver is not null)
        {
            _dragOver.Classes.Remove("dragOver");
        }

        _dragOver = target;
        if (_dragOver is not null)
        {
            _dragOver.Classes.Add("dragOver");
        }
    }

    private async Task ShowCurrentAsync()
    {
        ClearAnimatedSource();
        _bitmap?.Dispose();
        _bitmap = null;
        while (Current is { } missingCandidate && !File.Exists(missingCandidate.Path))
        {
            RemoveMissingSourceImage(missingCandidate);
        }

        var current = Current;
        if (current is null)
        {
            PreviewImage.Source = null;
            DragThumbnailImage.Source = null;
            PreviewImage.IsVisible = false;
            EmptyState.IsVisible = true;
            EmptyTitle.Text = _sourcePath is null ? "Start with a source folder" : "No images in this folder";
            EmptyMessage.Text = _sourcePath is null ? "Open a folder containing images to begin." : "Select another source folder or subfolder.";
            PositionText.Text = "0 / 0";
            StatusFile.Text = "No image";
            Progress.Value = 0;
            ClearFileInfo();
            UpdateSourceImageHighlight();
            return;
        }
        LoadingOverlay.IsVisible = true;
        LoadingText.Text = "Loading preview…";
        await Task.Yield();
        try
        {
            _bitmap = new Bitmap(current.Path);
            DragThumbnailImage.Source = _bitmap;
            PrepareDragThumbnail();
            var extension = Path.GetExtension(current.Path);
            if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
            {
                _animationStream = new MemoryStream(File.ReadAllBytes(current.Path), writable: false);
                ImageBehavior.SetAnimatedSource(PreviewImage, new AnimatedImageSourceStream(_animationStream));
            }
            else
            {
                PreviewImage.Source = _bitmap;
            }
            PreviewImage.IsVisible = true;
            EmptyState.IsVisible = false;
            ShowFileDimensions(_bitmap.PixelSize.Width, _bitmap.PixelSize.Height);
        }
        catch when (!File.Exists(current.Path))
        {
            RemoveMissingSourceImage(current);
            await ShowCurrentAsync();
            return;
        }
        catch
        {
            ClearAnimatedSource();
            PreviewImage.IsVisible = false;
            EmptyState.IsVisible = true;
            EmptyTitle.Text = "Preview unavailable";
            EmptyMessage.Text = current.Name;
        }
        finally
        {
            LoadingOverlay.IsVisible = false;
        }
        ShowFileInfo(current);
        PositionText.Text = $"{_index + 1} / {_images.Count}";
        StatusFile.Text = current.Name;
        Progress.Value = (double)(_index + 1) / _images.Count;
        UpdateSourceImageHighlight();
        ResetZoom();
    }

    private void ClearAnimatedSource()
    {
        PreviewImage.ClearValue(ImageBehavior.AnimatedSourceProperty);
        _animationStream?.Dispose();
        _animationStream = null;
    }

    private async Task ChangeIndexAsync(int amount)
    {
        if (_images.Count == 0)
        {
            return;
        }

        _index = Math.Clamp(_index + amount, 0, _images.Count - 1);
        await ShowCurrentAsync();
    }

    private async void PreviousClick(object? s, RoutedEventArgs e) => await ChangeIndexAsync(-1);

    private async void NextClick(object? s, RoutedEventArgs e) => await ChangeIndexAsync(1);

    private void ZoomInClick(object? s, RoutedEventArgs e) => SetZoom(_zoom + .15);

    private void ZoomOutClick(object? s, RoutedEventArgs e) => SetZoom(_zoom - .15);

    private void ResetZoomClick(object? s, RoutedEventArgs e) => ResetZoom();

    private void PreviewWheelChanged(object? s, PointerWheelEventArgs e)
    {
        SetZoom(_zoom + (e.Delta.Y > 0 ? .12 : -.12), e.GetPosition(PreviewImage));
        e.Handled = true;
    }

    private void SetZoom(double value, Point? pointerAnchor = null)
    {
        var nextZoom = Math.Clamp(value, .15, 5);
        if (Math.Abs(nextZoom - _zoom) < .0001)
        {
            return;
        }

        var anchor = pointerAnchor ?? new Point(
            (PreviewImage.Bounds.Width / 2 - _zoomOffsetX) / _zoom,
            (PreviewImage.Bounds.Height / 2 - _zoomOffsetY) / _zoom);

        _zoomOffsetX += anchor.X * (_zoom - nextZoom);
        _zoomOffsetY += anchor.Y * (_zoom - nextZoom);
        _zoom = nextZoom;
        ApplyZoomTransform();
    }

    private void ResetZoom()
    {
        _zoom = 1;
        _zoomOffsetX = 0;
        _zoomOffsetY = 0;
        ApplyZoomTransform();
    }

    private void ApplyZoomTransform()
    {
        PreviewImage.RenderTransformOrigin = RelativePoint.TopLeft;
        PreviewImage.RenderTransform = new MatrixTransform(new Matrix(_zoom, 0, 0, _zoom, _zoomOffsetX, _zoomOffsetY));
        ZoomText.Text = $"{_zoom * 100:0}%";
    }
}

