using SstvVPrinter.App.Transmission;

namespace SstvVPrinter.Desktop.Ptt;

public sealed class SerialPttTransmissionGate(
    PttController controller,
    IPttPlatform platform,
    string portName,
    PttControlMethod method) : ITransmissionGate
{
    public async Task RunAsync(Func<CancellationToken, Task> playback, CancellationToken cancellationToken)
    {
        if (portName.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            await NoOpTransmissionGate.Instance.RunAsync(playback, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var lease = await controller.AcquireAutomaticAsync(portName, method, cancellationToken)
            .ConfigureAwait(false);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.FaultToken);
        try
        {
            await platform.DelayAsync(TimeSpan.FromSeconds(1), linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            await playback(linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            await platform.DelayAsync(TimeSpan.FromSeconds(1), linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            await lease.ReleaseAsync().ConfigureAwait(false);
            lease.FaultToken.ThrowIfCancellationRequested();
        }
        catch (Exception error)
        {
            // Abort is intentionally independent of the user's canceled token.
            try { await lease.AbortAsync().ConfigureAwait(false); }
            catch (Exception cleanupError)
            {
                throw new IOException("PTT release failed.", new AggregateException(error, cleanupError));
            }

            // Playback must unwind before a device cancellation becomes a job failure.
            if (lease.FaultToken.IsCancellationRequested)
                throw new IOException(controller.Snapshot.Error ?? "PTT connection failed.", error);
            throw;
        }
    }
}
