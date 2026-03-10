namespace Events.Extensions;

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

public sealed class TimeoutValueTaskSource : IValueTaskSource<bool>, IDisposable
{
    // Ограничиваем пул, например, 10 000 объектов. 
    // Этого хватит для очень высокой нагрузки, при этом память будет под контролем.
    private const int MaxPoolSize = 10000;
    private static readonly ConcurrentQueue<TimeoutValueTaskSource> Pool = new();
    private static int _poolCount = 0;

    private ManualResetValueTaskSourceCore<bool> _core;
    private Timer? _timer; // Теперь может быть null после Dispose
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
        // Инициализируем таймер сразу
        _timer = new Timer(_onTimerFiredDelegate, null, Timeout.Infinite, Timeout.Infinite);
    }

    private ValueTask<bool> Run(ValueTask<bool> originalTask, TimeSpan delay)
    {
        _core.Reset();
        _state = 0;
        _completions = 0;
        _originalAwaiter = originalTask.GetAwaiter();

        // Проверка на случай, если объект был деактивирован (редкий сценарий)
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
            // Атомарно проверяем размер пула
            if (_poolCount < MaxPoolSize)
            {
                Interlocked.Increment(ref _poolCount);
                Pool.Enqueue(this);
            }
            else
            {
                // Если пул полон — уничтожаем таймер и отдаем объект GC
                Dispose();
            }
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }

    // Реализация IValueTaskSource
    public bool GetResult(short token) => _core.GetResult(token);
    public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);
    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);
}