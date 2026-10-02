namespace SstvVPrinter.Sstv.Signal;

public readonly record struct ToneSpan
{
    public ToneSpan(double frequencyHz, double durationSeconds)
    {
        if (!double.IsFinite(frequencyHz) || frequencyHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frequencyHz));
        }

        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        FrequencyHz = frequencyHz;
        DurationSeconds = durationSeconds;
    }

    public double FrequencyHz { get; }
    public double DurationSeconds { get; }
}
