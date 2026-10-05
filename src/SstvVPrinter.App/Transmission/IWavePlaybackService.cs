namespace SstvVPrinter.App.Transmission;

public interface IWavePlaybackService
{
    Task PlayAsync(
        string path,
        string endpointId,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}
