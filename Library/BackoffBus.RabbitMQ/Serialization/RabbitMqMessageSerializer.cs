using BackoffBus.Abstractions;
using BackoffBus.DeadLetter;
using BackoffBus.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackoffBus.RabbitMQ.Serialization;

internal static class RabbitMqMessageSerializer
{
    private static readonly JsonSerializerOptions Options =
        new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition =
                JsonIgnoreCondition.WhenWritingNull
        };

    public static ReadOnlyMemory<byte> Serialize(
        IIntegrationEvent integrationEvent,
        int retryCount,
        DateTimeOffset executeAfter)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);

        var envelope = new RabbitMqMessageWireEnvelope(
            IntegrationEventJsonSerializer.SerializeToElement(
                integrationEvent),
            IntegrationEventJson: null,
            retryCount,
            executeAfter);
        return JsonSerializer.SerializeToUtf8Bytes(envelope, Options);
    }

    public static RabbitMqMessageEnvelope Deserialize(
        ReadOnlySpan<byte> body)
    {
        var wireEnvelope =
            JsonSerializer.Deserialize<RabbitMqMessageWireEnvelope>(
                body,
                Options)
            ?? throw new JsonException(
                "RabbitMQ integration event envelope is empty.");
        var integrationEvent = ReadIntegrationEvent(
            wireEnvelope.IntegrationEvent,
            wireEnvelope.IntegrationEventJson);

        ArgumentOutOfRangeException.ThrowIfNegative(
            wireEnvelope.RetryCount);
        return new RabbitMqMessageEnvelope(
            integrationEvent,
            wireEnvelope.RetryCount,
            wireEnvelope.ExecuteAfter);
    }

    public static IIntegrationEvent DeserializeIntegrationEvent(
        RabbitMqMessageEnvelope envelope) =>
        IntegrationEventJsonSerializer.Deserialize(
            envelope.IntegrationEvent);

    public static ReadOnlyMemory<byte> SerializeDeadLetter(
        DeadLetterIntegrationEvent deadLetterEvent)
    {
        ArgumentNullException.ThrowIfNull(deadLetterEvent);

        var envelope = new RabbitMqDeadLetterWireEnvelope(
            IntegrationEventJsonSerializer.SerializeToElement(
                deadLetterEvent.IntegrationEvent),
            IntegrationEventJson: null,
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
        var wireEnvelope =
            JsonSerializer.Deserialize<RabbitMqDeadLetterWireEnvelope>(
                body,
                Options)
            ?? throw new JsonException(
                "RabbitMQ dead-letter envelope is empty.");
        var integrationEvent = ReadIntegrationEvent(
            wireEnvelope.IntegrationEvent,
            wireEnvelope.IntegrationEventJson);

        ArgumentOutOfRangeException.ThrowIfNegative(
            wireEnvelope.RetryCount);
        return new RabbitMqDeadLetterEnvelope(
            integrationEvent,
            wireEnvelope.RetryCount,
            wireEnvelope.ExceptionType,
            wireEnvelope.ExceptionMessage,
            wireEnvelope.ExceptionStackTrace,
            wireEnvelope.FailedAt);
    }

    public static IIntegrationEvent DeserializeDeadLetterIntegrationEvent(
        RabbitMqDeadLetterEnvelope envelope) =>
        IntegrationEventJsonSerializer.Deserialize(
            envelope.IntegrationEvent);

    private static JsonElement ReadIntegrationEvent(
        JsonElement integrationEvent,
        string? integrationEventJson)
    {
        if (integrationEvent.ValueKind is
            not JsonValueKind.Undefined
            and not JsonValueKind.Null)
        {
            return integrationEvent;
        }

        if (string.IsNullOrWhiteSpace(integrationEventJson))
        {
            throw new JsonException(
                "RabbitMQ integration event payload is empty.");
        }

        using var document = JsonDocument.Parse(integrationEventJson);
        return document.RootElement.Clone();
    }

    private sealed record RabbitMqMessageWireEnvelope(
        JsonElement IntegrationEvent,
        string? IntegrationEventJson,
        int RetryCount,
        DateTimeOffset ExecuteAfter);

    private sealed record RabbitMqDeadLetterWireEnvelope(
        JsonElement IntegrationEvent,
        string? IntegrationEventJson,
        int RetryCount,
        string ExceptionType,
        string ExceptionMessage,
        string? ExceptionStackTrace,
        DateTimeOffset FailedAt);
}
