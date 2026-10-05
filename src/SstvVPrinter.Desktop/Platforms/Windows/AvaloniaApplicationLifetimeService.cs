using Avalonia.Controls.ApplicationLifetimes;
using SstvVPrinter.App.Platform;
using SstvVPrinter.Desktop.Views;

namespace SstvVPrinter.Desktop.Platforms.Windows;

public sealed class AvaloniaApplicationLifetimeService(
    IClassicDesktopStyleApplicationLifetime lifetime,
    MainWindow window,
    Func<CancellationToken, Task<bool>> confirmPendingExit,
    Func<CancellationToken, Task> cleanup) : IApplicationLifetimeService
{
    public void ShowMainWindow()
    {
        window.Show();
        window.WindowState = Avalonia.Controls.WindowState.Normal;
    }

    public void HideMainWindow() => window.Hide();

    public Task<bool> ConfirmPendingExitAsync(CancellationToken cancellationToken) =>
        confirmPendingExit(cancellationToken);

    public async Task ExitAsync(CancellationToken cancellationToken)
    {
        window.AllowExplicitClose = true;
        await cleanup(cancellationToken);
        lifetime.Shutdown();
    }
}
