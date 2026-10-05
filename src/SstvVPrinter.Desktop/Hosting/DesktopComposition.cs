using Avalonia.Threading;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using SstvVPrinter.App.Audio;
using SstvVPrinter.App.Jobs;
using SstvVPrinter.App.Settings;
using SstvVPrinter.App.Transmission;
using SstvVPrinter.Desktop.Platforms.Windows;
using SstvVPrinter.Desktop.ViewModels;
using SstvVPrinter.Desktop.Views;
using SstvVPrinter.Imaging.Composition;
using SstvVPrinter.Imaging.Pwg;
using SstvVPrinter.IppProbe.Hosting;

namespace SstvVPrinter.Desktop.Hosting;

public sealed class DesktopComposition : IAsyncDisposable
{
    private readonly DesktopHost _host;
    private readonly TransmissionController _transmission;
    private readonly IUserPreferencesStore _preferencesStore;

    private DesktopComposition(DesktopHost host, MainWindowViewModel viewModel, PrintJobQueue queue, TransmissionController transmission, IUserPreferencesStore preferencesStore, ApplicationMenuViewModel menu)
    {
        _host = host;
        ViewModel = viewModel;
        Queue = queue;
        _transmission = transmission;
        _preferencesStore = preferencesStore;
        Menu = menu;
    }

    public MainWindowViewModel ViewModel { get; }
    public PrintJobQueue Queue { get; }
    public ApplicationMenuViewModel Menu { get; }

