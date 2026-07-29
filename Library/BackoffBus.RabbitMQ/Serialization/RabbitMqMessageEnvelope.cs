namespace BackoffBus.RabbitMQ.Serialization;

internal sealed record RabbitMqMessageEnvelope(
    string IntegrationEventJson,
    int RetryCount,
    DateTimeOffset ExecuteAfter);

internal sealed record RabbitMqDeadLetterEnvelope(
    string IntegrationEventJson,
    int RetryCount,
    string ExceptionType,
    string ExceptionMessage,
    string? ExceptionStackTrace,
    DateTimeOffset FailedAt);
