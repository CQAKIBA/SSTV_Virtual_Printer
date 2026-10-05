using SstvVPrinter.Desktop.Platforms.Windows;

namespace SstvVPrinter.Desktop.Tests;

public sealed class WindowsLifetimeTests
{
    [Fact]
    public async Task Second_instance_signals_the_primary_instance()
    {
        var name = $"SstvVPrinter.Tests.{Guid.NewGuid():N}";
        await using var primary = new WindowsSingleInstanceCoordinator(name);
        await using var secondary = new WindowsSingleInstanceCoordinator(name);
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.ActivationRequested += () => activated.TrySetResult();

        Assert.True(await primary.TryAcquireAsync(TestContext.Current.CancellationToken));
        Assert.False(await secondary.TryAcquireAsync(TestContext.Current.CancellationToken));
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Printer_commands_use_the_fixed_loopback_uri_and_display_name_as_structured_arguments()
    {
        var runner = new RecordingProcessRunner();
        var service = new WindowsPrinterRegistrationService(runner);

        await service.RegisterAsync(TestContext.Current.CancellationToken);
        await service.RepairAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, runner.Invocations.Count);
        Assert.All(runner.Invocations, invocation => Assert.Equal("powershell.exe", invocation.FileName));
        Assert.Contains(runner.Invocations, invocation => invocation.Arguments.Contains("ipp://localhost:8631/ipp/print"));
        Assert.Contains(runner.Invocations, invocation => invocation.Arguments.Contains("SSTV Virtual Printer Probe"));
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public List<ProcessInvocation> Invocations { get; } = [];

        public Task<ProcessResult> RunAsync(ProcessInvocation invocation, CancellationToken cancellationToken)
        {
            Invocations.Add(invocation);
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }
}
