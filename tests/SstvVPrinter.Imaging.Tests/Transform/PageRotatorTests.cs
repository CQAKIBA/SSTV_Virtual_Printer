using SstvVPrinter.Imaging.Model;
using SstvVPrinter.Imaging.Transform;

namespace SstvVPrinter.Imaging.Tests.Transform;

public sealed class PageRotatorTests
{
    [Theory]
    [InlineData(90, 3, 2, new byte[] { 5, 3, 1, 6, 4, 2 })]
    [InlineData(180, 2, 3, new byte[] { 6, 5, 4, 3, 2, 1 })]
    [InlineData(270, 3, 2, new byte[] { 2, 4, 6, 1, 3, 5 })]
    public void Rotate_moves_pixels_clockwise_and_updates_dimensions(
        int degrees, int expectedWidth, int expectedHeight, byte[] expectedRedValues)
    {
        var source = CreatePage();

        var rotated = PageRotator.Rotate(source, degrees);

        Assert.Equal(expectedWidth, rotated.Width);
        Assert.Equal(expectedHeight, rotated.Height);
        Assert.Equal(expectedRedValues, Pixels(rotated));
        Assert.Equal(expectedWidth, rotated.Metadata.PhysicalWidth);
        Assert.Equal(expectedHeight, rotated.Metadata.PhysicalHeight);
        Assert.Equal(0, rotated.Metadata.Orientation);
    }

    [Fact]
    public void Zero_degrees_returns_the_original_page()
    {
        var source = CreatePage();

        Assert.Same(source, PageRotator.Rotate(source, 0));
    }

    private static DecodedPage CreatePage()
    {
        byte[] pixels =
        [
            1, 0, 0, 2, 0, 0,
            3, 0, 0, 4, 0, 0,
            5, 0, 0, 6, 0, 0,
        ];
        return DecodedPage.Create(
            2, 3, pixels,
            new PwgPageMetadata(2, 3, 2, 3, 200, 300, 0, 20, 30));
    }

    private static byte[] Pixels(DecodedPage page) =>
        Enumerable.Range(0, page.Width * page.Height)
            .Select(index => page.GetPixel(index % page.Width, index / page.Width).R)
            .ToArray();
}
