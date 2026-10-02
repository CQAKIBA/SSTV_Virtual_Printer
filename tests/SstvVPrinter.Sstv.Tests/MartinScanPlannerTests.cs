using SstvVPrinter.Sstv.Planning;
using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Tests;

public sealed class MartinScanPlannerTests
{
    [Fact]
    public void PlanLine_emits_sync_porch_and_gbr_scans()
    {
        var spans = PlanFirstLine(new Rgb24(10, 20, 30));

        Assert.Equal(965, spans.Length);
        Assert.Equal(new ToneSpan(1200, 0.004862), spans[0]);
        Assert.Equal(new ToneSpan(1500, 0.000572), spans[1]);
        Assert.Equal(VideoFrequency.FromByte(20), spans[2].FrequencyHz);
        Assert.Equal(new ToneSpan(1500, 0.000572), spans[322]);
        Assert.Equal(VideoFrequency.FromByte(30), spans[323].FrequencyHz);
        Assert.Equal(new ToneSpan(1500, 0.000572), spans[643]);
        Assert.Equal(VideoFrequency.FromByte(10), spans[644].FrequencyHz);
    }

    [Fact]
    public void PlanLine_uses_approved_4_862_0_572_146_432_ms_timing()
    {
        var spans = PlanFirstLine(new Rgb24(10, 20, 30));

        Assert.All(spans.Skip(2).Take(320), span => Assert.Equal(0.146432 / 320, span.DurationSeconds, 12));
        Assert.All(spans.Skip(323).Take(320), span => Assert.Equal(0.146432 / 320, span.DurationSeconds, 12));
        Assert.All(spans.Skip(644).Take(320), span => Assert.Equal(0.146432 / 320, span.DurationSeconds, 12));
        Assert.Equal(0.446446, spans.Sum(span => span.DurationSeconds), 12);
    }

    [Fact]
    public void PlanFrame_emits_256_lines()
    {
        var frame = CreateFrame(320, 256, new Rgb24(0, 0, 0));
        var options = new SstvEncodeOptions(SstvMode.Martin1);

        var count = SstvScanPlannerFactory.Create(options.Mode)
            .PlanFrame(frame, options, CancellationToken.None).Count();

        Assert.Equal(965 * 256, count);
    }

    [Fact]
    public void PlanFrame_maps_color_bar_channels_to_expected_frequencies()
    {
        var spans = PlanFirstLine(new Rgb24(255, 128, 0));

        Assert.Equal(VideoFrequency.FromByte(128), spans[2].FrequencyHz, 6);
        Assert.Equal(1500, spans[323].FrequencyHz, 6);
        Assert.Equal(2300, spans[644].FrequencyHz, 6);
    }

    private static ToneSpan[] PlanFirstLine(Rgb24 firstPixel)
    {
        var frame = CreateFrame(320, 256, new Rgb24(0, 0, 0), firstPixel);
        var options = new SstvEncodeOptions(SstvMode.Martin1);
        return SstvScanPlannerFactory.Create(options.Mode)
            .PlanFrame(frame, options, CancellationToken.None).Take(965).ToArray();
    }

    internal static RgbFrame CreateFrame(int width, int height, Rgb24 fill, Rgb24? firstPixel = null)
    {
        var data = new byte[width * height * 3];
        for (var offset = 0; offset < data.Length; offset += 3)
        {
            data[offset] = fill.R;
            data[offset + 1] = fill.G;
            data[offset + 2] = fill.B;
        }

        if (firstPixel is { } first)
        {
            data[0] = first.R;
            data[1] = first.G;
            data[2] = first.B;
        }

        return RgbFrame.Create(width, height, data);
    }
}
