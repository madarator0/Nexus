namespace BackoffBus.Tests;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _sync = new();
    private readonly List<ManualTimer> _timers = [];
    private readonly TaskCompletionSource<bool> _timerCreated =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DateTimeOffset _utcNow;

    public ManualTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public Task TimerCreated => _timerCreated.Task;

    public int ActiveTimerCount
    {
        get
        {
            lock (_sync)
            {
                return _timers.Count;
            }
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_sync)
        {
            return _utcNow;
        }
    }

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var timer = new ManualTimer(this, callback, state);

        lock (_sync)
        {
            _timers.Add(timer);
            ChangeTimer(timer, dueTime, period);
        }

        _timerCreated.TrySetResult(true);
        return timer;
    }

    public void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);

        List<ManualTimer> dueTimers;

        lock (_sync)
        {
            _utcNow = _utcNow.Add(elapsed);
            dueTimers = _timers
                .Where(timer => timer.IsDue(_utcNow))
                .ToList();

            foreach (var timer in dueTimers)
            {
                timer.ScheduleNext();
            }
        }

        foreach (var timer in dueTimers)
        {
            timer.Invoke();
        }
    }

    private bool ChangeTimer(
        ManualTimer timer,
        TimeSpan dueTime,
        TimeSpan period)
    {
        ValidateTimeout(dueTime, nameof(dueTime));
        ValidateTimeout(period, nameof(period));

        lock (_sync)
        {
            if (timer.IsDisposed)
            {
                return false;
            }

            timer.DueAt = dueTime == Timeout.InfiniteTimeSpan
                ? null
                : _utcNow.Add(dueTime);
            timer.Period = period;
            return true;
        }
    }

    private void DisposeTimer(ManualTimer timer)
    {
        lock (_sync)
        {
            timer.IsDisposed = true;
            timer.DueAt = null;
            _timers.Remove(timer);
        }
    }

    private static void ValidateTimeout(
        TimeSpan timeout,
        string parameterName)
    {
        if (timeout < TimeSpan.Zero
            && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private sealed class ManualTimer(
        ManualTimeProvider owner,
        TimerCallback callback,
        object? state) : ITimer
    {
        public DateTimeOffset? DueAt { get; set; }
        public TimeSpan Period { get; set; } = Timeout.InfiniteTimeSpan;
        public bool IsDisposed { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period) =>
            owner.ChangeTimer(this, dueTime, period);

        public void Dispose() => owner.DisposeTimer(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public bool IsDue(DateTimeOffset utcNow) =>
            !IsDisposed
            && DueAt is { } dueAt
            && dueAt <= utcNow;

        public void ScheduleNext()
        {
            DueAt = Period == Timeout.InfiniteTimeSpan
                ? null
                : DueAt!.Value.Add(Period);
        }

        public void Invoke() => callback(state);
    }
}
