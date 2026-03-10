namespace Events.Extensions;

public static class ValueTaskExtensions
{
    public static ValueTask<bool> WaitAsync(
        this ValueTask<bool> valueTask,
        TimeSpan delay)
    {
        return TimeoutValueTaskSource.WaitAsync(valueTask, delay);
    }
}
