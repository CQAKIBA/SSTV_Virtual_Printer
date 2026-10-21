using SstvVPrinter.Sstv;
using SstvVPrinter.Imaging.Composition;

namespace SstvVPrinter.App.Transmission;

public sealed record TransmissionSettings
{
    public TransmissionSettings(
        string CallSign,
        SstvMode Mode,
        int HorizontalSampleMultiplier,
        int RotationDegrees = 0,
        HeaderPlacement headerPlacement = HeaderPlacement.ReserveSpace)
    {
        ArgumentNullException.ThrowIfNull(CallSign);
        TransmissionModes.Validate(Mode);
        if (HorizontalSampleMultiplier is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(HorizontalSampleMultiplier));
        }
        if (RotationDegrees is not (0 or 90 or 180 or 270))
        {
            throw new ArgumentOutOfRangeException(nameof(RotationDegrees));
        }
        if (headerPlacement is not (HeaderPlacement.ReserveSpace or HeaderPlacement.Overlay))
        {
            throw new ArgumentOutOfRangeException(nameof(headerPlacement));
        }

        this.CallSign = CallSign;
        this.Mode = Mode;
        this.HorizontalSampleMultiplier = HorizontalSampleMultiplier;
        this.RotationDegrees = RotationDegrees;
        HeaderPlacement = headerPlacement;
    }

    public string CallSign { get; init; }
    public SstvMode Mode { get; init; }
    public int HorizontalSampleMultiplier { get; init; }
    public int RotationDegrees { get; init; }
    public HeaderPlacement HeaderPlacement { get; init; }
}
