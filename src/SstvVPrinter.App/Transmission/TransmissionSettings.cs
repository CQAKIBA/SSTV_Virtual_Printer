using SstvVPrinter.Sstv;

namespace SstvVPrinter.App.Transmission;

public sealed record TransmissionSettings
{
    public TransmissionSettings(string CallSign, SstvMode Mode, int HorizontalSampleMultiplier)
    {
        ArgumentNullException.ThrowIfNull(CallSign);
        _ = SstvModes.Get(Mode);
        if (HorizontalSampleMultiplier is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(HorizontalSampleMultiplier));
        }

        this.CallSign = CallSign;
        this.Mode = Mode;
        this.HorizontalSampleMultiplier = HorizontalSampleMultiplier;
    }

    public string CallSign { get; init; }
    public SstvMode Mode { get; init; }
    public int HorizontalSampleMultiplier { get; init; }
}
