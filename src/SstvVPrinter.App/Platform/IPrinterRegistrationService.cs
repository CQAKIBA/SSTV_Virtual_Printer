namespace SstvVPrinter.App.Platform;

public interface IPrinterRegistrationService
{
    Task<bool> VerifyAsync(CancellationToken cancellationToken);
    Task RegisterAsync(CancellationToken cancellationToken);
    Task RepairAsync(CancellationToken cancellationToken);
}
