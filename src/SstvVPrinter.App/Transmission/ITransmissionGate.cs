namespace SstvVPrinter.App.Transmission;

public interface ITransmissionGate
{
    Task RunAsync(Func<CancellationToken, Task> playback, CancellationToken cancellationToken);
}

public sealed class NoOpTransmissionGate : ITransmissionGate
{
    public static NoOpTransmissionGate Instance { get; } = new();

    private NoOpTransmissionGate() { }

    public Task RunAsync(Func<CancellationToken, Task> playback, CancellationToken cancellationToken) =>
        playback(cancellationToken);
}
