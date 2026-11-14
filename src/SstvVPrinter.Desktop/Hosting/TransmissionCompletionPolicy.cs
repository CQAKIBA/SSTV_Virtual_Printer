using SstvVPrinter.App.Jobs;

namespace SstvVPrinter.Desktop.Hosting;

public static class TransmissionCompletionPolicy
{
    public static void Apply(PrintJobQueue queue, bool deleteAfterSend)
    {
        ArgumentNullException.ThrowIfNull(queue);
        if (deleteAfterSend) queue.CompleteActive();
    }
}
