using SstvVPrinter.App.Jobs;

namespace SstvVPrinter.App.Transmission;

public sealed class TransmissionController(
    ITransmissionArtifactService artifacts,
    IWavePlaybackService playback)
{
    private readonly Lock _gate = new();
    private CancellationTokenSource? _activeCancellation;

    public JobState State { get; private set; } = JobState.Ready;
    public double Progress { get; private set; }
    public event Action<JobState>? StateChanged;
    public event Action<double>? ProgressChanged;

    public Task SaveWaveAsync(
        PreparedTransmission prepared,
        string destinationPath,
        CancellationToken cancellationToken) =>
        artifacts.SaveAsync(prepared, destinationPath, null, cancellationToken);

    public async Task SendAsync(
        PreparedTransmission prepared,
        string endpointId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        CancellationTokenSource linked;
        lock (_gate)
        {
            if (_activeCancellation is not null)
            {
                throw new InvalidOperationException("A transmission is already active.");
            }

            linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeCancellation = linked;
        }

        string? temporaryPath = null;
        try
        {
            SetState(JobState.Encoding);
            ResetProgress();
            temporaryPath = await artifacts.CreateTemporaryAsync(
                prepared,
                new InlineProgress(value => SetProgress(value * 0.2)),
                linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            SetState(JobState.Transmitting);
            temporaryPath = Path.GetFullPath(temporaryPath);
            await playback.PlayAsync(
                temporaryPath,
                endpointId,
                new InlineProgress(value => SetProgress(0.2 + (value * 0.8))),
                linked.Token);
            SetProgress(1);
            SetState(JobState.Completed);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            SetState(JobState.Stopped);
        }
        catch
        {
            SetState(JobState.Failed);
            throw;
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try { File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            lock (_gate)
            {
                _activeCancellation = null;
            }
            linked.Dispose();
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _activeCancellation?.Cancel();
        }
    }

    private void SetState(JobState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    private void SetProgress(double value)
    {
        var normalized = Math.Clamp(value, 0, 1);
        if (normalized < Progress) return;
        Progress = normalized;
        ProgressChanged?.Invoke(normalized);
    }

    private void ResetProgress()
    {
        Progress = 0;
        ProgressChanged?.Invoke(0);
    }

    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
