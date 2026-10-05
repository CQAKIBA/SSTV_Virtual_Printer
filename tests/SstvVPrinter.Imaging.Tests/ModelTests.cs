using SstvVPrinter.Imaging.Model;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Tests;

public sealed class ModelTests
{
    private static readonly PwgPageMetadata Metadata = new(2, 1, 2, 1, 300, 300, 0, 612, 792);

    [Fact]
    public void DecodedPage_copies_source_bytes_and_returns_row_major_pixels()
    {
        byte[] bytes = [1, 2, 3, 4, 5, 6];
        var page = DecodedPage.Create(2, 1, bytes, Metadata);

        bytes[0] = 99;

        Assert.Equal(new Rgb24(1, 2, 3), page.GetPixel(0, 0));
        Assert.Equal(new Rgb24(4, 5, 6), page.GetPixel(1, 0));
        Assert.Equal(2, page.Width);
        Assert.Equal(1, page.Height);
        Assert.Same(Metadata, page.Metadata);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    public void DecodedPage_rejects_invalid_dimensions(int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DecodedPage.Create(width, height, [], Metadata));

    [Fact]
    public void DecodedPage_rejects_wrong_byte_count() =>
        Assert.Throws<ArgumentException>(() => DecodedPage.Create(2, 1, [1, 2, 3], Metadata));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 1)]
    public void DecodedPage_rejects_out_of_range_coordinates(int x, int y)
    {
        var page = DecodedPage.Create(2, 1, [1, 2, 3, 4, 5, 6], Metadata);
        Assert.Throws<ArgumentOutOfRangeException>(() => page.GetPixel(x, y));
    }

    [Fact]
    public void PixelRect_validates_and_calculates_checked_edges()
    {
        var rectangle = new PixelRect(3, 4, 5, 6);
        Assert.Equal(8, rectangle.Right);
        Assert.Equal(10, rectangle.Bottom);

        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelRect(-1, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelRect(0, -1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelRect(0, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelRect(0, 0, 1, 0));
        Assert.Throws<OverflowException>(() => new PixelRect(int.MaxValue, 0, 1, 1));
    }
}
