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
    private readonly IAudioEndpointMonitor _audioMonitor;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _preferenceGate = new();
    private Task _preferenceSaveTask = Task.CompletedTask;
    private Task? _audioWatchTask;
    private Guid? _preparedJobId;
    private bool _updatingSelection;
    private readonly object _queueIdentityGate = new();
    private HashSet<Guid> _knownJobIds = [];
    private int _disposed;

    private DesktopComposition(DesktopHost host, MainWindowViewModel viewModel, PrintJobQueue queue, TransmissionController transmission, IUserPreferencesStore preferencesStore, IAudioEndpointMonitor audioMonitor, ApplicationMenuViewModel menu)
    {
        _host = host;
        ViewModel = viewModel;
        Queue = queue;
        _transmission = transmission;
        _preferencesStore = preferencesStore;
        _audioMonitor = audioMonitor;
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

        var sink = new PwgPrintJobSink(queue, new PwgRasterDecoder());
        var webApplication = ProbeApplication.Build(sink, 8631);
        var transmission = new TransmissionController(new TransmissionArtifactService(), new NaudioWavePlaybackService());
        DesktopComposition? composition = null;
        var lifetimeService = new AvaloniaApplicationLifetimeService(
            lifetime,
            window,
            window.ConfirmPendingExitAsync,
            async _ =>
            {
                if (composition is not null) await composition.DisposeAsync();
            });
        var menu = new ApplicationMenuViewModel(
            lifetimeService,
            new WindowsStartupRegistrationService(Environment.ProcessPath ?? throw new InvalidOperationException("Executable path is unavailable.")),
            new WindowsUserAttentionService(() => window.TryGetPlatformHandle()?.Handle ?? 0),
            new WindowsPrinterRegistrationService(new ProcessRunner()),
            () => queue.Active is not null);
        composition = new DesktopComposition(
            new DesktopHost([new WebApplicationHostedService(webApplication)]), viewModel, queue, transmission, preferencesStore, audio, menu);
        composition.WireWindow(window);
        queue.Changed += composition.OnQueueChanged;
        composition.WirePreferencePersistence();
        return composition;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _host.StartAsync(cancellationToken);
        _audioWatchTask = WatchAudioAsync(_shutdown.Token);
    }

    public async Task InitializeAncillaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Menu.InitializeAsync(cancellationToken);
            ViewModel.StartAtSignIn = Menu.StartAtSignIn;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ViewModel.ValidationError = $"プリンタ状態を確認できません: {exception.Message}";
        }
    }

    private void WireWindow(MainWindow window)
    {
        _transmission.StateChanged += state => Dispatcher.UIThread.Post(() =>
            ViewModel.ApplyTransmissionState(state, _transmission.Progress, Remaining()));
        _transmission.ProgressChanged += progress => Dispatcher.UIThread.Post(() =>
            ViewModel.ApplyTransmissionState(_transmission.State, progress, Remaining()));
        window.SendRequested = async () =>
        {
            if (ViewModel.Preview is not { } preview || ViewModel.SelectedAudioEndpoint is not { } endpoint) return;
            if (Queue.Active is not { } active || Queue.Selected?.Id != active.Id) return;
            try
            {
                await _transmission.SendAsync(preview, endpoint.Id, _shutdown.Token);
                if (_transmission.State == JobState.Completed) Queue.CompleteActive();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                ViewModel.ValidationError = $"送信に失敗しました: {exception.Message}";
            }
        };
        window.SaveWaveRequested = async path =>
        {
            if (ViewModel.Preview is not { } preview) return;
            if (Queue.Active is not { } active || Queue.Selected?.Id != active.Id) return;
            try
            {
                await _transmission.SaveWaveAsync(preview, path, _shutdown.Token);
                Queue.CompleteActive();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                ViewModel.ValidationError = $"WAV保存に失敗しました: {exception.Message}";
            }
        };
        window.StopRequested = () =>
        {
            ViewModel.CancelPreparation();
            _transmission.Stop();
        };
        window.DiscardRequested = () =>
        {
            if (Queue.Selected is { } selected) Queue.Discard(selected.Id);
        };
        window.StartupChanged = async enabled =>
        {
            await Menu.SetStartAtSignInAsync(enabled);
            ViewModel.StartAtSignIn = enabled;
            QueuePreferenceSave();
        };
        window.ExitRequested = () => Menu.ExitAsync();
        window.VerifyPrinterRequested = async () =>
        {
            await Menu.InitializeAsync();
            ViewModel.StatusText = Menu.PrinterIsRegistered is true ? "プリンタ登録済み" : "プリンタ未登録";
        };
        window.RegisterPrinterRequested = () => RunPrinterOperationAsync(
            "プリンタを登録しています（PowerShellを確認してください）...",
            "プリンタを登録しました",
            Menu.RegisterPrinterAsync);
        window.RepairPrinterRequested = () => RunPrinterOperationAsync(
            "プリンタを修復しています（PowerShellを確認してください）...",
            "プリンタを修復しました",
            Menu.RepairPrinterAsync);
    }

    private async Task RunPrinterOperationAsync(
        string progressText,
        string successText,
        Func<CancellationToken, Task> operation)
    {
        ViewModel.ValidationError = null;
        ViewModel.StatusText = progressText;
        try
        {
            await operation(_shutdown.Token);
            ViewModel.StatusText = successText;
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        catch (Exception exception)
        {
            ViewModel.StatusText = "プリンタ操作に失敗しました";
            ViewModel.ValidationError = exception.Message;
        }
    }

    private void WirePreferencePersistence()
    {
        ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.SelectedJob)
                && !_updatingSelection
                && ViewModel.SelectedJob is { } selected)
            {
                Queue.Select(selected.Id);
            }
            if (args.PropertyName == nameof(MainWindowViewModel.SelectedAudioEndpoint))
            {
                ApplyEndpointState(ViewModel);
                QueuePreferenceSave();
            }
            else if (PreviewRegenerationPolicy.RequiresRegeneration(args.PropertyName))
            {
                QueuePreferenceSave();
                if (Queue.Selected is { } jobToPrepare && _transmission.State is not (JobState.Encoding or JobState.Transmitting))
                {
                    _preparedJobId = null;
                    Dispatcher.UIThread.Post(() => _ = PrepareSelectedAsync(jobToPrepare));
                }
            }
        };
    }

    private void QueuePreferenceSave()
    {
        var snapshot = CapturePreferences();
        lock (_preferenceGate)
        {
            _preferenceSaveTask = SaveAfterAsync(_preferenceSaveTask, snapshot);
        }
    }

    private async Task SaveAfterAsync(Task previous, UserPreferences snapshot)
    {
        try { await previous; }
        catch { }
        try { await _preferencesStore.SaveAsync(snapshot, _shutdown.Token); }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        catch (Exception exception) { ViewModel.ValidationError = $"設定を保存できません: {exception.Message}"; }
    }

    private UserPreferences CapturePreferences() => new(
        ViewModel.CallSign, ViewModel.SelectedMode, ViewModel.IsDoubleHorizontal ? 2 : 1,
        ViewModel.SelectedAudioEndpoint?.Id, ViewModel.StartAtSignIn);

    private TimeSpan? Remaining() => ViewModel.Preview is { } preview
        ? TimeSpan.FromTicks((long)(preview.EstimatedDuration.Ticks * (1 - _transmission.Progress)))
        : null;

    private void OnQueueChanged(PrintJobQueueSnapshot snapshot)
    {
        var currentIds = snapshot.Waiting.Select(job => job.Id).ToHashSet();
        if (snapshot.Active is not null) currentIds.Add(snapshot.Active.Id);
        bool containsNewJob;
        lock (_queueIdentityGate)
        {
            containsNewJob = currentIds.Except(_knownJobIds).Any();
            _knownJobIds = currentIds;
        }
        Dispatcher.UIThread.Post(() => _ = HandleQueueSnapshotAsync(snapshot, containsNewJob));
    }

    private async Task HandleQueueSnapshotAsync(PrintJobQueueSnapshot snapshot, bool containsNewJob)
    {
        try
        {
            ViewModel.WaitingJobs = snapshot.Active is { } active
                ? [active, .. snapshot.Waiting]
                : [];
            _updatingSelection = true;
            ViewModel.SelectedJob = snapshot.Selected;
            ViewModel.IsSelectedJobActive = snapshot.Active is not null && snapshot.Selected?.Id == snapshot.Active.Id;
            _updatingSelection = false;
            if (snapshot.Selected is not { } selected)
            {
                _preparedJobId = null;
                ViewModel.ClearPreview();
                return;
            }
            if (containsNewJob) await Menu.ActivateAsync(_shutdown.Token);
            if (_transmission.State is JobState.Encoding or JobState.Transmitting) return;
            if (_preparedJobId == selected.Id && ViewModel.Preview is not null) return;
            await PrepareSelectedAsync(selected);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        catch (Exception exception)
        {
            ViewModel.ValidationError = $"印刷ジョブを準備できません: {exception.Message}";
        }
        finally { _updatingSelection = false; }
    }

    private async Task PrepareSelectedAsync(PrintJob selected)
    {
        await ViewModel.UpdatePreviewAsync(selected, new TransmissionSettings(
            ViewModel.CallSign, ViewModel.SelectedMode, ViewModel.IsDoubleHorizontal ? 2 : 1));
        if (ViewModel.IsReady) _preparedJobId = selected.Id;
    }

    private async Task WatchAudioAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (!cancellationToken.IsCancellationRequested)
            {
                var endpoints = await Task.Run(
                    () => _audioMonitor.GetEndpointsAsync(cancellationToken).AsTask(), cancellationToken);
                Dispatcher.UIThread.Post(() =>
                {
                    var selectedId = ViewModel.SelectedAudioEndpoint?.Id;
                    ViewModel.AudioEndpoints = endpoints;
                    ViewModel.SelectedAudioEndpoint = endpoints.FirstOrDefault(item => item.Id == selectedId && item.IsAvailable)
                        ?? endpoints.FirstOrDefault(item => item.IsDefault && item.IsAvailable)
                        ?? endpoints.FirstOrDefault(item => item.IsAvailable);
                    ApplyEndpointState(ViewModel);
                });
                await timer.WaitForNextTickAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Dispatcher.UIThread.Post(() => ViewModel.ValidationError = $"音声デバイスを監視できません: {exception.Message}");
        }
    }

    private static void ApplyEndpointState(MainWindowViewModel viewModel)
    {
        var endpoint = viewModel.SelectedAudioEndpoint;
        viewModel.VolumeText = endpoint?.VolumePercent is { } volume ? $"{Math.Round(volume):0}%" : "不明";
        viewModel.MuteWarning = endpoint?.IsMuted is true ? "MUTED / ミュート中" : null;
        viewModel.DeviceError = endpoint is null ? "使用可能な音声出力デバイスがありません。" : null;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        QueuePreferenceSave();
        Task saveTask;
        lock (_preferenceGate) saveTask = _preferenceSaveTask;
        try { await saveTask; } catch { }
        _shutdown.Cancel();
        if (_audioWatchTask is not null)
        {
            try { await _audioWatchTask; } catch (OperationCanceledException) { }
        }
        await _host.DisposeAsync();
        _shutdown.Dispose();
    }
}
