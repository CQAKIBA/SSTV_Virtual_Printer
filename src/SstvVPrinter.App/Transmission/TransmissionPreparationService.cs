using SstvVPrinter.App.Jobs;
using SstvVPrinter.Imaging.Composition;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.App.Transmission;

public interface ITransmissionPreparationService
{
    Task<PreparedTransmission> PrepareAsync(
        PrintJob job,
        TransmissionSettings settings,
        CancellationToken cancellationToken);
}

public sealed class TransmissionPreparationService(SstvFrameComposer composer)
    : ITransmissionPreparationService
{
    public Task<PreparedTransmission> PrepareAsync(
        PrintJob job,
        TransmissionSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(settings);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var headerTime = job.AcceptedAt.ToOffset(TimeSpan.FromHours(9));
            var request = new CompositionRequest(
                settings.Mode,
                settings.HorizontalSampleMultiplier,
                settings.CallSign,
                headerTime);
            var frame = composer.Compose(job.Page, request, cancellationToken);
            var options = new SstvEncodeOptions(settings.Mode, settings.HorizontalSampleMultiplier);
            return new PreparedTransmission(
                frame,
                options,
                SstvDurationEstimator.Estimate(options),
                headerTime);
        }, cancellationToken);
    }
}
