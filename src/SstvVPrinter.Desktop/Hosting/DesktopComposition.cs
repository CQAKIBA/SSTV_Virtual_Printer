using Avalonia.Threading;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using SstvVPrinter.App.Audio;
using SstvVPrinter.App.Jobs;
using SstvVPrinter.App.Settings;
using SstvVPrinter.App.Transmission;
using SstvVPrinter.Desktop.Platforms.Windows;
using SstvVPrinter.Desktop.Ptt;
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
    private readonly IPttPlatform _pttPlatform;
    private readonly PttController _ptt;
    private CancellationTokenSource _pttOperations = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _preferenceGate = new();
    private readonly object _disposalGate = new();
    private Task _preferenceSaveTask = Task.CompletedTask;
    private Task? _disposalTask;
    private Task? _audioWatchTask;
    private Guid? _preparedJobId;
    private string _committedCallSign;
    private bool _updatingSelection;
    private bool _refreshingPttSelection;
    private readonly object _queueIdentityGate = new();
    private HashSet<Guid> _knownJobIds = [];
    private int _disposed;

    private DesktopComposition(DesktopHost host, MainWindowViewModel viewModel, PrintJobQueue queue, TransmissionController transmission, IUserPreferencesStore preferencesStore, IAudioEndpointMonitor audioMonitor, ApplicationMenuViewModel menu, string committedCallSign, IPttPlatform pttPlatform)
    {
        _host = host;
        ViewModel = viewModel;
        Queue = queue;
        _transmission = transmission;
        _preferencesStore = preferencesStore;
        _audioMonitor = audioMonitor;
        _pttPlatform = pttPlatform;
        _ptt = new PttController(pttPlatform);
        Menu = menu;
        _committedCallSign = committedCallSign;
    }

    public MainWindowViewModel ViewModel { get; }
    public PrintJobQueue Queue { get; }
    public ApplicationMenuViewModel Menu { get; }

    public static async Task<DesktopComposition> CreateAsync(
        MainWindow window, IClassicDesktopStyleApplicationLifetime lifetime, CancellationToken cancellationToken = default,
        IUserPreferencesStore? preferencesStore = null, IAudioEndpointMonitor? audio = null,
        TransmissionController? transmission = null, IPttPlatform? pttPlatform = null, DesktopHost? host = null)
    {
        preferencesStore ??= new JsonUserPreferencesStore();
        var preferences = await preferencesStore.LoadAsync(cancellationToken);
        var queue = new PrintJobQueue();
        var viewModel = new MainWindowViewModel(new TransmissionPreparationService(new TransmissionFrameComposer()))
        {
            CallSign = preferences.CallSign,
            SelectedMode = preferences.Mode,
            IsDoubleHorizontal = preferences.HorizontalSampleMultiplier == 2,
            RotationDegrees = preferences.RotationDegrees,
            StartAtSignIn = preferences.StartAtSignIn,
            HeaderOverlayModeMask = preferences.HeaderOverlayModeMask,
            DeleteAfterSend = preferences.DeleteAfterSend,
            SelectedPttPort = preferences.PttPortName ?? "N/A",
            SelectedPttMethod = string.Equals(preferences.PttMethod, "DTR", StringComparison.OrdinalIgnoreCase)
                ? PttControlMethod.Dtr : PttControlMethod.Rts,
        };
        audio ??= new WindowsAudioEndpointMonitor();
        var endpoints = await audio.GetEndpointsAsync(cancellationToken);
        viewModel.AudioEndpoints = endpoints;
        viewModel.SelectedAudioEndpoint = endpoints.FirstOrDefault(item => item.Id == preferences.AudioEndpointId)
            ?? endpoints.FirstOrDefault(item => item.IsDefault)
            ?? endpoints.FirstOrDefault();
        ApplyEndpointState(viewModel);

        if (host is null)
        {
            var sink = new PwgPrintJobSink(queue, new PwgRasterDecoder());
            var webApplication = ProbeApplication.Build(sink, 8631);
            host = new DesktopHost([new WebApplicationHostedService(webApplication)]);
        }
        transmission ??= new TransmissionController(new TransmissionArtifactService(), new NaudioWavePlaybackService());
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
            host, viewModel, queue, transmission, preferencesStore, audio, menu, preferences.CallSign,
            pttPlatform ?? new WindowsPttPlatform());
        composition.WireWindow(window);
        queue.Changed += composition.OnQueueChanged;
        composition.WirePreferencePersistence();
        await composition.RunPttOperationAsync(() => composition.RefreshPttPortsAsync(cancellationToken));
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
        _ptt.StateChanged += OnPttStateChanged;
        window.PttPortsRefreshRequested = () => RunPttOperationAsync(() => RefreshPttPortsAsync(_pttOperations.Token));
        window.PttOpenCloseRequested = () => RunPttOperationAsync(() => _ptt.Snapshot.IsOpen
            ? _ptt.CloseAsync(_pttOperations.Token)
            : HasSelectedPttPort ? _ptt.OpenAsync(ViewModel.SelectedPttPort!, ViewModel.SelectedPttMethod, _pttOperations.Token)
            : Task.CompletedTask);
        window.ManualPttPressed = () => RunPttOperationAsync(() => HasSelectedPttPort
            ? _ptt.BeginManualAsync(ViewModel.SelectedPttPort!, ViewModel.SelectedPttMethod, _pttOperations.Token)
            : Task.CompletedTask);
        window.ManualPttReleased = () => RunPttOperationAsync(() => _ptt.EndManualAsync());
        _transmission.StateChanged += state => Dispatcher.UIThread.Post(() =>
            ViewModel.ApplyTransmissionState(state, _transmission.Progress, Remaining()));
        _transmission.ProgressChanged += progress => Dispatcher.UIThread.Post(() =>
            ViewModel.ApplyTransmissionState(_transmission.State, progress, Remaining()));
        window.SendRequested = async () =>
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            if (ViewModel.Preview is not { } preview || ViewModel.SelectedAudioEndpoint is not { } endpoint) return;
            if (Queue.Active is not { } active || Queue.Selected?.Id != active.Id) return;
            try
            {
                ITransmissionGate gate = HasSelectedPttPort
                    ? new SerialPttTransmissionGate(_ptt, _pttPlatform, ViewModel.SelectedPttPort!, ViewModel.SelectedPttMethod)
                    : NoOpTransmissionGate.Instance;
                await _transmission.SendAsync(preview, endpoint.Id, gate, _shutdown.Token);
                if (_transmission.State == JobState.Completed)
                    TransmissionCompletionPolicy.Apply(Queue, ViewModel.DeleteAfterSend);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                ViewModel.ValidationError = $"送信に失敗しました: {exception.Message}";
            }
        };
        window.SaveWaveRequested = async path =>
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            if (ViewModel.Preview is not { } preview) return;
            if (Queue.Active is not { } active || Queue.Selected?.Id != active.Id) return;
            try
            {
                await _transmission.SaveWaveAsync(preview, path, _shutdown.Token);
                TransmissionCompletionPolicy.Apply(Queue, ViewModel.DeleteAfterSend);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                ViewModel.ValidationError = $"WAV保存に失敗しました: {exception.Message}";
            }
        };
        window.StopRequested = () =>
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            ViewModel.CancelPreparation();
            _transmission.Stop();
            var canceled = _pttOperations;
            _pttOperations = new CancellationTokenSource();
            canceled.Cancel();
            canceled.Dispose();
            _ = RunPttOperationAsync(() => _ptt.EndManualAsync());
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
        window.CallSignCommitRequested = CommitCallSignAsync;
    }

    private bool HasSelectedPttPort => !string.IsNullOrWhiteSpace(ViewModel.SelectedPttPort)
        && !string.Equals(ViewModel.SelectedPttPort, "N/A", StringComparison.OrdinalIgnoreCase);

    private async Task RefreshPttPortsAsync(CancellationToken cancellationToken)
    {
        var ports = await _ptt.RefreshPortsAsync(cancellationToken);
        if (Volatile.Read(ref _disposed) != 0) return;
        var selected = ViewModel.SelectedPttPort;
        var restored = ports.FirstOrDefault(port => string.Equals(port, selected, StringComparison.OrdinalIgnoreCase)) ?? "N/A";
        _refreshingPttSelection = true;
        try
        {
            ViewModel.PttPorts = ports;
            ViewModel.SelectedPttPort = restored;
        }
        finally { _refreshingPttSelection = false; }

        if (!string.Equals(selected, restored, StringComparison.Ordinal)) QueuePreferenceSave();
    }

    private async Task RunPttOperationAsync(Func<Task> operation)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        try { await operation(); }
        catch (OperationCanceledException) { }
        catch (Exception exception) { ViewModel.ValidationError = $"PTT操作に失敗しました: {exception.Message}"; }
    }

    private void OnPttStateChanged(object? sender, PttSnapshot snapshot) => Dispatcher.UIThread.Post(() =>
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        ViewModel.ApplyPttSnapshot(snapshot);
        if (snapshot.Error is not null) ViewModel.ValidationError = $"PTT操作に失敗しました: {snapshot.Error}";
    });

    private async Task CommitCallSignAsync()
    {
        if (string.Equals(_committedCallSign, ViewModel.CallSign, StringComparison.Ordinal)) return;
        _committedCallSign = ViewModel.CallSign;
        QueuePreferenceSave();
        if (Queue.Selected is { } selected && _transmission.State is not (JobState.Encoding or JobState.Transmitting))
        {
            _preparedJobId = null;
            await PrepareSelectedAsync(selected);
        }
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
            if (Volatile.Read(ref _disposed) != 0) return;
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
            else if (args.PropertyName is nameof(MainWindowViewModel.DeleteAfterSend)
                or nameof(MainWindowViewModel.SelectedPttMethod)
                || args.PropertyName == nameof(MainWindowViewModel.SelectedPttPort) && !_refreshingPttSelection)
            {
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
        ViewModel.CallSign, ViewModel.SelectedMode, ViewModel.StoredHorizontalSampleMultiplier,
        ViewModel.SelectedAudioEndpoint?.Id, ViewModel.StartAtSignIn, ViewModel.RotationDegrees,
        ViewModel.HeaderOverlayModeMask, ViewModel.DeleteAfterSend,
        HasSelectedPttPort ? ViewModel.SelectedPttPort : null,
        ViewModel.SelectedPttMethod == PttControlMethod.Dtr ? "DTR" : "RTS");

    private TimeSpan? Remaining() => ViewModel.Preview is { } preview
        ? TimeSpan.FromTicks((long)(preview.EstimatedDuration.Ticks * (1 - _transmission.Progress)))
        : null;

    private void OnQueueChanged(PrintJobQueueSnapshot snapshot)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
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
        if (Volatile.Read(ref _disposed) != 0) return;
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
            ViewModel.CallSign, ViewModel.SelectedMode, ViewModel.IsDoubleHorizontal ? 2 : 1,
            ViewModel.RotationDegrees, ViewModel.SelectedHeaderPlacement));
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
        viewModel.RefreshAudioDisplay();
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposalGate)
        {
            return new(_disposalTask ??= DisposeCoreAsync());
        }
    }

    private async Task DisposeCoreAsync()
    {
        Volatile.Write(ref _disposed, 1);
        _transmission.Stop();
        _pttOperations.Cancel();
        try { await _ptt.DisposeAsync(); }
        catch (Exception exception) { ViewModel.ValidationError = $"PTTを終了できません: {exception.Message}"; }
        _ptt.StateChanged -= OnPttStateChanged;
        Queue.Changed -= OnQueueChanged;
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
        _pttOperations.Dispose();
        _shutdown.Dispose();
    }
}
