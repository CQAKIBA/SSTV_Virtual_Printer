using SstvVPrinter.Desktop.Hosting;

namespace SstvVPrinter.Desktop.Tests;

public sealed class DesktopHostTests
{
    [Fact]
    public async Task Host_starts_and_stops_each_owned_service_exactly_once()
    {
        var first = new FakeService();
        var second = new FakeService();
        await using var host = new DesktopHost([first, second]);

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, first.StartCount);
        Assert.Equal(1, first.StopCount);
        Assert.Equal(1, second.StartCount);
        Assert.Equal(1, second.StopCount);
    }

    private sealed class FakeService : IDesktopHostedService
    {
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken) { StartCount++; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken) { StopCount++; return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
