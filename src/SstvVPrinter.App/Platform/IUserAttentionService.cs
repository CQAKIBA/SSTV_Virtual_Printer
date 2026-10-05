namespace SstvVPrinter.App.Platform;

public interface IUserAttentionService
{
    bool TryBringToForeground();
    void FlashTaskbar();
    void ShowNotification(string title, string message);
}