    public static async Task<DesktopComposition> CreateAsync(MainWindow window, IClassicDesktopStyleApplicationLifetime lifetime, CancellationToken cancellationToken = default)
    {
        IUserPreferencesStore preferencesStore = new JsonUserPreferencesStore();
        var preferences = await preferencesStore.LoadAsync(cancellationToken);
        var queue = new PrintJobQueue();
        var viewModel = new MainWindowViewModel(new TransmissionPreparationService(new SstvFrameComposer()))
        {
            CallSign = preferences.CallSign,
            SelectedMode = preferences.Mode,
            IsDoubleHorizontal = preferences.HorizontalSampleMultiplier == 2,
            StartAtSignIn = preferences.StartAtSignIn,
        };
        IAudioEndpointMonitor audio = new WindowsAudioEndpointMonitor();
        var endpoints = await audio.GetEndpointsAsync(cancellationToken);
        viewModel.AudioEndpoints = endpoints;
        viewModel.SelectedAudioEndpoint = endpoints.FirstOrDefault(item => item.Id == preferences.AudioEndpointId)
            ?? endpoints.FirstOrDefault(item => item.IsDefault)
            ?? endpoints.FirstOrDefault();
        ApplyEndpointState(viewModel);

        queue.Changed += snapshot => Dispatcher.UIThread.Post(async () =>
        {
            viewModel.WaitingJobs = snapshot.Waiting;
            if (snapshot.Selected is not { } selected) return;
            window.Show();
            window.WindowState = Avalonia.Controls.WindowState.Normal;
            window.Activate();
            await viewModel.UpdatePreviewAsync(selected, new TransmissionSettings(
                viewModel.CallSign, viewModel.SelectedMode, viewModel.IsDoubleHorizontal ? 2 : 1));
        });

        var sink = new PwgPrintJobSink(queue, new PwgRasterDecoder());
        var webApplication = ProbeApplication.Build(sink, 8631);
        var transmission = new TransmissionController(new TransmissionArtifactService(), new NaudioWavePlaybackService());
        var lifetimeService = new AvaloniaApplicationLifetimeService(lifetime, window, window.ConfirmPendingExitAsync);
        var menu = new ApplicationMenuViewModel(
            lifetimeService,
            new WindowsStartupRegistrationService(Environment.ProcessPath ?? throw new InvalidOperationException("Executable path is unavailable.")),
            new WindowsUserAttentionService(() => window.TryGetPlatformHandle()?.Handle ?? 0),
            new WindowsPrinterRegistrationService(new ProcessRunner()),
            () => queue.Active is not null);
        var composition = new DesktopComposition(
            new DesktopHost([new WebApplicationHostedService(webApplication)]), viewModel, queue, transmission, preferencesStore, menu);
        composition.WireWindow(window);
        await menu.InitializeAsync(cancellationToken);
        viewModel.StartAtSignIn = menu.StartAtSignIn;
        composition.WirePreferencePersistence();
        return composition;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => _host.StartAsync(cancellationToken);

    private void WireWindow(MainWindow window)
    {
        _transmission.StateChanged += state => Dispatcher.UIThread.Post(() =>
            ViewModel.ApplyTransmissionState(state, _transmission.Progress, Remaining()));
        _transmission.ProgressChanged += progress => Dispatcher.UIThread.Post(() =>
            ViewModel.ApplyTransmissionState(_transmission.State, progress, Remaining()));
        window.SendRequested = async () =>
        {
            if (ViewModel.Preview is not { } preview || ViewModel.SelectedAudioEndpoint is not { } endpoint) return;
            await _transmission.SendAsync(preview, endpoint.Id, CancellationToken.None);
            if (_transmission.State == JobState.Completed) Queue.CompleteActive();
        };
        window.SaveWaveRequested = path => ViewModel.Preview is { } preview
            ? _transmission.SaveWaveAsync(preview, path, CancellationToken.None)
            : Task.CompletedTask;
        window.StopRequested = _transmission.Stop;
        window.DiscardRequested = () =>
        {
            if (Queue.Selected is { } selected) Queue.Discard(selected.Id);
        };
        window.StartupChanged = async enabled =>
        {
            await Menu.SetStartAtSignInAsync(enabled);
            ViewModel.StartAtSignIn = enabled;
            await SavePreferencesAsync();
        };
        window.ExitRequested = () => Menu.ExitAsync();
        window.VerifyPrinterRequested = async () =>
        {
            await Menu.InitializeAsync();
            ViewModel.StatusText = Menu.PrinterIsRegistered is true ? "プリンタ登録済み" : "プリンタ未登録";
        };
        window.RegisterPrinterRequested = async () => { await Menu.RegisterPrinterAsync(); ViewModel.StatusText = "プリンタを登録しました"; };
        window.RepairPrinterRequested = async () => { await Menu.RepairPrinterAsync(); ViewModel.StatusText = "プリンタを修復しました"; };
    }

    private void WirePreferencePersistence()
    {
        ViewModel.PropertyChanged += async (_, args) =>
        {
            if (args.PropertyName is nameof(MainWindowViewModel.CallSign)
                or nameof(MainWindowViewModel.SelectedMode)
                or nameof(MainWindowViewModel.IsDoubleHorizontal)
                or nameof(MainWindowViewModel.SelectedAudioEndpoint))
            {
                await SavePreferencesAsync();
                if (Queue.Selected is { } selected)
                    await ViewModel.UpdatePreviewAsync(selected, new TransmissionSettings(ViewModel.CallSign, ViewModel.SelectedMode, ViewModel.IsDoubleHorizontal ? 2 : 1));
            }
        };
    }

    private Task SavePreferencesAsync() => _preferencesStore.SaveAsync(new UserPreferences(
        ViewModel.CallSign,
        ViewModel.SelectedMode,
        ViewModel.IsDoubleHorizontal ? 2 : 1,
        ViewModel.SelectedAudioEndpoint?.Id,
        ViewModel.StartAtSignIn));

    private TimeSpan? Remaining() => ViewModel.Preview is { } preview
        ? TimeSpan.FromTicks((long)(preview.EstimatedDuration.Ticks * (1 - _transmission.Progress)))
        : null;

    private static void ApplyEndpointState(MainWindowViewModel viewModel)
    {
        var endpoint = viewModel.SelectedAudioEndpoint;
        viewModel.VolumeText = endpoint?.VolumePercent is { } volume ? $"{Math.Round(volume):0}%" : "不明";
        viewModel.MuteWarning = endpoint?.IsMuted is true ? "MUTED / ミュート中" : null;
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
