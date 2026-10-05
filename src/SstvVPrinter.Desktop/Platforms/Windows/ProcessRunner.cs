using System.Diagnostics;

namespace SstvVPrinter.Desktop.Platforms.Windows;

public sealed record ProcessInvocation(
    string FileName,
    IReadOnlyList<string> Arguments,
    bool ShowWindow = false,
    bool RunElevated = false);
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessInvocation invocation, CancellationToken cancellationToken);
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessInvocation invocation, CancellationToken cancellationToken)
    {
        var captureOutput = !invocation.ShowWindow && !invocation.RunElevated;
        var startInfo = new ProcessStartInfo(invocation.FileName)
        {
            CreateNoWindow = captureOutput,
            UseShellExecute = invocation.RunElevated,
            Verb = invocation.RunElevated ? "runas" : string.Empty,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput,
        };
        foreach (var argument in invocation.Arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Unable to start {invocation.FileName}.");
        Task<string> output = captureOutput ? process.StandardOutput.ReadToEndAsync(cancellationToken) : Task.FromResult(string.Empty);
        Task<string> error = captureOutput ? process.StandardError.ReadToEndAsync(cancellationToken) : Task.FromResult(string.Empty);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            throw;
        }
        return new(process.ExitCode, await output, await error);
    }
}
