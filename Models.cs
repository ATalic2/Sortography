using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sortography;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class ImageItem
{
    public required string Path
    {
        get; init;
    }
    public required string Name
    {
        get; init;
    }
    public long Size
    {
        get; init;
    }
    public DateTime Modified
    {
        get; init;
    }
}

public sealed class FolderNode : ObservableObject
{
    private bool _isExpanded;
    private bool _isSourceSelected;
    private int _imageCount;
    public required string Path
    {
        get; init;
    }
    public required string Name
    {
        get; init;
    }
    public int ImageCount
    {
        get => _imageCount; set => Set(ref _imageCount, value);
    }
    public ObservableCollection<FolderNode> Children { get; } = [];
    public ObservableCollection<object> SourceChildren { get; } = [];
    public bool IsExpanded
    {
        get => _isExpanded; set => Set(ref _isExpanded, value);
    }
    public bool IsSourceSelected
    {
        get => _isSourceSelected; set => Set(ref _isSourceSelected, value);
    }
}

public sealed class ImageNode : ObservableObject
{
    private bool _isCurrent;
    public required string Path
    {
        get; init;
    }
    public required string Name
    {
        get; init;
    }
    public bool IsCurrent
    {
        get => _isCurrent; set => Set(ref _isCurrent, value);
    }
}

public sealed class DestinationRoot
{
    public required string Path
    {
        get; init;
    }
    public required string Name
    {
        get; init;
    }
    public required string Color
    {
        get; init;
    }
}

public sealed class DestinationTarget : ObservableObject
{
    private string _shortcut = "—";
    private double _cardWidth = 184;
    private string _color = "#EF8F5A";
    private string _borderColor = "#33383C";
    public required string Path
    {
        get; init;
    }
    public required string RootPath
    {
        get; init;
    }
    public required string Name
    {
        get; init;
    }
    public required string Color
    {
        get => _color; set => Set(ref _color, value);
    }
    public string BorderColor
    {
        get => _borderColor; set => Set(ref _borderColor, value);
    }
    public int Depth
    {
        get; init;
    }
    public string Shortcut
    {
        get => _shortcut; set => Set(ref _shortcut, value);
    }
    public double CardWidth
    {
        get => _cardWidth; set => Set(ref _cardWidth, value);
    }
}

public enum CollisionStrategy
{
    KeepBoth, Skip, Replace
}

public sealed class AppSettings
{
    public List<DestinationRoot> Destinations { get; set; } = [];
    public Dictionary<int, string> Shortcuts { get; set; } = [];
    public Dictionary<string, string> DestinationColors { get; set; } = [];
    public List<string> DestinationOrder { get; set; } = [];
    public bool ShortcutDefaultsInitialized
    {
        get; set;
    }
    public double LeftWidth { get; set; } = 278;
    public double RightWidth { get; set; } = 238;
    public double TrayHeight { get; set; } = 180;
}
