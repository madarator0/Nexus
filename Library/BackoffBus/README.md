# BackoffBus

BackoffBus is a provider-based integration event bus for hosted .NET
applications. The core package owns contracts, handler discovery,
serialization, retry configuration, dispatching, and dead-letter
contracts. Install one provider package:

- `BackoffBus.InMemory`
- `BackoffBus.RabbitMQ`

## Registration

```csharp
builder.Services
    .AddBackoffBus(
        options =>
        {
            options.ProcessorConcurrency = 8;
            options.InitialRetryDelay = TimeSpan.FromSeconds(2);
            options.MaximumRetryDelay = TimeSpan.FromMinutes(2);
        },
        typeof(OrderCreated).Assembly)
    .UseInMemory();
```

For an API that only publishes messages:

```csharp
builder.Services
    .AddBackoffBus(typeof(OrderCreated).Assembly)
    .UseRabbitMqPublisher(options =>
    {
        options.ConnectionString =
            builder.Configuration.GetConnectionString("RabbitMQ")!;
        options.QueueName = "orders";
    });
```

For a Worker that consumes messages and can publish retries:

```csharp
builder.Services
    .AddBackoffBus(
        options => options.ProcessorConcurrency = 8,
        typeof(OrderCreatedHandler).Assembly)
    .UseRabbitMqConsumer(options =>
    {
        options.ConnectionString =
            builder.Configuration.GetConnectionString("RabbitMQ")!;
        options.QueueName = "orders";
    });
```

`UseRabbitMq` remains a shorthand for `UseRabbitMqConsumer`.

Event names and versions should be stable across CLR type and assembly
renames:

```csharp
[IntegrationEvent("orders.created", version: 1)]
public sealed record OrderCreated(Guid Id, string OrderNumber)
    : IntegrationEvent(Id)
{
    public override int MaxRetries => 5;
}
```

Handle events through the BackoffBus contract. Handler implementations
are discovered in the assemblies passed to `AddBackoffBus`:

```csharp
public sealed class OrderCreatedHandler
    : IIntegrationEventHandler<OrderCreated>
{
    public ValueTask HandleAsync(
        OrderCreated integrationEvent,
        CancellationToken cancellationToken)
    {
        // Handle the event.
        return ValueTask.CompletedTask;
    }
}
```

An event can be scheduled while it is being constructed:

```csharp
await eventBus.PublishAsync(
    new OrderCreated(Guid.NewGuid(), "SO-1001")
    {
        ExecuteAfter = DateTimeOffset.UtcNow.AddMinutes(1)
    },
    cancellationToken);
```

Implement `IDeadLetterIntegrationEventHandler` and register it before
`AddBackoffBus` to persist, alert on, or otherwise handle exhausted
events.

## Delivery model

Both providers use at-least-once delivery, so handlers should be
idempotent. The in-memory provider loses pending messages when the
process terminates. The RabbitMQ provider uses durable queues,
persistent messages, publisher confirms, and manual acknowledgements.
