namespace Events.Extensions;

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

public sealed class TimeoutValueTaskSource : IValueTaskSource<bool>, IDisposable
{
    private const int MaxPoolSize = 1024;
    private static readonly ConcurrentQueue<TimeoutValueTaskSource> Pool = new();
    private static int _poolCount = 0;

    private ManualResetValueTaskSourceCore<bool> _core;
    private Timer? _timer;
    private readonly Action _onOriginalTaskCompletedDelegate;
    private readonly TimerCallback _onTimerFiredDelegate;

    private ValueTaskAwaiter<bool> _originalAwaiter;
    private int _state;
    private int _completions;

    public static ValueTask<bool> WaitAsync(ValueTask<bool> task, TimeSpan delay)
    {
        if (task.IsCompletedSuccessfully)
            return task;

        if (Pool.TryDequeue(out var source))
        {
            Interlocked.Decrement(ref _poolCount);
            return source.Run(task, delay);
        }

        return new TimeoutValueTaskSource().Run(task, delay);
    }

    private TimeoutValueTaskSource()
    {
        _onOriginalTaskCompletedDelegate = OnOriginalTaskCompleted;
        _onTimerFiredDelegate = OnTimerFired;
        _timer = new Timer(_onTimerFiredDelegate, null, Timeout.Infinite, Timeout.Infinite);
    }

    private ValueTask<bool> Run(ValueTask<bool> originalTask, TimeSpan delay)
    {
        _core.Reset();
        _state = 0;
        _completions = 0;
        _originalAwaiter = originalTask.GetAwaiter();

        if (_timer == null)
            _timer = new Timer(_onTimerFiredDelegate, null, delay, Timeout.InfiniteTimeSpan);
        else
            _timer.Change(delay, Timeout.InfiniteTimeSpan);

        _originalAwaiter.UnsafeOnCompleted(_onOriginalTaskCompletedDelegate);

        return new ValueTask<bool>(this, _core.Version);
    }

    private void OnOriginalTaskCompleted()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);

        if (Interlocked.CompareExchange(ref _state, 1, 0) == 0)
        {
            try
            {
                _core.SetResult(_originalAwaiter.GetResult());
            }
            catch (Exception ex) { _core.SetException(ex); }
        }

        CheckReturnToPool();
    }

    private void OnTimerFired(object? state)
    {
        if (Interlocked.CompareExchange(ref _state, 2, 0) == 0)
        {
            _core.SetResult(false);
        }
        CheckReturnToPool();
    }

    private void CheckReturnToPool()
    {
        if (Interlocked.Increment(ref _completions) == 2)
        {
            if (_poolCount < MaxPoolSize)
            {
                Interlocked.Increment(ref _poolCount);
                Pool.Enqueue(this);
            }
            else
            {
                Dispose();
            }
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public bool GetResult(short token) => _core.GetResult(token);
    public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);
    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);
}