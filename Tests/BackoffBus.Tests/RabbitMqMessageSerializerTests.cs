using BackoffBus.Events;
using BackoffBus.RabbitMQ.Serialization;
using BackoffBus.Serialization;

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
    }

    [IntegrationEvent("tests.rabbitmq", 1)]
    private sealed record RabbitMqTestEvent(
        Guid Id,
        string Message) : IntegrationEvent(Id)
    {
        public override int MaxRetries => 3;
    }
}
