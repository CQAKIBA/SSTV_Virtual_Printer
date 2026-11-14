using SstvVPrinter.App.Jobs;
using SstvVPrinter.App.Audio;
using SstvVPrinter.App.Transmission;
using SstvVPrinter.Desktop.ViewModels;
using SstvVPrinter.Desktop.Ptt;
using SstvVPrinter.Imaging.Model;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Desktop.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Ptt_requires_a_selected_port_and_keeps_closed_selection_separate_from_snapshot()
    {
        var viewModel = new MainWindowViewModel(new ControlledPreparationService());

        Assert.Equal(["N/A"], viewModel.PttPorts);
        Assert.Equal("N/A", viewModel.SelectedPttPort);
        Assert.False(viewModel.CanTogglePttConnection);
        Assert.False(viewModel.CanPressPtt);
        Assert.Equal([PttControlMethod.Rts, PttControlMethod.Dtr], viewModel.PttMethods);
        viewModel.SelectedPttPort = "COM4";
        viewModel.SelectedPttMethod = PttControlMethod.Dtr;
        viewModel.ApplyPttSnapshot(new(null, PttControlMethod.Rts, false, false, false, null));

        Assert.Equal("COM4", viewModel.SelectedPttPort);
        Assert.Equal(PttControlMethod.Dtr, viewModel.SelectedPttMethod);
        Assert.Equal("OPEN", viewModel.PttConnectionButtonText);
        Assert.Equal("#344050", viewModel.PttConnectionBrush);
        Assert.True(viewModel.CanTogglePttConnection);
        Assert.True(viewModel.CanPressPtt);
        Assert.True(viewModel.CanEditPttSettings);
    }

    [Fact]
    public void Ptt_snapshot_updates_connection_assertion_and_every_dependent_binding()
    {
        var viewModel = new MainWindowViewModel(new ControlledPreparationService()) { SelectedPttPort = "COM4" };
        var notified = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notified.Add(args.PropertyName);

        viewModel.ApplyPttSnapshot(new("COM4", PttControlMethod.Rts, true, true, false, null));

        Assert.True(viewModel.IsPttOpen);
        Assert.True(viewModel.IsPttAsserted);
        Assert.False(viewModel.IsPttBusy);
        Assert.Equal("CLOSE", viewModel.PttConnectionButtonText);
        Assert.Equal("#FF3D21", viewModel.PttConnectionBrush);
        Assert.Equal("#FF3D21", viewModel.PttButtonBrush);
        Assert.False(viewModel.CanEditPttSettings);
        Assert.False(viewModel.CanTogglePttConnection);
        foreach (var name in new[] { "PttConnectionButtonText", "PttConnectionBrush", "PttButtonBrush", "CanEditPttSettings", "CanTogglePttConnection", "CanPressPtt" })
            Assert.Contains(name, notified);
    }

    [Fact]
    public void Sending_locks_ptt_controls_while_preserving_visible_connection_state()
    {
        var viewModel = new MainWindowViewModel(new ControlledPreparationService()) { SelectedPttPort = "COM4" };
        viewModel.ApplyPttSnapshot(new("COM4", PttControlMethod.Rts, true, false, false, null));
        Assert.True(viewModel.CanTogglePttConnection);
        Assert.True(viewModel.CanPressPtt);

        viewModel.ApplyTransmissionState(JobState.Transmitting, 0, null);

        Assert.True(viewModel.IsPttOpen);
        Assert.Equal("CLOSE", viewModel.PttConnectionButtonText);
        Assert.Equal("#FF3D21", viewModel.PttConnectionBrush);
        Assert.False(viewModel.CanEditPttSettings);
        Assert.False(viewModel.CanTogglePttConnection);
        Assert.False(viewModel.CanPressPtt);
        viewModel.ApplyTransmissionState(JobState.Stopped, 0, null);
        Assert.True(viewModel.CanTogglePttConnection);
        Assert.True(viewModel.CanPressPtt);
    }

    [Fact]
    public void Automatic_ptt_busy_disables_close_and_manual_ptt_without_hiding_assertion()
    {
        var viewModel = new MainWindowViewModel(new ControlledPreparationService()) { SelectedPttPort = "COM4" };

        viewModel.ApplyPttSnapshot(new("COM4", PttControlMethod.Rts, true, true, true, null));

        Assert.True(viewModel.IsPttBusy);
        Assert.Equal("#FF3D21", viewModel.PttButtonBrush);
        Assert.False(viewModel.CanTogglePttConnection);
        Assert.False(viewModel.CanPressPtt);
        Assert.False(viewModel.CanEditPttSettings);
    }

    [Fact]
    public void Radiofax_modes_follow_existing_sstv_modes_and_force_effective_one_x()
    {
        var viewModel = new MainWindowViewModel(new ControlledPreparationService())
        {
            IsDoubleHorizontal = true,
        };

        Assert.Equal(
            [SstvMode.Scottie2, SstvMode.Scottie1, SstvMode.Martin1, SstvMode.Robot36,
                (SstvMode)4, (SstvMode)5, (SstvMode)6, (SstvMode)7],
            viewModel.AvailableModes);
        viewModel.SelectedMode = (SstvMode)4;
        Assert.True(viewModel.IsRadiofaxMode);
        Assert.False(viewModel.IsHorizontalMultiplierEnabled);
        Assert.False(viewModel.IsDoubleHorizontal);
        Assert.Equal(2, viewModel.StoredHorizontalSampleMultiplier);

        viewModel.SelectedMode = SstvMode.Scottie2;
        Assert.False(viewModel.IsRadiofaxMode);
        Assert.True(viewModel.IsHorizontalMultiplierEnabled);
        Assert.True(viewModel.IsDoubleHorizontal);
    }

    [Fact]
    public void Header_overlay_choice_is_remembered_independently_for_each_mode()
    {
        var viewModel = new MainWindowViewModel(new ControlledPreparationService());
        var property = typeof(MainWindowViewModel).GetProperty("IsHeaderOverlay");
        Assert.NotNull(property);

        property.SetValue(viewModel, true);
        viewModel.SelectedMode = SstvMode.Martin1;
        Assert.False(Assert.IsType<bool>(property.GetValue(viewModel)));
        property.SetValue(viewModel, true);
        viewModel.SelectedMode = SstvMode.Scottie2;
        Assert.True(Assert.IsType<bool>(property.GetValue(viewModel)));
    }

    [Fact]
    public async Task Recoverable_radiofax_validation_keeps_discard_and_settings_available()
    {
        var service = new ControlledPreparationService();
        var job = CreateJob();
        var viewModel = new MainWindowViewModel(service)
        {
            SelectedJob = job,
            IsSelectedJobActive = true,
        };
        var update = viewModel.UpdatePreviewAsync(
            job, new TransmissionSettings("JA1UMW", SstvMode.Radiofax120Ioc288Sync20Ms, 1));
        service.Fail(0, new TransmissionValidationException("画像比率は1:2から2:1までです。"));

        await update;

        Assert.False(viewModel.IsReady);
        Assert.False(viewModel.CanSend);
        Assert.False(viewModel.CanSaveWave);
        Assert.True(viewModel.CanDiscard);
        Assert.Contains("1:2", viewModel.ValidationError, StringComparison.Ordinal);
        Assert.False(viewModel.IsSettingsLocked);
    }

    [Fact]
    public async Task Stale_valid_generation_cannot_replace_newer_invalid_state()
    {
        var service = new ControlledPreparationService();
        var viewModel = new MainWindowViewModel(service) { SelectedJob = CreateJob() };
        var first = viewModel.UpdatePreviewAsync(
            viewModel.SelectedJob, new TransmissionSettings("JA1UMW", SstvMode.Scottie2, 1));
        var second = viewModel.UpdatePreviewAsync(
            viewModel.SelectedJob, new TransmissionSettings("JA1UMW", SstvMode.Radiofax120Ioc288Sync20Ms, 1));
        service.Fail(1, new TransmissionValidationException("画像比率は1:2から2:1までです。"));
        await second;
        service.Complete(0, 320, 256);
        await first;

        Assert.False(viewModel.IsReady);
        Assert.Null(viewModel.Preview);
        Assert.Contains("1:2", viewModel.ValidationError, StringComparison.Ordinal);
    }

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
    public void Transmitting_locks_settings_and_combines_remaining_time_with_percentage()
    {
        var viewModel = new MainWindowViewModel(new ControlledPreparationService());

        viewModel.ApplyTransmissionState(JobState.Transmitting, 0.25, TimeSpan.FromSeconds(90));

        Assert.True(viewModel.IsSettingsLocked);
        Assert.True(viewModel.CanStop);
        Assert.False(viewModel.CanSend);
        Assert.Equal("送信中 残り01:30 (25.0%)", viewModel.StatusText);
        Assert.Null(viewModel.RemainingTimeText);
    }

    [Fact]
    public async Task Stopping_transmission_keeps_prepared_job_ready_for_send_and_wave_save()
    {
        var service = new ControlledPreparationService();
        var viewModel = new MainWindowViewModel(service)
        {
            SelectedAudioEndpoint = new AudioEndpointInfo("test", "Test", true, 50, false),
        };
        Task update = viewModel.UpdatePreviewAsync(CreateJob(), new TransmissionSettings("JA1UMW", SstvMode.Scottie2, 1));
        service.Complete(0, 320, 256);
        await update;
        viewModel.ApplyTransmissionState(JobState.Transmitting, 0.25, TimeSpan.FromSeconds(90));

        viewModel.CancelPreparation();
        viewModel.ApplyTransmissionState(JobState.Stopped, 0, null);

        Assert.True(viewModel.IsReady);
        Assert.True(viewModel.CanSend);
        Assert.True(viewModel.CanSaveWave);
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

    [Fact]
    public async Task Radiofax_scan_line_is_visible_only_during_image_section()
    {
        var service = new ControlledPreparationService();
        var viewModel = new MainWindowViewModel(service);
        Task update = viewModel.UpdatePreviewAsync(
            CreateJob(), new TransmissionSettings("JA1UMW", SstvMode.Radiofax120Ioc288Sync20Ms, 1));
        service.Complete(0, 864, 432, TimeSpan.FromSeconds(266));
        await update;
        var visible = typeof(MainWindowViewModel).GetProperty("IsScanLineVisible");
        var y = typeof(MainWindowViewModel).GetProperty("ScanLineY");
        Assert.NotNull(visible);
        Assert.NotNull(y);

        viewModel.ApplyTransmissionState(JobState.Transmitting, 34.0 / 266, TimeSpan.FromSeconds(232));
        Assert.False(Assert.IsType<bool>(visible.GetValue(viewModel)));

        viewModel.ApplyTransmissionState(JobState.Transmitting, 143.0 / 266, TimeSpan.FromSeconds(123));
        Assert.True(Assert.IsType<bool>(visible.GetValue(viewModel)));
        Assert.InRange(Assert.IsType<double>(y.GetValue(viewModel)), 215, 216);

        viewModel.ApplyTransmissionState(JobState.Transmitting, 251.0 / 266, TimeSpan.FromSeconds(15));
        Assert.False(Assert.IsType<bool>(visible.GetValue(viewModel)));
    }

    [Fact]
    public async Task Double_horizontal_preview_uses_corrected_height_for_scan_line()
    {
        var service = new ControlledPreparationService();
        var model = new MainWindowViewModel(service);
        var update = model.UpdatePreviewAsync(
            CreateJob(), new TransmissionSettings("JA1UMW", SstvMode.Scottie2, 2));
        service.Complete(0, 640, 256);
        await update;
        Assert.Equal(640, model.PreviewWidth);
        Assert.Equal(512, model.PreviewHeight);
        model.ApplyTransmissionState(JobState.Transmitting, (72 + 0.910) / 144, null);
        Assert.True(model.IsScanLineVisible);
        Assert.InRange(model.ScanLineY, 254, 257);
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

        public void Complete(int index, int width, int height, TimeSpan? duration = null)
        {
            var request = Requests[index];
            var options = new TransmissionEncodeOptions(
                request.Settings.Mode, request.Settings.HorizontalSampleMultiplier);
            request.Completion.SetResult(new(
                RgbFrame.Create(width, height, new byte[width * height * 3]),
                options,
                duration ?? TimeSpan.FromSeconds(72),
                DateTimeOffset.UtcNow));
        }

        public void Fail(int index, Exception exception) => Requests[index].Completion.SetException(exception);

        public sealed record Request(
            TransmissionSettings Settings,
            CancellationToken CancellationToken,
            TaskCompletionSource<PreparedTransmission> Completion);
    }
}
