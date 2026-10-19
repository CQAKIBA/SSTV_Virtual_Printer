namespace SstvVPrinter.Sstv;

public sealed record TransmissionEncodeOptions
{
    public TransmissionEncodeOptions(
        SstvMode Mode,
        int HorizontalSampleMultiplier = 1,
        float Amplitude = 0.8f)
    {
        TransmissionModes.Validate(Mode);

        if (HorizontalSampleMultiplier is not (1 or 2) ||
            (TransmissionModes.IsRadiofax(Mode) && HorizontalSampleMultiplier != 1))
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
}
