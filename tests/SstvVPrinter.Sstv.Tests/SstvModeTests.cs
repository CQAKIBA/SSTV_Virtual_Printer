using SstvVPrinter.Sstv;

namespace SstvVPrinter.Sstv.Tests;

public sealed class SstvModeTests
{
    public static TheoryData<SstvMode, string, byte, int, int, SstvScanFamily> Definitions => new()
    {
        { SstvMode.Martin1, "Martin 1", 44, 320, 256, SstvScanFamily.Martin },
        { SstvMode.Scottie1, "Scottie 1", 60, 320, 256, SstvScanFamily.Scottie },
        { SstvMode.Scottie2, "Scottie 2", 56, 320, 256, SstvScanFamily.Scottie },
        { SstvMode.Robot36, "Robot 36", 8, 320, 240, SstvScanFamily.Robot36 },
    };

    [Theory]
    [MemberData(nameof(Definitions))]
    public void Definitions_match_approved_vis_dimensions_and_families(
        SstvMode mode,
        string displayName,
        byte visCode,
        int width,
        int height,
        SstvScanFamily family)
    {
        var definition = SstvModes.Get(mode);

        Assert.Equal(mode, definition.Mode);
        Assert.Equal(displayName, definition.DisplayName);
        Assert.Equal(visCode, definition.VisCode);
        Assert.Equal(width, definition.Width);
        Assert.Equal(height, definition.Height);
        Assert.Equal(family, definition.ScanFamily);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Options_accept_only_horizontal_multipliers_one_and_two(int multiplier)
    {
        var options = new SstvEncodeOptions(SstvMode.Martin1, multiplier);

        Assert.Equal(multiplier, options.HorizontalSampleMultiplier);
        Assert.Equal(48_000, SstvEncodeOptions.SampleRate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void Options_reject_unsupported_horizontal_multipliers(int multiplier)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SstvEncodeOptions(SstvMode.Martin1, multiplier));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.1f)]
    [InlineData(1.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Options_reject_nonfinite_or_out_of_range_amplitude(float amplitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SstvEncodeOptions(SstvMode.Martin1, Amplitude: amplitude));
    }

    [Fact]
    public void Options_use_approved_defaults()
    {
        var options = new SstvEncodeOptions(SstvMode.Martin1);

        Assert.Equal(1, options.HorizontalSampleMultiplier);
        Assert.Equal(0.8f, options.Amplitude);
    }

    [Theory]
    [InlineData(SstvMode.Martin1, 1, 320, 256)]
    [InlineData(SstvMode.Martin1, 2, 640, 256)]
    [InlineData(SstvMode.Scottie1, 1, 320, 256)]
    [InlineData(SstvMode.Scottie2, 2, 640, 256)]
    [InlineData(SstvMode.Robot36, 1, 320, 240)]
    [InlineData(SstvMode.Robot36, 2, 640, 240)]
    public void ValidateFrame_accepts_exact_mode_dimensions(SstvMode mode, int multiplier, int width, int height)
    {
        var options = new SstvEncodeOptions(mode, multiplier);
        var frame = RgbFrame.Create(width, height, new byte[width * height * 3]);

        options.Validate(frame);
    }

    [Theory]
    [InlineData(SstvMode.Martin1, 1, 640, 256)]
    [InlineData(SstvMode.Martin1, 2, 320, 256)]
    [InlineData(SstvMode.Scottie1, 1, 320, 255)]
    [InlineData(SstvMode.Robot36, 1, 320, 256)]
    public void ValidateFrame_requires_mode_dimensions_times_multiplier(
        SstvMode mode,
        int multiplier,
        int width,
        int height)
    {
        var options = new SstvEncodeOptions(mode, multiplier);
        var frame = RgbFrame.Create(width, height, new byte[width * height * 3]);

        Assert.Throws<ArgumentException>(() => options.Validate(frame));
    }
}
