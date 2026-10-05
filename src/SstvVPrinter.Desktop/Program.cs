using Avalonia;

namespace SstvVPrinter.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        App.BuildAvaloniaApp()
            .UsePlatformDetect()
            .WithInterFont()
            .StartWithClassicDesktopLifetime(args);
}
