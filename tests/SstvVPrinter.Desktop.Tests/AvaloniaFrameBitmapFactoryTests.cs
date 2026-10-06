using SstvVPrinter.Desktop.Imaging;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Desktop.Tests;

public sealed class PreviewFrameFactoryTests
{
    [Fact]
    public void Double_horizontal_preview_duplicates_each_scanline_vertically()
    {
        var frame = RgbFrame.Create(2, 2,
        [
            255, 0, 0, 0, 255, 0,
            0, 0, 255, 255, 255, 255,
        ]);

        var preview = PreviewFrameFactory.Create(frame, verticalScale: 2);

        Assert.Equal(2, preview.Width);
        Assert.Equal(4, preview.Height);
        Assert.Equal(preview.GetPixel(0, 0), preview.GetPixel(0, 1));
        Assert.Equal(preview.GetPixel(1, 0), preview.GetPixel(1, 1));
        Assert.Equal(preview.GetPixel(0, 2), preview.GetPixel(0, 3));
        Assert.Equal(preview.GetPixel(1, 2), preview.GetPixel(1, 3));
        Assert.NotEqual(preview.GetPixel(0, 0), preview.GetPixel(0, 2));
    }
}
