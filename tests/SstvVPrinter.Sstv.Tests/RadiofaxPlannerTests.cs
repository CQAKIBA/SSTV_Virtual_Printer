using SstvVPrinter.Sstv.Radiofax;
using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Tests;

public sealed class RadiofaxPlannerTests
{
    [Theory]
    [InlineData(SstvMode.Radiofax120Ioc288, 675, 6750, 864, 32)]
    [InlineData(SstvMode.Radiofax120Ioc576, 300, 3000, 1728, 64)]
    public void Plan_emits_wmo_control_sections_and_exact_half_second_lines(
        SstvMode mode,
        double selectorHz,
        int selectorSpanCount,
        int width,
        int height)
    {
        var frame = GrayFrame(width, height);
        var spans = RadiofaxPlanner.Plan(frame, new RadiofaxEncodeOptions(mode), CancellationToken.None).ToArray();
        var selector = spans[..selectorSpanCount];
        var phasing = spans[selectorSpanCount..(selectorSpanCount + 120)];
        var imageStart = selectorSpanCount + 120;
        var imageSpanCount = height * (width + 3);
        var stopStart = imageStart + imageSpanCount;
        var stop = spans[stopStart..(stopStart + 4500)];
        var trailer = spans[^1];

        Assert.Equal(5.0, selector.Sum(span => span.DurationSeconds), 9);
        Assert.All(selector, span => Assert.Equal(1.0 / (2 * selectorHz), span.DurationSeconds, 12));
        AssertAlternatingBlackWhite(selector);

        for (var line = 0; line < 60; line++)
        {
            Assert.Equal(2300, phasing[line * 2].FrequencyHz);
            Assert.Equal(0.025, phasing[line * 2].DurationSeconds, 12);
            Assert.Equal(1500, phasing[(line * 2) + 1].FrequencyHz);
            Assert.Equal(0.475, phasing[(line * 2) + 1].DurationSeconds, 12);
        }

        for (var line = 0; line < height; line++)
        {
            var lineSpans = spans[(imageStart + (line * (width + 3)))..(imageStart + ((line + 1) * (width + 3)))];
            Assert.Equal(0.5, lineSpans.Sum(span => span.DurationSeconds), 9);
            Assert.Equal(2300, lineSpans[0].FrequencyHz);
            Assert.Equal(0.005625, lineSpans[0].DurationSeconds, 12);
            Assert.Equal(1500, lineSpans[1].FrequencyHz);
            Assert.Equal(0.01125, lineSpans[1].DurationSeconds, 12);
            Assert.Equal(2300, lineSpans[2].FrequencyHz);
            Assert.Equal(0.005625, lineSpans[2].DurationSeconds, 12);
            Assert.Equal(0.4775, lineSpans[3..].Sum(span => span.DurationSeconds), 9);
        }

        Assert.Equal(5.0, stop.Sum(span => span.DurationSeconds), 9);
        Assert.All(stop, span => Assert.Equal(1.0 / 900.0, span.DurationSeconds, 12));
        AssertAlternatingBlackWhite(stop);
        Assert.Equal(1500, trailer.FrequencyHz);
        Assert.Equal(10, trailer.DurationSeconds);
        Assert.Equal(50 + (height * 0.5), spans.Sum(span => span.DurationSeconds), 8);
    }

    [Fact]
    public void Plan_scans_active_pixels_left_to_right_after_synchronized_dead_sector()
    {
        const int width = 864;
        const int height = 32;
        var pixels = new byte[width * height * 3];
        SetGray(pixels, width, 0, 0, 0);
        SetGray(pixels, width, 1, 0, 128);
        SetGray(pixels, width, 2, 0, 255);
        var spans = RadiofaxPlanner.Plan(
            RgbFrame.Create(width, height, pixels),
            new RadiofaxEncodeOptions(SstvMode.Radiofax120Ioc288),
            CancellationToken.None).ToArray();
        const int firstImageSpan = 6750 + 120;

        Assert.Equal(2300, spans[firstImageSpan].FrequencyHz);
        Assert.Equal(1500, spans[firstImageSpan + 1].FrequencyHz);
        Assert.Equal(2300, spans[firstImageSpan + 2].FrequencyHz);
        Assert.Equal(1500, spans[firstImageSpan + 3].FrequencyHz);
        Assert.Equal(1901.5686274509803, spans[firstImageSpan + 4].FrequencyHz, 9);
        Assert.Equal(2300, spans[firstImageSpan + 5].FrequencyHz);
    }

    [Fact]
    public void Plan_observes_cancellation_during_enumeration()
    {
        var options = new RadiofaxEncodeOptions(SstvMode.Radiofax120Ioc288);
        using var cancellation = new CancellationTokenSource();
        var enumerator = RadiofaxPlanner.Plan(GrayFrame(864, 32), options, cancellation.Token).GetEnumerator();
        Assert.True(enumerator.MoveNext());
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
        {
            enumerator.MoveNext();
        });
    }

    private static RgbFrame GrayFrame(int width, int height) =>
        RgbFrame.Create(width, height, new byte[width * height * 3]);

    private static void SetGray(byte[] pixels, int width, int x, int y, byte value)
    {
        var offset = ((y * width) + x) * 3;
        pixels[offset] = value;
        pixels[offset + 1] = value;
        pixels[offset + 2] = value;
    }

    private static void AssertAlternatingBlackWhite(ToneSpan[] spans)
    {
        for (var index = 0; index < spans.Length; index++)
        {
            Assert.Equal(index % 2 == 0 ? 1500 : 2300, spans[index].FrequencyHz);
        }
    }
}
