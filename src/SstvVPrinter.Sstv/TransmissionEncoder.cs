using SstvVPrinter.Sstv.Radiofax;
using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv;

public static class TransmissionEncoder
{
    public static IAsyncEnumerable<PcmBlock> EncodeAsync(
        RgbFrame frame,
        TransmissionEncodeOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate(frame);

        return TransmissionModes.IsRadiofax(options.Mode)
            ? RadiofaxEncoder.EncodeAsync(
                frame,
                new RadiofaxEncodeOptions(options.Mode, options.Amplitude),
                cancellationToken)
            : SstvEncoder.EncodeAsync(
                frame,
                new SstvEncodeOptions(options.Mode, options.HorizontalSampleMultiplier, options.Amplitude),
                cancellationToken);
    }
}
