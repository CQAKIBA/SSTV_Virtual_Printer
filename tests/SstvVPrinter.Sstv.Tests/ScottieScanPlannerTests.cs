using SstvVPrinter.Sstv.Planning;
using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Tests;

public sealed class ScottieScanPlannerTests
{
    [Theory]
    [InlineData(SstvMode.Scottie1)]
    [InlineData(SstvMode.Scottie2)]
    public void PlanLine_places_sync_between_blue_and_red(SstvMode mode)
    {
        var spans = PlanFirstLine(mode, new Rgb24(10, 20, 30));

        Assert.Equal(new ToneSpan(1200, 0.009), spans[0]);
        Assert.Equal(VideoFrequency.FromByte(20), spans[1].FrequencyHz);
        Assert.Equal(new ToneSpan(1500, 0.0015), spans[321]);
        Assert.Equal(VideoFrequency.FromByte(30), spans[322].FrequencyHz);
        Assert.Equal(new ToneSpan(1500, 0.0015), spans[642]);
        Assert.Equal(new ToneSpan(1200, 0.009), spans[643]);
        Assert.Equal(new ToneSpan(1500, 0.0015), spans[644]);
        Assert.Equal(VideoFrequency.FromByte(10), spans[645].FrequencyHz);
    }

    [Theory]
    [InlineData(SstvMode.Scottie1)]
    [InlineData(SstvMode.Scottie2)]
    public void PlanFrame_emits_initial_sync_once(SstvMode mode)
    {
        var frame = MartinScanPlannerTests.CreateFrame(320, 256, new Rgb24(0, 0, 0));
        var options = new SstvEncodeOptions(mode);
        var spans = SstvScanPlannerFactory.Create(mode).PlanFrame(frame, options, CancellationToken.None).ToArray();

        Assert.Equal(1 + (964 * 256), spans.Length);
        Assert.Equal(new ToneSpan(1200, 0.009), spans[0]);
        Assert.Equal(VideoFrequency.FromByte(0), spans[965].FrequencyHz);
    }

    [Theory]
    [InlineData(SstvMode.Scottie1, 0.138240, 0.428220)]
    [InlineData(SstvMode.Scottie2, 0.088064, 0.277692)]
    public void Modes_use_approved_scan_and_line_timing(SstvMode mode, double scanDuration, double lineDuration)
    {
        var line = PlanFirstLine(mode, new Rgb24(1, 2, 3)).Skip(1).ToArray();

        Assert.All(line.Take(320), span => Assert.Equal(scanDuration / 320, span.DurationSeconds, 12));
        Assert.All(line.Skip(321).Take(320), span => Assert.Equal(scanDuration / 320, span.DurationSeconds, 12));
        Assert.All(line.Skip(644).Take(320), span => Assert.Equal(scanDuration / 320, span.DurationSeconds, 12));
        Assert.Equal(lineDuration, line.Sum(span => span.DurationSeconds), 12);
    }

    [Theory]
    [InlineData(SstvMode.Scottie1)]
    [InlineData(SstvMode.Scottie2)]
    public void PlanFrame_maps_gbr_channels_without_swapping(SstvMode mode)
    {
        var spans = PlanFirstLine(mode, new Rgb24(255, 128, 0));

        Assert.Equal(VideoFrequency.FromByte(128), spans[1].FrequencyHz, 6);
        Assert.Equal(1500, spans[322].FrequencyHz, 6);
        Assert.Equal(2300, spans[645].FrequencyHz, 6);
    }

    private static ToneSpan[] PlanFirstLine(SstvMode mode, Rgb24 firstPixel)
    {
        var frame = MartinScanPlannerTests.CreateFrame(320, 256, new Rgb24(0, 0, 0), firstPixel);
        var options = new SstvEncodeOptions(mode);
        return SstvScanPlannerFactory.Create(mode).PlanFrame(frame, options, CancellationToken.None).Take(965).ToArray();
    }
}
