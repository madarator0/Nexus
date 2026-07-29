using BackoffBus.Events;
using BackoffBus.Serialization;

namespace BackoffBus.RabbitMQ.Benchmarks;

[IntegrationEvent("benchmarks.rabbitmq.delivery", 1)]
internal sealed record BenchmarkIntegrationEvent(
    Guid Id,
    Guid RunId,
    int Sequence,
    DateTimeOffset PublishedAt) : IntegrationEvent(Id)
{
    public override int MaxRetries => 0;
}
