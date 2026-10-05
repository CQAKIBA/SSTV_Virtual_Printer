using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Planning;

internal sealed class SequentialRgbScanPlanner(SstvMode mode) : ISstvScanPlanner
{
    public IEnumerable<ToneSpan> PlanFrame(RgbFrame frame, SstvEncodeOptions options, CancellationToken cancellationToken)
    {
        options.Validate(frame);
        if (options.Mode != mode)
        {
            throw new ArgumentException("Options mode does not match this planner.", nameof(options));
        }

        return mode switch
        {
            SstvMode.Martin1 => PlanMartin(frame, cancellationToken),
            SstvMode.Scottie1 => PlanScottie(frame, 0.138240, cancellationToken),
            SstvMode.Scottie2 => PlanScottie(frame, 0.088064, cancellationToken),
            _ => throw new NotSupportedException($"Mode {mode} is not implemented."),
        };
    }

    private static IEnumerable<ToneSpan> PlanScottie(RgbFrame frame, double scanDuration, CancellationToken cancellationToken)
    {
        var pixelDuration = scanDuration / frame.Width;
        yield return new ToneSpan(1200, 0.009);
        for (var y = 0; y < frame.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var span in Scan(frame, y, pixelDuration, static pixel => pixel.G, cancellationToken)) yield return span;
            yield return new ToneSpan(1500, 0.0015);
            foreach (var span in Scan(frame, y, pixelDuration, static pixel => pixel.B, cancellationToken)) yield return span;
            yield return new ToneSpan(1500, 0.0015);
            yield return new ToneSpan(1200, 0.009);
            yield return new ToneSpan(1500, 0.0015);
            foreach (var span in Scan(frame, y, pixelDuration, static pixel => pixel.R, cancellationToken)) yield return span;
        }
    }

    private static IEnumerable<ToneSpan> PlanMartin(RgbFrame frame, CancellationToken cancellationToken)
    {
        var pixelDuration = 0.146432 / frame.Width;
        for (var y = 0; y < frame.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ToneSpan(1200, 0.004862);
            yield return new ToneSpan(1500, 0.000572);
            foreach (var span in Scan(frame, y, pixelDuration, static pixel => pixel.G, cancellationToken)) yield return span;
            yield return new ToneSpan(1500, 0.000572);
            foreach (var span in Scan(frame, y, pixelDuration, static pixel => pixel.B, cancellationToken)) yield return span;
            yield return new ToneSpan(1500, 0.000572);
            foreach (var span in Scan(frame, y, pixelDuration, static pixel => pixel.R, cancellationToken)) yield return span;
            yield return new ToneSpan(1500, 0.000572);
        }
    }

    private static IEnumerable<ToneSpan> Scan(RgbFrame frame, int y, double pixelDuration, Func<Rgb24, byte> select, CancellationToken cancellationToken)
    {
        for (var x = 0; x < frame.Width; x++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ToneSpan(VideoFrequency.FromByte(select(frame.GetPixel(x, y))), pixelDuration);
        }
    }
}
