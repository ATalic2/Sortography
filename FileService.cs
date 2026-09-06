using System.Collections.ObjectModel;
using System.Text.Json;

namespace Sortography;

public sealed class FileService
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tif", ".tiff" };
    private readonly Stack<UndoRecord> _undo = new();

    public Task<List<ImageItem>> ScanImagesAsync(string path) => Task.Run(() =>
        Directory.EnumerateFiles(path)
            .Where(file => Extensions.Contains(System.IO.Path.GetExtension(file)))
            .Select(file => new FileInfo(file))
            .Select(file => new ImageItem { Path = file.FullName, Name = file.Name, Size = file.Length, Modified = file.LastWriteTime })
            .OrderBy(image => image.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());

    public Task<int> CountImagesAsync(string path) => Task.Run(() =>
        Directory.EnumerateFiles(path).Count(file => Extensions.Contains(System.IO.Path.GetExtension(file))));

    public Task<FolderNode> BuildTreeAsync(string path, bool expandAll = false) => Task.Run(() => BuildTree(path, expandAll));

    private static FolderNode BuildTree(string path, bool expandAll)
    {
        var directory = new DirectoryInfo(path);
        var node = new FolderNode
        {
            Path = directory.FullName,
            Name = directory.Name.Length > 0 ? directory.Name : directory.FullName,
            IsExpanded = expandAll,
            ImageCount = SafeFiles(directory).Count(file => Extensions.Contains(file.Extension))
        };
        foreach (var child in SafeDirectories(directory).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var childNode = BuildTree(child.FullName, expandAll);
                node.Children.Add(childNode);
                node.SourceChildren.Add(childNode);
            }
            catch
            {
                /* Inaccessible folders remain omitted without failing the rest of the tree. */
            }
        }
        return node;
    }

    private static IEnumerable<FileInfo> SafeFiles(DirectoryInfo directory)
    {
        try
        {
            return directory.EnumerateFiles().ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<DirectoryInfo> SafeDirectories(DirectoryInfo directory)
    {
        try
        {
            return directory.EnumerateDirectories().ToArray();
        }
        catch
        {
            return [];
        }
    }

    public Task<MoveOutcome> MoveAsync(string source, string destinationDirectory, CollisionStrategy strategy) => Task.Run(() =>
    {
        if (!File.Exists(source))
        {
            throw new IOException("The source image no longer exists.");
        }

        if (!Directory.Exists(destinationDirectory))
        {
            throw new IOException("The destination folder no longer exists.");
        }

        var target = System.IO.Path.Combine(destinationDirectory, System.IO.Path.GetFileName(source));
        if (PathsEqual(source, target))
        {
            return new MoveOutcome(source, target, true);
        }

        string? backup = null;
        if (File.Exists(target))
        {
            if (strategy == CollisionStrategy.Skip)
            {
                return new MoveOutcome(source, null, true);
            }

            if (strategy == CollisionStrategy.KeepBoth)
            {
                target = UniquePath(target);
            }

            if (strategy == CollisionStrategy.Replace)
            {
                var backupDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sortography-undo");
                Directory.CreateDirectory(backupDirectory);
                backup = UniquePath(System.IO.Path.Combine(backupDirectory, System.IO.Path.GetFileName(target)));
                Relocate(target, backup);
            }
        }
        try
        {
            Relocate(source, target);
        }
        catch
        {
            if (backup is not null && File.Exists(backup))
            {
                Relocate(backup, target);
            }

            throw;
        }
        lock (_undo)
        {
            _undo.Push(new(source, target, backup));
        }

        return new MoveOutcome(source, target, false);
    });

    public Task<MoveOutcome> UndoAsync() => Task.Run(() =>
    {
        UndoRecord record;
        lock (_undo)
        {
            if (!_undo.TryPop(out record!))
            {
                throw new InvalidOperationException("Nothing to undo.");
            }
        }
        if (!File.Exists(record.MovedTo))
        {
            throw new IOException("The moved image no longer exists.");
        }

        if (File.Exists(record.Original))
        {
            throw new IOException("The original location is occupied.");
        }

        Relocate(record.MovedTo, record.Original);
        if (record.ReplacedBackup is not null && File.Exists(record.ReplacedBackup))
        {
            Relocate(record.ReplacedBackup, record.MovedTo);
        }

        return new MoveOutcome(record.MovedTo, record.Original, false);
    });

    public static void CreateFolder(string parent, string name)
    {
        ValidateName(name);
        Directory.CreateDirectory(System.IO.Path.Combine(parent, name.Trim()));
    }
    public static string RenameFolder(string path, string name)
    {
        ValidateName(name);
        var target = System.IO.Path.Combine(Directory.GetParent(path)?.FullName ?? throw new IOException("Cannot rename this folder."), name.Trim());
        Directory.Move(path, target);
        return target;
    }
    public static void DeleteEmptyFolder(string path) => Directory.Delete(path, false);

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new IOException("Enter a valid folder name.");
        }
    }
    private static bool PathsEqual(string a, string b) => string.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static string UniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = System.IO.Path.GetDirectoryName(path)!;
        var stem = System.IO.Path.GetFileNameWithoutExtension(path);
        var extension = System.IO.Path.GetExtension(path);
        for (var index = 1; ; index++)
        {
            var candidate = System.IO.Path.Combine(directory, $"{stem} ({index}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
    private static void Relocate(string source, string target)
    {
        try
        {
            File.Move(source, target);
        }
        catch (IOException)
        {
            File.Copy(source, target, false);
            try
            {
                File.Delete(source);
            }
            catch
            {
                File.Delete(target);
                throw;
            }
        }
    }
    private sealed record UndoRecord(string Original, string MovedTo, string? ReplacedBackup);
}

public sealed record MoveOutcome(string Source, string? Destination, bool Skipped);

public sealed class SettingsService
{
    private readonly string _path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sortography", "settings.json");
    private readonly string _legacyPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImageSorterAvalonia", "settings.json");

    public AppSettings Load()
    {
        try
        {
            var settingsPath = File.Exists(_path) ? _path : _legacyPath;
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath)) ?? new();
        }
        catch
        {
            return new();
        }
    }
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}
