namespace SstvVPrinter.IppProbe.Capture;

public sealed record CapturedJob(int JobId, string UserName, string? JobName, string DocumentFormat, long ByteLength, string Sha256, DateTimeOffset ReceivedUtc, string PayloadPath, string MetadataPath);
