using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SstvVPrinter.Desktop.Hosting;
using SstvVPrinter.Desktop.Platforms.Windows;
using SstvVPrinter.Desktop.Views;

namespace SstvVPrinter.Desktop;

public sealed partial class App : Application
{
    private DesktopComposition? _composition;
    private TrayIcon? _trayIcon;
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
            desktop.Exit += async (_, _) =>
            {
                _trayIcon?.Dispose();
                if (_composition is not null) await _composition.DisposeAsync();
            };
            if (SingleInstance is not null)
            {
                SingleInstance.ActivationRequested += () => Dispatcher.UIThread.Post(() =>
                {
                    window.Show();
                    window.WindowState = WindowState.Normal;
                    window.Activate();
                });
            }
            Dispatcher.UIThread.Post(async () =>
            {
                _composition = await DesktopComposition.CreateAsync(window, desktop);
                window.DataContext = _composition.ViewModel;
                await _composition.StartAsync();
                _trayIcon = TrayIconFactory.Create(
                    () =>
                    {
                        window.Show();
                        window.WindowState = WindowState.Normal;
                        window.Activate();
                    },
                    () => _ = _composition.Menu.ExitAsync());
                TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
            });
        }

        base.OnFrameworkInitializationCompleted();
    }
}
