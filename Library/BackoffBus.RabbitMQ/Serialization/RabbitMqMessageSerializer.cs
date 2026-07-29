using BackoffBus.Abstractions;
using BackoffBus.DeadLetter;
using BackoffBus.Serialization;
using System.Text.Json;

namespace BackoffBus.RabbitMQ.Serialization;

internal static class RabbitMqMessageSerializer
{
    private static readonly JsonSerializerOptions Options =
        new(JsonSerializerDefaults.Web);

    public static ReadOnlyMemory<byte> Serialize(
        IIntegrationEvent integrationEvent,
        int retryCount,
        DateTimeOffset executeAfter)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);

        var envelope = new RabbitMqMessageEnvelope(
            IntegrationEventJsonSerializer.Serialize(integrationEvent),
            retryCount,
            executeAfter);
        return JsonSerializer.SerializeToUtf8Bytes(envelope, Options);
    }

    public static RabbitMqMessageEnvelope Deserialize(
        ReadOnlySpan<byte> body)
    {
        var envelope = JsonSerializer.Deserialize<RabbitMqMessageEnvelope>(
                body,
                Options)
            ?? throw new JsonException(
                "RabbitMQ integration event envelope is empty.");

        if (string.IsNullOrWhiteSpace(envelope.IntegrationEventJson))
        {
            throw new JsonException(
                "RabbitMQ integration event payload is empty.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(envelope.RetryCount);
        return envelope;
    }

    public static IIntegrationEvent DeserializeIntegrationEvent(
        RabbitMqMessageEnvelope envelope) =>
        IntegrationEventJsonSerializer.Deserialize(
            envelope.IntegrationEventJson);

    public static ReadOnlyMemory<byte> SerializeDeadLetter(
        DeadLetterIntegrationEvent deadLetterEvent)
    {
        ArgumentNullException.ThrowIfNull(deadLetterEvent);

        var envelope = new RabbitMqDeadLetterEnvelope(
            IntegrationEventJsonSerializer.Serialize(
                deadLetterEvent.IntegrationEvent),
            deadLetterEvent.RetryCount,
            deadLetterEvent.Exception.GetType().FullName
                ?? deadLetterEvent.Exception.GetType().Name,
            deadLetterEvent.Exception.Message,
            deadLetterEvent.Exception.StackTrace,
            deadLetterEvent.FailedAt);
        return JsonSerializer.SerializeToUtf8Bytes(envelope, Options);
    }

    public static RabbitMqDeadLetterEnvelope DeserializeDeadLetter(
        ReadOnlySpan<byte> body)
    {
        var envelope =
            JsonSerializer.Deserialize<RabbitMqDeadLetterEnvelope>(
                body,
                Options)
            ?? throw new JsonException(
                "RabbitMQ dead-letter envelope is empty.");

        if (string.IsNullOrWhiteSpace(envelope.IntegrationEventJson))
        {
            throw new JsonException(
                "RabbitMQ dead-letter payload is empty.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(envelope.RetryCount);
        return envelope;
    }
}
