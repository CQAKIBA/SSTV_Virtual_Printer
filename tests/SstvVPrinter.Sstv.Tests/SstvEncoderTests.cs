using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Tests;

public sealed class SstvEncoderTests
{
    [Fact]
    public void EncodeAsync_validates_before_enumeration()
    {
        var frame = RgbFrame.Create(1, 1, new byte[3]);
        Assert.Throws<ArgumentException>(() => SstvEncoder.EncodeAsync(frame, new SstvEncodeOptions(SstvMode.Martin1)));
    }

    [Theory]
    [InlineData(SstvMode.Martin1)]
    [InlineData(SstvMode.Scottie1)]
    [InlineData(SstvMode.Scottie2)]
    [InlineData(SstvMode.Robot36)]
    public async Task EncodeAsync_starts_with_matching_vis_header(SstvMode mode)
    {
        var options = new SstvEncodeOptions(mode);
        var actual = await FirstBlock(SstvEncoder.EncodeAsync(CreateFrame(options), options));
        var expected = await FirstBlock(PcmRenderer.RenderAsync(VisHeader.Plan(SstvModes.Get(mode).VisCode), 0.8f));
        Assert.Equal(expected.Samples.ToArray(), actual.Samples.ToArray());
    }

    [Theory]
    [InlineData(SstvMode.Martin1)]
    [InlineData(SstvMode.Scottie1)]
    [InlineData(SstvMode.Scottie2)]
    [InlineData(SstvMode.Robot36)]
    public async Task EncodeAsync_generates_all_four_modes(SstvMode mode)
    {
        var options = new SstvEncodeOptions(mode);
        var first = await FirstBlock(SstvEncoder.EncodeAsync(CreateFrame(options), options));
        Assert.Equal(0, first.StartSample);
        Assert.NotEmpty(first.Samples.ToArray());
    }

    [Theory]
    [InlineData(SstvMode.Martin1)]
    [InlineData(SstvMode.Scottie1)]
    [InlineData(SstvMode.Scottie2)]
    [InlineData(SstvMode.Robot36)]
    public async Task EncodeAsync_one_and_two_have_equal_sample_counts(SstvMode mode)
    {
        var one = new SstvEncodeOptions(mode, 1);
        var two = new SstvEncodeOptions(mode, 2);
        Assert.Equal(await Count(SstvEncoder.EncodeAsync(CreateFrame(one), one)), await Count(SstvEncoder.EncodeAsync(CreateFrame(two), two)));
    }

    [Fact]
    public async Task EncodeAsync_is_safe_for_two_concurrent_calls()
    {
        var options = new SstvEncodeOptions(SstvMode.Robot36);
        var frame = CreateFrame(options);
        var results = await Task.WhenAll(Count(SstvEncoder.EncodeAsync(frame, options)), Count(SstvEncoder.EncodeAsync(frame, options)));
        Assert.Equal(results[0], results[1]);
    }

    [Fact]
    public async Task EncodeAsync_cancellation_stops_boundedly()
    {
        var options = new SstvEncodeOptions(SstvMode.Martin1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in SstvEncoder.EncodeAsync(CreateFrame(options), options, cancellation.Token)) { }
        });
    }

    private static RgbFrame CreateFrame(SstvEncodeOptions options)
    {
        var mode = SstvModes.Get(options.Mode);
        var width = mode.Width * options.HorizontalSampleMultiplier;
        return RgbFrame.Create(width, mode.Height, new byte[width * mode.Height * 3]);
    }

    private static async Task<PcmBlock> FirstBlock(IAsyncEnumerable<PcmBlock> source)
    {
        await foreach (var block in source) return block;
        throw new InvalidOperationException();
    }

    private static async Task<long> Count(IAsyncEnumerable<PcmBlock> source)
    {
        long count = 0;
        await foreach (var block in source) count += block.Samples.Length;
        return count;
    }
}
