using Avalonia;
using SstvVPrinter.Desktop.Platforms.Windows;

namespace SstvVPrinter.Desktop;

internal static class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        await using var singleInstance = new WindowsSingleInstanceCoordinator();
        if (!await singleInstance.TryAcquireAsync()) return;
        App.SingleInstance = singleInstance;
        App.BuildAvaloniaApp()
            .UsePlatformDetect()
            .WithInterFont()
            .StartWithClassicDesktopLifetime(args);
        App.SingleInstance = null;
    }
}
