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
    private TrayIcons? _trayIcons;
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
            desktop.Exit += (_, _) =>
            {
                _trayIcon?.Dispose();
                _trayIcon = null;
                _trayIcons = null;
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
                try
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
                    _trayIcons = new TrayIcons { _trayIcon };
                    TrayIcon.SetIcons(this, _trayIcons);
                    await _composition.InitializeAncillaryAsync();
                }
                catch (Exception exception)
                {
                    await ShowStartupErrorAsync(window, exception);
                }
            });
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task ShowStartupErrorAsync(Window owner, Exception exception)
    {
        var dialog = new Window
        {
            Title = "SSTV Virtual Printer 起動エラー",
            Width = 520,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new TextBlock
            {
                Margin = new Thickness(20),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Text = $"ローカルIPP受信部を開始できませんでした。\n\n{exception.Message}\n\nポート8631を使用中の旧プローブや別インスタンスがないか確認してください。",
            },
        };
        await dialog.ShowDialog(owner);
    }
}
