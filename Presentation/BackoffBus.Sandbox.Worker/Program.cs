using BackoffBus.Extensions;
using BackoffBus.Sandbox.Application.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddSandboxEvents()
    .UseRabbitMqConsumer(options =>
    {
        options.ConnectionString =
            builder.Configuration["RabbitMq:ConnectionString"]
            ?? throw new InvalidOperationException(
                "RabbitMq:ConnectionString is not configured.");
        options.QueueName =
            builder.Configuration["RabbitMq:QueueName"]
            ?? throw new InvalidOperationException(
                "RabbitMq:QueueName is not configured.");
        options.PrefetchCount = 16;
    });

await builder.Build().RunAsync();
