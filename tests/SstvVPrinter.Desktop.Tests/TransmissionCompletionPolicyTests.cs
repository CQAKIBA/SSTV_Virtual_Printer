using SstvVPrinter.App.Jobs;
using SstvVPrinter.Desktop.Hosting;
using SstvVPrinter.Imaging.Model;

namespace SstvVPrinter.Desktop.Tests;

public sealed class TransmissionCompletionPolicyTests
{
    [Fact]
    public void Successful_completion_retains_active_job_when_deletion_is_disabled()
    {
        var queue = QueueWithOneJob();

        Apply(queue, deleteAfterSend: false);

        Assert.NotNull(queue.Active);
    }

    [Fact]
    public void Successful_completion_removes_active_job_when_deletion_is_enabled()
    {
        var queue = QueueWithOneJob();

        Apply(queue, deleteAfterSend: true);

        Assert.Null(queue.Active);
    }

    private static PrintJobQueue QueueWithOneJob()
    {
        var queue = new PrintJobQueue();
        queue.Enqueue(new(
            Guid.NewGuid(), "test", DateTimeOffset.UnixEpoch,
            DecodedPage.Create(1, 1, [0, 0, 0], new(1, 1, 1, 1, 300, 300, 0, 1, 1))));
        return queue;
    }

    private static void Apply(PrintJobQueue queue, bool deleteAfterSend)
    {
        var type = typeof(DesktopComposition).Assembly.GetType(
            "SstvVPrinter.Desktop.Hosting.TransmissionCompletionPolicy");
        Assert.NotNull(type);
        var method = type.GetMethod("Apply");
        Assert.NotNull(method);
        method.Invoke(null, [queue, deleteAfterSend]);
    }
}
