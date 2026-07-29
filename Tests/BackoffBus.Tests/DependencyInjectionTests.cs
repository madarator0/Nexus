using BackoffBus.Abstractions;
using BackoffBus.Events;
using BackoffBus.Extensions;
using BackoffBus.RabbitMQ.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

    [Fact]
    public void UseInMemory_RegistersInMemoryProvider()
    {
        var services = new ServiceCollection();

        services
            .AddBackoffBus(typeof(DependencyInjectionTests).Assembly)
            .UseInMemory();

        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IEventBus)
                && descriptor.ImplementationType?.Name == "EventBus");
        Assert.Equal(
            3,
            services.Count(
                descriptor =>
                    descriptor.ServiceType
                    == typeof(IHostedService)));
    }

    [Fact]
    public void UseRabbitMq_RegistersRabbitMqProviderAndOptions()
    {
        var services = new ServiceCollection();

        services
            .AddBackoffBus(typeof(DependencyInjectionTests).Assembly)
            .UseRabbitMq(options =>
            {
                options.ConnectionString =
                    "amqp://backoff:secret@rabbitmq/";
                options.QueueName = "orders";
            });

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IEventBus));
        Assert.Equal(
            2,
            services.Count(
                descriptor =>
                    descriptor.ServiceType
                    == typeof(IHostedService)));
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType
                    .IsGenericType
                && descriptor.ServiceType.GetGenericArguments()
                    .Contains(typeof(RabbitMqBackoffBusOptions)));
    }

    [Fact]
    public void UseRabbitMqPublisher_DoesNotRegisterConsumers()
    {
        var services = new ServiceCollection();

        services
            .AddBackoffBus(typeof(DependencyInjectionTests).Assembly)
            .UseRabbitMqPublisher();

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IEventBus));
        Assert.DoesNotContain(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void UseRabbitMqConsumer_RegistersBothConsumers()
    {
        var services = new ServiceCollection();

        services
            .AddBackoffBus(typeof(DependencyInjectionTests).Assembly)
            .UseRabbitMqConsumer();

        Assert.Equal(
            2,
            services.Count(
                descriptor =>
                    descriptor.ServiceType == typeof(IHostedService)));
    }

    [Fact]
    public void ConfiguringTwoProviders_Throws()
    {
        var builder = new ServiceCollection()
            .AddBackoffBus(typeof(DependencyInjectionTests).Assembly);

        builder.UseInMemory();

        var exception = Assert.Throws<InvalidOperationException>(
            () => builder.UseRabbitMq());

        Assert.Contains("already configured", exception.Message);
    }

    [Fact]
    public void ConfiguringProvidersAcrossTwoCoreRegistrations_Throws()
    {
        var services = new ServiceCollection();
        services
            .AddBackoffBus(typeof(DependencyInjectionTests).Assembly)
            .UseInMemory();
        var secondBuilder = services.AddBackoffBus(
            typeof(DependencyInjectionTests).Assembly);

        var exception = Assert.Throws<InvalidOperationException>(
            () => secondBuilder.UseRabbitMq());

        Assert.Contains("InMemory", exception.Message);
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
