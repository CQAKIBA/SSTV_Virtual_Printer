using SstvVPrinter.Imaging.Composition;
using SstvVPrinter.Imaging.Header;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Tests.Header;

public sealed class RadiofaxHeaderRendererTests
{
    [Theory]
    [InlineData(SstvMode.Radiofax120Ioc288, 864, 32, "FAX288")]
    [InlineData(SstvMode.Radiofax120Ioc576, 1728, 64, "FAX576")]
    public void Draw_renders_eight_wmo_gray_cells_outline_and_identification(
        SstvMode mode,
        int width,
        int headerHeight,
        string modeLabel)
    {
        var request = new CompositionRequest(
            mode,
            1,
            "ja1umw",
            new DateTimeOffset(2026, 10, 19, 1, 23, 0, TimeSpan.Zero));
        var pixels = Enumerable.Repeat((byte)211, width * (headerHeight + 1) * 3).ToArray();

        RadiofaxHeaderRenderer.Draw(pixels, width, headerHeight + 1, request);

        Assert.Equal($"2026-10-19 10:23 JST {modeLabel}", RadiofaxHeaderRenderer.MetadataText(request));
        AssertPixel(pixels, width, 0, 0, 0);
        AssertPixel(pixels, width, (8 * headerHeight) - 1, headerHeight - 1, 0);
        byte[] levels = [0, 36, 73, 109, 146, 182, 219, 255];
        for (var cell = 0; cell < levels.Length; cell++)
        {
            AssertPixel(pixels, width, (cell * headerHeight) + (headerHeight / 2), headerHeight / 2, levels[cell]);
        }

        AssertPixel(pixels, width, width / 2, headerHeight, 211);
        Assert.Contains((byte)0, pixels.AsSpan((8 * headerHeight + 1) * 3, (width - (8 * headerHeight + 1)) * headerHeight * 3).ToArray());
    }

    private static void AssertPixel(byte[] pixels, int width, int x, int y, byte expected)
    {
        var offset = ((y * width) + x) * 3;
        Assert.Equal(expected, pixels[offset]);
        Assert.Equal(expected, pixels[offset + 1]);
        Assert.Equal(expected, pixels[offset + 2]);
    }
}
