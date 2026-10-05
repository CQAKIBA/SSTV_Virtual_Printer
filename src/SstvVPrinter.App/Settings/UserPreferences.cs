using SstvVPrinter.Sstv;

namespace SstvVPrinter.App.Settings;

public sealed record UserPreferences
{
    public static UserPreferences Default { get; } =
        new(string.Empty, SstvMode.Scottie2, 1, null, true);

    public UserPreferences(
        string CallSign,
        SstvMode Mode,
        int HorizontalSampleMultiplier,
        string? AudioEndpointId,
        bool StartAtSignIn)
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
        this.AudioEndpointId = AudioEndpointId;
        this.StartAtSignIn = StartAtSignIn;
    }

    public string CallSign { get; init; }
    public SstvMode Mode { get; init; }
    public int HorizontalSampleMultiplier { get; init; }
    public string? AudioEndpointId { get; init; }
    public bool StartAtSignIn { get; init; }
}
