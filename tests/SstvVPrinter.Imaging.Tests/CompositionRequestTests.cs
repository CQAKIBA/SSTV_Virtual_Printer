using SstvVPrinter.Imaging.Composition;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Tests;

public sealed class CompositionRequestTests
{
    [Theory]
    [InlineData(SstvMode.Martin1, 1)]
    [InlineData(SstvMode.Scottie1, 2)]
    [InlineData(SstvMode.Scottie2, 1)]
    [InlineData(SstvMode.Robot36, 2)]
    public void Constructor_accepts_supported_modes_and_multipliers(SstvMode mode, int multiplier)
    {
        var request = new CompositionRequest(mode, multiplier, "ja1umw", DateTimeOffset.UnixEpoch);

        Assert.Equal("JA1UMW", request.CallSign);
        Assert.Equal(HeaderPlacement.Top, request.Placement);
    }

    [Theory]
    [InlineData(SstvMode.Radiofax120Ioc288)]
    [InlineData(SstvMode.Radiofax120Ioc576)]
    public void Constructor_accepts_radiofax_only_at_effective_multiplier_one(SstvMode mode)
    {
        var request = new CompositionRequest(mode, 1, "ja1umw", DateTimeOffset.UnixEpoch);

        Assert.Equal(mode, request.Mode);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CompositionRequest(mode, 2, "ja1umw", DateTimeOffset.UnixEpoch));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Constructor_rejects_unsupported_multiplier(int multiplier) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CompositionRequest(SstvMode.Martin1, multiplier, "JA1UMW", DateTimeOffset.UnixEpoch));

    [Fact]
    public void Constructor_rejects_unsupported_mode() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CompositionRequest((SstvMode)999, 1, "JA1UMW", DateTimeOffset.UnixEpoch));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("JA1_UMW")]
    [InlineData("ＪＡ１ＵＭＷ")]
    public void Constructor_rejects_empty_or_unrenderable_callsign(string callsign) =>
        Assert.Throws<ArgumentException>(() =>
            new CompositionRequest(SstvMode.Martin1, 1, callsign, DateTimeOffset.UnixEpoch));

    [Theory]
    [InlineData("JA1UMW/P-1")]
    [InlineData("JA1 UMW:1.0")]
    public void Constructor_accepts_every_supported_character(string callsign)
    {
        var request = new CompositionRequest(SstvMode.Martin1, 1, callsign, DateTimeOffset.UnixEpoch);
        Assert.Equal(callsign, request.CallSign);
    }

    [Fact]
    public void Constructor_rejects_non_top_placement() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CompositionRequest(SstvMode.Martin1, 1, "JA1UMW", DateTimeOffset.UnixEpoch, (HeaderPlacement)999));
}
