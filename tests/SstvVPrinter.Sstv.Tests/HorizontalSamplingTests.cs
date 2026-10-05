using SstvVPrinter.Sstv.Planning;
using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Tests;

public sealed class HorizontalSamplingTests
{
    [Fact]
    public void Multiplier_two_doubles_video_updates_only()
    {
        var one = PlanMartin(1).Take(965).ToArray();
        var two = PlanMartin(2).Take(1925).ToArray();

        Assert.Equal(960, one.Count(IsVideo));
        Assert.Equal(1920, two.Count(IsVideo));
        Assert.Equal(5, one.Count(span => !IsVideo(span)));
        Assert.Equal(5, two.Count(span => !IsVideo(span)));
    }

    [Fact]
    public void Multiplier_two_preserves_each_component_and_frame_duration()
    {
        var one = PlanMartin(1).ToArray();
        var two = PlanMartin(2).ToArray();

        Assert.Equal(0.146432, one.Skip(2).Take(320).Sum(span => span.DurationSeconds), 12);
        Assert.Equal(0.146432, two.Skip(2).Take(640).Sum(span => span.DurationSeconds), 12);
        var durationDifference = Math.Abs(
            one.Sum(span => span.DurationSeconds) - two.Sum(span => span.DurationSeconds));
        Assert.InRange(durationDifference, 0, 1.0 / SstvEncodeOptions.SampleRate);
    }

    [Fact]
    public void Multiplier_two_uses_native_640_source_values()
    {
        var data = new byte[640 * 256 * 3];
        data[4] = 255;
        var frame = RgbFrame.Create(640, 256, data);
        var options = new SstvEncodeOptions(SstvMode.Martin1, 2);

        var spans = SstvScanPlannerFactory.Create(options.Mode)
            .PlanFrame(frame, options, CancellationToken.None).Take(4).ToArray();

        Assert.Equal(1500, spans[2].FrequencyHz, 6);
        Assert.Equal(2300, spans[3].FrequencyHz, 6);
    }

    private static IEnumerable<ToneSpan> PlanMartin(int multiplier)
    {
        var width = 320 * multiplier;
        var frame = MartinScanPlannerTests.CreateFrame(width, 256, new Rgb24(0, 0, 0));
        var options = new SstvEncodeOptions(SstvMode.Martin1, multiplier);
        return SstvScanPlannerFactory.Create(options.Mode).PlanFrame(frame, options, CancellationToken.None);
    }

    private static bool IsVideo(ToneSpan span) => span.DurationSeconds < 0.0005;
}
