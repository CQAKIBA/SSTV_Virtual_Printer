namespace SstvVPrinter.App.Audio;

public interface IAudioEndpointMonitor
{
    ValueTask<IReadOnlyList<AudioEndpointInfo>> GetEndpointsAsync(
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<IReadOnlyList<AudioEndpointInfo>> WatchAsync(
        CancellationToken cancellationToken = default);
}
