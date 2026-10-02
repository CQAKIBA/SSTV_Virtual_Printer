using SstvVPrinter.SstvTool;

namespace SstvVPrinter.Sstv.Tests;

public sealed class ColorBarFrameTests
{
    [Theory]
    [InlineData(SstvMode.Martin1, 1, 320, 256)]
    [InlineData(SstvMode.Scottie2, 2, 640, 256)]
    [InlineData(SstvMode.Robot36, 1, 320, 240)]
    [InlineData(SstvMode.Robot36, 2, 640, 240)]
    public void Create_uses_mode_dimensions_and_multiplier(SstvMode mode, int multiplier, int width, int height)
    {
        var frame = ColorBarFrame.Create(new SstvEncodeOptions(mode, multiplier));
        Assert.Equal(width, frame.Width);
        Assert.Equal(height, frame.Height);
    }

    [Fact]
    public void Create_contains_black_white_primary_and_gray_bars()
    {
        var frame = ColorBarFrame.Create(new SstvEncodeOptions(SstvMode.Martin1));
        var expected = new[]
        {
            new Rgb24(0, 0, 0), new Rgb24(255, 255, 255), new Rgb24(255, 0, 0),
            new Rgb24(0, 255, 0), new Rgb24(0, 0, 255), new Rgb24(128, 128, 128),
        };
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index], frame.GetPixel((index * 40) + 20, 20));
        }
    }

    [Fact]
    public void Create_is_deterministic_and_has_native_640_detail()
    {
        var options = new SstvEncodeOptions(SstvMode.Martin1, 2);
        var first = ColorBarFrame.Create(options);
        var second = ColorBarFrame.Create(options);
        for (var x = 0; x < 640; x++)
        {
            Assert.Equal(first.GetPixel(x, 255), second.GetPixel(x, 255));
            Assert.Equal(x % 2 == 0 ? new Rgb24(0, 0, 0) : new Rgb24(255, 255, 255), first.GetPixel(x, 255));
        }
    }
}
