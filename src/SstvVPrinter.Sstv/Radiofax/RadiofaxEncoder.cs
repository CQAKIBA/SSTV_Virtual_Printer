using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Radiofax;

public static class RadiofaxEncoder
{
    public static IAsyncEnumerable<PcmBlock> EncodeAsync(
        RgbFrame frame,
        RadiofaxEncodeOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate(frame);
        return PcmRenderer.RenderAsync(
            RadiofaxPlanner.Plan(frame, options, cancellationToken),
            options.Amplitude,
            cancellationToken);
    }
}
