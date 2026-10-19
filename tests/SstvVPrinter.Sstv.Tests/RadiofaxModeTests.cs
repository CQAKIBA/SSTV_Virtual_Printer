using SstvVPrinter.Sstv.Radiofax;

namespace SstvVPrinter.Sstv.Tests;

public sealed class RadiofaxModeTests
{
    [Theory]
    [InlineData(0, 1500.0)]
    [InlineData(128, 1901.5686274509803)]
    [InlineData(255, 2300.0)]
    public void Luminance_maps_linearly_to_radiofax_frequency(byte luminance, double expectedHz)
    {
        Assert.Equal(expectedHz, RadiofaxFrequency.FromLuminance(luminance), 9);
    }

    [Theory]
    [InlineData(0, 1500)]
    [InlineData(36, 1614)]
    [InlineData(73, 1729)]
    [InlineData(109, 1843)]
    [InlineData(146, 1957)]
    [InlineData(182, 2071)]
    [InlineData(219, 2186)]
    [InlineData(255, 2300)]
    public void Header_reference_levels_stay_within_one_point_one_hertz_of_wmo_targets(
        byte luminance,
        double targetHz)
    {
        Assert.InRange(Math.Abs(RadiofaxFrequency.FromLuminance(luminance) - targetHz), 0, 1.1);
    }

    [Fact]
    public void Radiofax_options_reject_sstv_mode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadiofaxEncodeOptions(SstvMode.Scottie2));
    }

    [Theory]
    [InlineData(SstvMode.Radiofax120Ioc288, 864, 32, 1760)]
    [InlineData(SstvMode.Radiofax120Ioc576, 1728, 64, 3520)]
    public void Radiofax_options_accept_exact_width_and_inclusive_height_range(
        SstvMode mode,
        int width,
        int minimumHeight,
        int maximumHeight)
    {
        var options = new RadiofaxEncodeOptions(mode);

        options.Validate(Frame(width, minimumHeight));
        options.Validate(Frame(width, maximumHeight));
    }

    [Theory]
    [InlineData(SstvMode.Radiofax120Ioc288, 863, 32)]
    [InlineData(SstvMode.Radiofax120Ioc288, 864, 31)]
    [InlineData(SstvMode.Radiofax120Ioc288, 864, 1761)]
    [InlineData(SstvMode.Radiofax120Ioc576, 1727, 64)]
    [InlineData(SstvMode.Radiofax120Ioc576, 1728, 63)]
    [InlineData(SstvMode.Radiofax120Ioc576, 1728, 3521)]
    public void Radiofax_options_reject_dimensions_outside_mode_limits(
        SstvMode mode,
        int width,
        int height)
    {
        Assert.Throws<ArgumentException>(() => new RadiofaxEncodeOptions(mode).Validate(Frame(width, height)));
    }

    private static RgbFrame Frame(int width, int height) =>
        RgbFrame.Create(width, height, new byte[checked(width * height * 3)]);
}
