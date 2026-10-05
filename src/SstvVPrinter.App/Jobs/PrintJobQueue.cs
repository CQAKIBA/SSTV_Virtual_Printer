using System.Collections.Immutable;

namespace SstvVPrinter.App.Jobs;

public sealed record PrintJobQueueSnapshot(
    PrintJob? Active,
    PrintJob? Selected,
    ImmutableArray<PrintJob> Waiting);

public sealed class PrintJobQueue
{
    private readonly Lock _gate = new();
    private readonly List<PrintJob> _jobs = [];
    private Guid? _selectedId;

    public event Action<PrintJobQueueSnapshot>? Changed;

    public PrintJob? Active
    {
        get
        {
            lock (_gate)
            {
                return _jobs.Count == 0 ? null : _jobs[0];
            }
        }
    }

    public PrintJob? Selected
    {
        get
        {
            lock (_gate)
            {
                return FindSelected();
            }
        }
    }

    public ImmutableArray<PrintJob> Waiting
    {
        get
        {
            lock (_gate)
            {
                return _jobs.Skip(1).ToImmutableArray();
            }
        }
    }

    public void Enqueue(PrintJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        PrintJobQueueSnapshot snapshot;
        lock (_gate)
        {
            if (_jobs.Any(candidate => candidate.Id == job.Id))
            {
                throw new ArgumentException("A job with the same ID is already queued.", nameof(job));
            }

            _jobs.Add(job);
            _selectedId ??= job.Id;
            snapshot = CreateSnapshot();
        }

        Changed?.Invoke(snapshot);
    }

    public bool Select(Guid id)
    {
        PrintJobQueueSnapshot snapshot;
        lock (_gate)
        {
            if (_jobs.All(job => job.Id != id))
            {
                return false;
            }

            _selectedId = id;
            snapshot = CreateSnapshot();
        }

        Changed?.Invoke(snapshot);
        return true;
    }

    public void SetActiveState(JobState state)
    {
        PrintJobQueueSnapshot snapshot;
        lock (_gate)
        {
            if (_jobs.Count == 0)
            {
                throw new InvalidOperationException("There is no active print job.");
            }

            _jobs[0] = _jobs[0] with { State = state };
            snapshot = CreateSnapshot();
        }

        Changed?.Invoke(snapshot);
    }

    public bool CompleteActive()
    {
        PrintJobQueueSnapshot snapshot;
        lock (_gate)
        {
            if (_jobs.Count == 0)
            {
                return false;
            }

            _jobs.RemoveAt(0);
            _selectedId = _jobs.Count == 0 ? null : _jobs[0].Id;
            snapshot = CreateSnapshot();
        }

        Changed?.Invoke(snapshot);
        return true;
    }

    public bool Discard(Guid id)
    {
        PrintJobQueueSnapshot snapshot;
        lock (_gate)
        {
            var index = _jobs.FindIndex(job => job.Id == id);
            if (index < 0)
            {
                return false;
            }

            _jobs.RemoveAt(index);
            if (_jobs.Count == 0)
            {
                _selectedId = null;
            }
            else if (_selectedId == id || index == 0)
            {
                _selectedId = _jobs[0].Id;
            }

            snapshot = CreateSnapshot();
        }

        Changed?.Invoke(snapshot);
        return true;
    }

    private PrintJob? FindSelected() =>
        _selectedId is { } selectedId
            ? _jobs.FirstOrDefault(job => job.Id == selectedId)
            : null;

    private PrintJobQueueSnapshot CreateSnapshot() =>
        new(
            _jobs.Count == 0 ? null : _jobs[0],
            FindSelected(),
            _jobs.Skip(1).ToImmutableArray());
}
