using System.Text.Json;

namespace BackoffBus.RabbitMQ.Serialization;

internal sealed record RabbitMqMessageEnvelope(
    JsonElement IntegrationEvent,
    int RetryCount,
    DateTimeOffset ExecuteAfter);

internal sealed record RabbitMqDeadLetterEnvelope(
    JsonElement IntegrationEvent,
    int RetryCount,
    string ExceptionType,
    string ExceptionMessage,
    string? ExceptionStackTrace,
    DateTimeOffset FailedAt);
