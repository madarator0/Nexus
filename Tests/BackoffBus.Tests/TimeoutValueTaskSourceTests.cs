using BackoffBus.Extensions;

namespace BackoffBus.Tests;

public sealed class TimeoutValueTaskSourceTests
{
    [Fact]
    public async Task WaitAsync_TimeoutCancelsOriginalOperation()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var timeProvider = CreateTimeProvider();
        var input = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var result = TimeoutValueTaskSource.WaitAsync(
            operationToken =>
            {
                operationToken.Register(
                    () =>
                    {
                        input.TrySetCanceled(operationToken);
                        cancellationObserved.TrySetResult(true);
                    });
                return new ValueTask<bool>(input.Task);
            },
            TimeSpan.FromMinutes(1),
            timeProvider,
            cancellationToken);

        await timeProvider.TimerCreated.WaitAsync(
            TimeSpan.FromSeconds(1),
            cancellationToken);
        timeProvider.Advance(TimeSpan.FromMinutes(1));

        Assert.False(await result);
        Assert.True(
            await cancellationObserved.Task.WaitAsync(
                TimeSpan.FromSeconds(1),
                cancellationToken));
        Assert.True(input.Task.IsCanceled);
        await AssertTimerDisposedAsync(timeProvider);
    }

    [Fact]
    public async Task WaitAsync_StoppingTokenPropagatesCancellation()
    {
        var timeProvider = CreateTimeProvider();
        using var stoppingTokenSource =
            new CancellationTokenSource();
        var input = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var result = TimeoutValueTaskSource.WaitAsync(
            operationToken =>
            {
                operationToken.Register(
                    () => input.TrySetCanceled(operationToken));
                return new ValueTask<bool>(input.Task);
            },
            TimeSpan.FromHours(1),
            timeProvider,
            stoppingTokenSource.Token);

        stoppingTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => result.AsTask());
        await AssertTimerDisposedAsync(timeProvider);
    }

    [Fact]
    public async Task WaitAsync_DoesNotReuseSourceBeforeOriginalCompletes()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var firstTimeProvider = CreateTimeProvider();
        var firstInput = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstResult = TimeoutValueTaskSource.WaitAsync(
            _ => new ValueTask<bool>(firstInput.Task),
            TimeSpan.FromMinutes(1),
            firstTimeProvider,
            cancellationToken);

        firstTimeProvider.Advance(TimeSpan.FromMinutes(1));
        Assert.False(await firstResult);

        var secondTimeProvider = CreateTimeProvider();
        var secondInput = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondResult = TimeoutValueTaskSource.WaitAsync(
            _ => new ValueTask<bool>(secondInput.Task),
            TimeSpan.FromMinutes(1),
            secondTimeProvider,
            cancellationToken);

        secondInput.SetResult(true);
        Assert.True(await secondResult);

        firstInput.SetResult(true);
        await firstInput.Task;
    }

    [Fact]
    public async Task WaitAsync_SafelyReusesPooledSource()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        for (var iteration = 0; iteration < 250; iteration++)
        {
            var timeProvider = CreateTimeProvider();
            var input = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var result = TimeoutValueTaskSource.WaitAsync(
                _ => new ValueTask<bool>(input.Task),
                TimeSpan.FromHours(1),
                timeProvider,
                cancellationToken);

            input.SetResult(true);

            Assert.True(await result);
            await AssertTimerDisposedAsync(timeProvider);
        }
    }

    [Fact]
    public async Task WaitAsync_SafelyReusesSourceAfterTimeout()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        for (var iteration = 0; iteration < 250; iteration++)
        {
            var timeProvider = CreateTimeProvider();
            var input = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationObserved = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var result = TimeoutValueTaskSource.WaitAsync(
                operationToken =>
                {
                    operationToken.Register(
                        () =>
                        {
                            input.TrySetCanceled(operationToken);
                            cancellationObserved.TrySetResult(true);
                        });
                    return new ValueTask<bool>(input.Task);
                },
                TimeSpan.FromMinutes(1),
                timeProvider,
                cancellationToken);

            timeProvider.Advance(TimeSpan.FromMinutes(1));

            Assert.False(await result);
            await cancellationObserved.Task.WaitAsync(
                TimeSpan.FromSeconds(1),
                cancellationToken);
            await AssertTimerDisposedAsync(timeProvider);
        }
    }

    private static async Task AssertTimerDisposedAsync(
        ManualTimeProvider timeProvider)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (timeProvider.ActiveTimerCount == 0)
            {
                return;
            }

            await Task.Delay(1);
        }

        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    private static ManualTimeProvider CreateTimeProvider() =>
        new(new DateTimeOffset(
            2030,
            1,
            1,
            0,
            0,
            0,
            TimeSpan.Zero));
}
