using SstvVPrinter.Imaging.Model;

namespace SstvVPrinter.App.Jobs;

public sealed record PrintJob(
    Guid Id,
    string? SourceName,
    DateTimeOffset AcceptedAt,
    DecodedPage Page)
{
    public JobState State { get; init; } = JobState.Received;
}
