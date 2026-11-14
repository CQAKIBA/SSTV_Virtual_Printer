using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media.Imaging;
using SstvVPrinter.App.Audio;
using SstvVPrinter.App.Jobs;
using SstvVPrinter.App.Transmission;
using SstvVPrinter.App.Validation;
using SstvVPrinter.Desktop.Ptt;
using SstvVPrinter.Imaging.Composition;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Desktop.ViewModels;

public sealed partial class MainWindowViewModel(ITransmissionPreparationService preparationService)
    : ObservableObject
{
    private CancellationTokenSource? _preparationCancellation;
    private long _preparationGeneration;

    [ObservableProperty]
    private PreparedTransmission? _preview;

    [ObservableProperty]
    private bool _isReady;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private string _callSign = string.Empty;

    private SstvMode _selectedMode = SstvMode.Scottie2;
    private bool _isDoubleHorizontal;
    private int _headerOverlayModeMask;

    [ObservableProperty]
    private int _rotationDegrees;

    [ObservableProperty]
    private IReadOnlyList<AudioEndpointInfo> _audioEndpoints = [];

    [ObservableProperty]
    private AudioEndpointInfo? _selectedAudioEndpoint;

    [ObservableProperty]
    private string _volumeText = "不明";

    [ObservableProperty]
    private double _masterVolumePercent;

    [ObservableProperty]
    private string _appVolumeText = "待機中";

    [ObservableProperty]
    private double _appVolumePercent;

    [ObservableProperty]
    private bool _isAppVolumeActive;

    [ObservableProperty]
    private string? _muteWarning;

    [ObservableProperty]
    private string? _deviceError;

    [ObservableProperty]
    private IReadOnlyList<PrintJob> _waitingJobs = [];

    [ObservableProperty]
    private PrintJob? _selectedJob;

    [ObservableProperty]
    private Bitmap? _previewBitmap;

    [ObservableProperty]
    private string _statusText = "待機中";

    [ObservableProperty]
    private string _statusBrush = "#344050";

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private bool _isScanLineVisible;

    [ObservableProperty]
    private double _scanLineY;

    [ObservableProperty]
    private string? _remainingTimeText;

    [ObservableProperty]
    private bool _isSettingsLocked;

    [ObservableProperty]
    private IReadOnlyList<string> _pttPorts = ["N/A"];

    [ObservableProperty]
    private string? _selectedPttPort = "N/A";

    [ObservableProperty]
    private PttControlMethod _selectedPttMethod = PttControlMethod.Rts;

    [ObservableProperty]
    private bool _isPttOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    private bool _isPttAsserted;

    [ObservableProperty]
    private bool _isPttBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    private bool _isManualPttPending;

    private bool _canStopTransmission;

    [ObservableProperty]
    private bool _startAtSignIn = true;

    [ObservableProperty]
    private bool _deleteAfterSend;

    [ObservableProperty]
    private bool _isSelectedJobActive = true;

    public IReadOnlyList<SstvMode> AvailableModes { get; } =
    [
        SstvMode.Scottie2,
        SstvMode.Scottie1,
        SstvMode.Martin1,
        SstvMode.Robot36,
        SstvMode.Radiofax120Ioc288Sync10Ms,
        SstvMode.Radiofax120Ioc288Sync20Ms,
        SstvMode.Radiofax120Ioc576Sync10Ms,
        SstvMode.Radiofax120Ioc576Sync20Ms,
    ];
    public SstvMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (!SetProperty(ref _selectedMode, value)) return;
            OnPropertyChanged(nameof(IsRadiofaxMode));
            OnPropertyChanged(nameof(IsHorizontalMultiplierEnabled));
            OnPropertyChanged(nameof(IsDoubleHorizontal));
            OnPropertyChanged(nameof(IsHeaderOverlay));
        }
    }
    public bool IsDoubleHorizontal
    {
        get => !IsRadiofaxMode && _isDoubleHorizontal;
        set
        {
            if (_isDoubleHorizontal == value) return;
            _isDoubleHorizontal = value;
            OnPropertyChanged();
        }
    }
    public bool IsRadiofaxMode => TransmissionModes.IsRadiofax(SelectedMode);
    public bool IsHeaderOverlay
    {
        get => (_headerOverlayModeMask & (1 << (int)SelectedMode)) != 0;
        set
        {
            var bit = 1 << (int)SelectedMode;
            var updated = value ? _headerOverlayModeMask | bit : _headerOverlayModeMask & ~bit;
            if (updated == _headerOverlayModeMask) return;
            _headerOverlayModeMask = updated;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HeaderOverlayModeMask));
        }
    }
    public int HeaderOverlayModeMask
    {
        get => _headerOverlayModeMask;
        set
        {
            if (!SetProperty(ref _headerOverlayModeMask, value)) return;
            OnPropertyChanged(nameof(IsHeaderOverlay));
        }
    }
    public HeaderPlacement SelectedHeaderPlacement => IsHeaderOverlay
        ? HeaderPlacement.Overlay
        : HeaderPlacement.ReserveSpace;
    public bool IsHorizontalMultiplierEnabled => !IsRadiofaxMode;
    public int StoredHorizontalSampleMultiplier => _isDoubleHorizontal ? 2 : 1;
    public bool IsPreviewEmpty => PreviewBitmap is null;
    public double PreviewWidth => Preview?.Frame.Width ?? 1;
    public double PreviewHeight => Preview?.Frame.Height ?? 1;
    public bool CanSend => IsReady && IsSelectedJobActive && !IsSettingsLocked && SelectedAudioEndpoint?.IsAvailable is true;
    public bool CanSaveWave => IsReady && IsSelectedJobActive && !IsSettingsLocked;
    public bool CanDiscard => (Preview is not null || SelectedJob is not null) && !IsSettingsLocked;
    public bool CanStop => _canStopTransmission || IsManualPttPending || IsPttAsserted;
    public string RotationText => $"{RotationDegrees}°";
    public IReadOnlyList<PttControlMethod> PttMethods { get; } = [PttControlMethod.Rts, PttControlMethod.Dtr];
    public string PttConnectionButtonText => IsPttOpen ? "CLOSE" : "OPEN";
    public string PttConnectionBrush => IsPttOpen ? "#FF3D21" : "#344050";
    public string PttButtonBrush => IsPttAsserted ? "#FF3D21" : "#344050";
    public bool CanEditPttSettings => !IsSettingsLocked && !IsPttOpen && !IsPttAsserted && !IsPttBusy;
    public bool CanTogglePttConnection => !IsSettingsLocked && !IsPttBusy && !IsPttAsserted && (IsPttOpen || HasPttPort);
    public bool CanPressPtt => !IsSettingsLocked && !IsPttBusy && HasPttPort;
    private bool HasPttPort => !string.IsNullOrWhiteSpace(SelectedPttPort)
        && !string.Equals(SelectedPttPort, "N/A", StringComparison.OrdinalIgnoreCase);

    public void ApplyPttSnapshot(PttSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        IsPttOpen = snapshot.IsOpen;
        IsPttAsserted = snapshot.IsAsserted;
        IsPttBusy = snapshot.IsBusy;
        NotifyPttState();
    }

    public void RotateClockwise() => RotationDegrees = (RotationDegrees + 90) % 360;
    public void RotateCounterclockwise() => RotationDegrees = (RotationDegrees + 270) % 360;

    public async Task UpdatePreviewAsync(PrintJob job, TransmissionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(settings);
        var validation = CallSignValidator.Validate(settings.CallSign);
        if (!validation.IsValid)
        {
            _preparationCancellation?.Cancel();
            Interlocked.Increment(ref _preparationGeneration);
            Preview = null;
            IsReady = false;
            ValidationError = validation.Error;
            return;
        }

        _preparationCancellation?.Cancel();
        _preparationCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _preparationCancellation = cancellation;
        var generation = Interlocked.Increment(ref _preparationGeneration);
        IsReady = false;
        ValidationError = null;
        ApplyTransmissionState(JobState.Preparing, 0, null);
        try
        {
            var normalized = settings with { CallSign = validation.Normalized };
            var prepared = await preparationService.PrepareAsync(
                job, normalized, cancellation.Token);
            if (generation == Volatile.Read(ref _preparationGeneration))
            {
                Preview = prepared;
                IsReady = true;
                ApplyTransmissionState(JobState.Ready, 0, prepared.EstimatedDuration);
                NotifyActionState();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (generation == Volatile.Read(ref _preparationGeneration))
                ApplyTransmissionState(JobState.Stopped, 0, null);
        }
        catch (TransmissionValidationException exception)
        {
            if (generation == Volatile.Read(ref _preparationGeneration))
            {
                Preview = null;
                IsReady = false;
                ValidationError = exception.Message;
                ApplyTransmissionState(JobState.Failed, 0, null);
                NotifyActionState();
            }
        }
    }

    public void CancelPreparation()
    {
        if (!_canStopTransmission) return;
        _preparationCancellation?.Cancel();
        if (IsSettingsLocked) return;
        IsReady = false;
        ApplyTransmissionState(JobState.Stopped, 0, null);
    }

    public void ClearPreview()
    {
        _preparationCancellation?.Cancel();
        Interlocked.Increment(ref _preparationGeneration);
        Preview = null;
        IsReady = false;
        SelectedJob = null;
        IsSelectedJobActive = false;
        ApplyTransmissionState(JobState.Received, 0, null);
        NotifyActionState();
    }

    public void ApplyTransmissionState(JobState state, double progress, TimeSpan? remaining)
    {
        ProgressPercent = Math.Clamp(progress, 0, 1) * 100;
        IsAppVolumeActive = state == JobState.Transmitting;
        IsSettingsLocked = state is JobState.Encoding or JobState.Transmitting;
        SetProperty(ref _canStopTransmission, state is JobState.Preparing or JobState.Encoding or JobState.Transmitting, nameof(CanStop));
        StatusText = state switch
        {
            JobState.Encoding => "信号生成中",
            JobState.Preparing => "プレビュー生成中",
            JobState.Transmitting when remaining is { } value =>
                $"送信中 残り{(int)value.TotalMinutes:00}:{value.Seconds:00} ({Math.Clamp(progress, 0, 1) * 100:0.0}%)",
            JobState.Transmitting => $"送信中 ({Math.Clamp(progress, 0, 1) * 100:0.0}%)",
            JobState.Ready => "送信準備完了",
            JobState.Completed => "送信完了",
            JobState.Failed => "エラー",
            JobState.Stopped => "停止しました",
            _ => "待機中",
        };
        StatusBrush = state switch
        {
            JobState.Transmitting => "#FF3D21",
            JobState.Encoding => "#FF9F1C",
            JobState.Preparing => "#FF9F1C",
            JobState.Ready or JobState.Completed => "#80FF3D",
            _ => "#344050",
        };
        RemainingTimeText = null;
        UpdateScanLine(state, progress);
        RefreshAudioDisplay();
        NotifyActionState();
    }

    partial void OnPreviewChanged(PreparedTransmission? value)
    {
        OnPropertyChanged(nameof(PreviewWidth));
        OnPropertyChanged(nameof(PreviewHeight));
    }
    partial void OnPreviewBitmapChanged(Bitmap? value) => OnPropertyChanged(nameof(IsPreviewEmpty));
    partial void OnRotationDegreesChanged(int value) => OnPropertyChanged(nameof(RotationText));
    partial void OnSelectedAudioEndpointChanged(AudioEndpointInfo? value)
    {
        RefreshAudioDisplay();
        NotifyActionState();
    }
    partial void OnIsSelectedJobActiveChanged(bool value) => NotifyActionState();
    partial void OnSelectedJobChanged(PrintJob? value) => NotifyActionState();
    partial void OnSelectedPttPortChanged(string? value) => NotifyPttState();
    partial void OnIsSettingsLockedChanged(bool value) => NotifyPttState();

    private void NotifyPttState()
    {
        OnPropertyChanged(nameof(PttConnectionButtonText));
        OnPropertyChanged(nameof(PttConnectionBrush));
        OnPropertyChanged(nameof(PttButtonBrush));
        OnPropertyChanged(nameof(CanEditPttSettings));
        OnPropertyChanged(nameof(CanTogglePttConnection));
        OnPropertyChanged(nameof(CanPressPtt));
    }

    private void NotifyActionState()
    {
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(CanSaveWave));
        OnPropertyChanged(nameof(CanDiscard));
    }

    private void UpdateScanLine(JobState state, double progress)
    {
        if (state != JobState.Transmitting || Preview is not { } preview)
        {
            IsScanLineVisible = false;
            return;
        }

        var totalSeconds = preview.EstimatedDuration.TotalSeconds;
        var imageStartSeconds = TransmissionModes.IsRadiofax(preview.Options.Mode) ? 35.0 : 0.910;
        var imageEndSeconds = TransmissionModes.IsRadiofax(preview.Options.Mode)
            ? totalSeconds - 15.0
            : totalSeconds;
        var elapsedSeconds = Math.Clamp(progress, 0, 1) * totalSeconds;
        if (imageEndSeconds <= imageStartSeconds || elapsedSeconds < imageStartSeconds || elapsedSeconds >= imageEndSeconds)
        {
            IsScanLineVisible = false;
            return;
        }

        var imageProgress = (elapsedSeconds - imageStartSeconds) / (imageEndSeconds - imageStartSeconds);
        ScanLineY = imageProgress * Math.Max(0, preview.Frame.Height - 1);
        IsScanLineVisible = true;
    }

    public void RefreshAudioDisplay()
    {
        var endpoint = SelectedAudioEndpoint;
        VolumeText = endpoint?.VolumePercent is { } master ? $"{Math.Round(master):0}%" : "不明";
        MasterVolumePercent = endpoint?.VolumePercent ?? 0;
        AppVolumeText = !IsAppVolumeActive
            ? "待機中"
            : endpoint?.AppVolumePercent is { } app ? $"{Math.Round(app):0}%" : "検出中";
        AppVolumePercent = IsAppVolumeActive ? endpoint?.AppVolumePercent ?? 0 : 0;
        MuteWarning = endpoint?.IsMuted is true
            ? "MASTER MUTED"
            : IsAppVolumeActive && endpoint?.AppIsMuted is true
                ? "APP MUTED"
                : IsAppVolumeActive && endpoint?.AppVolumePercent is <= 5
                    ? "APP VOLUME LOW"
                    : null;
        DeviceError = endpoint is null ? "使用可能な音声出力デバイスがありません。" : null;
    }
}
