using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SstvVPrinter.Desktop.Platforms.Windows;
using SstvVPrinter.Desktop.Views;

namespace SstvVPrinter.Desktop;

public sealed partial class App : Application
{
    internal static WindowsSingleInstanceCoordinator? SingleInstance { get; set; }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>();

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var window = new MainWindow();
            desktop.MainWindow = window;
            if (SingleInstance is not null)
            {
                SingleInstance.ActivationRequested += () => Dispatcher.UIThread.Post(() =>
                {
                    window.Show();
                    window.WindowState = WindowState.Normal;
                    window.Activate();
                });
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
