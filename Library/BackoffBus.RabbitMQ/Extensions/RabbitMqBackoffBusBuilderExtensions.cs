using BackoffBus.Abstractions;
using BackoffBus.Configuration;
using BackoffBus.RabbitMQ.Configuration;
using BackoffBus.RabbitMQ.Job;
using BackoffBus.RabbitMQ.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BackoffBus.Extensions;

/// <summary>
/// Provides registration for the BackoffBus RabbitMQ provider.
/// </summary>
public static class RabbitMqBackoffBusBuilderExtensions
{
    /// <summary>
    /// Uses RabbitMQ for publishing and consuming with default options.
    /// </summary>
    public static BackoffBusBuilder UseRabbitMq(
        this BackoffBusBuilder builder) =>
        UseRabbitMqConsumer(builder, static _ => { });

    /// <summary>
    /// Uses RabbitMQ for publishing and consuming with custom options.
    /// </summary>
    public static BackoffBusBuilder UseRabbitMq(
        this BackoffBusBuilder builder,
        Action<RabbitMqBackoffBusOptions> configure) =>
        UseRabbitMqConsumer(builder, configure);

    /// <summary>
    /// Uses RabbitMQ only for publishing with default options.
    /// </summary>
    public static BackoffBusBuilder UseRabbitMqPublisher(
        this BackoffBusBuilder builder) =>
        UseRabbitMqPublisher(builder, static _ => { });

    /// <summary>
    /// Uses RabbitMQ only for publishing with custom options.
    /// </summary>
    public static BackoffBusBuilder UseRabbitMqPublisher(
        this BackoffBusBuilder builder,
        Action<RabbitMqBackoffBusOptions> configure) =>
        ConfigureRabbitMq(
            builder,
            configure,
            registerConsumers: false);

    /// <summary>
    /// Uses RabbitMQ for consuming and publishing with default options.
    /// </summary>
    public static BackoffBusBuilder UseRabbitMqConsumer(
        this BackoffBusBuilder builder) =>
        UseRabbitMqConsumer(builder, static _ => { });

    /// <summary>
    /// Uses RabbitMQ for consuming and publishing with custom options.
    /// </summary>
    public static BackoffBusBuilder UseRabbitMqConsumer(
        this BackoffBusBuilder builder,
        Action<RabbitMqBackoffBusOptions> configure) =>
        ConfigureRabbitMq(
            builder,
            configure,
            registerConsumers: true);

    private static BackoffBusBuilder ConfigureRabbitMq(
        BackoffBusBuilder builder,
        Action<RabbitMqBackoffBusOptions> configure,
        bool registerConsumers)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        builder.UseProvider("RabbitMQ");

        builder.Services
            .AddOptions<RabbitMqBackoffBusOptions>()
            .Configure(configure);
        builder.Services.TryAddSingleton<RabbitMqTransport>();
        builder.Services.TryAddSingleton<IEventBus, RabbitMqEventBus>();

        if (registerConsumers)
        {
            builder.Services.AddHostedService<
                RabbitMqIntegrationEventProcessorJob>();
            builder.Services.AddHostedService<
                RabbitMqDeadLetterIntegrationEventProcessorJob>();
        }

        return builder;
    }
}
