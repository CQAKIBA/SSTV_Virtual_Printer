using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Planning;

internal interface ISstvScanPlanner
{
    IEnumerable<ToneSpan> PlanFrame(RgbFrame frame, SstvEncodeOptions options, CancellationToken cancellationToken);
}
