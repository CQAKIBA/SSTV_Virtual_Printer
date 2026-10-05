using SstvVPrinter.Imaging.Pwg;
using SstvVPrinter.IppProbe.Capture;
using SstvVPrinter.IppProbe.Ipp;

namespace SstvVPrinter.App.Jobs;

public sealed class PwgPrintJobSink : IPrintJobSink
{
    private readonly PrintJobQueue _queue;
    private readonly PwgRasterDecoder _decoder;
    private readonly Func<DateTimeOffset> _clock;

    public PwgPrintJobSink(
        PrintJobQueue queue,
        PwgRasterDecoder decoder,
        Func<DateTimeOffset>? clock = null)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async ValueTask AcceptAsync(
        ProbeJob job,
        Stream document,
        string documentFormat,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(document);
        if (!string.Equals(documentFormat, "image/pwg-raster", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Unsupported print document format: {documentFormat}");
        }

        var page = await _decoder.DecodeFirstPageAsync(document, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _queue.Enqueue(new PrintJob(Guid.NewGuid(), job.JobName ?? job.UserName, _clock(), page));
    }
}
