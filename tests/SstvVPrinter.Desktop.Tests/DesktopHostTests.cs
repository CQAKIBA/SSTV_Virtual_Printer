using SstvVPrinter.Desktop.Hosting;
using SstvVPrinter.IppProbe.Capture;
using SstvVPrinter.IppProbe.Hosting;
using SstvVPrinter.IppProbe.Ipp;

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

    [Fact]
    public async Task Real_ipp_web_application_stops_and_disposes_boundedly()
    {
        var application = ProbeApplication.Build(new DiscardingPrintJobSink(), port: 0);
        var service = new WebApplicationHostedService(application);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);
        await service.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    }

    private sealed class FakeService : IDesktopHostedService
    {
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken) { StartCount++; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken) { StopCount++; return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class DiscardingPrintJobSink : IPrintJobSink
    {
        public ValueTask AcceptAsync(
            ProbeJob job,
            Stream document,
            string documentFormat,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
