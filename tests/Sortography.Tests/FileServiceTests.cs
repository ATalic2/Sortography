using Sortography;
using Xunit;

namespace Sortography.Tests;

public sealed class FileServiceTests
{
    [Fact]
    public async Task ScanImagesAsync_ReturnsSupportedImagesInNameOrder()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "zebra.JPG"), "image");
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "Alpha.png"), "image");
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "notes.txt"), "not an image");

        var images = await new FileService().ScanImagesAsync(directory.Path);

        Assert.Equal(["Alpha.png", "zebra.JPG"], images.Select(image => image.Name));
    }

    [Fact]
    public async Task MoveAsync_KeepBoth_PreservesExistingFile()
    {
        using var sourceDirectory = new TemporaryDirectory();
        using var destinationDirectory = new TemporaryDirectory();
        var source = Path.Combine(sourceDirectory.Path, "photo.jpg");
        var existing = Path.Combine(destinationDirectory.Path, "photo.jpg");
        await File.WriteAllTextAsync(source, "new image");
        await File.WriteAllTextAsync(existing, "existing image");

        var result = await new FileService().MoveAsync(
            source,
            destinationDirectory.Path,
            CollisionStrategy.KeepBoth);

        Assert.False(result.Skipped);
        Assert.Equal("existing image", await File.ReadAllTextAsync(existing));
        Assert.NotNull(result.Destination);
        Assert.Equal("new image", await File.ReadAllTextAsync(result.Destination));
        Assert.NotEqual(existing, result.Destination);
        Assert.False(File.Exists(source));
    }

    [Fact]
    public async Task MoveAsync_ReplaceThenUndo_RestoresBothFiles()
    {
        using var sourceDirectory = new TemporaryDirectory();
        using var destinationDirectory = new TemporaryDirectory();
        var source = Path.Combine(sourceDirectory.Path, "photo.jpg");
        var destination = Path.Combine(destinationDirectory.Path, "photo.jpg");
        await File.WriteAllTextAsync(source, "new image");
        await File.WriteAllTextAsync(destination, "existing image");
        var service = new FileService();

        await service.MoveAsync(source, destinationDirectory.Path, CollisionStrategy.Replace);
        var result = await service.UndoAsync();

        Assert.Equal(source, result.Destination);
        Assert.Equal("new image", await File.ReadAllTextAsync(source));
        Assert.Equal("existing image", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public void CreateFolder_RejectsBlankNames()
    {
        using var directory = new TemporaryDirectory();

        var exception = Assert.Throws<IOException>(() => FileService.CreateFolder(directory.Path, "   "));

        Assert.Equal("Enter a valid folder name.", exception.Message);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sortography-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path
        {
            get;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
