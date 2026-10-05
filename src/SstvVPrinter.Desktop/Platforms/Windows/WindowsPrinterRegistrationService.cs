using SstvVPrinter.App.Platform;
using SstvVPrinter.IppProbe.Ipp;

namespace SstvVPrinter.Desktop.Platforms.Windows;

public sealed class WindowsPrinterRegistrationService(IProcessRunner runner) : IPrinterRegistrationService
{
    private const string VerifyScript = "param($name) if (Get-Printer -Name $name -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }";
    private const string AddScript = "param($name,$uri) Add-Printer -Name $name -IppURL $uri -ErrorAction Stop";
    private const string RemoveScript = "param($name) Remove-Printer -Name $name -ErrorAction SilentlyContinue";

    public async Task<bool> VerifyAsync(CancellationToken cancellationToken) =>
        (await RunAsync(VerifyScript, [ProbePrinter.DisplayName], cancellationToken)).ExitCode == 0;

    public async Task RegisterAsync(CancellationToken cancellationToken)
    {
        EnsureSuccess(await RunAsync(AddScript, [ProbePrinter.DisplayName, ProbePrinter.DefaultPrinterUri], cancellationToken));
    }

    public async Task RepairAsync(CancellationToken cancellationToken)
    {
        await RunAsync(RemoveScript, [ProbePrinter.DisplayName], cancellationToken);
        await RegisterAsync(cancellationToken);
    }

    private Task<ProcessResult> RunAsync(string script, IReadOnlyList<string> values, CancellationToken cancellationToken)
    {
        var arguments = new List<string> { "-NoProfile", "-NonInteractive", "-Command", script };
        arguments.AddRange(values);
        return runner.RunAsync(new ProcessInvocation("powershell.exe", arguments), cancellationToken);
    }

    private static void EnsureSuccess(ProcessResult result)
    {
        if (result.ExitCode != 0) throw new InvalidOperationException(result.StandardError);
    }
}
