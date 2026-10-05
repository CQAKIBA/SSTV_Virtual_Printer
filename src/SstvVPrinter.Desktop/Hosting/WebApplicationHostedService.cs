using Microsoft.AspNetCore.Builder;

namespace SstvVPrinter.Desktop.Hosting;

public sealed class WebApplicationHostedService(WebApplication application) : IDesktopHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => application.StartAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => application.StopAsync(cancellationToken);
    public async ValueTask DisposeAsync() => await application.DisposeAsync();
}
