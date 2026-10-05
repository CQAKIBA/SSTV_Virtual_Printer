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
