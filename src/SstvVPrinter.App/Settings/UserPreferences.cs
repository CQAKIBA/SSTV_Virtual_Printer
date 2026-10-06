using SstvVPrinter.Sstv;

namespace SstvVPrinter.App.Settings;

public sealed record UserPreferences
{
    public static UserPreferences Default { get; } =
        new(string.Empty, SstvMode.Scottie2, 1, null, true, 0);

    public UserPreferences(
        string CallSign,
        SstvMode Mode,
        int HorizontalSampleMultiplier,
        string? AudioEndpointId,
        bool StartAtSignIn,
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
        this.AudioEndpointId = AudioEndpointId;
        this.StartAtSignIn = StartAtSignIn;
        this.RotationDegrees = RotationDegrees;
    }

    public string CallSign { get; init; }
    public SstvMode Mode { get; init; }
    public int HorizontalSampleMultiplier { get; init; }
    public string? AudioEndpointId { get; init; }
    public bool StartAtSignIn { get; init; }
    public int RotationDegrees { get; init; }
}
