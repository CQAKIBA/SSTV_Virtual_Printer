using SstvVPrinter.Desktop.Platforms.Windows;
using System.ComponentModel;

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
    public async Task Primary_instance_disposes_without_ever_receiving_activation()
    {
        var coordinator = new WindowsSingleInstanceCoordinator($"SstvVPrinter.Tests.{Guid.NewGuid():N}");
        Assert.True(await coordinator.TryAcquireAsync(TestContext.Current.CancellationToken));

        await coordinator.DisposeAsync().AsTask()
            .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Printer_commands_use_the_fixed_loopback_uri_and_display_name_as_structured_arguments()
    {
        var runner = new RecordingProcessRunner();
        var service = new WindowsPrinterRegistrationService(runner);

        await service.VerifyAsync(TestContext.Current.CancellationToken);
        await service.RegisterAsync(TestContext.Current.CancellationToken);
        await service.RepairAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, runner.Invocations.Count);
        Assert.All(runner.Invocations, invocation => Assert.Equal("powershell.exe", invocation.FileName));
        Assert.False(runner.Invocations[0].ShowWindow);
        Assert.False(runner.Invocations[0].RunElevated);
        Assert.True(runner.Invocations[1].ShowWindow);
        Assert.True(runner.Invocations[1].RunElevated);
        Assert.True(runner.Invocations[2].ShowWindow);
        Assert.True(runner.Invocations[2].RunElevated);
        Assert.Contains("-File", runner.Invocations[0].Arguments);
        Assert.DoesNotContain("-Command", runner.Invocations[0].Arguments);
        Assert.Contains(runner.Invocations, invocation => invocation.Arguments.Contains("ipp://localhost:8631/ipp/print"));
        Assert.Contains(runner.Invocations, invocation => invocation.Arguments.Contains("SSTV Virtual Printer Probe"));
        Assert.Equal(3, runner.Scripts.Count);
        Assert.All(runner.Scripts.Skip(1), script =>
        {
            Assert.Contains("Add-PrinterDriver -Name 'Microsoft IPP Class Driver'", script);
            Assert.Contains("Add-Printer -IppURL $uri", script);
            Assert.DoesNotContain("Add-Printer -Name $name -IppURL", script);
        });
    }

    [Fact]
    public async Task Printer_registration_times_out_when_windows_never_completes_the_command()
    {
        var runner = new HangingProcessRunner();
        var service = new WindowsPrinterRegistrationService(runner, TimeSpan.FromMilliseconds(20));

        var error = await Assert.ThrowsAsync<TimeoutException>(() =>
            service.RegisterAsync(TestContext.Current.CancellationToken));

        Assert.Contains("プリンタ登録", error.Message);
    }

    [Fact]
    public async Task Printer_registration_explains_that_administrator_permission_is_required()
    {
        var service = new WindowsPrinterRegistrationService(new ElevationDeniedProcessRunner());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegisterAsync(TestContext.Current.CancellationToken));

        Assert.Contains("管理者権限", error.Message);
    }

    [Fact]
    public async Task Printer_registration_surfaces_the_elevated_powershell_error_and_removes_its_result_file()
    {
        var diagnosticDirectory = Path.Combine(Path.GetTempPath(), $"SstvVPrinter.Tests.{Guid.NewGuid():N}");
        var runner = new FailingAdministrativeProcessRunner();
        var service = new WindowsPrinterRegistrationService(runner, diagnosticDirectory: diagnosticDirectory);

        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RegisterAsync(TestContext.Current.CancellationToken));

            Assert.Contains("IPP診断エラー 1234", error.Message);
            Assert.NotNull(runner.ResultPath);
            Assert.False(File.Exists(runner.ResultPath));
            Assert.NotNull(runner.ScriptPath);
            Assert.False(File.Exists(runner.ScriptPath));
            Assert.NotNull(runner.LogPath);
            Assert.True(File.Exists(runner.LogPath));
            Assert.Contains("IPP診断エラー 1234", await File.ReadAllTextAsync(runner.LogPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(diagnosticDirectory)) Directory.Delete(diagnosticDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Exit_sequence_cleans_up_before_shutdown_and_process_termination()
    {
        var events = new List<string>();
        var cleanupCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new ProcessExitCoordinator(
            async cancellationToken =>
            {
                events.Add("cleanup-started");
                await cleanupCompletion.Task.WaitAsync(cancellationToken);
                events.Add("cleanup-finished");
            },
            () => events.Add("shutdown"),
            exitCode => events.Add($"terminate-{exitCode}"));

        var exit = coordinator.ExitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["cleanup-started"], events);

        cleanupCompletion.SetResult();
        await exit;

        Assert.Equal(["cleanup-started", "cleanup-finished", "shutdown", "terminate-0"], events);
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public List<ProcessInvocation> Invocations { get; } = [];
        public List<string> Scripts { get; } = [];

        public async Task<ProcessResult> RunAsync(ProcessInvocation invocation, CancellationToken cancellationToken)
        {
            Invocations.Add(invocation);
            string? scriptPath = invocation.Arguments.FirstOrDefault(argument =>
                argument.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase));
            if (scriptPath is not null)
                Scripts.Add(await File.ReadAllTextAsync(scriptPath, cancellationToken));
            return new ProcessResult(0, string.Empty, string.Empty);
        }
    }

    private sealed class HangingProcessRunner : IProcessRunner
    {
        public async Task<ProcessResult> RunAsync(ProcessInvocation invocation, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
    }

    private sealed class ElevationDeniedProcessRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessInvocation invocation, CancellationToken cancellationToken) =>
            throw new Win32Exception(5, "Access is denied.");
    }

    private sealed class FailingAdministrativeProcessRunner : IProcessRunner
    {
        public string? ResultPath { get; private set; }
        public string? ScriptPath { get; private set; }
        public string? LogPath { get; private set; }

        public async Task<ProcessResult> RunAsync(ProcessInvocation invocation, CancellationToken cancellationToken)
        {
            ScriptPath = invocation.Arguments.FirstOrDefault(argument =>
                argument.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase));
            ResultPath = invocation.Arguments.FirstOrDefault(argument =>
                argument.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
            LogPath = invocation.Arguments.FirstOrDefault(argument =>
                argument.EndsWith(".log", StringComparison.OrdinalIgnoreCase));
            if (ScriptPath is null || !File.Exists(ScriptPath))
                throw new InvalidOperationException("Administrative script file was not supplied.");
            if (ResultPath is not null)
                await File.WriteAllTextAsync(ResultPath, "IPP診断エラー 1234", cancellationToken);
            if (LogPath is not null)
                await File.WriteAllTextAsync(LogPath, "IPP診断エラー 1234", cancellationToken);
            return new ProcessResult(1, string.Empty, string.Empty);
        }
    }
}
