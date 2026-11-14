namespace SstvVPrinter.Sstv;

public sealed record SstvEncodeOptions
{
    public const int SampleRate = Signal.SignalConstants.SampleRate;

    public SstvEncodeOptions(
        SstvMode Mode,
        int HorizontalSampleMultiplier = 1,
        float Amplitude = 0.8f)
    {
        _ = SstvModes.Get(Mode);

        if (HorizontalSampleMultiplier is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(HorizontalSampleMultiplier));
        }

        if (!float.IsFinite(Amplitude) || Amplitude <= 0 || Amplitude > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Amplitude));
        }

        this.Mode = Mode;
        this.HorizontalSampleMultiplier = HorizontalSampleMultiplier;
        this.Amplitude = Amplitude;
    }

    public SstvMode Mode { get; }

    public int HorizontalSampleMultiplier { get; }

    public float Amplitude { get; }

    public void Validate(RgbFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var definition = SstvModes.Get(Mode);
        var expectedWidth = definition.Width * HorizontalSampleMultiplier;
        if (frame.Width != expectedWidth || frame.Height != definition.Height)
        {
            throw new ArgumentException(
                $"Mode {definition.DisplayName} at {HorizontalSampleMultiplier}x requires " +
                $"{expectedWidth}x{definition.Height}, but received {frame.Width}x{frame.Height}.",
                nameof(frame));
        }
    }
}
