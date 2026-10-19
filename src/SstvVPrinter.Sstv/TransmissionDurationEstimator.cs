namespace SstvVPrinter.Sstv;

public static class TransmissionDurationEstimator
{
    public static TimeSpan Estimate(RgbFrame frame, TransmissionEncodeOptions options)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate(frame);

        return TransmissionModes.IsRadiofax(options.Mode)
            ? TimeSpan.FromSeconds(50 + (frame.Height * 0.5))
            : SstvDurationEstimator.Estimate(
                new SstvEncodeOptions(options.Mode, options.HorizontalSampleMultiplier, options.Amplitude));
    }
}
