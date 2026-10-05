using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media.Imaging;
using SstvVPrinter.App.Audio;
using SstvVPrinter.App.Jobs;
using SstvVPrinter.App.Transmission;
using SstvVPrinter.App.Validation;
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

    [ObservableProperty]
    private SstvMode _selectedMode = SstvMode.Scottie2;

    [ObservableProperty]
    private bool _isDoubleHorizontal;

    [ObservableProperty]
    private IReadOnlyList<AudioEndpointInfo> _audioEndpoints = [];

    [ObservableProperty]
    private AudioEndpointInfo? _selectedAudioEndpoint;

    [ObservableProperty]
    private string _volumeText = "不明";

    [ObservableProperty]
    private string? _muteWarning;

    [ObservableProperty]
    private IReadOnlyList<PrintJob> _waitingJobs = [];

    [ObservableProperty]
    private Bitmap? _previewBitmap;

    [ObservableProperty]
    private string _statusText = "待機中";

    [ObservableProperty]
    private string _statusBrush = "#344050";

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string? _remainingTimeText;

    [ObservableProperty]
    private bool _isSettingsLocked;

    [ObservableProperty]
    private bool _canStop;

    [ObservableProperty]
    private bool _startAtSignIn = true;

    public IReadOnlyList<SstvMode> AvailableModes { get; } = [SstvMode.Scottie2, SstvMode.Scottie1, SstvMode.Martin1, SstvMode.Robot36];
    public bool IsPreviewEmpty => PreviewBitmap is null;
    public bool CanSend => IsReady && !IsSettingsLocked && SelectedAudioEndpoint?.IsAvailable is not false;
    public bool CanSaveWave => IsReady && !IsSettingsLocked;
    public bool CanDiscard => Preview is not null && !IsSettingsLocked;

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
        }
    }

    public void ApplyTransmissionState(JobState state, double progress, TimeSpan? remaining)
    {
        ProgressPercent = Math.Clamp(progress, 0, 1) * 100;
        IsSettingsLocked = state is JobState.Encoding or JobState.Transmitting;
        CanStop = IsSettingsLocked;
        StatusText = state switch
        {
            JobState.Encoding => "信号生成中",
            JobState.Transmitting => "送信中",
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
            JobState.Ready or JobState.Completed => "#80FF3D",
            _ => "#344050",
        };
        RemainingTimeText = remaining is { } value
            ? $"残り {(int)value.TotalMinutes:00}:{value.Seconds:00}"
            : null;
        NotifyActionState();
    }

    partial void OnPreviewBitmapChanged(Bitmap? value) => OnPropertyChanged(nameof(IsPreviewEmpty));
    partial void OnSelectedAudioEndpointChanged(AudioEndpointInfo? value) => NotifyActionState();

    private void NotifyActionState()
    {
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(CanSaveWave));
        OnPropertyChanged(nameof(CanDiscard));
    }
}
