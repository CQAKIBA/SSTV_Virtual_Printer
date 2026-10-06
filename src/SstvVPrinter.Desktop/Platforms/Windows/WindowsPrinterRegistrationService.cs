using SstvVPrinter.App.Platform;
using SstvVPrinter.IppProbe.Ipp;
using System.ComponentModel;

namespace SstvVPrinter.Desktop.Platforms.Windows;

public sealed class WindowsPrinterRegistrationService : IPrinterRegistrationService
{
    private readonly IProcessRunner _runner;
    private readonly TimeSpan _operationTimeout;
    private const string VerifyScript = "param($name) if (Get-Printer -Name $name -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }";
    private const string AddScript = "param($name,$uri,$resultPath) Write-Host 'SSTV仮想プリンタを登録しています...'; Write-Host ('接続先: ' + $uri); $code=0; try { Add-Printer -Name $name -IppURL $uri -ErrorAction Stop; Write-Host '登録が完了しました。' -ForegroundColor Green } catch { $detail=$_.Exception.ToString(); Write-Host ('登録に失敗しました: ' + $detail) -ForegroundColor Red; Set-Content -LiteralPath $resultPath -Value $detail -Encoding UTF8; $code=1 }; Start-Sleep -Seconds 3; exit $code";
    private const string RepairScript = "param($name,$uri,$resultPath) Write-Host 'SSTV仮想プリンタを修復しています...'; $code=0; try { Remove-Printer -Name $name -ErrorAction SilentlyContinue; Write-Host '再登録しています...'; Add-Printer -Name $name -IppURL $uri -ErrorAction Stop; Write-Host '修復が完了しました。' -ForegroundColor Green } catch { $detail=$_.Exception.ToString(); Write-Host ('修復に失敗しました: ' + $detail) -ForegroundColor Red; Set-Content -LiteralPath $resultPath -Value $detail -Encoding UTF8; $code=1 }; Start-Sleep -Seconds 3; exit $code";

    public WindowsPrinterRegistrationService(IProcessRunner runner, TimeSpan? operationTimeout = null)
    {
        _runner = runner;
        _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(60);
    }

    public async Task<bool> VerifyAsync(CancellationToken cancellationToken) =>
        (await RunAsync(VerifyScript, [ProbePrinter.DisplayName], false, false, cancellationToken)).ExitCode == 0;

    public async Task RegisterAsync(CancellationToken cancellationToken)
    {
        await RunAdministrativeAsync(AddScript, cancellationToken);
    }

    public async Task RepairAsync(CancellationToken cancellationToken)
    {
        await RunAdministrativeAsync(RepairScript, cancellationToken);
    }

    private async Task RunAdministrativeAsync(string script, CancellationToken cancellationToken)
    {
        var resultPath = Path.Combine(Path.GetTempPath(), $"SstvVPrinter-printer-{Guid.NewGuid():N}.txt");
        try
        {
            var result = await RunAsync(
                script,
                [ProbePrinter.DisplayName, ProbePrinter.DefaultPrinterUri, resultPath],
                true,
                true,
                cancellationToken);
            if (result.ExitCode == 0) return;

            var detail = File.Exists(resultPath)
                ? await File.ReadAllTextAsync(resultPath, cancellationToken)
                : result.StandardError;
            EnsureSuccess(new ProcessResult(result.ExitCode, result.StandardOutput, detail));
        }
        finally
        {
            try { File.Delete(resultPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private async Task<ProcessResult> RunAsync(
        string script,
        IReadOnlyList<string> values,
        bool showWindow,
        bool runElevated,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string> { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script };
        arguments.AddRange(values);
        using var timeout = new CancellationTokenSource(_operationTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await _runner.RunAsync(new ProcessInvocation("powershell.exe", arguments, showWindow, runElevated), linked.Token);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 5 or 1223)
        {
            throw new InvalidOperationException(
                "プリンタの登録・修復には管理者権限が必要です。Windowsのユーザー アカウント制御を許可して、もう一度実行してください。",
                exception);
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
