using SstvVPrinter.Imaging.Model;
using SstvVPrinter.Imaging.Resampling;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Tests.Resampling;

public sealed class DetailPreservingResamplerTests
{
    [Theory]
    [InlineData(4, 2, 8, 8, 8, 4, 0, 2)]
    [InlineData(2, 4, 8, 8, 4, 8, 2, 0)]
    [InlineData(4, 4, 8, 6, 6, 6, 1, 0)]
    [InlineData(4, 2, 8, 4, 8, 4, 0, 0)]
    public void Fit_preserves_aspect_and_centers_content(
        int sourceWidth, int sourceHeight, int targetWidth, int targetHeight,
        int fittedWidth, int fittedHeight, int x, int y)
    {
        var result = new DetailPreservingResampler().Fit(
            SolidPage(sourceWidth, sourceHeight, new Rgb24(40, 50, 60)), targetWidth, targetHeight);

        Assert.Equal(new PixelRect(x, y, fittedWidth, fittedHeight), result.ContentBounds);
        Assert.Equal(targetWidth, result.Width);
        Assert.Equal(targetHeight, result.Height);
        Assert.Equal(new Rgb24(40, 50, 60), result.GetPixel(x, y));
        if (y > 0)
        {
            Assert.Equal(new Rgb24(0, 0, 0), result.GetPixel(0, 0));
        }
    }

    [Theory]
    [InlineData(20000, 1, 10, 10, 10, 1)]
    [InlineData(1, 20000, 10, 10, 1, 10)]
    public void Fit_extreme_aspect_ratios_keeps_one_visible_axis_pixel(
        int sourceWidth, int sourceHeight, int targetWidth, int targetHeight,
        int fittedWidth, int fittedHeight)
    {
        var result = new DetailPreservingResampler().Fit(
            SolidPage(sourceWidth, sourceHeight, new Rgb24(255, 255, 255)), targetWidth, targetHeight);

        Assert.Equal(fittedWidth, result.ContentBounds.Width);
        Assert.Equal(fittedHeight, result.ContentBounds.Height);
    }

    [Fact]
    public void Fit_is_deterministic()
    {
        var source = SolidPage(7, 3, new Rgb24(31, 127, 223));
        var first = new DetailPreservingResampler().Fit(source, 11, 8);
        var second = new DetailPreservingResampler().Fit(source, 11, 8);

        for (var y = 0; y < 8; y++)
            for (var x = 0; x < 11; x++)
                Assert.Equal(first.GetPixel(x, y), second.GetPixel(x, y));
    }

    [Fact]
    public void Downscale_retains_isolated_details()
    {
        var blackStroke = Page(8, 1, index => index == 3 ? new(0, 0, 0) : new(255, 255, 255));
        var whiteStroke = Page(8, 1, index => index == 3 ? new(255, 255, 255) : new(0, 0, 0));
        var redMark = Page(8, 1, index => index == 3 ? new(255, 0, 0) : new(0, 0, 0));

        var blackResult = new DetailPreservingResampler().Fit(blackStroke, 1, 1).GetPixel(0, 0);
        var whiteResult = new DetailPreservingResampler().Fit(whiteStroke, 1, 1).GetPixel(0, 0);
        var redResult = new DetailPreservingResampler().Fit(redMark, 1, 1).GetPixel(0, 0);

        Assert.True(blackResult.R < 230, $"Expected preserved dark stroke, got {blackResult.R}.");
        Assert.True(whiteResult.R > 115, $"Expected preserved light stroke, got {whiteResult.R}.");
        Assert.True(redResult.R > 115 && redResult.G == 0 && redResult.B == 0);
    }

    [Fact]
    public void Downscale_does_not_overdrive_smooth_gradients_or_uniform_fields()
    {
        var uniform = new DetailPreservingResampler().Fit(SolidPage(32, 8, new(80, 120, 160)), 4, 1);
        Assert.Equal(new Rgb24(80, 120, 160), uniform.GetPixel(0, 0));

        var gradient = Page(256, 1, index => new((byte)index, (byte)index, (byte)index));
        var result = new DetailPreservingResampler().Fit(gradient, 64, 1);
        byte previous = 0;
        for (var x = 0; x < 64; x++)
        {
            var value = result.GetPixel(x, 0).R;
            Assert.True(value >= previous);
            Assert.InRange(Math.Abs(value - ((x * 4) + 2)), 0, 14);
            previous = value;
        }
    }

    [Fact]
    public void Fit_renders_native_640_width_and_observes_cancellation()
    {
        var source = Page(640, 1, index => new((byte)(index % 256), 0, 0));
        var result = new DetailPreservingResampler().Fit(source, 640, 1);
        Assert.NotEqual(result.GetPixel(318, 0), result.GetPixel(319, 0));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            new DetailPreservingResampler().Fit(source, 320, 1, cancellation.Token));
    }

    [Fact]
    public void Enlarge_preserves_nonuniform_left_and_top_edges()
    {
        var horizontal = Page(2, 1, index => index == 0 ? new(0, 0, 0) : new(255, 255, 255));
        var horizontalResult = new DetailPreservingResampler().Fit(horizontal, 4, 2);
        Assert.Equal(new Rgb24(0, 0, 0), horizontalResult.GetPixel(0, 0));
        Assert.True(horizontalResult.GetPixel(1, 0).R < horizontalResult.GetPixel(2, 0).R);
        Assert.Equal(new Rgb24(255, 255, 255), horizontalResult.GetPixel(3, 0));

        var vertical = Page(1, 2, index => index == 0 ? new(0, 0, 0) : new(255, 255, 255));
        var verticalResult = new DetailPreservingResampler().Fit(vertical, 2, 4);
        Assert.Equal(new Rgb24(0, 0, 0), verticalResult.GetPixel(0, 0));
        Assert.True(verticalResult.GetPixel(0, 1).R < verticalResult.GetPixel(0, 2).R);
        Assert.Equal(new Rgb24(255, 255, 255), verticalResult.GetPixel(0, 3));
    }

    [Fact]
    public async Task Downscale_observes_cancellation_inside_large_source_footprint()
    {
        var source = SolidPage(5000, 5000, new(80, 90, 100));
        using var cancellation = new CancellationTokenSource();
        var task = Task.Run(() => new DetailPreservingResampler().Fit(source, 1, 1, cancellation.Token));
        await Task.Delay(20);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(500), $"Cancellation took {stopwatch.Elapsed}.");
    }

    private static DecodedPage SolidPage(int width, int height, Rgb24 color)
    {
        var bytes = new byte[checked(width * height * 3)];
        for (var offset = 0; offset < bytes.Length; offset += 3)
        {
            bytes[offset] = color.R;
            bytes[offset + 1] = color.G;
            bytes[offset + 2] = color.B;
        }

        return DecodedPage.Create(width, height, bytes,
            new PwgPageMetadata(width, height, width, height, 300, 300, 0, 612, 792));
    }

    private static DecodedPage Page(int width, int height, Func<int, Rgb24> pixel)
    {
        var bytes = new byte[checked(width * height * 3)];
        for (var index = 0; index < width * height; index++)
        {
            var color = pixel(index);
            bytes[index * 3] = color.R;
            bytes[(index * 3) + 1] = color.G;
            bytes[(index * 3) + 2] = color.B;
        }

        return DecodedPage.Create(width, height, bytes,
            new PwgPageMetadata(width, height, width, height, 300, 300, 0, 612, 792));
    }
}
