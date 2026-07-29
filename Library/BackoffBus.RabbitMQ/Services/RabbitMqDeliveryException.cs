namespace BackoffBus.RabbitMQ.Services;

internal sealed class RabbitMqDeliveryException : Exception
{
    public RabbitMqDeliveryException(
        string exceptionType,
        string message,
        string? originalStackTrace)
        : base($"{exceptionType}: {message}")
    {
        OriginalExceptionType = exceptionType;
        OriginalStackTrace = originalStackTrace;
    }

    public string OriginalExceptionType { get; }

    public string? OriginalStackTrace { get; }
}
