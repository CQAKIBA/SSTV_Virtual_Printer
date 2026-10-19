using SstvVPrinter.App.Jobs;
using SstvVPrinter.Imaging.Composition;
using SstvVPrinter.Imaging.Transform;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.App.Transmission;

public interface ITransmissionPreparationService
{
    Task<PreparedTransmission> PrepareAsync(
        PrintJob job,
        TransmissionSettings settings,
        CancellationToken cancellationToken);
}

public sealed class TransmissionPreparationService : ITransmissionPreparationService
{
    private readonly TransmissionFrameComposer _composer;

    public TransmissionPreparationService(TransmissionFrameComposer composer)
    {
        _composer = composer ?? throw new ArgumentNullException(nameof(composer));
    }

    public TransmissionPreparationService(SstvFrameComposer composer)
        : this(new TransmissionFrameComposer(composer, new RadiofaxFrameComposer()))
    {
    }

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
            var multiplier = TransmissionModes.IsRadiofax(settings.Mode)
                ? 1
                : settings.HorizontalSampleMultiplier;
            var request = new CompositionRequest(
                settings.Mode,
                multiplier,
                settings.CallSign,
                headerTime);
            var page = PageRotator.Rotate(job.Page, settings.RotationDegrees, cancellationToken);
            RgbFrame frame;
            try
            {
                frame = _composer.Compose(page, request, cancellationToken);
            }
            catch (RadiofaxPageShapeException exception)
            {
                throw new TransmissionValidationException(exception.Message, exception);
            }

            var options = new TransmissionEncodeOptions(settings.Mode, multiplier);
            return new PreparedTransmission(
                frame,
                options,
                TransmissionDurationEstimator.Estimate(frame, options),
                headerTime);
        }, cancellationToken);
    }
}
