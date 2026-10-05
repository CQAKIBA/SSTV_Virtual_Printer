namespace SstvVPrinter.App.Platform;

public interface IApplicationLifetimeService
{
    void ShowMainWindow();
    void HideMainWindow();
    Task<bool> ConfirmPendingExitAsync(CancellationToken cancellationToken);
    void Exit();
}
