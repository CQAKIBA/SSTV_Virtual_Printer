namespace SstvVPrinter.App.Audio;

public sealed record AudioEndpointInfo(
    string Id,
    string Name,
    bool IsAvailable,
    float? VolumePercent,
    bool? IsMuted)
{
    public bool IsDefault { get; init; }
    public float? AppVolumePercent { get; init; }
    public bool? AppIsMuted { get; init; }

    public string DisplayName
    {
        get
        {
            var trimmedId = Id.TrimEnd('}');
            var suffix = trimmedId.Length <= 12 ? trimmedId : trimmedId[^12..];
            return $"{Name} - {suffix}{(IsDefault ? " (既定)" : string.Empty)}";
        }
    }
}
