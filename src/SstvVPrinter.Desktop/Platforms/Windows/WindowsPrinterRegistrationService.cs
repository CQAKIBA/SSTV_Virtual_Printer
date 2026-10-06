using SstvVPrinter.App.Platform;
using SstvVPrinter.IppProbe.Ipp;
using System.ComponentModel;
using System.Text;

namespace SstvVPrinter.Desktop.Platforms.Windows;

public sealed class WindowsPrinterRegistrationService : IPrinterRegistrationService
{
    private readonly IProcessRunner _runner;
    private readonly TimeSpan _operationTimeout;
    private readonly string _diagnosticDirectory;
    private const string VerifyScript = "param($name) if (Get-Printer -Name $name -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }";
    private const string AddScript = "param($name,$uri,$resultPath,$logPath)\n$code=0\n$stamp=Get-Date -Format o\ntry {\n  Add-Content -LiteralPath $logPath -Value \"[$stamp] Register $name at $uri\" -Encoding UTF8 -ErrorAction SilentlyContinue\n  Add-Printer -Name $name -IppURL $uri -ErrorAction Stop\n  Write-Host 'Printer registration completed.' -ForegroundColor Green\n  Add-Content -LiteralPath $logPath -Value \"[$stamp] Registration completed.\" -Encoding UTF8 -ErrorAction SilentlyContinue\n} catch {\n  $detail=$_.Exception.ToString()\n  Write-Host ('Registration failed: ' + $detail) -ForegroundColor Red\n  Set-Content -LiteralPath $resultPath -Value $detail -Encoding UTF8 -ErrorAction SilentlyContinue\n  Add-Content -LiteralPath $logPath -Value \"[$stamp] Registration failed: $detail\" -Encoding UTF8 -ErrorAction SilentlyContinue\n  $code=1\n} finally { Start-Sleep -Seconds 3 }\nexit $code";
    private const string RepairScript = "param($name,$uri,$resultPath,$logPath)\n$code=0\n$stamp=Get-Date -Format o\ntry {\n  Add-Content -LiteralPath $logPath -Value \"[$stamp] Repair $name at $uri\" -Encoding UTF8 -ErrorAction SilentlyContinue\n  Remove-Printer -Name $name -ErrorAction SilentlyContinue\n  Add-Printer -Name $name -IppURL $uri -ErrorAction Stop\n  Write-Host 'Printer repair completed.' -ForegroundColor Green\n  Add-Content -LiteralPath $logPath -Value \"[$stamp] Repair completed.\" -Encoding UTF8 -ErrorAction SilentlyContinue\n} catch {\n  $detail=$_.Exception.ToString()\n  Write-Host ('Repair failed: ' + $detail) -ForegroundColor Red\n  Set-Content -LiteralPath $resultPath -Value $detail -Encoding UTF8 -ErrorAction SilentlyContinue\n  Add-Content -LiteralPath $logPath -Value \"[$stamp] Repair failed: $detail\" -Encoding UTF8 -ErrorAction SilentlyContinue\n  $code=1\n} finally { Start-Sleep -Seconds 3 }\nexit $code";

    public WindowsPrinterRegistrationService(
        IProcessRunner runner,
        TimeSpan? operationTimeout = null,
        string? diagnosticDirectory = null)
    {
        _runner = runner;
        _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(60);
        _diagnosticDirectory = Path.GetFullPath(diagnosticDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SstvVPrinter"));
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
        var scriptPath = Path.Combine(Path.GetTempPath(), $"SstvVPrinter-printer-{Guid.NewGuid():N}.ps1");
        Directory.CreateDirectory(_diagnosticDirectory);
        var logPath = Path.Combine(_diagnosticDirectory, "printer-setup.log");
        try
        {
            await File.WriteAllTextAsync(scriptPath, script, Encoding.Unicode, cancellationToken);
            var arguments = new List<string>
            {
                "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath,
                ProbePrinter.DisplayName, ProbePrinter.DefaultPrinterUri, resultPath, logPath,
            };
            var result = await RunPowerShellAsync(arguments, true, true, cancellationToken);
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
            try { File.Delete(scriptPath); }
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
        return await RunPowerShellAsync(arguments, showWindow, runElevated, cancellationToken);
    }

    private async Task<ProcessResult> RunPowerShellAsync(
        IReadOnlyList<string> arguments,
        bool showWindow,
        bool runElevated,
        CancellationToken cancellationToken)
    {
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
