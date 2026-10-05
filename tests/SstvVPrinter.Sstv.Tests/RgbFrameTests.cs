using SstvVPrinter.Sstv;

namespace SstvVPrinter.Sstv.Tests;

public sealed class RgbFrameTests
{
    [Fact]
    public void Create_copies_source_bytes()
    {
        var source = new byte[] { 1, 2, 3 };

        var frame = RgbFrame.Create(1, 1, source);
        source[0] = 99;

        Assert.Equal(new Rgb24(1, 2, 3), frame.GetPixel(0, 0));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void Create_rejects_nonpositive_dimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RgbFrame.Create(width, height, []));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(7)]
    public void Create_rejects_wrong_rgb_byte_count(int byteCount)
    {
        Assert.Throws<ArgumentException>(() => RgbFrame.Create(2, 1, new byte[byteCount]));
    }

    [Fact]
    public void GetPixel_returns_row_major_rgb()
    {
        var frame = RgbFrame.Create(2, 2,
        [
            1, 2, 3, 4, 5, 6,
            7, 8, 9, 10, 11, 12,
        ]);

        Assert.Equal(2, frame.Width);
        Assert.Equal(2, frame.Height);
        Assert.Equal(new Rgb24(1, 2, 3), frame.GetPixel(0, 0));
        Assert.Equal(new Rgb24(4, 5, 6), frame.GetPixel(1, 0));
        Assert.Equal(new Rgb24(7, 8, 9), frame.GetPixel(0, 1));
        Assert.Equal(new Rgb24(10, 11, 12), frame.GetPixel(1, 1));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(2, 0)]
    [InlineData(0, 2)]
    public void GetPixel_rejects_out_of_range_coordinates(int x, int y)
    {
        var frame = RgbFrame.Create(2, 2, new byte[12]);

        Assert.Throws<ArgumentOutOfRangeException>(() => frame.GetPixel(x, y));
    }

    [Fact]
    public void CopyPixelsTo_copies_exact_rgb_bytes_defensively()
    {
        var frame = RgbFrame.Create(2, 1, [1, 2, 3, 4, 5, 6]);
        var destination = new byte[6];

        frame.CopyPixelsTo(destination);
        destination[0] = 99;

        Assert.Equal([99, 2, 3, 4, 5, 6], destination);
        Assert.Equal(new Rgb24(1, 2, 3), frame.GetPixel(0, 0));
    }

    [Theory]
    [InlineData(SstvMode.Martin1, 115.200176)]
    [InlineData(SstvMode.Scottie1, 110.543320)]
    [InlineData(SstvMode.Scottie2, 72.008152)]
    [InlineData(SstvMode.Robot36, 36.910000)]
    public void Duration_estimator_uses_approved_mode_timing(SstvMode mode, double seconds)
    {
        Assert.Equal(seconds, SstvDurationEstimator.Estimate(new SstvEncodeOptions(mode)).TotalSeconds, 6);
        Assert.Equal(seconds, SstvDurationEstimator.Estimate(new SstvEncodeOptions(mode, 2)).TotalSeconds, 6);
    }
}
