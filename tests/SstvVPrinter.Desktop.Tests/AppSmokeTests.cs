using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using SstvVPrinter.Desktop;
using SstvVPrinter.Desktop.Views;

[assembly: AvaloniaTestApplication(typeof(SstvVPrinter.Desktop.Tests.TestAppBuilder))]

namespace SstvVPrinter.Desktop.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        App.BuildAvaloniaApp().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class AppSmokeTests
{
    private static readonly string[] RequiredBrushes =
    [
        "AppBackgroundBrush",
        "ReadyBrush",
        "WarningBrush",
        "TransmitBrush",
        "CyanActionBrush",
        "SelectionBrush",
    ];

    [AvaloniaFact]
    public void Main_window_loads_with_fixed_dark_palette()
    {
        var window = new MainWindow();

        Assert.IsType<Grid>(window.Content);
        foreach (var key in RequiredBrushes)
        {
            Assert.True(window.TryFindResource(key, out var value), $"Missing palette resource {key}.");
            Assert.IsAssignableFrom<IBrush>(value);
        }
    }
}
