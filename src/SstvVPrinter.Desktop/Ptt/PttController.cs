namespace SstvVPrinter.Desktop.Ptt;

/// <summary>Owns one serial connection. Serial operations and discovery run on worker threads.</summary>
public sealed class PttController(IPttPlatform platform) : IAsyncDisposable
{
    private readonly SemaphoreSlim mutex = new(1, 1);
    private readonly CancellationTokenSource shutdown = new();
    private readonly object stateLock = new();
    private readonly object disposalLock = new();
    private IPttSerialConnection? connection;
    private AutomaticLease? automaticLease;
    private PttSnapshot snapshot = new(null, PttControlMethod.Rts, false, false, false, null);
    private IReadOnlyList<string> ports = Array.AsReadOnly(new[] { "N/A" });
    private long manualGeneration;
    private long manualOwnerGeneration;
    private long connectionGeneration;
    private bool manualAsserted;
    private int disposalRequested;
    private bool publicationStopped;
    private Task? disposalTask;
    private CancellationTokenSource? monitorCancellation;
    private Task? monitorTask;

    public event EventHandler<PttSnapshot>? StateChanged;
    public PttSnapshot Snapshot => Volatile.Read(ref snapshot);
    public IReadOnlyList<string> Ports => Volatile.Read(ref ports);

