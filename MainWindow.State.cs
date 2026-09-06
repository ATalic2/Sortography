using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;

namespace Sortography;

/// <summary>
/// Contains the long-lived dependencies and UI state used by <see cref="MainWindow"/>.
/// Keeping these fields separate leaves the main code-behind focused on event handling.
/// </summary>
public partial class MainWindow
{
    // Reused cursors communicate whether the preview can be dragged or clicked.
    private static readonly Cursor DragMoveCursor = new(StandardCursorType.DragMove);
    private static readonly Cursor PreviewHandCursor = new(StandardCursorType.Hand);

    // Services perform filesystem operations and persist user preferences.
    private readonly FileService _files = new();
    private readonly SettingsService _settingsService = new();
    private readonly AppSettings _settings;

    // Collections provide the images, destination cards, and destination trees shown by the view.
    private readonly ObservableCollection<DestinationTarget> _targets = [];
    private readonly List<ImageItem> _images = [];
    private readonly List<FolderNode> _destinationTreeRoots = [];

    // Source state tracks the opened root, selected folder, and corresponding tree nodes.
    private FolderNode? _selectedSourceFolder;
    private string? _sourceRoot;
    private string? _sourcePath;

    // Navigation and operation state identify the displayed image and serialize filesystem work.
    private int _index;
    private bool _fileOperationInProgress;

    // Resolves the selected image without storing a second copy of the current item.
    private ImageItem? Current =>
        _images.Count > 0 && _index >= 0 && _index < _images.Count
            ? _images[_index]
            : null;

    // Preview state owns the decoded image resources and its zoomed position.
    private double _zoom = 1;
    private double _zoomOffsetX;
    private double _zoomOffsetY;
    private Bitmap? _bitmap;
    private MemoryStream? _animationStream;

    // Layout state remembers which resizable panes are currently visible.
    private bool _trayOpen = true;
    private bool _leftPaneOpen = true;
    private bool _rightPaneOpen = true;
    private double _destinationCardsWidth;

    // Pointer state supports image dragging and right-button panning.
    private bool _dragging;
    private bool _dragActive;
    private bool _panningImage;
    private Point _dragStart;
    private Point _panStart;
    private double _panStartOffsetX;
    private double _panStartOffsetY;
    private Border? _dragOver;

    // Destination reorder state tracks the card being moved and its current drop position.
    private DestinationTarget? _reorderingTarget;
    private Border? _reorderOver;
    private Point _reorderStart;
    private bool _reorderActive;

    // Keyboard context determines whether arrow keys navigate the source tree or images.
    private bool _sourceTreeKeyboardActive;
}
