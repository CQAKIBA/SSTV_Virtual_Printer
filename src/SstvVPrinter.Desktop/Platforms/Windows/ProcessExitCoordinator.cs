namespace SstvVPrinter.Desktop.Platforms.Windows;

public sealed class ProcessExitCoordinator(
    Func<CancellationToken, Task> cleanup,
    Action shutdown,
    Action<int> terminateProcess)
{
    public async Task ExitAsync(CancellationToken cancellationToken)
    {
        await cleanup(cancellationToken);
        shutdown();
        terminateProcess(0);
    }
}
