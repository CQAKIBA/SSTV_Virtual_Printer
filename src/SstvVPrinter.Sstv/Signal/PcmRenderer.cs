using System.Runtime.CompilerServices;

namespace SstvVPrinter.Sstv.Signal;

public static class PcmRenderer
{
    private const int MaximumBlockSamples = 4096;
    private const double TwoPi = 2 * Math.PI;

    public static async IAsyncEnumerable<PcmBlock> RenderAsync(
        IEnumerable<ToneSpan> spans,
        float amplitude,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spans);
        if (!float.IsFinite(amplitude) || amplitude <= 0 || amplitude > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(amplitude));
        }

        var buffer = new float[MaximumBlockSamples];
        var buffered = 0;
        long emittedSamples = 0;
        var cumulativeIdealSamples = 0.0;
        var phase = 0.0;

        foreach (var span in spans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            cumulativeIdealSamples += span.DurationSeconds * SstvEncodeOptions.SampleRate;
            var spanEnd = checked((long)Math.Round(cumulativeIdealSamples, MidpointRounding.AwayFromZero));
            var increment = TwoPi * span.FrequencyHz / SstvEncodeOptions.SampleRate;

            while (emittedSamples + buffered < spanEnd)
            {
                cancellationToken.ThrowIfCancellationRequested();
                buffer[buffered++] = (float)(amplitude * Math.Sin(phase));
                phase += increment;
                if (phase >= TwoPi)
                {
                    phase %= TwoPi;
                }

                if (buffered == MaximumBlockSamples)
                {
                    yield return new PcmBlock(emittedSamples, buffer.ToArray());
                    emittedSamples += buffered;
                    buffered = 0;
                    await Task.Yield();
                }
            }
        }

        if (buffered > 0)
        {
            yield return new PcmBlock(emittedSamples, buffer.AsMemory(0, buffered).ToArray());
        }
    }
}
