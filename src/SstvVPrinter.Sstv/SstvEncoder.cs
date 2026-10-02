using SstvVPrinter.Sstv.Planning;
using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv;

public static class SstvEncoder
{
    public static IAsyncEnumerable<PcmBlock> EncodeAsync(
        RgbFrame frame,
        SstvEncodeOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate(frame);

        return PcmRenderer.RenderAsync(Plan(), options.Amplitude, cancellationToken);

        IEnumerable<ToneSpan> Plan()
        {
            var definition = SstvModes.Get(options.Mode);
            foreach (var span in VisHeader.Plan(definition.VisCode))
            {
                yield return span;
            }

            var planner = SstvScanPlannerFactory.Create(options.Mode);
            foreach (var span in planner.PlanFrame(frame, options, cancellationToken))
            {
                yield return span;
            }
        }
    }
}
