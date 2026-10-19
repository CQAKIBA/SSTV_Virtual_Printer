using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Tests;

public sealed class TransmissionEncoderTests
{
    [Fact]
    public void Generic_options_validate_radiofax_dimensions_before_enumeration()
    {
        var options = new TransmissionEncodeOptions(SstvMode.Radiofax120Ioc288);
        var wrongFrame = Frame(320, 256);

        Assert.Throws<ArgumentException>(() => options.Validate(wrongFrame));
        Assert.Throws<ArgumentException>(() => TransmissionEncoder.EncodeAsync(wrongFrame, options));
    }

    [Fact]
    public async Task Generic_encoder_dispatches_radiofax_and_duration_matches_samples()
    {
        var frame = Frame(864, 32);
        var options = new TransmissionEncodeOptions(SstvMode.Radiofax120Ioc288);

        var sampleCount = await Count(TransmissionEncoder.EncodeAsync(frame, options));

        Assert.Equal(TimeSpan.FromSeconds(66), TransmissionDurationEstimator.Estimate(frame, options));
        Assert.Equal(66L * SignalConstants.SampleRate, sampleCount);
    }

    [Fact]
    public async Task Generic_encoder_keeps_legacy_sstv_dispatch_and_duration()
    {
        var frame = Frame(320, 240);
        var generic = new TransmissionEncodeOptions(SstvMode.Robot36);
        var legacy = new SstvEncodeOptions(SstvMode.Robot36);

        var genericFirst = await First(TransmissionEncoder.EncodeAsync(frame, generic));
        var legacyFirst = await First(SstvEncoder.EncodeAsync(frame, legacy));

        Assert.Equal(legacyFirst.Samples.ToArray(), genericFirst.Samples.ToArray());
        Assert.Equal(SstvDurationEstimator.Estimate(legacy), TransmissionDurationEstimator.Estimate(frame, generic));
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

    private static async Task<PcmBlock> First(IAsyncEnumerable<PcmBlock> source)
    {
        await foreach (var block in source)
        {
            return block;
        }

        throw new InvalidOperationException();
    }
}
