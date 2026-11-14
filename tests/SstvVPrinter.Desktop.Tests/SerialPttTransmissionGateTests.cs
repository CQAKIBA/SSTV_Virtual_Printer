using System.Collections.Concurrent;
using System.Threading.Channels;
using SstvVPrinter.Desktop.Ptt;

namespace SstvVPrinter.Desktop.Tests;

public sealed class SerialPttTransmissionGateTests
{
    [Fact]
    public async Task Not_applicable_plays_directly_without_serial_operations_or_delays()
    {
        await using var rig = new Rig();
        var gate = rig.CreateGate("N/A");
        var played = false;

        await gate.RunAsync(token =>
        {
            Assert.Equal(TestContext.Current.CancellationToken, token);
            played = true;
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Assert.True(played);
        Assert.Empty(rig.Events);
        Assert.Equal(0, rig.Serial.DelayCount);
        Assert.Equal(0, rig.Clock.DelayCount);
    }

    [Theory]
    [InlineData(PttControlMethod.Rts, "rts:True", "dtr:False")]
    [InlineData(PttControlMethod.Dtr, "rts:False", "dtr:True")]
    public async Task Opens_asserts_waits_lead_plays_waits_tail_and_releases(
        PttControlMethod method, string assertedRts, string assertedDtr)
    {
        await using var rig = new Rig();
        var run = rig.CreateGate(method: method).RunAsync(_ =>
        {
            Assert.True(rig.Controller.Snapshot.IsAsserted);
            rig.Events.Enqueue("playback");
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);
        var lead = await rig.Clock.NextDelayAsync();
        Assert.Equal(["create:COM2", "open", "rts:False", "dtr:False", assertedRts, assertedDtr, "delay"], rig.Events);
        lead.SetResult();
        var tail = await rig.Clock.NextDelayAsync();
        Assert.Equal("delay", rig.Events.Last());
        Assert.True(rig.Controller.Snapshot.IsAsserted);
        Assert.False(run.IsCompleted);
        tail.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(["create:COM2", "open", "rts:False", "dtr:False", assertedRts, assertedDtr,
            "delay", "playback", "delay", "rts:False", "dtr:False"], rig.Events);
        Assert.True(rig.Controller.Snapshot.IsOpen);
        Assert.False(rig.Controller.Snapshot.IsAsserted);
        Assert.False(rig.Controller.Snapshot.IsBusy);
    }

    [Theory]
    [InlineData("lead", 1, false)]
    [InlineData("playback", 1, true)]
    [InlineData("tail", 2, true)]
    public async Task User_cancellation_releases_immediately_and_skips_remaining_delay(
        string phase, int expectedDelays, bool expectedPlayback)
    {
        await using var rig = new Rig();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var played = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = rig.CreateGate().RunAsync(async token =>
        {
            played.SetResult();
            if (phase == "playback") await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, stop.Token);
        var lead = await rig.Clock.NextDelayAsync();
        if (phase != "lead")
        {
            lead.SetResult();
            await played.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (phase == "tail") await rig.Clock.NextDelayAsync();
        }

        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Assert.Equal(expectedPlayback, played.Task.IsCompletedSuccessfully);
        Assert.Equal(expectedDelays, rig.Clock.DelayCount);
        Assert.False(rig.Controller.Snapshot.IsAsserted);
        Assert.False(rig.Controller.Snapshot.IsBusy);
        Assert.True(rig.Controller.Snapshot.IsOpen);
    }

    [Fact]
    public async Task Device_removal_cancels_playback_and_throws_fault_after_playback_unwinds()
    {
        await using var rig = new Rig();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unwound = false;
        var run = rig.CreateGate().RunAsync(async token =>
        {
            started.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { unwound = true; }
        }, TestContext.Current.CancellationToken);
        (await rig.Clock.NextDelayAsync()).SetResult();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        rig.Serial.PortNames = [];
        (await rig.Serial.NextDelayAsync()).SetResult();
        var failure = await Assert.ThrowsAsync<IOException>(() =>
            run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Assert.True(unwound);
        Assert.Contains("PTT", failure.Message);
        Assert.Equal(1, rig.Clock.DelayCount);
        Assert.False(rig.Controller.Snapshot.IsAsserted);
        Assert.False(rig.Controller.Snapshot.IsOpen);
    }

    [Fact]
    public async Task Playback_exception_releases_without_tail_and_preserves_failure()
    {
        await using var rig = new Rig();
        var failure = new InvalidOperationException("Audio failed");
        var run = rig.CreateGate().RunAsync(_ => Task.FromException(failure), TestContext.Current.CancellationToken);
        (await rig.Clock.NextDelayAsync()).SetResult();

        var caught = await Assert.ThrowsAsync<InvalidOperationException>(() => run);

        Assert.Same(failure, caught);
        Assert.Equal(1, rig.Clock.DelayCount);
        Assert.False(rig.Controller.Snapshot.IsAsserted);
        Assert.False(rig.Controller.Snapshot.IsBusy);
    }

    [Fact]
    public async Task Release_exception_surfaces_as_failure()
    {
        await using var rig = new Rig();
        var run = rig.CreateGate().RunAsync(_ => Task.CompletedTask, TestContext.Current.CancellationToken);
        (await rig.Clock.NextDelayAsync()).SetResult();
        var tail = await rig.Clock.NextDelayAsync();
        rig.Serial.Connection.FailRelease = true;
        tail.SetResult();

        await Assert.ThrowsAsync<IOException>(() => run);

        Assert.False(rig.Controller.Snapshot.IsAsserted);
        Assert.False(rig.Controller.Snapshot.IsOpen);
        Assert.NotNull(rig.Controller.Snapshot.Error);
    }

    private sealed class Rig : IAsyncDisposable
    {
        public ConcurrentQueue<string> Events { get; } = new();
        public Platform Serial { get; }
        public Platform Clock { get; }
        public PttController Controller { get; }
        public Rig()
        {
            Serial = new Platform(Events, recordDelays: false);
            Clock = new Platform(Events, recordDelays: true);
            Controller = new PttController(Serial);
        }
        public SerialPttTransmissionGate CreateGate(string port = "COM2", PttControlMethod method = PttControlMethod.Rts) =>
            new(Controller, Clock, port, method);
        public ValueTask DisposeAsync() => Controller.DisposeAsync();
    }

    private sealed class Platform(ConcurrentQueue<string> events, bool recordDelays) : IPttPlatform
    {
        private readonly Channel<TaskCompletionSource> delays = Channel.CreateUnbounded<TaskCompletionSource>();
        private int delayCount;
        public int DelayCount => Volatile.Read(ref delayCount);
        public string[] PortNames { get; set; } = ["COM2"];
        public Connection Connection { get; } = new(events);
        public string[] GetPortNames() => PortNames;
        public IPttSerialConnection CreateConnection(string portName)
        {
            events.Enqueue($"create:{portName}");
            return Connection;
        }
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Assert.Equal(TimeSpan.FromSeconds(1), delay);
            Interlocked.Increment(ref delayCount);
            if (recordDelays) events.Enqueue("delay");
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            delays.Writer.TryWrite(completion);
            return completion.Task.WaitAsync(cancellationToken);
        }
        public async Task<TaskCompletionSource> NextDelayAsync() =>
            await delays.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    private sealed class Connection(ConcurrentQueue<string> events) : IPttSerialConnection
    {
        public bool IsOpen { get; private set; }
        public bool FailRelease { get; set; }
        public void Open() { events.Enqueue("open"); IsOpen = true; }
        public void Close() { events.Enqueue("close"); IsOpen = false; }
        public void SetRts(bool value)
        {
            events.Enqueue($"rts:{value}");
            if (!value && FailRelease) { FailRelease = false; throw new IOException("PTT release failed"); }
        }
        public void SetDtr(bool value) => events.Enqueue($"dtr:{value}");
        public void Dispose() { events.Enqueue("dispose"); IsOpen = false; }
    }
}
