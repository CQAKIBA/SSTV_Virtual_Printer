using System.IO.Pipes;
using System.Text;

namespace SstvVPrinter.Desktop.Platforms.Windows;

public sealed class WindowsSingleInstanceCoordinator : IAsyncDisposable
{
    private readonly Mutex _mutex;
    private readonly bool _createdNew;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _listener;
    private bool _primary;

    public WindowsSingleInstanceCoordinator(string applicationId = "SstvVPrinter.Desktop")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        var safeName = applicationId.Replace('\\', '.').Replace('/', '.');
        _mutex = new Mutex(false, $"Local\\{safeName}.Mutex", out _createdNew);
        _pipeName = $"{safeName}.Activate";
    }

    public event Action? ActivationRequested;

    public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        if (_primary) return true;
        _primary = _createdNew;

        if (_primary)
        {
            _listener = ListenAsync(_shutdown.Token);
            return true;
        }

        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
        await client.ConnectAsync(2000, cancellationToken);
        await client.WriteAsync(Encoding.UTF8.GetBytes("activate"), cancellationToken);
        await client.FlushAsync(cancellationToken);
        return false;
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken);
                var buffer = new byte[16];
                if (await server.ReadAsync(buffer, cancellationToken) > 0) ActivationRequested?.Invoke();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        if (_listener is not null)
        {
            try { await _listener; }
            catch (OperationCanceledException) { }
        }
        _mutex.Dispose();
        _shutdown.Dispose();
    }
}
