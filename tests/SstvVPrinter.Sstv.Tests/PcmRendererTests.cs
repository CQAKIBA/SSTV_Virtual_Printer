using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Tests;

public sealed class PcmRendererTests
{
    [Fact]
    public async Task RenderAsync_emits_bounded_4096_sample_blocks()
    {
        var blocks = await CollectBlocks(new ToneSpan(1000, 0.2));

        Assert.All(blocks, block => Assert.InRange(block.Samples.Length, 1, 4096));
        Assert.Equal(9600, blocks.Sum(block => block.Samples.Length));
        Assert.Equal([0L, 4096L, 8192L], blocks.Select(block => block.StartSample));
    }

    [Fact]
    public async Task RenderAsync_keeps_every_boundary_and_total_within_one_sample()
    {
        const double duration = 0.146432 / 320;
        var spans = Enumerable.Repeat(new ToneSpan(1200, duration), 320 * 256).ToArray();

        foreach (var count in new[] { 1, 319, 320, 1000, spans.Length })
        {
            var samples = await CollectSamples(spans.Take(count));
            var expectedBoundary = (long)Math.Round(count * duration * 48_000, MidpointRounding.AwayFromZero);
            Assert.Equal(expectedBoundary, samples.LongLength);
        }
    }

    [Fact]
    public async Task RenderAsync_preserves_phase_across_blocks_and_spans()
    {
        var spans = new[] { new ToneSpan(1000, 0.09731), new ToneSpan(1700, 0.1) };
        var actual = await CollectSamples(spans);
        var transitionSample = (int)Math.Round(0.09731 * 48_000, MidpointRounding.AwayFromZero);
        var expected = new float[actual.Length];
        var phase = 0.0;
        for (var i = 0; i < expected.Length; i++)
        {
            expected[i] = (float)(0.8 * Math.Sin(phase));
            phase += 2 * Math.PI * (i < transitionSample ? 1000 : 1700) / 48_000;
            phase %= 2 * Math.PI;
        }

        for (var i = 0; i < actual.Length; i++)
        {
            Assert.Equal(expected[i], actual[i], 5);
        }
    }

    [Fact]
    public async Task RenderAsync_applies_amplitude_without_clipping()
    {
        var samples = await CollectSamples([new ToneSpan(1200, 0.1)], 0.25f);

        Assert.InRange(samples.Max(), 0.2499f, 0.2501f);
        Assert.InRange(samples.Min(), -0.2501f, -0.2499f);
    }

    [Fact]
    public async Task RenderAsync_is_deterministic()
    {
        var spans = new[] { new ToneSpan(1234, 0.03), new ToneSpan(1876, 0.04) };

        Assert.Equal(await CollectSamples(spans), await CollectSamples(spans));
    }

    [Fact]
    public async Task RenderAsync_observes_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in PcmRenderer.RenderAsync([new ToneSpan(1000, 1)], 0.8f, cancellation.Token))
            {
            }
        });
    }

    private static async Task<PcmBlock[]> CollectBlocks(params ToneSpan[] spans)
    {
        var blocks = new List<PcmBlock>();
        await foreach (var block in PcmRenderer.RenderAsync(spans, 0.8f, CancellationToken.None))
        {
            blocks.Add(block);
        }

        return blocks.ToArray();
    }

    private static async Task<float[]> CollectSamples(IEnumerable<ToneSpan> spans, float amplitude = 0.8f)
    {
        var samples = new List<float>();
        await foreach (var block in PcmRenderer.RenderAsync(spans, amplitude, CancellationToken.None))
        {
            samples.AddRange(block.Samples.ToArray());
        }

        return samples.ToArray();
    }
}
