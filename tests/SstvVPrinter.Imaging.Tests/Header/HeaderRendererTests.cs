using SstvVPrinter.Imaging.Composition;
using SstvVPrinter.Imaging.Header;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Tests.Header;

public sealed class HeaderRendererTests
{
    [Theory]
    [InlineData(SstvMode.Martin1, "M1")]
    [InlineData(SstvMode.Scottie1, "S1")]
    [InlineData(SstvMode.Scottie2, "S2")]
    [InlineData(SstvMode.Robot36, "R36")]
    public void Layout_uses_fixed_JST_and_mode_label(SstvMode mode, string label)
    {
        var accepted = new DateTimeOffset(2026, 10, 3, 0, 34, 0, TimeSpan.Zero);
        var layout = HeaderRenderer.MeasureAndSelect(new(mode, 1, "ja1umw", accepted), 320);

        Assert.Equal($"2026-10-03 09:34 JST {label}", layout.MetadataText);
        Assert.Equal("JA1UMW", layout.CallSign);
        Assert.Equal(320 - 2 - (6 * 12), layout.CallSignX);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Draw_paints_exact_scaled_patches_and_opaque_text(int scale)
    {
        var width = 320 * scale;
        var pixels = Enumerable.Repeat((byte)255, width * 16 * 3).ToArray();
        var request = new CompositionRequest(SstvMode.Martin1, scale, "JA1UMW", DateTimeOffset.UnixEpoch);
        var layout = HeaderRenderer.MeasureAndSelect(request, width);

        HeaderRenderer.Draw(pixels, width, 16, layout);

        AssertPixel(pixels, width, 0, 0, 255, 0, 0);
        AssertPixel(pixels, width, (16 * scale) - 1, 15, 255, 0, 0);
        AssertPixel(pixels, width, 16 * scale, 0, 0, 255, 0);
        AssertPixel(pixels, width, 32 * scale, 0, 0, 0, 255);
        var textRegionContainsBlack = false;
        for (var y = 1; y < 15; y++)
            for (var x = layout.MetadataX; x < layout.CallSignX; x++)
                textRegionContainsBlack |= pixels[((y * width) + x) * 3] == 0;
        Assert.True(textRegionContainsBlack);
        AssertPixel(pixels, width, layout.MetadataX, 0, 255, 255, 255);
        AssertPixel(pixels, width, layout.MetadataX, 15, 255, 255, 255);
    }

    [Fact]
    public void Draw_leaves_two_pixels_between_double_width_callsign_glyphs()
    {
        const int width = 320;
        var pixels = Enumerable.Repeat((byte)255, width * 16 * 3).ToArray();
        var request = new CompositionRequest(SstvMode.Scottie2, 1, "UU", DateTimeOffset.UnixEpoch);
        var layout = HeaderRenderer.MeasureAndSelect(request, width);

        HeaderRenderer.Draw(pixels, width, 16, layout);

        AssertPixel(pixels, width, layout.CallSignX + 8, 1, 0, 0, 0);
        AssertPixel(pixels, width, layout.CallSignX + 9, 1, 0, 0, 0);
        AssertPixel(pixels, width, layout.CallSignX + 10, 1, 255, 255, 255);
        AssertPixel(pixels, width, layout.CallSignX + 11, 1, 255, 255, 255);
        AssertPixel(pixels, width, layout.CallSignX + 12, 1, 0, 0, 0);
    }

    [Fact]
    public void Layout_falls_back_before_colliding_with_callsign()
    {
        var request = new CompositionRequest(
            SstvMode.Martin1, 1, "JA1UMW", new DateTimeOffset(2026, 10, 3, 0, 34, 0, TimeSpan.Zero));

        Assert.Equal("2026-10-03 09:34 JST M1", HeaderRenderer.MeasureAndSelect(request, 268).MetadataText);
        Assert.Equal("10-03 09:34 JST M1", HeaderRenderer.MeasureAndSelect(request, 267).MetadataText);
        Assert.Equal("10-03 09:34 JST M1", HeaderRenderer.MeasureAndSelect(request, 238).MetadataText);
        Assert.Equal("09:34 JST M1", HeaderRenderer.MeasureAndSelect(request, 237).MetadataText);
        Assert.Equal("09:34 JST M1", HeaderRenderer.MeasureAndSelect(request, 202).MetadataText);
        Assert.Equal("JST M1", HeaderRenderer.MeasureAndSelect(request, 201).MetadataText);
        Assert.Throws<ArgumentException>(() => HeaderRenderer.MeasureAndSelect(request, 159));
    }

    [Fact]
    public void Layout_scales_all_horizontal_geometry_at_two_x()
    {
        var accepted = new DateTimeOffset(2026, 10, 3, 0, 34, 0, TimeSpan.Zero);
        var one = HeaderRenderer.MeasureAndSelect(new(SstvMode.Robot36, 1, "JA1UMW", accepted), 320);
        var two = HeaderRenderer.MeasureAndSelect(new(SstvMode.Robot36, 2, "JA1UMW", accepted), 640);

        Assert.Equal(one.MetadataX * 2, two.MetadataX);
        Assert.Equal(one.CallSignX * 2, two.CallSignX);
        Assert.Equal(one.GlyphAdvance * 2, two.GlyphAdvance);
        Assert.Equal(one.PatchWidth * 2, two.PatchWidth);

        var constrained = new CompositionRequest(SstvMode.Martin1, 2, "JA1UMW", accepted);
        Assert.Equal("2026-10-03 09:34 JST M1", HeaderRenderer.MeasureAndSelect(constrained, 536).MetadataText);
        Assert.Equal("10-03 09:34 JST M1", HeaderRenderer.MeasureAndSelect(constrained, 534).MetadataText);
        Assert.Throws<ArgumentException>(() => HeaderRenderer.MeasureAndSelect(constrained, 318));
    }

    private static void AssertPixel(byte[] pixels, int width, int x, int y, byte r, byte g, byte b)
    {
        var offset = ((y * width) + x) * 3;
        Assert.Equal(r, pixels[offset]);
        Assert.Equal(g, pixels[offset + 1]);
        Assert.Equal(b, pixels[offset + 2]);
    }
}
