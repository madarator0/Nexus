using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Events.Extensions;

internal sealed class TimeoutValueTaskSource : IValueTaskSource<bool>
{
    private ManualResetValueTaskSourceCore<bool> _core;

    private Timer? _timer;
    private ValueTaskAwaiter<bool> _awaiter;

    private int _completed; // 0 = pending, 1 = completed

    public static ValueTask<bool> WaitAsync(ValueTask<bool> task, TimeSpan delay)
    {
        if (task.IsCompletedSuccessfully)
            return task;

        var source = new TimeoutValueTaskSource();
        return source.Run(task, delay);
    }

    private ValueTask<bool> Run(ValueTask<bool> task, TimeSpan delay)
    {
        _core.Reset();
        _completed = 0;

        _awaiter = task.GetAwaiter();

        _timer = new Timer(OnTimeout, null, delay, Timeout.InfiniteTimeSpan);

        _awaiter.UnsafeOnCompleted(OnCompleted);

        return new ValueTask<bool>(this, _core.Version);
    }

    private void OnCompleted()
    {
        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            return;

        _timer?.Dispose();

        try
        {
            var result = _awaiter.GetResult();
            _core.SetResult(result);
        }
        catch (Exception ex)
        {
            _core.SetException(ex);
        }
    }

    private void OnTimeout(object? state)
    {
        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            return;

        _core.SetResult(false);
    }

    public bool GetResult(short token) => _core.GetResult(token);

    public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

    public void OnCompleted(
        Action<object?> continuation,
        object? state,
        short token,
        ValueTaskSourceOnCompletedFlags flags)
    {
        _core.OnCompleted(continuation, state, token, flags);
    }
}