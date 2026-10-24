using System.Collections.Concurrent;
using System.Threading.Channels;
using SstvVPrinter.Desktop.Ptt;

namespace SstvVPrinter.Desktop.Tests;

public sealed class PttControllerTests
{
    [Fact]
    public async Task Discovery_sorts_naturally_and_preserves_unchanged_list()
    {
        var platform = new FakePlatform { PortNames = ["COM10", "COM2", "COM1"] };
        await using var controller = new PttController(platform);

        var first = await controller.RefreshPortsAsync(TestContext.Current.CancellationToken);
        platform.PortNames = ["COM2", "COM1", "COM10"];
        var second = await controller.RefreshPortsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["N/A", "COM1", "COM2", "COM10"], first);
        Assert.Same(first, second);
        platform.PortNames = [];
        Assert.Equal(["N/A"], await controller.RefreshPortsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Open_and_close_are_idempotent_and_close_clears_both_lines_first()
    {
        var platform = new FakePlatform();
        await using var controller = new PttController(platform);
        await controller.OpenAsync("COM2", PttControlMethod.Rts, TestContext.Current.CancellationToken);
        await controller.OpenAsync("COM2", PttControlMethod.Rts, TestContext.Current.CancellationToken);

        Assert.True(controller.Snapshot.IsOpen);
        Assert.Equal(1, platform.CreateCount);
        await controller.CloseAsync(TestContext.Current.CancellationToken);
        await controller.CloseAsync(TestContext.Current.CancellationToken);

        Assert.False(controller.Snapshot.IsOpen);
        Assert.Equal(["open", "rts:False", "dtr:False", "rts:False", "dtr:False", "close", "dispose"], platform.Connection.Calls);
    }

    [Theory]
    [InlineData(PttControlMethod.Rts, true, false)]
    [InlineData(PttControlMethod.Dtr, false, true)]
    public async Task Manual_assertion_uses_only_selected_line(PttControlMethod method, bool rts, bool dtr)
    {
        var platform = new FakePlatform();
        await using var controller = new PttController(platform);
        await controller.BeginManualAsync("COM2", method, TestContext.Current.CancellationToken);

        Assert.True(controller.Snapshot.IsAsserted);
        Assert.Equal(rts, platform.Connection.Rts);
        Assert.Equal(dtr, platform.Connection.Dtr);
        await controller.EndManualAsync(TestContext.Current.CancellationToken);
        Assert.True(controller.Snapshot.IsOpen);
        Assert.False(controller.Snapshot.IsAsserted);
        Assert.False(platform.Connection.Rts);
        Assert.False(platform.Connection.Dtr);
    }

    [Fact]
    public async Task Release_before_open_completes_never_asserts()
    {
        var platform = new FakePlatform();
        platform.Connection.BlockOpen = true;
        await using var controller = new PttController(platform);
        var press = controller.BeginManualAsync("COM2", PttControlMethod.Rts, TestContext.Current.CancellationToken);
        await platform.Connection.OpenStarted.Task;
        var release = controller.EndManualAsync(TestContext.Current.CancellationToken);
        platform.Connection.ContinueOpen.SetResult();
        await Task.WhenAll(press, release);

        Assert.DoesNotContain("rts:True", platform.Connection.Calls);
        Assert.DoesNotContain("dtr:True", platform.Connection.Calls);
        Assert.True(controller.Snapshot.IsOpen);
        Assert.False(controller.Snapshot.IsAsserted);
    }

    [Fact]
    public async Task Removed_port_aborts_automatic_lease_and_closes()
    {
        var platform = new FakePlatform();
        await using var controller = new PttController(platform);
        await using var lease = await controller.AcquireAutomaticAsync("COM2", PttControlMethod.Rts, TestContext.Current.CancellationToken);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.StateChanged += (_, state) => { if (!state.IsOpen) closed.TrySetResult(); };
        Assert.True(controller.Snapshot.IsBusy);
        platform.PortNames = [];
        await platform.TickAsync();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(lease.FaultToken.IsCancellationRequested);
        Assert.False(controller.Snapshot.IsAsserted);
        Assert.False(controller.Snapshot.IsBusy);
        Assert.NotNull(controller.Snapshot.Error);
        Assert.False(platform.Connection.IsOpen);
        Assert.False(platform.Connection.Rts);
    }

    [Fact]
    public async Task Serial_exception_closes_and_clears_assertion()
    {
        var platform = new FakePlatform();
        platform.Connection.FailNextAssertion = true;
        await using var controller = new PttController(platform);

        await Assert.ThrowsAsync<IOException>(() => controller.BeginManualAsync("COM2", PttControlMethod.Rts, TestContext.Current.CancellationToken));

        Assert.False(controller.Snapshot.IsOpen);
        Assert.False(controller.Snapshot.IsAsserted);
        Assert.NotNull(controller.Snapshot.Error);
        Assert.Contains("dispose", platform.Connection.Calls);
        Assert.False(platform.Connection.Dtr);
    }

    [Fact]
    public async Task Release_failure_closes_and_faults_lease()
    {
        var platform = new FakePlatform();
        await using var controller = new PttController(platform);
        await using var lease = await controller.AcquireAutomaticAsync("COM2", PttControlMethod.Rts, TestContext.Current.CancellationToken);
        platform.Connection.FailNextRelease = true;

        await Assert.ThrowsAsync<IOException>(() => lease.ReleaseAsync());

        Assert.True(lease.FaultToken.IsCancellationRequested);
        Assert.False(controller.Snapshot.IsOpen);
        Assert.False(controller.Snapshot.IsAsserted);
        Assert.False(controller.Snapshot.IsBusy);
        Assert.Contains("dispose", platform.Connection.Calls);
    }

    [Fact]
    public async Task Repeated_release_close_and_dispose_are_safe()
    {
        var platform = new FakePlatform();
        var controller = new PttController(platform);
        var lease = await controller.AcquireAutomaticAsync("COM2", PttControlMethod.Dtr, TestContext.Current.CancellationToken);
        await lease.ReleaseAsync();
        await lease.ReleaseAsync();
        await lease.AbortAsync();
        await lease.DisposeAsync();
        Assert.True(controller.Snapshot.IsOpen);
        Assert.False(controller.Snapshot.IsBusy);
        Assert.False(lease.FaultToken.IsCancellationRequested);
        await controller.CloseAsync(TestContext.Current.CancellationToken);
        await controller.CloseAsync(TestContext.Current.CancellationToken);
        await controller.DisposeAsync();
        await controller.DisposeAsync();
        await lease.ReleaseAsync();
        Assert.Equal(1, platform.Connection.Calls.Count(call => call == "dispose"));
    }

    [Fact]
    public async Task Manual_close_is_rejected_while_asserted()
    {
        var platform = new FakePlatform();
        await using var controller = new PttController(platform);
        await controller.BeginManualAsync("COM2", PttControlMethod.Rts, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CloseAsync(TestContext.Current.CancellationToken));

        Assert.True(controller.Snapshot.IsOpen);
        Assert.True(controller.Snapshot.IsAsserted);
        await controller.EndManualAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Manual_release_with_canceled_request_still_deasserts()
    {
        var platform = new FakePlatform();
        await using var controller = new PttController(platform);
        await controller.BeginManualAsync("COM2", PttControlMethod.Rts, TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        await controller.EndManualAsync(cancellation.Token);

        Assert.False(controller.Snapshot.IsAsserted);
        Assert.False(platform.Connection.Rts);
        Assert.True(controller.Snapshot.IsOpen);
    }

    [Fact]
    public async Task Automatic_ownership_rejects_close_and_ignores_manual_actions()
    {
        var platform = new FakePlatform();
        await using var controller = new PttController(platform);
        await using var lease = await controller.AcquireAutomaticAsync("COM2", PttControlMethod.Rts, TestContext.Current.CancellationToken);
        await controller.BeginManualAsync("COM2", PttControlMethod.Dtr, TestContext.Current.CancellationToken);
        await controller.EndManualAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CloseAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.AcquireAutomaticAsync("COM2", PttControlMethod.Rts, TestContext.Current.CancellationToken));
        Assert.True(controller.Snapshot.IsAsserted);
        Assert.True(platform.Connection.Rts);
        Assert.False(platform.Connection.Dtr);
        await lease.AbortAsync();
        Assert.False(platform.Connection.Rts);
        Assert.True(controller.Snapshot.IsOpen);
    }

    [Fact]
    public async Task Dispose_cancels_monitor_and_releases_active_lease()
    {
        var platform = new FakePlatform();
        var controller = new PttController(platform);
        var lease = await controller.AcquireAutomaticAsync("COM2", PttControlMethod.Dtr, TestContext.Current.CancellationToken);
        await platform.DelayStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await controller.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(platform.LastDelayToken.IsCancellationRequested);
        Assert.True(lease.FaultToken.IsCancellationRequested);
        Assert.False(controller.Snapshot.IsOpen);
        Assert.False(platform.Connection.Dtr);
        await lease.DisposeAsync();
    }

    [Fact]
    public async Task Dispose_is_bounded_when_serial_close_stalls_and_late_cleanup_does_not_publish()
    {
        var platform = new FakePlatform();
        var controller = new PttController(platform);
        await controller.OpenAsync("COM2", PttControlMethod.Rts, TestContext.Current.CancellationToken);
        platform.Connection.BlockClose = true;
        var disposal = controller.DisposeAsync().AsTask();
        await platform.Connection.CloseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await disposal.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var timedOutState = controller.Snapshot;
        Assert.False(timedOutState.IsOpen);
        Assert.False(timedOutState.IsAsserted);
        Assert.NotNull(timedOutState.Error);
        platform.Connection.ContinueClose.SetResult();
        await platform.Connection.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await controller.DisposeAsync();
        Assert.Same(timedOutState, controller.Snapshot);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => controller.OpenAsync("COM2", PttControlMethod.Rts, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Faulted_lease_abort_does_not_wait_for_stalled_cleanup()
    {
        var platform = new FakePlatform();
        var controller = new PttController(platform);
        var lease = await controller.AcquireAutomaticAsync("COM2", PttControlMethod.Rts, TestContext.Current.CancellationToken);
        platform.Connection.BlockClose = true;
        var disposal = controller.DisposeAsync().AsTask();
        await platform.Connection.CloseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        try
        {
            await lease.AbortAsync().WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            Assert.True(lease.FaultToken.IsCancellationRequested);
        }
        finally
        {
            platform.Connection.ContinueClose.TrySetResult();
            await disposal;
        }
    }

    private sealed class FakePlatform : IPttPlatform
    {
        private readonly Channel<TaskCompletionSource> delays = Channel.CreateUnbounded<TaskCompletionSource>();
        public string[] PortNames { get; set; } = ["COM2"];
        public FakeConnection Connection { get; } = new();
        public int CreateCount { get; private set; }
        public TaskCompletionSource DelayStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken LastDelayToken { get; private set; }
        public string[] GetPortNames() => PortNames;
        public IPttSerialConnection CreateConnection(string portName)
        {
            Assert.Equal("COM2", portName);
            CreateCount++;
            return Connection;
        }
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Assert.Equal(TimeSpan.FromSeconds(1), delay);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            LastDelayToken = cancellationToken;
            delays.Writer.TryWrite(completion);
            DelayStarted.TrySetResult();
            return completion.Task.WaitAsync(cancellationToken);
        }
        public async Task TickAsync() => (await delays.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).SetResult();
    }

    private sealed class FakeConnection : IPttSerialConnection
    {
        public ConcurrentQueue<string> Calls { get; } = new();
        public bool IsOpen { get; private set; }
        public bool Rts { get; private set; }
        public bool Dtr { get; private set; }
        public bool BlockOpen { get; set; }
        public bool BlockClose { get; set; }
        public bool FailNextAssertion { get; set; }
        public bool FailNextRelease { get; set; }
        public TaskCompletionSource OpenStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ContinueOpen { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CloseStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ContinueClose { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Open()
        {
            Calls.Enqueue("open");
            OpenStarted.TrySetResult();
            if (BlockOpen) ContinueOpen.Task.GetAwaiter().GetResult();
            IsOpen = true;
        }
        public void Close()
        {
            Calls.Enqueue("close");
            CloseStarted.TrySetResult();
            if (BlockClose) ContinueClose.Task.GetAwaiter().GetResult();
            IsOpen = false;
        }
        public void SetRts(bool value)
        {
            Calls.Enqueue($"rts:{value}");
            if (value && FailNextAssertion) { FailNextAssertion = false; throw new IOException("assert failed"); }
            if (!value && FailNextRelease) { FailNextRelease = false; throw new IOException("release failed"); }
            Rts = value;
        }
        public void SetDtr(bool value) { Calls.Enqueue($"dtr:{value}"); Dtr = value; }
        public void Dispose() { Calls.Enqueue("dispose"); IsOpen = false; Disposed.TrySetResult(); }
    }
}
