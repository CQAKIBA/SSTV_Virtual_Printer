using SstvVPrinter.Sstv;

namespace SstvVPrinter.App.Transmission;

public sealed record TransmissionSettings
{
    public TransmissionSettings(
        string CallSign,
        SstvMode Mode,
        int HorizontalSampleMultiplier,
        int RotationDegrees = 0)
    {
        ArgumentNullException.ThrowIfNull(CallSign);
        _ = SstvModes.Get(Mode);
        if (HorizontalSampleMultiplier is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(HorizontalSampleMultiplier));
        }
        if (RotationDegrees is not (0 or 90 or 180 or 270))
        {
            throw new ArgumentOutOfRangeException(nameof(RotationDegrees));
        }

        this.CallSign = CallSign;
        this.Mode = Mode;
        this.HorizontalSampleMultiplier = HorizontalSampleMultiplier;
        this.RotationDegrees = RotationDegrees;
    }

    public string CallSign { get; init; }
    public SstvMode Mode { get; init; }
    public int HorizontalSampleMultiplier { get; init; }
    public int RotationDegrees { get; init; }
}
