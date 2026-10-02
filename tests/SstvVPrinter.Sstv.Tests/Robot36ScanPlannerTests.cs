using SstvVPrinter.Sstv.Planning;
using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Tests;

public sealed class Robot36ScanPlannerTests
{
    [Fact]
    public void PlanLine_uses_9_3_88_4_5_1_5_44_ms_timing()
    {
        var line = Plan(1).Take(644).ToArray();

        Assert.Equal(new ToneSpan(1200, 0.009), line[0]);
        Assert.Equal(new ToneSpan(1500, 0.003), line[1]);
        Assert.All(line.Skip(2).Take(320), span => Assert.Equal(0.088 / 320, span.DurationSeconds, 12));
        Assert.Equal(0.0045, line[322].DurationSeconds, 12);
        Assert.Equal(new ToneSpan(1900, 0.0015), line[323]);
        Assert.All(line.Skip(324).Take(320), span => Assert.Equal(0.044 / 320, span.DurationSeconds, 12));
        Assert.Equal(0.150, line.Sum(span => span.DurationSeconds), 12);
    }

    [Fact]
    public void PlanPair_averages_chrominance_and_alternates_channels()
    {
        var spans = PlanPair(new Rgb24(255, 0, 0), new Rgb24(0, 0, 255));

        Assert.Equal(VideoFrequency.FromByte(181), spans[324].FrequencyHz, 6);
        Assert.Equal(VideoFrequency.FromByte(170), spans[644 + 324].FrequencyHz, 6);
        Assert.Equal(VideoFrequency.FromByte(76), spans[2].FrequencyHz, 6);
        Assert.Equal(VideoFrequency.FromByte(29), spans[644 + 2].FrequencyHz, 6);
    }

    [Fact]
    public void Even_line_uses_1500_marker_and_red_difference()
    {
        var spans = PlanPair(new Rgb24(255, 0, 0), new Rgb24(0, 0, 255));

        Assert.Equal(new ToneSpan(1500, 0.0045), spans[322]);
        Assert.Equal(VideoFrequency.FromByte(181), spans[324].FrequencyHz, 6);
    }

    [Fact]
    public void Odd_line_uses_2300_marker_and_blue_difference()
    {
        var spans = PlanPair(new Rgb24(255, 0, 0), new Rgb24(0, 0, 255));

        Assert.Equal(new ToneSpan(2300, 0.0045), spans[644 + 322]);
        Assert.Equal(VideoFrequency.FromByte(170), spans[644 + 324].FrequencyHz, 6);
    }

    [Fact]
    public void PlanFrame_emits_240_lines()
    {
        Assert.Equal(644 * 240, Plan(1).Count());
    }

    [Fact]
    public void Multiplier_two_doubles_y_and_chrominance_updates_without_changing_150_ms_line_time()
    {
        var line = Plan(2).Take(1284).ToArray();

        Assert.Equal(640, line.Skip(2).TakeWhile(span => span.DurationSeconds < 0.001).Count());
        Assert.Equal(640, line.Skip(644).TakeWhile(span => span.DurationSeconds < 0.001).Count());
        Assert.Equal(0.150, line.Sum(span => span.DurationSeconds), 12);
    }

    private static IEnumerable<ToneSpan> Plan(int multiplier)
    {
        var width = 320 * multiplier;
        var frame = MartinScanPlannerTests.CreateFrame(width, 240, new Rgb24(0, 0, 0));
        var options = new SstvEncodeOptions(SstvMode.Robot36, multiplier);
        return SstvScanPlannerFactory.Create(options.Mode).PlanFrame(frame, options, CancellationToken.None);
    }

    private static ToneSpan[] PlanPair(Rgb24 even, Rgb24 odd)
    {
        var data = new byte[320 * 240 * 3];
        SetPixel(data, 320, 0, even);
        SetPixel(data, 320, 1, odd);
        var frame = RgbFrame.Create(320, 240, data);
        var options = new SstvEncodeOptions(SstvMode.Robot36);
        return SstvScanPlannerFactory.Create(options.Mode).PlanFrame(frame, options, CancellationToken.None).Take(1288).ToArray();
    }

    private static void SetPixel(byte[] data, int width, int y, Rgb24 pixel)
    {
        var offset = y * width * 3;
        data[offset] = pixel.R;
        data[offset + 1] = pixel.G;
        data[offset + 2] = pixel.B;
    }
}
