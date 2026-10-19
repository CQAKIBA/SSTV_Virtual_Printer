using SstvVPrinter.Sstv.Radiofax;

namespace SstvVPrinter.Sstv.Tests;

public sealed class TransmissionModeTests
{
    [Fact]
    public void Existing_and_radiofax_modes_keep_stable_numeric_values()
    {
        Assert.Equal(0, (int)SstvMode.Martin1);
        Assert.Equal(1, (int)SstvMode.Scottie1);
        Assert.Equal(2, (int)SstvMode.Scottie2);
        Assert.Equal(3, (int)SstvMode.Robot36);
        Assert.Equal(4, (int)SstvMode.Radiofax120Ioc288);
        Assert.Equal(5, (int)SstvMode.Radiofax120Ioc576);
    }

    [Theory]
    [InlineData(SstvMode.Radiofax120Ioc288, "Radiofax 120/288", 288, 904, 864, 32, 1728, 675)]
    [InlineData(SstvMode.Radiofax120Ioc576, "Radiofax 120/576", 576, 1809, 1728, 64, 3456, 300)]
    public void Radiofax_catalog_exposes_approved_mode_parameters(
        SstvMode mode,
        string displayName,
        int ioc,
        int totalLineGrid,
        int activeWidth,
        int headerHeight,
        int maximumBodyLines,
        double iocSelectionHz)
    {
        var definition = RadiofaxModes.Get(mode);

        Assert.Equal(mode, definition.Mode);
        Assert.Equal(displayName, definition.DisplayName);
        Assert.Equal(120, definition.LinesPerMinute);
        Assert.Equal(ioc, definition.Ioc);
        Assert.Equal(totalLineGrid, definition.TotalLineGrid);
        Assert.Equal(activeWidth, definition.ActiveWidth);
        Assert.Equal(headerHeight, definition.HeaderHeight);
        Assert.Equal(maximumBodyLines, definition.MaximumBodyLines);
        Assert.Equal(iocSelectionHz, definition.IocSelectionHz);
        Assert.True(TransmissionModes.IsRadiofax(mode));
        Assert.Equal(displayName, TransmissionModes.DisplayName(mode));
    }

    [Fact]
    public void Transmission_catalog_keeps_sstv_display_names()
    {
        Assert.False(TransmissionModes.IsRadiofax(SstvMode.Scottie2));
        Assert.Equal("Scottie 2", TransmissionModes.DisplayName(SstvMode.Scottie2));
    }

    [Fact]
    public void Transmission_catalog_rejects_unknown_mode()
    {
        var mode = (SstvMode)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => TransmissionModes.Validate(mode));
        Assert.Throws<ArgumentOutOfRangeException>(() => TransmissionModes.DisplayName(mode));
        Assert.Throws<ArgumentOutOfRangeException>(() => RadiofaxModes.Get(mode));
    }

    [Fact]
    public void Radiofax_options_reject_horizontal_multiplier_two()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TransmissionEncodeOptions(SstvMode.Radiofax120Ioc288, HorizontalSampleMultiplier: 2));
    }

    [Fact]
    public void Signal_sample_rate_is_shared_with_legacy_sstv_options()
    {
        Assert.Equal(48_000, Signal.SignalConstants.SampleRate);
        Assert.Equal(Signal.SignalConstants.SampleRate, SstvEncodeOptions.SampleRate);
    }
}
