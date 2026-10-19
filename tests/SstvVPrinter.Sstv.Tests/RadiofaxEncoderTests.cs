using SstvVPrinter.Sstv.Radiofax;
using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Tests;

public sealed class RadiofaxEncoderTests
{
    [Fact]
    public async Task EncodeAsync_renders_exact_total_sample_count()
    {
        var frame = Frame(864, 32);
        var options = new RadiofaxEncodeOptions(SstvMode.Radiofax120Ioc288);

        var samples = await Count(RadiofaxEncoder.EncodeAsync(frame, options));

        Assert.Equal((long)((50 + (32 * 0.5)) * SignalConstants.SampleRate), samples);
    }

    [Fact]
    public async Task EncodeAsync_observes_pre_cancelled_token()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in RadiofaxEncoder.EncodeAsync(
                Frame(864, 32),
                new RadiofaxEncodeOptions(SstvMode.Radiofax120Ioc288),
                cancellation.Token))
            {
            }
        });
    }

    private static RgbFrame Frame(int width, int height) =>
        RgbFrame.Create(width, height, new byte[width * height * 3]);

    private static async Task<long> Count(IAsyncEnumerable<PcmBlock> source)
    {
        long count = 0;
        await foreach (var block in source)
        {
            count += block.Samples.Length;
        }

        return count;
    }
}
