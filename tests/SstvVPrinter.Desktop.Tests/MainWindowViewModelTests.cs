using SstvVPrinter.App.Jobs;
using SstvVPrinter.App.Audio;
using SstvVPrinter.App.Transmission;
using SstvVPrinter.Desktop.ViewModels;
using SstvVPrinter.Imaging.Model;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Desktop.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Rotation_buttons_cycle_through_canonical_angles()
    {
        var viewModel = new MainWindowViewModel(new ControlledPreparationService());

        viewModel.RotateCounterclockwise();
        Assert.Equal(270, viewModel.RotationDegrees);
        Assert.Equal("270°", viewModel.RotationText);

        viewModel.RotateClockwise();
        Assert.Equal(0, viewModel.RotationDegrees);
        viewModel.RotateClockwise();
        Assert.Equal(90, viewModel.RotationDegrees);
    }

    [Fact]
    public async Task Invalid_call_sign_never_becomes_ready()
    {
        var service = new ControlledPreparationService();
        var viewModel = new MainWindowViewModel(service);

        await viewModel.UpdatePreviewAsync(
            CreateJob(), new TransmissionSettings("", SstvMode.Scottie2, 1));

        Assert.False(viewModel.IsReady);
        Assert.Null(viewModel.Preview);
        Assert.NotNull(viewModel.ValidationError);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task New_settings_cancel_stale_preparation_and_only_newest_becomes_preview()
    {
        var service = new ControlledPreparationService();
        var viewModel = new MainWindowViewModel(service);
        var job = CreateJob();

        Task first = viewModel.UpdatePreviewAsync(
            job, new TransmissionSettings("JA1UMW", SstvMode.Scottie2, 1));
        Task second = viewModel.UpdatePreviewAsync(
            job, new TransmissionSettings("JA1UMW", SstvMode.Scottie2, 2));

        Assert.True(service.Requests[0].CancellationToken.IsCancellationRequested);
        service.Complete(1, 640, 256);
        await second;
        service.Complete(0, 320, 256);
        await first;

        Assert.True(viewModel.IsReady);
        Assert.Equal(640, viewModel.Preview!.Frame.Width);
        Assert.Equal(2, viewModel.Preview.Options.HorizontalSampleMultiplier);
    }

    [Fact]
    public async Task Ready_preview_enables_send_save_and_discard_but_not_stop()
    {
        var service = new ControlledPreparationService();
        var viewModel = new MainWindowViewModel(service);
        Task update = viewModel.UpdatePreviewAsync(CreateJob(), new TransmissionSettings("JA1UMW", SstvMode.Scottie2, 1));
        service.Complete(0, 320, 256);
        await update;
        viewModel.SelectedAudioEndpoint = new AudioEndpointInfo("test", "Test", true, 50, false);

        Assert.True(viewModel.CanSend);
        Assert.True(viewModel.CanSaveWave);
        Assert.True(viewModel.CanDiscard);
        Assert.False(viewModel.CanStop);
        Assert.False(viewModel.IsSettingsLocked);
    }

    [Fact]
    public async Task Ready_preview_without_audio_endpoint_blocks_send_but_allows_wave_save()
    {
        var service = new ControlledPreparationService();
        var viewModel = new MainWindowViewModel(service);
        Task update = viewModel.UpdatePreviewAsync(CreateJob(), new TransmissionSettings("JA1UMW", SstvMode.Scottie2, 1));
        service.Complete(0, 320, 256);
        await update;

        Assert.False(viewModel.CanSend);
        Assert.True(viewModel.CanSaveWave);
    }

    [Fact]
    public async Task Waiting_job_preview_cannot_send_or_save_ahead_of_fifo_active_job()
    {
        var service = new ControlledPreparationService();
        var viewModel = new MainWindowViewModel(service)
        {
            SelectedAudioEndpoint = new AudioEndpointInfo("test", "Test", true, 50, false),
            IsSelectedJobActive = false,
        };
        Task update = viewModel.UpdatePreviewAsync(CreateJob(), new TransmissionSettings("JA1UMW", SstvMode.Scottie2, 1));
        service.Complete(0, 320, 256);
        await update;

        Assert.False(viewModel.CanSend);
        Assert.False(viewModel.CanSaveWave);
        Assert.True(viewModel.CanDiscard);
    }

    [Fact]
    public async Task Empty_queue_clear_removes_stale_preview_and_disables_all_actions()
    {
        var service = new ControlledPreparationService();
        var viewModel = new MainWindowViewModel(service)
        {
            SelectedAudioEndpoint = new AudioEndpointInfo("test", "Test", true, 50, false),
        };
        Task update = viewModel.UpdatePreviewAsync(CreateJob(), new TransmissionSettings("JA1UMW", SstvMode.Scottie2, 1));
        service.Complete(0, 320, 256);
        await update;

        viewModel.ClearPreview();

        Assert.Null(viewModel.Preview);
        Assert.False(viewModel.CanSend);
        Assert.False(viewModel.CanSaveWave);
        Assert.False(viewModel.CanDiscard);
    }

    [Fact]
    public void Stop_cancels_preview_preparation()
    {
        var service = new ControlledPreparationService();
        var viewModel = new MainWindowViewModel(service);
        _ = viewModel.UpdatePreviewAsync(CreateJob(), new TransmissionSettings("JA1UMW", SstvMode.Scottie2, 1));

        viewModel.CancelPreparation();

        Assert.True(service.Requests[0].CancellationToken.IsCancellationRequested);
        Assert.Equal("停止しました", viewModel.StatusText);
    }

    [Fact]
    public void Transmitting_locks_settings_and_exposes_stop_and_remaining_time()
    {
        var viewModel = new MainWindowViewModel(new ControlledPreparationService());

        viewModel.ApplyTransmissionState(JobState.Transmitting, 0.25, TimeSpan.FromSeconds(90));

        Assert.True(viewModel.IsSettingsLocked);
        Assert.True(viewModel.CanStop);
        Assert.False(viewModel.CanSend);
        Assert.Equal("送信中", viewModel.StatusText);
        Assert.Equal("残り 01:30", viewModel.RemainingTimeText);
    }

    [Fact]
    public void Preparing_keeps_settings_editable_but_disables_actions()
    {
        var service = new ControlledPreparationService();
        var viewModel = new MainWindowViewModel(service)
        {
            SelectedAudioEndpoint = new AudioEndpointInfo("test", "Test", true, 50, false),
        };

        _ = viewModel.UpdatePreviewAsync(
            CreateJob(), new TransmissionSettings("JA1UMW", SstvMode.Scottie2, 1));

        Assert.Equal("プレビュー生成中", viewModel.StatusText);
        Assert.False(viewModel.IsSettingsLocked);
        Assert.False(viewModel.CanSend);
        Assert.False(viewModel.CanSaveWave);
    }

    [Fact]
    public void Application_volume_is_greyed_while_idle_and_visible_while_transmitting()
    {
        var viewModel = new MainWindowViewModel(new ControlledPreparationService())
        {
            SelectedAudioEndpoint = new AudioEndpointInfo("test", "Test", true, 80, false)
            {
                AppVolumePercent = 35,
                AppIsMuted = false,
            },
        };

        viewModel.ApplyTransmissionState(JobState.Ready, 0, null);
        Assert.False(viewModel.IsAppVolumeActive);
        Assert.Equal("待機中", viewModel.AppVolumeText);

        viewModel.ApplyTransmissionState(JobState.Transmitting, 0, null);
        Assert.True(viewModel.IsAppVolumeActive);
        Assert.Equal("35%", viewModel.AppVolumeText);
        Assert.Equal(35, viewModel.AppVolumePercent);
    }

    [Fact]
    public void Muted_application_session_warns_only_during_transmission()
    {
        var viewModel = new MainWindowViewModel(new ControlledPreparationService())
        {
            SelectedAudioEndpoint = new AudioEndpointInfo("test", "Test", true, 80, false)
            {
                AppVolumePercent = 25,
                AppIsMuted = true,
            },
        };

        viewModel.ApplyTransmissionState(JobState.Ready, 0, null);
        Assert.Null(viewModel.MuteWarning);

        viewModel.ApplyTransmissionState(JobState.Transmitting, 0, null);
        Assert.Equal("APP MUTED", viewModel.MuteWarning);
    }

    [Fact]
    public void Low_application_volume_uses_compact_english_warning()
    {
        var viewModel = new MainWindowViewModel(new ControlledPreparationService())
        {
            SelectedAudioEndpoint = new AudioEndpointInfo("test", "Test", true, 80, false)
            {
                AppVolumePercent = 5,
                AppIsMuted = false,
            },
        };

        viewModel.ApplyTransmissionState(JobState.Transmitting, 0, null);

        Assert.Equal("APP VOLUME LOW", viewModel.MuteWarning);
    }

    private static PrintJob CreateJob() =>
        new(
            Guid.NewGuid(), "Paint", DateTimeOffset.UtcNow,
            DecodedPage.Create(
                1, 1, [1, 2, 3],
                new PwgPageMetadata(1, 1, 1, 1, 300, 300, 0, 1, 1)));

    private sealed class ControlledPreparationService : ITransmissionPreparationService
    {
        public List<Request> Requests { get; } = [];

        public Task<PreparedTransmission> PrepareAsync(
            PrintJob job,
            TransmissionSettings settings,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<PreparedTransmission>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Requests.Add(new(settings, cancellationToken, completion));
            return completion.Task;
        }

        public void Complete(int index, int width, int height)
        {
            var request = Requests[index];
            var options = new SstvEncodeOptions(
                request.Settings.Mode, request.Settings.HorizontalSampleMultiplier);
            request.Completion.SetResult(new(
                RgbFrame.Create(width, height, new byte[width * height * 3]),
                options,
                TimeSpan.FromSeconds(72),
                DateTimeOffset.UtcNow));
        }

        public sealed record Request(
            TransmissionSettings Settings,
            CancellationToken CancellationToken,
            TaskCompletionSource<PreparedTransmission> Completion);
    }
}
