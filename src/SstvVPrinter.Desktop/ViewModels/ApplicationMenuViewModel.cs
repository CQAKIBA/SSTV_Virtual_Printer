using CommunityToolkit.Mvvm.ComponentModel;
using SstvVPrinter.App.Platform;

namespace SstvVPrinter.Desktop.ViewModels;

public sealed partial class ApplicationMenuViewModel(
    IApplicationLifetimeService lifetime,
    IStartupRegistrationService startup,
    IUserAttentionService attention,
    IPrinterRegistrationService printer,
    Func<bool> hasPendingJobs) : ObservableObject
{
    [ObservableProperty]
    private bool _startAtSignIn = true;

    [ObservableProperty]
    private bool? _printerIsRegistered;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        StartAtSignIn = await startup.IsEnabledAsync(cancellationToken);
        PrinterIsRegistered = await printer.VerifyAsync(cancellationToken);
    }

    public async Task SetStartAtSignInAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await startup.SetEnabledAsync(enabled, cancellationToken);
        StartAtSignIn = enabled;
    }

    public Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lifetime.ShowMainWindow();
        if (!attention.TryBringToForeground())
        {
            attention.FlashTaskbar();
            attention.ShowNotification("SSTV Virtual Printer", "印刷ジョブを受信しました。");
        }
        return Task.CompletedTask;
    }

    public async Task ExitAsync(CancellationToken cancellationToken = default)
    {
        if (hasPendingJobs() && !await lifetime.ConfirmPendingExitAsync(cancellationToken)) return;
        await lifetime.ExitAsync(cancellationToken);
    }

    public async Task RegisterPrinterAsync(CancellationToken cancellationToken = default)
    {
        await printer.RegisterAsync(cancellationToken);
        PrinterIsRegistered = true;
    }

    public async Task RepairPrinterAsync(CancellationToken cancellationToken = default)
    {
        await printer.RepairAsync(cancellationToken);
        PrinterIsRegistered = await printer.VerifyAsync(cancellationToken);
    }
}
