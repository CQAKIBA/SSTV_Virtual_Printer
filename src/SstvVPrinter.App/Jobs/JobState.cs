namespace SstvVPrinter.App.Jobs;

public enum JobState
{
    Received,
    Preparing,
    Ready,
    Encoding,
    Transmitting,
    Stopped,
    Completed,
    Failed,
}
