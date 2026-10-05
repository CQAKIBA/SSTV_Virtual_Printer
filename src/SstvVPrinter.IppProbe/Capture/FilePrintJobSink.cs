using SstvVPrinter.IppProbe.Ipp;

namespace SstvVPrinter.IppProbe.Capture;

public sealed class FilePrintJobSink(PrintJobCapture capture) : IPrintJobSink
{
    public async ValueTask AcceptAsync(
        ProbeJob job,
        Stream document,
        string documentFormat,
        CancellationToken cancellationToken)
    {
        CapturedJob captured = await capture.CaptureAsync(
            job, document, documentFormat, cancellationToken);
        await using var payload = File.OpenRead(captured.PayloadPath);
        PwgRasterReport report = await PwgRasterInspector.InspectAsync(payload, cancellationToken);
        Console.WriteLine(
            $"Job {captured.JobId}: {captured.ByteLength} bytes SHA-256 {captured.Sha256}; " +
            $"PWG={report.IsPwgRaster} {report.Width}x{report.Height} " +
            $"{report.XResolution}x{report.YResolution} dpi; orientation={report.Orientation} " +
            $"landscape={report.IsLandscape} page={report.PageWidthPoints}x{report.PageHeightPoints}pt");
    }
}
