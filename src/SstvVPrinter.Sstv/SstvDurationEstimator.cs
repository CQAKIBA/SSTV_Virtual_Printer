namespace SstvVPrinter.Sstv;

public static class SstvDurationEstimator
{
    private const double VisHeaderSeconds = 0.910;

    public static TimeSpan Estimate(SstvEncodeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var seconds = options.Mode switch
        {
            SstvMode.Martin1 => VisHeaderSeconds + (256 * 0.446446),
            SstvMode.Scottie1 => VisHeaderSeconds + 0.009 + (256 * ((3 * 0.138240) + 0.0135)),
            SstvMode.Scottie2 => VisHeaderSeconds + 0.009 + (256 * ((3 * 0.088064) + 0.0135)),
            SstvMode.Robot36 => VisHeaderSeconds + (240 * 0.150),
            _ => throw new ArgumentOutOfRangeException(nameof(options)),
        };
        return TimeSpan.FromSeconds(seconds);
    }
}
