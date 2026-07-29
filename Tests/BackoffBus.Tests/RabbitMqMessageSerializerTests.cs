using BackoffBus.Events;
using BackoffBus.DeadLetter;
using BackoffBus.RabbitMQ.Serialization;
using BackoffBus.Serialization;
using System.Text.Json;

namespace BackoffBus.Tests;

public sealed class RabbitMqMessageSerializerTests
{
    [Fact]
    public void SerializeAndDeserialize_PreservesProviderMetadata()
    {
        IntegrationEventJsonSerializer.Register<RabbitMqTestEvent>();
        var integrationEvent = new RabbitMqTestEvent(
            Guid.NewGuid(),
            "payload");
        var executeAfter = DateTimeOffset.UtcNow.AddMinutes(5);

        var body = RabbitMqMessageSerializer.Serialize(
            integrationEvent,
            retryCount: 2,
            executeAfter);
        var envelope = RabbitMqMessageSerializer.Deserialize(body.Span);
        var restored =
            RabbitMqMessageSerializer.DeserializeIntegrationEvent(
                envelope);

        Assert.Equal(2, envelope.RetryCount);
        Assert.Equal(executeAfter, envelope.ExecuteAfter);
        Assert.Equal(integrationEvent, restored);

        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal(
            JsonValueKind.Object,
            json.GetProperty("integrationEvent").ValueKind);
        Assert.False(
            json.TryGetProperty("integrationEventJson", out _));

        var legacyBody = CreateLegacyBody(
            integrationEvent,
            retryCount: 2,
            executeAfter);
        Assert.True(body.Length < legacyBody.Length);
    }

    [Fact]
    public void Deserialize_LegacyStringEnvelope_RemainsSupported()
    {
        IntegrationEventJsonSerializer.Register<RabbitMqTestEvent>();
        var integrationEvent = new RabbitMqTestEvent(
            Guid.NewGuid(),
            "legacy");
        var legacyBody = CreateLegacyBody(
            integrationEvent,
            retryCount: 1,
            DateTimeOffset.UtcNow);

        var envelope = RabbitMqMessageSerializer.Deserialize(
            legacyBody);
        var restored =
            RabbitMqMessageSerializer.DeserializeIntegrationEvent(
                envelope);

        Assert.Equal(1, envelope.RetryCount);
        Assert.Equal(integrationEvent, restored);
    }

    [Fact]
    public void SerializeDeadLetter_RoundTripsEventAndFailure()
    {
        IntegrationEventJsonSerializer.Register<RabbitMqTestEvent>();
        var integrationEvent = new RabbitMqTestEvent(
            Guid.NewGuid(),
            "failed");
        var failedAt = DateTimeOffset.UtcNow;
        var deadLetterEvent = new DeadLetterIntegrationEvent(
            integrationEvent,
            retryCount: 3,
            new InvalidOperationException("failure"),
            failedAt);

        var body = RabbitMqMessageSerializer.SerializeDeadLetter(
            deadLetterEvent);
        var envelope =
            RabbitMqMessageSerializer.DeserializeDeadLetter(body.Span);
        var restored = RabbitMqMessageSerializer
            .DeserializeDeadLetterIntegrationEvent(envelope);

        Assert.Equal(integrationEvent, restored);
        Assert.Equal(3, envelope.RetryCount);
        Assert.Equal("failure", envelope.ExceptionMessage);
        Assert.Equal(failedAt, envelope.FailedAt);
    }

    private static byte[] CreateLegacyBody(
        RabbitMqTestEvent integrationEvent,
        int retryCount,
        DateTimeOffset executeAfter) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                IntegrationEventJson =
                    IntegrationEventJsonSerializer.Serialize(
                        integrationEvent),
                RetryCount = retryCount,
                ExecuteAfter = executeAfter
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [IntegrationEvent("tests.rabbitmq", 1)]
    private sealed record RabbitMqTestEvent(
        Guid Id,
        string Message) : IntegrationEvent(Id)
    {
        public override int MaxRetries => 3;
    }
}
