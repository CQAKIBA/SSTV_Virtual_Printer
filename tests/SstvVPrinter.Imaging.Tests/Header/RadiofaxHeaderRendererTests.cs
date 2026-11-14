using SstvVPrinter.Imaging.Composition;
using SstvVPrinter.Imaging.Header;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Tests.Header;

public sealed class RadiofaxHeaderRendererTests
{
    [Theory]
    [InlineData(SstvMode.Radiofax120Ioc288Sync20Ms, 864, 32, "FAX288")]
    [InlineData(SstvMode.Radiofax120Ioc576Sync20Ms, 1728, 64, "FAX576")]
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

    [Theory]
    [InlineData(SstvMode.Radiofax120Ioc288Sync20Ms, 864, 32)]
    [InlineData(SstvMode.Radiofax120Ioc576Sync20Ms, 1728, 64)]
    public void Draw_leaves_a_clear_gap_before_a_long_call_sign(SstvMode mode, int width, int height)
    {
        const string callSign = "EA8/JA1UMW/P";
        var request = new CompositionRequest(mode, 1, callSign, DateTimeOffset.UnixEpoch);
        var pixels = Enumerable.Repeat((byte)211, width * height * 3).ToArray();
        RadiofaxHeaderRenderer.Draw(pixels, width, height, request);
        var unit = height / 16;
        var callSignX = width - (2 + callSign.Length * 12) * unit;
        for (var y = 0; y < height; y++)
        for (var x = callSignX - 2 * unit; x < callSignX; x++)
            AssertPixel(pixels, width, x, y, 211);
        AssertPixel(pixels, width, callSignX, unit, 0);
        AssertPixel(pixels, width, callSignX + (callSign.Length - 1) * 12 * unit, unit, 0);
    }

    private static void AssertPixel(byte[] pixels, int width, int x, int y, byte expected)
    {
        var offset = ((y * width) + x) * 3;
        Assert.Equal(expected, pixels[offset]);
        Assert.Equal(expected, pixels[offset + 1]);
        Assert.Equal(expected, pixels[offset + 2]);
    }
}
