using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Radiofax;

public static class RadiofaxPlanner
{
    private const double BlackHz = 1500;
    private const double WhiteHz = 2300;

    public static IEnumerable<ToneSpan> Plan(
        RgbFrame frame,
        RadiofaxEncodeOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate(frame);
        return Enumerate();

        IEnumerable<ToneSpan> Enumerate()
        {
            var definition = RadiofaxModes.Get(options.Mode);
            foreach (var span in Alternating(definition.IocSelectionHz, 5, cancellationToken))
            {
                yield return span;
            }

            for (var line = 0; line < 60; line++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new ToneSpan(WhiteHz, 0.025);
                yield return new ToneSpan(BlackHz, 0.475);
            }

            var pixelDuration = 0.4775 / frame.Width;
            for (var y = 0; y < frame.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new ToneSpan(WhiteHz, 0.0225);
                for (var x = 0; x < frame.Width; x++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return new ToneSpan(RadiofaxFrequency.FromLuminance(frame.GetPixel(x, y).R), pixelDuration);
                }
            }

            foreach (var span in Alternating(450, 5, cancellationToken))
            {
                yield return span;
            }

            cancellationToken.ThrowIfCancellationRequested();
            yield return new ToneSpan(BlackHz, 10);
        }
    }

    private static IEnumerable<ToneSpan> Alternating(
        double selectorHz,
        double durationSeconds,
        CancellationToken cancellationToken)
    {
        var halfCycleDuration = 1 / (2 * selectorHz);
        var spanCount = checked((int)Math.Round(durationSeconds / halfCycleDuration));
        for (var index = 0; index < spanCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ToneSpan(index % 2 == 0 ? BlackHz : WhiteHz, halfCycleDuration);
        }
    }
}
