using BackoffBus.Abstractions;
using BackoffBus.Events;
using BackoffBus.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace BackoffBus.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddBackoffBus_DiscoversIntegrationEventHandlers()
    {
        var services = new ServiceCollection();

        services.AddBackoffBus(
            typeof(DependencyInjectionTests).Assembly);

        var descriptor = Assert.Single(
            services,
            serviceDescriptor =>
                serviceDescriptor.ServiceType
                == typeof(IIntegrationEventHandler<
                    DiscoveredIntegrationEvent>)
                && serviceDescriptor.ImplementationType
                == typeof(DiscoveredIntegrationEventHandler));

        Assert.Equal(
            ServiceLifetime.Transient,
            descriptor.Lifetime);
    }

    public sealed record DiscoveredIntegrationEvent(Guid EventId)
        : IntegrationEvent(EventId)
    {
        public override int MaxRetries => 0;
    }

    public sealed class DiscoveredIntegrationEventHandler
        : IIntegrationEventHandler<DiscoveredIntegrationEvent>
    {
        public ValueTask HandleAsync(
            DiscoveredIntegrationEvent integrationEvent,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}
