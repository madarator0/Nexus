using BackoffBus.Extensions;

namespace BackoffBus.Tests;

public sealed class TimeoutValueTaskSourceTests
{
    [Fact]
    public async Task WaitAsync_DoesNotReuseSourceBeforeResultIsConsumed()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var firstInput = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstResult = TimeoutValueTaskSource.WaitAsync(
            new ValueTask<bool>(firstInput.Task),
            TimeSpan.FromMilliseconds(5));

        await Task.Delay(30, cancellationToken);
        firstInput.SetResult(true);
        await Task.Delay(30, cancellationToken);

        var secondInput = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondResult = TimeoutValueTaskSource.WaitAsync(
            new ValueTask<bool>(secondInput.Task),
            TimeSpan.FromMilliseconds(100));

        Assert.False(await firstResult);

        secondInput.SetResult(true);
        Assert.True(await secondResult);
    }

    [Fact]
    public async Task WaitAsync_ReturnsOriginalResultWhenItWins()
    {
        var input = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var result = TimeoutValueTaskSource.WaitAsync(
            new ValueTask<bool>(input.Task),
            TimeSpan.FromMilliseconds(100));

        input.SetResult(true);

        Assert.True(await result);
    }

    [Fact]
    public async Task WaitAsync_SafelyReusesSourceWhenOriginalRepeatedlyWins()
    {
        for (var iteration = 0; iteration < 250; iteration++)
        {
            var input = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var result = TimeoutValueTaskSource.WaitAsync(
                new ValueTask<bool>(input.Task),
                TimeSpan.FromMinutes(1));

            input.SetResult(true);

            Assert.True(await result);
        }
    }

    [Fact]
    public async Task WaitAsync_ConsumesFaultedOriginalAfterTimeout()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var input = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var result = TimeoutValueTaskSource.WaitAsync(
            new ValueTask<bool>(input.Task),
            TimeSpan.FromMilliseconds(5));

        await Task.Delay(30, cancellationToken);
        input.SetException(new InvalidOperationException("late failure"));

        Assert.False(await result);
    }
}
