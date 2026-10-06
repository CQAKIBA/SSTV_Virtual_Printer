namespace SstvVPrinter.IppProbe.Capture;

public sealed record CaptureOptions(string DirectoryPath)
{
    public const long MaximumDocumentBytes = 64L * 1024 * 1024;
}
