using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Planning;

internal sealed class Robot36ScanPlanner : ISstvScanPlanner
{
    public IEnumerable<ToneSpan> PlanFrame(RgbFrame frame, SstvEncodeOptions options, CancellationToken cancellationToken)
    {
        options.Validate(frame);
        if (options.Mode != SstvMode.Robot36)
        {
            throw new ArgumentException("Robot 36 planner requires Robot 36 options.", nameof(options));
        }

        var yDuration = 0.088 / frame.Width;
        var chromaDuration = 0.044 / frame.Width;
        for (var y = 0; y < frame.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ToneSpan(1200, 0.009);
            yield return new ToneSpan(1500, 0.003);

            for (var x = 0; x < frame.Width; x++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var components = RobotColorConverter.Convert(frame.GetPixel(x, y));
                yield return new ToneSpan(VideoFrequency.FromByte(components.Y), yDuration);
            }

            var evenLine = (y & 1) == 0;
            yield return new ToneSpan(evenLine ? 1500 : 2300, 0.0045);
            yield return new ToneSpan(1900, 0.0015);

            var pairStart = y & ~1;
            for (var x = 0; x < frame.Width; x++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var first = RobotColorConverter.Convert(frame.GetPixel(x, pairStart));
                var second = RobotColorConverter.Convert(frame.GetPixel(x, pairStart + 1));
                var chroma = evenLine
                    ? RobotColorConverter.Average(first.RedDifference, second.RedDifference)
                    : RobotColorConverter.Average(first.BlueDifference, second.BlueDifference);
                yield return new ToneSpan(VideoFrequency.FromByte(chroma), chromaDuration);
            }
        }
    }
}
