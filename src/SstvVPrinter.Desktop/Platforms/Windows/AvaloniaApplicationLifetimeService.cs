using Avalonia.Controls.ApplicationLifetimes;
using SstvVPrinter.App.Platform;
using SstvVPrinter.Desktop.Views;

namespace SstvVPrinter.Desktop.Platforms.Windows;

public sealed class AvaloniaApplicationLifetimeService(
    IClassicDesktopStyleApplicationLifetime lifetime,
    MainWindow window,
    Func<CancellationToken, Task<bool>> confirmPendingExit) : IApplicationLifetimeService
{
    public void ShowMainWindow()
    {
        window.Show();
        window.WindowState = Avalonia.Controls.WindowState.Normal;
    }

    public void HideMainWindow() => window.Hide();

    public Task<bool> ConfirmPendingExitAsync(CancellationToken cancellationToken) =>
        confirmPendingExit(cancellationToken);

    public void Exit()
    {
        window.AllowExplicitClose = true;
        lifetime.Shutdown();
    }
}
