using SstvVPrinter.IppProbe.Ipp;

namespace SstvVPrinter.IppProbe.Capture;

public interface IPrintJobSink
{
    ValueTask AcceptAsync(
        ProbeJob job,
        Stream document,
        string documentFormat,
        CancellationToken cancellationToken);
}
