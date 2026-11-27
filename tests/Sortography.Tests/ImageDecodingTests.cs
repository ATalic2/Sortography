using AnimatedImage.Avalonia;
using Avalonia;
using Avalonia.Media.Imaging;
using Xunit;

namespace Sortography.Tests;

public sealed class ImageDecodingTests
{
    static ImageDecodingTests()
    {
        Avalonia.Skia.SkiaPlatform.Initialize();
    }

    [Theory]
    [InlineData("sample.jpe")]
    [InlineData("sample.ico")]
    [InlineData("sample-bmp.ico")]
    [InlineData("sample.apng")]
    public void Preview_DecodesRealImage(string name)
    {
        using var bitmap = new Bitmap(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
        Assert.Equal(new PixelSize(32, 32), bitmap.PixelSize);
    }

    [Fact]
    public void Apng_RendersBothAnimationFrames()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.apng"));
        using var renderer = new AnimatedImageSourceStream(stream).TryCreate();
        Assert.NotNull(renderer);
        Assert.Equal(2, renderer.FrameCount);
        renderer.ProcessFrame(0);
        var red = new byte[4];
        renderer.Current.ReadBGRA(red, 0, 0, 1, 1);
        renderer.ProcessFrame(1);
        var blue = new byte[4];
        renderer.Current.ReadBGRA(blue, 0, 0, 1, 1);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, red);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, blue);
    }

    [Fact]
    public async Task ImageDiscovery_IncludesVerifiedFormatsAndExcludesAvif()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var service = new FileService();
        var images = await service.ScanImagesAsync(path);
        Assert.Equal(new[] { "sample-bmp.ico", "sample.apng", "sample.ico", "sample.jpe" },
            images.Select(image => image.Name));
        Assert.Equal(4, await service.CountImagesAsync(path));
        Assert.Equal(4, (await service.BuildTreeAsync(path)).ImageCount);
    }
}
