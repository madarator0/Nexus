# BackoffBus

BackoffBus is an in-memory integration event bus for hosted .NET
applications. It schedules and dispatches events, retries failed
delivery with bounded exponential backoff, and delegates exhausted
events to a configurable dead-letter handler.

## Registration

```csharp
builder.Services.AddBackoffBus(
    options =>
    {
        options.ProcessorConcurrency = 8;
        options.InitialRetryDelay = TimeSpan.FromSeconds(2);
        options.MaximumRetryDelay = TimeSpan.FromMinutes(2);
    },
    typeof(OrderCreated).Assembly);
```

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

BackoffBus is intentionally in-memory. Pending and dead-letter events
are lost when the process terminates. Delivery is at least once, so
notification handlers should be idempotent.
