using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace BackoffBus.Extensions;

internal sealed class TimeoutValueTaskSource
    : IValueTaskSource<bool>, IDisposable
{
    private const int MaxPoolSize = 1024;
    private const int OriginalTaskCompleted = 1;
    private const int TimerCompleted = 2;
    private const int ResultConsumed = 4;
    private const int AllOperationsCompleted =
        OriginalTaskCompleted | TimerCompleted | ResultConsumed;

    private const int Pending = 0;
    private const int OriginalTaskWon = 1;
    private const int TimerWon = 2;

    private static readonly ConcurrentQueue<TimeoutValueTaskSource>
        Pool = new();
    private static int _poolCount;

    private ManualResetValueTaskSourceCore<bool> _core;
    private readonly Action _onOriginalTaskCompletedDelegate;
    private readonly Action _onTimerDisposedDelegate;
    private readonly TimerCallback _onTimerFiredDelegate;

    private ValueTaskAwaiter<bool> _originalAwaiter;
    private ValueTaskAwaiter _timerDisposeAwaiter;
    private CancellationTokenSource? _operationCancellationTokenSource;
    private CancellationToken _stoppingToken;
    private ITimer? _timer;
    private int _state;
    private int _completedOperations;

    private TimeoutValueTaskSource()
    {
        _core.RunContinuationsAsynchronously = true;
        _onOriginalTaskCompletedDelegate = OnOriginalTaskCompleted;
        _onTimerDisposedDelegate = OnTimerDisposed;
        _onTimerFiredDelegate = OnTimerFired;
    }

    public static ValueTask<bool> WaitAsync(
        Func<CancellationToken, ValueTask<bool>> operationFactory,
        TimeSpan delay,
        TimeProvider timeProvider,
        CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(operationFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delay),
                "Timeout delay must be positive.");
        }

        if (stoppingToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<bool>(stoppingToken);
        }

        var source = Rent();

        try
        {
            return source.Run(
                operationFactory,
                delay,
                timeProvider,
                stoppingToken);
        }
        catch
        {
            source.ReleaseAfterFailedStart();
            throw;
        }
    }

    private ValueTask<bool> Run(
        Func<CancellationToken, ValueTask<bool>> operationFactory,
        TimeSpan delay,
        TimeProvider timeProvider,
        CancellationToken stoppingToken)
    {
        _core.Reset();
        _state = Pending;
        _completedOperations = 0;
        _stoppingToken = stoppingToken;
        _operationCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken);
        _timer = timeProvider.CreateTimer(
            _onTimerFiredDelegate,
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);

        var originalTask = operationFactory(
            _operationCancellationTokenSource.Token);
        _originalAwaiter = originalTask.GetAwaiter();

        if (_originalAwaiter.IsCompleted)
        {
            OnOriginalTaskCompleted();
        }
        else
        {
            _timer.Change(delay, Timeout.InfiniteTimeSpan);
            _originalAwaiter.UnsafeOnCompleted(
                _onOriginalTaskCompletedDelegate);
        }

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

        if (Interlocked.CompareExchange(
                ref _state,
                OriginalTaskWon,
                Pending) == Pending)
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
        if (Interlocked.CompareExchange(
                ref _state,
                TimerWon,
                Pending) != Pending)
        {
            return;
        }

        Exception? cancellationException = null;

        try
        {
            _operationCancellationTokenSource!.Cancel();
        }
        catch (Exception exception)
        {
            cancellationException = exception;
        }

        if (cancellationException is not null)
        {
            _core.SetException(cancellationException);
        }
        else if (_stoppingToken.IsCancellationRequested)
        {
            _core.SetException(
                new OperationCanceledException(_stoppingToken));
        }
        else
        {
            _core.SetResult(false);
        }

        DisposeElapsedTimer();
        MarkOperationCompleted(TimerCompleted);
    }

    private void StopTimer()
    {
        var timer = Interlocked.Exchange(ref _timer, null);

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

    private void DisposeElapsedTimer()
    {
        var timer = Interlocked.Exchange(ref _timer, null);
        timer?.Dispose();
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
        var previousOperations = Interlocked.Or(
            ref _completedOperations,
            operation);

        if ((previousOperations & operation) != 0
            || (previousOperations | operation)
            != AllOperationsCompleted)
        {
            return;
        }

        CleanupCompletedOperation();
        Return(this);
    }

    private void CleanupCompletedOperation()
    {
        _operationCancellationTokenSource?.Dispose();
        _operationCancellationTokenSource = null;
        _timer?.Dispose();
        _timer = null;
        _originalAwaiter = default;
        _timerDisposeAwaiter = default;
        _stoppingToken = default;
    }

    private void ReleaseAfterFailedStart()
    {
        try
        {
            _operationCancellationTokenSource?.Cancel();
        }
        finally
        {
            CleanupCompletedOperation();
            Return(this);
        }
    }

    private static TimeoutValueTaskSource Rent()
    {
        if (Pool.TryDequeue(out var source))
        {
            Interlocked.Decrement(ref _poolCount);
            return source;
        }

        return new TimeoutValueTaskSource();
    }

    private static void Return(TimeoutValueTaskSource source)
    {
        while (true)
        {
            var poolCount = Volatile.Read(ref _poolCount);

            if (poolCount >= MaxPoolSize)
            {
                source.Dispose();
                return;
            }

            if (Interlocked.CompareExchange(
                    ref _poolCount,
                    poolCount + 1,
                    poolCount) == poolCount)
            {
                Pool.Enqueue(source);
                return;
            }
        }
    }

    public void Dispose()
    {
        _operationCancellationTokenSource?.Dispose();
        _operationCancellationTokenSource = null;
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

    public ValueTaskSourceStatus GetStatus(short token) =>
        _core.GetStatus(token);

    public void OnCompleted(
        Action<object?> continuation,
        object? state,
        short token,
        ValueTaskSourceOnCompletedFlags flags) =>
        _core.OnCompleted(continuation, state, token, flags);
}
