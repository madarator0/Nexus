using BackoffBus.Serialization;
using BackoffBus.Events;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BackoffBus.Tests;

public sealed class IntegrationEventJsonSerializerTests
{
    [Fact]
    public void SerializeAndDeserialize_UsesStableDiscriminator()
    {
        var scheduledFor = DateTimeOffset.UtcNow.AddMinutes(5);
        var integrationEvent = new SerializableIntegrationEvent(
            Guid.NewGuid(),
            "payload")
        {
            ExecuteAfter = scheduledFor
        };

        var json = IntegrationEventJsonSerializer.Serialize(
            integrationEvent);
        var restored = IntegrationEventJsonSerializer
            .Deserialize<SerializableIntegrationEvent>(json);

        Assert.Equal(integrationEvent, restored);
        Assert.Equal(scheduledFor, restored.ExecuteAfter);
        Assert.Contains("\"name\":\"tests.serializable\"", json);
        Assert.Contains("\"version\":2", json);
    }

    [Fact]
    public void Deserialize_RejectsUnregisteredDiscriminator()
    {
        var json = IntegrationEventJsonSerializer.Serialize(
            new SerializableIntegrationEvent(
                Guid.NewGuid(),
                "payload"));
        var document = JsonNode.Parse(json)!.AsObject();
        document["name"] = "tests.not-registered";

        var exception = Assert.Throws<JsonException>(
            () => IntegrationEventJsonSerializer.Deserialize(
                document.ToJsonString()));

        Assert.Contains("is not registered", exception.Message);
    }

    [IntegrationEvent("tests.serializable", 2)]
    private sealed record SerializableIntegrationEvent(
        Guid Id,
        string Message) : IntegrationEvent(Id)
    {
        public override int MaxRetries => 5;
    }
}
