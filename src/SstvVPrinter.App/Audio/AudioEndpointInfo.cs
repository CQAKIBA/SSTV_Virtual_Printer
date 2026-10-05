namespace SstvVPrinter.App.Audio;

public sealed record AudioEndpointInfo(
    string Id,
    string Name,
    bool IsAvailable,
    float? VolumePercent,
    bool? IsMuted)
{
    public bool IsDefault { get; init; }
}
