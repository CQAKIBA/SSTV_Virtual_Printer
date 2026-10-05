using System.Runtime.Versioning;
using Microsoft.Win32;
using SstvVPrinter.App.Platform;

namespace SstvVPrinter.Desktop.Platforms.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsStartupRegistrationService(string executablePath, string valueName = "SstvVPrinter")
    : IStartupRegistrationService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return Task.FromResult(key?.GetValue(valueName) is string);
    }

    public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled) key.SetValue(valueName, $"\"{Path.GetFullPath(executablePath)}\" --background");
        else key.DeleteValue(valueName, false);
        return Task.CompletedTask;
    }
}
