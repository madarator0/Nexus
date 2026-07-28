using BackoffBus.Abstractions;
using BackoffBus.Events;
using BackoffBus.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BackoffBus.Tests;

public sealed class IntegrationEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_InvokesHandlersInRegistrationOrder()
    {
        var calls = new List<string>();
        var services = new ServiceCollection()
            .AddSingleton(calls)
            .AddTransient<
                IIntegrationEventHandler<DispatchedIntegrationEvent>,
                FirstHandler>()
            .AddTransient<
                IIntegrationEventHandler<DispatchedIntegrationEvent>,
                SecondHandler>()
            .AddScoped<
                IIntegrationEventDispatcher,
                IntegrationEventDispatcher>();
        await using var serviceProvider =
            services.BuildServiceProvider();
        await using var scope =
            serviceProvider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider
            .GetRequiredService<IIntegrationEventDispatcher>();
        var integrationEvent = new DispatchedIntegrationEvent(
            Guid.NewGuid());

        await dispatcher.DispatchAsync(
            integrationEvent,
            TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second"], calls);
    }

    private sealed record DispatchedIntegrationEvent(Guid EventId)
        : IntegrationEvent(EventId)
    {
        public override int MaxRetries => 0;
    }

    private sealed class FirstHandler(List<string> calls)
        : IIntegrationEventHandler<DispatchedIntegrationEvent>
    {
        public ValueTask HandleAsync(
            DispatchedIntegrationEvent integrationEvent,
            CancellationToken cancellationToken)
        {
            calls.Add("first");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SecondHandler(List<string> calls)
        : IIntegrationEventHandler<DispatchedIntegrationEvent>
    {
        public ValueTask HandleAsync(
            DispatchedIntegrationEvent integrationEvent,
            CancellationToken cancellationToken)
        {
            calls.Add("second");
            return ValueTask.CompletedTask;
        }
    }
}
