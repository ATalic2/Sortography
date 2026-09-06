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

// Owns window construction, lifecycle wiring, and top-level event registration.
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        Workspace.ColumnDefinitions[0].Width = new GridLength(Math.Clamp(_settings.LeftWidth, 180, 500));
        Workspace.ColumnDefinitions[4].Width = new GridLength(Math.Clamp(_settings.RightWidth, 180, 450));
        Workspace.RowDefinitions[2].Height = new GridLength(Math.Clamp(_settings.TrayHeight, 100, 450));
        DestinationCards.ItemsSource = _targets;
        AddHandler(KeyDownEvent, WindowKeyDown, RoutingStrategies.Tunnel, true);
        AddHandler(PointerPressedEvent, GlobalPointerPressed, RoutingStrategies.Tunnel, true);
        AddHandler(PointerMovedEvent, GlobalPointerMoved, RoutingStrategies.Tunnel, true);
        AddHandler(PointerReleasedEvent, GlobalPointerReleased, RoutingStrategies.Tunnel, true);
        Loaded += async (_, _) => await RefreshDestinationTreesAsync();
        Closing += (_, _) => { ClearAnimatedSource(); SaveSettings(); };
    }
}

