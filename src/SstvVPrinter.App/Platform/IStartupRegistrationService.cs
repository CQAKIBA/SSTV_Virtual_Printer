namespace SstvVPrinter.App.Platform;

public interface IStartupRegistrationService
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken);
    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken);
}
