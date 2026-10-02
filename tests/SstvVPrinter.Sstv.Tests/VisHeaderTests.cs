using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Tests;

public sealed class VisHeaderTests
{
    [Theory]
    [InlineData(44)]
    [InlineData(60)]
    [InlineData(56)]
    [InlineData(8)]
    public void Plan_emits_standard_vis_header(byte code)
    {
        var spans = VisHeader.Plan(code).ToArray();

        Assert.Equal(13, spans.Length);
        Assert.Equal(new ToneSpan(1900, 0.300), spans[0]);
        Assert.Equal(new ToneSpan(1200, 0.010), spans[1]);
        Assert.Equal(new ToneSpan(1900, 0.300), spans[2]);
        Assert.Equal(new ToneSpan(1200, 0.030), spans[3]);
        for (var bit = 0; bit < 7; bit++)
        {
            Assert.Equal(new ToneSpan(((code >> bit) & 1) == 1 ? 1100 : 1300, 0.030), spans[4 + bit]);
        }

        var parity = Enumerable.Range(0, 7).Count(bit => ((code >> bit) & 1) == 1) % 2;
        Assert.Equal(new ToneSpan(parity == 1 ? 1100 : 1300, 0.030), spans[11]);
        Assert.Equal(new ToneSpan(1200, 0.030), spans[12]);
        Assert.Equal(0.910, spans.Sum(span => span.DurationSeconds), 12);
    }

    [Theory]
    [InlineData(double.NaN, 0.1)]
    [InlineData(double.PositiveInfinity, 0.1)]
    [InlineData(0, 0.1)]
    [InlineData(1000, 0)]
    [InlineData(1000, double.NaN)]
    public void ToneSpan_rejects_invalid_values(double frequency, double duration)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToneSpan(frequency, duration));
    }
}
