using System.Diagnostics;

namespace SstvVPrinter.Desktop.Platforms.Windows;

public sealed record ProcessInvocation(string FileName, IReadOnlyList<string> Arguments, bool ShowWindow = false);
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessInvocation invocation, CancellationToken cancellationToken);
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessInvocation invocation, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(invocation.FileName)
        {
            CreateNoWindow = !invocation.ShowWindow,
            UseShellExecute = false,
            RedirectStandardOutput = !invocation.ShowWindow,
            RedirectStandardError = !invocation.ShowWindow,
        };
        foreach (var argument in invocation.Arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Unable to start {invocation.FileName}.");
        Task<string> output = invocation.ShowWindow ? Task.FromResult(string.Empty) : process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = invocation.ShowWindow ? Task.FromResult(string.Empty) : process.StandardError.ReadToEndAsync(cancellationToken);
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
