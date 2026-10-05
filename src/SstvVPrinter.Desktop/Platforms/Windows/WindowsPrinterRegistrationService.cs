using SstvVPrinter.App.Platform;
using SstvVPrinter.IppProbe.Ipp;

namespace SstvVPrinter.Desktop.Platforms.Windows;

public sealed class WindowsPrinterRegistrationService : IPrinterRegistrationService
{
    private readonly IProcessRunner _runner;
    private readonly TimeSpan _operationTimeout;
    private const string VerifyScript = "param($name) if (Get-Printer -Name $name -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }";
    private const string AddScript = "param($name,$uri) Write-Host 'SSTV仮想プリンタを登録しています...'; Write-Host ('接続先: ' + $uri); $code=0; try { Add-Printer -Name $name -IppURL $uri -ErrorAction Stop; Write-Host '登録が完了しました。' -ForegroundColor Green } catch { Write-Host ('登録に失敗しました: ' + $_.Exception.Message) -ForegroundColor Red; $code=1 }; Start-Sleep -Seconds 2; exit $code";
    private const string RemoveScript = "param($name) Write-Host '既存のプリンタ登録を確認しています...'; Remove-Printer -Name $name -ErrorAction SilentlyContinue; Write-Host '再登録を開始します。'";

    public WindowsPrinterRegistrationService(IProcessRunner runner, TimeSpan? operationTimeout = null)
    {
        _runner = runner;
        _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(60);
    }

    public async Task<bool> VerifyAsync(CancellationToken cancellationToken) =>
        (await RunAsync(VerifyScript, [ProbePrinter.DisplayName], false, cancellationToken)).ExitCode == 0;

    public async Task RegisterAsync(CancellationToken cancellationToken)
    {
        EnsureSuccess(await RunAsync(AddScript, [ProbePrinter.DisplayName, ProbePrinter.DefaultPrinterUri], true, cancellationToken));
    }

    public async Task RepairAsync(CancellationToken cancellationToken)
    {
        await RunAsync(RemoveScript, [ProbePrinter.DisplayName], true, cancellationToken);
        await RegisterAsync(cancellationToken);
    }

    private async Task<ProcessResult> RunAsync(string script, IReadOnlyList<string> values, bool showWindow, CancellationToken cancellationToken)
    {
        var arguments = new List<string> { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script };
        arguments.AddRange(values);
        using var timeout = new CancellationTokenSource(_operationTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await _runner.RunAsync(new ProcessInvocation("powershell.exe", arguments, showWindow), linked.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"プリンタ登録処理が{_operationTimeout.TotalSeconds:0}秒以内に完了しませんでした。");
        }
    }

    private static void EnsureSuccess(ProcessResult result)
    {
        if (result.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError)
                ? $"PowerShellのプリンタ操作が終了コード {result.ExitCode} で失敗しました。"
                : result.StandardError);
    }
}
