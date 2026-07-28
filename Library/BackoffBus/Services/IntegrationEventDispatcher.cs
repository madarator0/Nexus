using BackoffBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Reflection;

namespace BackoffBus.Services;

internal sealed class IntegrationEventDispatcher(
    IServiceProvider serviceProvider)
    : IIntegrationEventDispatcher
{
    private delegate ValueTask HandlerInvoker(
        IServiceProvider serviceProvider,
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);

    private static readonly ConcurrentDictionary<Type, HandlerInvoker>
        Invokers = new();

    private static readonly MethodInfo DispatchTypedMethod =
        typeof(IntegrationEventDispatcher).GetMethod(
            nameof(DispatchTypedAsync),
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "The typed integration event dispatch method was not found.");

    public ValueTask DispatchAsync(
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var invoker = Invokers.GetOrAdd(
            integrationEvent.GetType(),
            CreateInvoker);

        return invoker(
            serviceProvider,
            integrationEvent,
            cancellationToken);
    }

    private static HandlerInvoker CreateInvoker(Type integrationEventType)
    {
        var dispatchMethod = DispatchTypedMethod.MakeGenericMethod(
            integrationEventType);

        return (HandlerInvoker)dispatchMethod.CreateDelegate(
            typeof(HandlerInvoker));
    }

    private static async ValueTask DispatchTypedAsync<TEvent>(
        IServiceProvider serviceProvider,
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
        where TEvent : class, IIntegrationEvent
    {
        var handlers = serviceProvider.GetServices<
            IIntegrationEventHandler<TEvent>>();

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(
                (TEvent)integrationEvent,
                cancellationToken);
        }
    }
}
