using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Composition;

public sealed record CompositionRequest
{
    private const string SupportedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 /-:.";

    public CompositionRequest(
        SstvMode mode,
        int horizontalSampleMultiplier,
        string callSign,
        DateTimeOffset acceptedAt,
        HeaderPlacement placement = HeaderPlacement.Top)
    {
        _ = SstvModes.Get(mode);

        if (horizontalSampleMultiplier is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(horizontalSampleMultiplier));
        }

        ArgumentNullException.ThrowIfNull(callSign);
        var normalizedCallSign = callSign.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCallSign) ||
            normalizedCallSign.Any(character => !SupportedCharacters.Contains(character)))
        {
            throw new ArgumentException("Call sign is empty or contains an unsupported character.", nameof(callSign));
        }

        if (placement != HeaderPlacement.Top)
        {
            throw new ArgumentOutOfRangeException(nameof(placement));
        }

        Mode = mode;
        HorizontalSampleMultiplier = horizontalSampleMultiplier;
        CallSign = normalizedCallSign;
        AcceptedAt = acceptedAt;
        Placement = placement;
    }

    public SstvMode Mode { get; }
    public int HorizontalSampleMultiplier { get; }
    public string CallSign { get; }
    public DateTimeOffset AcceptedAt { get; }
    public HeaderPlacement Placement { get; }
}
