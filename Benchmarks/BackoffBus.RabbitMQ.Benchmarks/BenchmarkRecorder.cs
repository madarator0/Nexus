using BackoffBus.Abstractions;

namespace BackoffBus.RabbitMQ.Benchmarks;

internal sealed class BenchmarkRecorder
{
    private BenchmarkRunState? _currentRun;

    internal BenchmarkRunState BeginRun(Guid runId, int count)
    {
        var run = new BenchmarkRunState(runId, count);
        Volatile.Write(ref _currentRun, run);
        return run;
    }

    internal void Record(
        BenchmarkIntegrationEvent integrationEvent,
        DateTimeOffset receivedAt)
    {
        var run = Volatile.Read(ref _currentRun);

        if (run is not null)
        {
            run.Record(integrationEvent, receivedAt);
        }
    }
}

internal sealed class BenchmarkIntegrationEventHandler(
    BenchmarkRecorder recorder,
    TimeProvider timeProvider)
    : IIntegrationEventHandler<BenchmarkIntegrationEvent>
{
    public ValueTask HandleAsync(
        BenchmarkIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        recorder.Record(
            integrationEvent,
            timeProvider.GetUtcNow());
        return ValueTask.CompletedTask;
    }
}

internal sealed class BenchmarkRunState
{
    private readonly Guid _runId;
    private readonly int[] _seen;
    private readonly double[] _scheduleDriftMilliseconds;
    private readonly double[] _endToEndMilliseconds;
    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _receivedCount;
    private int _duplicateCount;

    internal BenchmarkRunState(Guid runId, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        _runId = runId;
        _seen = new int[count];
        _scheduleDriftMilliseconds = new double[count];
        _endToEndMilliseconds = new double[count];
    }

    internal int ReceivedCount => Volatile.Read(ref _receivedCount);

    internal int DuplicateCount => Volatile.Read(ref _duplicateCount);

    internal async Task WaitAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            await _completion.Task.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(
                $"Only {ReceivedCount} of {_seen.Length} messages were received in {timeout}.",
                exception);
        }
    }

    internal BenchmarkSnapshot CreateSnapshot() =>
        new(
            _scheduleDriftMilliseconds,
            _endToEndMilliseconds,
            DuplicateCount);

    internal void Record(
        BenchmarkIntegrationEvent integrationEvent,
        DateTimeOffset receivedAt)
    {
        if (integrationEvent.RunId != _runId
            || integrationEvent.Sequence < 0
            || integrationEvent.Sequence >= _seen.Length)
        {
            return;
        }

        if (Interlocked.Exchange(
                ref _seen[integrationEvent.Sequence],
                1) != 0)
        {
            Interlocked.Increment(ref _duplicateCount);
            return;
        }

        _scheduleDriftMilliseconds[integrationEvent.Sequence] =
            (receivedAt - integrationEvent.ExecuteAfter).TotalMilliseconds;
        _endToEndMilliseconds[integrationEvent.Sequence] =
            (receivedAt - integrationEvent.PublishedAt).TotalMilliseconds;

        if (Interlocked.Increment(ref _receivedCount) == _seen.Length)
        {
            _completion.TrySetResult();
        }
    }
}

internal sealed record BenchmarkSnapshot(
    double[] ScheduleDriftMilliseconds,
    double[] EndToEndMilliseconds,
    int DuplicateCount);
