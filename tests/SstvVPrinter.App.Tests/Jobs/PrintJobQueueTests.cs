using SstvVPrinter.App.Jobs;
using SstvVPrinter.Imaging.Model;

namespace SstvVPrinter.App.Tests.Jobs;

public sealed class PrintJobQueueTests
{
    [Fact]
    public void First_enqueue_becomes_active()
    {
        var queue = new PrintJobQueue();
        var job = CreateJob("first");

        queue.Enqueue(job);

        Assert.Equal(job.Id, queue.Active?.Id);
        Assert.Equal(job.Id, queue.Selected?.Id);
        Assert.Empty(queue.Waiting);
    }

    [Fact]
    public void Later_jobs_wait_in_fifo_order()
    {
        var queue = new PrintJobQueue();
        var first = CreateJob("first");
        var second = CreateJob("second");
        var third = CreateJob("third");

        queue.Enqueue(first);
        queue.Enqueue(second);
        queue.Enqueue(third);

        Assert.Equal([second.Id, third.Id], queue.Waiting.Select(job => job.Id));
    }

    [Fact]
    public void Complete_and_discard_advance_to_next_job()
    {
        var queue = new PrintJobQueue();
        var first = CreateJob("first");
        var second = CreateJob("second");
        var third = CreateJob("third");
        queue.Enqueue(first);
        queue.Enqueue(second);
        queue.Enqueue(third);

        queue.CompleteActive();
        Assert.Equal(second.Id, queue.Active?.Id);

        Assert.True(queue.Discard(second.Id));
        Assert.Equal(third.Id, queue.Active?.Id);
        Assert.Empty(queue.Waiting);
    }

    [Fact]
    public void Select_waiting_job_changes_inspection_without_reordering()
    {
        var queue = new PrintJobQueue();
        var first = CreateJob("first");
        var second = CreateJob("second");
        var third = CreateJob("third");
        queue.Enqueue(first);
        queue.Enqueue(second);
        queue.Enqueue(third);

        Assert.True(queue.Select(third.Id));

        Assert.Equal(first.Id, queue.Active?.Id);
        Assert.Equal(third.Id, queue.Selected?.Id);
        Assert.Equal([second.Id, third.Id], queue.Waiting.Select(job => job.Id));
    }

    [Fact]
    public void Arrival_during_transmitting_does_not_replace_active_or_preview()
    {
        var queue = new PrintJobQueue();
        var active = CreateJob("active");
        queue.Enqueue(active);
        queue.SetActiveState(JobState.Transmitting);

        var waiting = CreateJob("waiting");
        queue.Enqueue(waiting);

        Assert.Equal(active.Id, queue.Active?.Id);
        Assert.Equal(JobState.Transmitting, queue.Active?.State);
        Assert.Equal(active.Id, queue.Selected?.Id);
        Assert.Equal(waiting.Id, Assert.Single(queue.Waiting).Id);
    }

    [Fact]
    public void Changed_publishes_immutable_snapshot()
    {
        var queue = new PrintJobQueue();
        PrintJobQueueSnapshot? observed = null;
        queue.Changed += snapshot => observed = snapshot;

        queue.Enqueue(CreateJob("first"));
        queue.Enqueue(CreateJob("second"));

        Assert.NotNull(observed);
        Assert.Single(observed.Waiting);
        Assert.IsType<System.Collections.Immutable.ImmutableArray<PrintJob>>(observed.Waiting);
    }

    private static PrintJob CreateJob(string sourceName) =>
        new(
            Guid.NewGuid(),
            sourceName,
            new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.FromHours(9)),
            DecodedPage.Create(
                1,
                1,
                [1, 2, 3],
                new PwgPageMetadata(1, 1, 1, 1, 300, 300, 0, 1, 1)));
}