    public Task<IReadOnlyList<string>> RefreshPortsAsync(CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            await mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref disposalRequested) != 0, this);
                var discovered = platform.GetPortNames().Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(PortNumber).ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .Prepend("N/A").ToArray();
                if (!ports.SequenceEqual(discovered, StringComparer.OrdinalIgnoreCase))
                    Volatile.Write(ref ports, Array.AsReadOnly(discovered));
                return ports;
            }
            finally { mutex.Release(); }
        }, cancellationToken);

    public Task OpenAsync(string portName, PttControlMethod method, CancellationToken cancellationToken = default) =>
        RunAsync(generation => OpenCore(portName, method, generation, cancellationToken), cancellationToken);

    public Task CloseAsync(CancellationToken cancellationToken = default) => RunAsync(_ =>
    {
        if (manualAsserted || automaticLease is not null)
            throw new InvalidOperationException("Release PTT before closing the port.");
        CloseCore();
    }, cancellationToken);

    public Task BeginManualAsync(string portName, PttControlMethod method, CancellationToken cancellationToken = default)
    {
        var generation = Volatile.Read(ref manualGeneration);
        return RunAsync(connectionVersion =>
        {
            if (automaticLease is not null || generation != Volatile.Read(ref manualGeneration)) return;
            OpenCore(portName, method, connectionVersion, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != Volatile.Read(ref manualGeneration)) return;
            AssertCore(connectionVersion, cancellationToken);
            manualOwnerGeneration = generation;
            manualAsserted = true;
        }, cancellationToken);
    }

    public Task EndManualAsync(CancellationToken cancellationToken = default)
    {
        var generation = Interlocked.Increment(ref manualGeneration);
        // Releasing a keyed transmitter must survive cancellation of the initiating UI operation.
        return RunAsync(_ =>
        {
            if (automaticLease is not null || !manualAsserted || manualOwnerGeneration >= generation) return;
            ReleaseCore();
            manualAsserted = false;
        }, CancellationToken.None);
    }

    public async Task<IPttAutomaticLease> AcquireAutomaticAsync(
        string portName, PttControlMethod method, CancellationToken cancellationToken = default)
    {
        AutomaticLease? acquired = null;
        await RunAsync(generation =>
        {
            if (manualAsserted || automaticLease is not null)
                throw new InvalidOperationException("PTT is already in use.");
            Interlocked.Increment(ref manualGeneration);
            OpenCore(portName, method, generation, cancellationToken);
            ThrowIfInvalid(generation, cancellationToken);
            acquired = automaticLease = new AutomaticLease(this);
            AssertCore(generation, cancellationToken);
        }, cancellationToken).ConfigureAwait(false);
        return acquired!;
    }

    public ValueTask DisposeAsync()
    {
        lock (disposalLock)
        {
            if (disposalTask is not null) return new(disposalTask);
            Interlocked.Exchange(ref disposalRequested, 1);
            Interlocked.Increment(ref manualGeneration);
            Interlocked.Increment(ref connectionGeneration);
            shutdown.Cancel();
            Volatile.Read(ref automaticLease)?.Fault();
            disposalTask = DisposeCoreAsync();
            return new(disposalTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        var cleanup = Task.Run(async () =>
        {
            await mutex.WaitAsync().ConfigureAwait(false);
            try { CloseCore(boundCleanup: false); }
            finally { mutex.Release(); shutdown.Dispose(); }
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await cleanup.WaitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            // The worker keeps sole ownership of the connection until the driver returns.
            // Final publication prevents late cleanup from overwriting the timeout state.
            lock (stateLock)
            {
                publicationStopped = true;
                var value = snapshot with
                {
                    IsOpen = false, IsAsserted = false, IsBusy = false,
                    Error = "PTT shutdown timed out; serial cleanup is continuing in the background."
                };
                Volatile.Write(ref snapshot, value);
                StateChanged?.Invoke(this, value);
            }
            // Observe failures even if the caller has already stopped waiting.
            _ = cleanup.ContinueWith(task => _ = task.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    private Task RunAsync(Action<long> action, CancellationToken cancellationToken)
    {
        var generation = Volatile.Read(ref connectionGeneration);
        return Task.Run(async () =>
        {
            await mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfInvalid(generation, cancellationToken);
                action(generation);
            }
            finally { mutex.Release(); }
        }, cancellationToken);
    }

    private void ThrowIfInvalid(long generation, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposalRequested) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (generation != Volatile.Read(ref connectionGeneration))
            throw new OperationCanceledException("The PTT connection request is no longer current.");
    }

    private void OpenCore(string portName, PttControlMethod method, long generation, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        if (portName.Equals("N/A", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select a COM port.", nameof(portName));
        if (connection is not null)
        {
            if (snapshot.PortName == portName && snapshot.Method == method) return;
            throw new InvalidOperationException("Close the current port before changing PTT settings.");
        }
        Publish(snapshot with { PortName = portName, Method = method, Error = null });
        try
        {
            connection = platform.CreateConnection(portName);
            connection.Open();
            ThrowIfInvalid(generation, cancellationToken);
            connection.SetRts(false);
            ThrowIfInvalid(generation, cancellationToken);
            connection.SetDtr(false);
            ThrowIfInvalid(generation, cancellationToken);
            Publish(snapshot with { IsOpen = true });
            StartMonitor(connection);
        }
        catch (Exception ex) { FaultCore(ex); throw; }
    }

    private void AssertCore(long generation, CancellationToken cancellationToken)
    {
        try
        {
            ThrowIfInvalid(generation, cancellationToken);
            connection!.SetRts(snapshot.Method == PttControlMethod.Rts);
            ThrowIfInvalid(generation, cancellationToken);
            connection.SetDtr(snapshot.Method == PttControlMethod.Dtr);
            ThrowIfInvalid(generation, cancellationToken);
            Publish(snapshot with { IsAsserted = true, IsBusy = automaticLease is not null, Error = null });
        }
        catch (Exception ex) { FaultCore(ex); throw; }
    }

    private void ReleaseCore()
    {
        try
        {
            connection?.SetRts(false);
            connection?.SetDtr(false);
            Publish(snapshot with { IsAsserted = false, IsBusy = false });
        }
        catch (Exception ex) { FaultCore(ex); throw; }
    }

    private void CloseCore(bool boundCleanup = true)
    {
        var error = CleanupConnection(boundCleanup);
        manualAsserted = false;
        automaticLease?.Fault();
        automaticLease = null;
        Publish(snapshot with { IsOpen = false, IsAsserted = false, IsBusy = false, Error = error?.Message ?? snapshot.Error });
        if (error is not null) throw error;
    }

    private Exception? CleanupConnection(bool boundCleanup = true)
    {
        StopMonitor();
        var closing = connection;
        connection = null;
        if (closing is null) return null;
        if (!boundCleanup) return CleanupSerial(closing);

        // The detached worker only sees this connection. It never accesses controller state,
        // so a driver that resumes after the bound cannot affect a replacement connection.
        var cleanup = Task.Run(() => CleanupSerial(closing));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { return cleanup.WaitAsync(timeout.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return new TimeoutException("Serial cleanup timed out and is continuing in the background.");
        }
    }

    private static Exception? CleanupSerial(IPttSerialConnection closing)
    {
        Exception? error = null;
        void Try(Action action) { try { action(); } catch (Exception ex) { error ??= ex; } }
        Try(() => closing.SetRts(false));
        Try(() => closing.SetDtr(false));
        Try(closing.Close);
        Try(closing.Dispose);
        return error;
    }

    private void FaultCore(Exception error)
    {
        Interlocked.Increment(ref connectionGeneration);
        Interlocked.Increment(ref manualGeneration);
        automaticLease?.Fault();
        automaticLease = null;
        manualAsserted = false;
        // Publish the fault before attempting potentially unresponsive driver operations.
        Publish(snapshot with { IsOpen = false, IsAsserted = false, IsBusy = false, Error = error.Message });
        var cleanupError = CleanupConnection();
        if (cleanupError is TimeoutException)
            Publish(snapshot with { Error = $"{error.Message} {cleanupError.Message}" });
    }

    private Task ReleaseAutomaticAsync(AutomaticLease lease) => Task.Run(async () =>
    {
        // Fault cleanup already owns deassertion. Do not wait behind a stalled device driver.
        try { await mutex.WaitAsync(lease.FaultToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (lease.FaultToken.IsCancellationRequested) { return; }
        try
        {
            if (automaticLease != lease) return;
            ReleaseCore();
            automaticLease = null;
        }
        finally { mutex.Release(); }
    });

    private void Publish(PttSnapshot value)
    {
        lock (stateLock)
        {
            if (publicationStopped || snapshot == value) return;
            Volatile.Write(ref snapshot, value);
            StateChanged?.Invoke(this, value);
        }
    }

    private void StartMonitor(IPttSerialConnection monitored)
    {
        monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
        var token = monitorCancellation.Token;
        monitorTask = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    // WaitAsync also bounds shutdown when a platform ignores cancellation.
                    await platform.DelayAsync(TimeSpan.FromSeconds(1), token).WaitAsync(token).ConfigureAwait(false);
                    await mutex.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        if (connection != monitored || token.IsCancellationRequested) return;
                        try
                        {
                            if (!platform.GetPortNames().Contains(snapshot.PortName, StringComparer.OrdinalIgnoreCase))
                                FaultCore(new IOException($"PTT port {snapshot.PortName} was removed."));
                        }
                        catch (Exception ex) { FaultCore(ex); }
                    }
                    finally { mutex.Release(); }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                await mutex.WaitAsync().ConfigureAwait(false);
                try { if (connection == monitored) FaultCore(ex); }
                finally { mutex.Release(); }
            }
        });
    }

    private void StopMonitor()
    {
        var stopping = monitorCancellation;
        var completion = monitorTask;
        monitorCancellation = null;
        monitorTask = null;
        if (stopping is null) return;
        stopping.Cancel();
        _ = completion!.ContinueWith(_ => stopping.Dispose(), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private static int PortNumber(string name) =>
        name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) && int.TryParse(name.AsSpan(3), out var number)
            ? number : int.MaxValue;

    private sealed class AutomaticLease(PttController owner) : IPttAutomaticLease
    {
        private readonly CancellationTokenSource fault = new();
        public CancellationToken FaultToken => fault.Token;
        public void Fault()
        {
            try { fault.Cancel(); }
            catch (AggregateException) { /* A cancellation callback cannot prevent serial cleanup. */ }
        }
        public Task ReleaseAsync() => owner.ReleaseAutomaticAsync(this);
        public Task AbortAsync() => owner.ReleaseAutomaticAsync(this);
        public ValueTask DisposeAsync() => new(AbortAsync());
    }
}
