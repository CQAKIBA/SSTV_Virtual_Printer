namespace SstvVPrinter.Desktop.Hosting;

public interface IDesktopHostedService : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

public sealed class DesktopHost(IEnumerable<IDesktopHostedService> services) : IAsyncDisposable
{
    private readonly IReadOnlyList<IDesktopHostedService> _services = services.ToArray();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _started;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_started) return;
            var started = new List<IDesktopHostedService>();
            try
            {
                foreach (var service in _services)
                {
                    await service.StartAsync(cancellationToken);
                    started.Add(service);
                }
                _started = true;
            }
            catch
            {
                for (var index = started.Count - 1; index >= 0; index--)
                    await started[index].StopAsync(CancellationToken.None);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_started) return;
            for (var index = _services.Count - 1; index >= 0; index--)
                await _services[index].StopAsync(cancellationToken);
            _started = false;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        for (var index = _services.Count - 1; index >= 0; index--)
            await _services[index].DisposeAsync();
        _gate.Dispose();
    }
}
