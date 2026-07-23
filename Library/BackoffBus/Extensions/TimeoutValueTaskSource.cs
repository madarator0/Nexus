namespace BackoffBus.Extensions;

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

internal sealed class TimeoutValueTaskSource : IValueTaskSource<bool>, IDisposable
{
    private const int MaxPoolSize = 1024;
    private const int OriginalTaskCompleted = 1;
    private const int TimerCompleted = 2;
    private const int ResultConsumed = 4;
    private const int AllOperationsCompleted =
        OriginalTaskCompleted | TimerCompleted | ResultConsumed;

    private static readonly ConcurrentQueue<TimeoutValueTaskSource> Pool = new();
    private static int _poolCount;

    private ManualResetValueTaskSourceCore<bool> _core;
    private Timer? _timer;
    private readonly Action _onOriginalTaskCompletedDelegate;
    private readonly Action _onTimerDisposedDelegate;
    private readonly TimerCallback _onTimerFiredDelegate;

    private ValueTaskAwaiter<bool> _originalAwaiter;
    private ValueTaskAwaiter _timerDisposeAwaiter;
    private int _state;
    private int _completedOperations;

    public static ValueTask<bool> WaitAsync(ValueTask<bool> task, TimeSpan delay)
    {
        if (task.IsCompleted)
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
        _core.RunContinuationsAsynchronously = true;
        _onOriginalTaskCompletedDelegate = OnOriginalTaskCompleted;
        _onTimerDisposedDelegate = OnTimerDisposed;
        _onTimerFiredDelegate = OnTimerFired;
        _timer = new Timer(_onTimerFiredDelegate, null, Timeout.Infinite, Timeout.Infinite);
    }

    private ValueTask<bool> Run(ValueTask<bool> originalTask, TimeSpan delay)
    {
        _core.Reset();
        _state = 0;
        _completedOperations = 0;
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
        bool result = false;
        Exception? exception = null;

        try
        {
            result = _originalAwaiter.GetResult();
        }
        catch (Exception currentException)
        {
            exception = currentException;
        }

        if (Interlocked.CompareExchange(ref _state, 1, 0) == 0)
        {
            if (exception is null)
            {
                _core.SetResult(result);
            }
            else
            {
                _core.SetException(exception);
            }

            StopTimer();
        }

        MarkOperationCompleted(OriginalTaskCompleted);
    }

    private void OnTimerFired(object? state)
    {
        if (Interlocked.CompareExchange(ref _state, 2, 0) == 0)
        {
            _core.SetResult(false);
            MarkOperationCompleted(TimerCompleted);
        }
    }

    private void StopTimer()
    {
        var timer = _timer;
        _timer = null;

        if (timer is null)
        {
            MarkOperationCompleted(TimerCompleted);
            return;
        }

        var disposeTask = timer.DisposeAsync();
        _timerDisposeAwaiter = disposeTask.GetAwaiter();

        if (_timerDisposeAwaiter.IsCompleted)
        {
            OnTimerDisposed();
        }
        else
        {
            _timerDisposeAwaiter.UnsafeOnCompleted(
                _onTimerDisposedDelegate);
        }
    }

    private void OnTimerDisposed()
    {
        try
        {
            _timerDisposeAwaiter.GetResult();
        }
        finally
        {
            MarkOperationCompleted(TimerCompleted);
        }
    }

    private void MarkOperationCompleted(int operation)
    {
        var previousOperations = Interlocked.Or(ref _completedOperations, operation);

        if ((previousOperations & operation) != 0)
        {
            return;
        }

        if ((previousOperations | operation) != AllOperationsCompleted)
        {
            return;
        }

        while (true)
        {
            var poolCount = Volatile.Read(ref _poolCount);

            if (poolCount >= MaxPoolSize)
            {
                Dispose();
                return;
            }

            if (Interlocked.CompareExchange(
                    ref _poolCount,
                    poolCount + 1,
                    poolCount) == poolCount)
            {
                Pool.Enqueue(this);
                return;
            }
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public bool GetResult(short token)
    {
        try
        {
            return _core.GetResult(token);
        }
        finally
        {
            MarkOperationCompleted(ResultConsumed);
        }
    }

    public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);
    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);
}
