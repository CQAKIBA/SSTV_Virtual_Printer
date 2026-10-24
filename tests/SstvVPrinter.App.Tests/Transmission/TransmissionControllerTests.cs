using SstvVPrinter.App.Jobs;
using SstvVPrinter.App.Transmission;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.App.Tests.Transmission;

public sealed class TransmissionControllerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"sstv-controller-{Guid.NewGuid():N}");

    [Fact]
    public async Task Send_generates_wave_before_entering_gate_and_plays_inside_it()
    {
        var artifacts = new FakeArtifacts(_directory);
        var playback = new FakePlayback();
        var controller = new TransmissionController(artifacts, playback);
        var gate = new CallbackGate(async (play, token) =>
        {
            Assert.True(File.Exists(artifacts.TemporaryPath));
            Assert.Equal(JobState.Transmitting, controller.State);
            Assert.Equal(0, playback.PlayCount);
            await play(token);
            Assert.Equal(1, playback.PlayCount);
        });

        await controller.SendAsync(CreatePrepared(), "endpoint", gate, CancellationToken.None);

        Assert.True(gate.Entered);
        Assert.Equal(JobState.Completed, controller.State);
        Assert.False(File.Exists(artifacts.TemporaryPath));
    }

    [Fact]
    public async Task Stop_during_gate_sets_stopped_and_deletes_wave()
    {
        var artifacts = new FakeArtifacts(_directory);
        var playback = new FakePlayback();
        var controller = new TransmissionController(artifacts, playback);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new CallbackGate(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });
        var send = controller.SendAsync(CreatePrepared(), "endpoint", gate, CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        controller.Stop();
        await send;

        Assert.Equal(JobState.Stopped, controller.State);
        Assert.Equal(0, playback.PlayCount);
        Assert.False(File.Exists(artifacts.TemporaryPath));
    }

    [Fact]
    public async Task Gate_fault_sets_failed_propagates_and_deletes_wave()
    {
        var artifacts = new FakeArtifacts(_directory);
        var playback = new FakePlayback();
        var controller = new TransmissionController(artifacts, playback);
        var failure = new IOException("PTT lost");
        var gate = new CallbackGate((_, _) => Task.FromException(failure));

        var caught = await Assert.ThrowsAsync<IOException>(() =>
            controller.SendAsync(CreatePrepared(), "endpoint", gate, CancellationToken.None));

        Assert.Same(failure, caught);
        Assert.Equal(JobState.Failed, controller.State);
        Assert.Equal(0, playback.PlayCount);
        Assert.False(File.Exists(artifacts.TemporaryPath));
    }

    [Fact]
    public async Task Unrelated_gate_cancellation_is_failed_instead_of_stopped()
    {
        var controller = new TransmissionController(new FakeArtifacts(_directory), new FakePlayback());
        var gate = new CallbackGate((_, _) => Task.FromException(new OperationCanceledException("PTT fault")));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            controller.SendAsync(CreatePrepared(), "endpoint", gate, CancellationToken.None));

        Assert.Equal(JobState.Failed, controller.State);
    }

    [Fact]
    public async Task Save_wave_does_not_invoke_playback()
    {
        var artifacts = new FakeArtifacts(_directory);
        var playback = new FakePlayback();
        var controller = new TransmissionController(artifacts, playback);

        await controller.SaveWaveAsync(CreatePrepared(), Path.Combine(_directory, "saved.wav"), CancellationToken.None);

        Assert.Equal(0, playback.PlayCount);
        Assert.Equal(1, artifacts.SaveCount);
    }

    [Fact]
    public async Task Send_resets_progress_between_encoding_and_playback()
    {
        var artifacts = new FakeArtifacts(_directory);
        var playback = new FakePlayback();
        var controller = new TransmissionController(artifacts, playback);
        var states = new List<JobState>();
        var progress = new List<(JobState State, double Value)>();
        controller.StateChanged += states.Add;
        controller.ProgressChanged += value => progress.Add((controller.State, value));

        await controller.SendAsync(CreatePrepared(), "endpoint", CancellationToken.None);

        Assert.Equal([JobState.Encoding, JobState.Transmitting, JobState.Completed], states);
        Assert.Equal([0, 0.5, 1], progress.Where(item => item.State == JobState.Encoding).Select(item => item.Value).Distinct());
        Assert.Equal([0, 0.5, 1], progress.Where(item => item.State == JobState.Transmitting).Select(item => item.Value).Distinct());
        Assert.All(progress, item => Assert.InRange(item.Value, 0, 1));
        Assert.False(File.Exists(artifacts.TemporaryPath));
    }

    [Fact]
    public async Task A_new_send_resets_progress_after_a_completed_send()
    {
        var controller = new TransmissionController(new FakeArtifacts(_directory), new FakePlayback());
        await controller.SendAsync(CreatePrepared(), "endpoint", CancellationToken.None);
        var progress = new List<double>();
        controller.ProgressChanged += progress.Add;

        await controller.SendAsync(CreatePrepared(), "endpoint", CancellationToken.None);

        Assert.NotEmpty(progress);
        Assert.Equal(0, progress[0]);
        Assert.Equal(1, progress[^1]);
    }

    [Fact]
    public async Task Device_loss_fails_and_deletes_temporary_wave()
    {
        var artifacts = new FakeArtifacts(_directory);
        var playback = new FakePlayback { Failure = new IOException("Device lost") };
        var controller = new TransmissionController(artifacts, playback);

        await Assert.ThrowsAsync<IOException>(() =>
            controller.SendAsync(CreatePrepared(), "endpoint", CancellationToken.None));

        Assert.Equal(JobState.Failed, controller.State);
        Assert.False(File.Exists(artifacts.TemporaryPath));
    }

    [Fact]
    public async Task Stop_is_idempotent_during_encoding()
    {
        var artifacts = new FakeArtifacts(_directory) { WaitForCancellation = true };
        var controller = new TransmissionController(artifacts, new FakePlayback());
        Task send = controller.SendAsync(CreatePrepared(), "endpoint", CancellationToken.None);
        await artifacts.Started.Task;

        controller.Stop();
        controller.Stop();
        await send;

        Assert.Equal(JobState.Stopped, controller.State);
        Assert.False(File.Exists(artifacts.TemporaryPath));
    }

    [Fact]
    public async Task Stop_is_idempotent_during_playback()
    {
        var artifacts = new FakeArtifacts(_directory);
        var playback = new FakePlayback { WaitForCancellation = true };
        var controller = new TransmissionController(artifacts, playback);
        Task send = controller.SendAsync(CreatePrepared(), "endpoint", CancellationToken.None);
        await playback.Started.Task;

        controller.Stop();
        controller.Stop();
        await send;

        Assert.Equal(JobState.Stopped, controller.State);
        Assert.False(File.Exists(artifacts.TemporaryPath));
    }

    private static PreparedTransmission CreatePrepared()
    {
        var frame = RgbFrame.Create(320, 240, new byte[320 * 240 * 3]);
        var options = new TransmissionEncodeOptions(SstvMode.Robot36);
        return new(
            frame, options,
            TransmissionDurationEstimator.Estimate(frame, options), DateTimeOffset.UtcNow);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private sealed class CallbackGate(Func<Func<CancellationToken, Task>, CancellationToken, Task> run) : ITransmissionGate
    {
        public bool Entered { get; private set; }
        public Task RunAsync(Func<CancellationToken, Task> playback, CancellationToken cancellationToken)
        {
            Entered = true;
            return run(playback, cancellationToken);
        }
    }

    private sealed class FakeArtifacts(string directory) : ITransmissionArtifactService
    {
        public string TemporaryPath { get; } = Path.Combine(directory, "temporary.wav");
        public int SaveCount { get; private set; }
        public bool WaitForCancellation { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task SaveAsync(PreparedTransmission prepared, string destinationPath, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            SaveCount++;
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await File.WriteAllBytesAsync(destinationPath, [1], cancellationToken);
        }

        public async Task<string> CreateTemporaryAsync(PreparedTransmission prepared, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(directory);
            Started.TrySetResult();
            if (WaitForCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            progress?.Report(0);
            progress?.Report(0.5);
            progress?.Report(1);
            await File.WriteAllBytesAsync(TemporaryPath, [1], cancellationToken);
            return TemporaryPath;
        }
    }

    private sealed class FakePlayback : IWavePlaybackService
    {
        public int PlayCount { get; private set; }
        public Exception? Failure { get; init; }
        public bool WaitForCancellation { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task PlayAsync(string path, string endpointId, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            PlayCount++;
            Started.TrySetResult();
            if (Failure is not null) throw Failure;
            if (WaitForCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            progress?.Report(0);
            progress?.Report(0.5);
            progress?.Report(1);
        }
    }
}
