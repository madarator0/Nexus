using BackoffBus.Extensions;
using BackoffBus.Sandbox.IntegrationEvents.Email;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BackoffBus RabbitMQ Sandbox",
        Version = "v1",
        Description =
            "Publisher API for the BackoffBus RabbitMQ sandbox."
    });

    var xmlFilename =
        $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(
        AppContext.BaseDirectory,
        xmlFilename);
    options.IncludeXmlComments(xmlPath);
});

builder.Services
    .AddBackoffBus(
        typeof(EmailDeliveryRequestedIntegrationEvent).Assembly)
    .UseRabbitMqPublisher(options =>
    {
        options.ConnectionString =
            builder.Configuration["RabbitMq:ConnectionString"]
            ?? throw new InvalidOperationException(
                "RabbitMq:ConnectionString is not configured.");
        options.QueueName =
            builder.Configuration["RabbitMq:QueueName"]
            ?? throw new InvalidOperationException(
                "RabbitMq:QueueName is not configured.");
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "BackoffBus RabbitMQ Sandbox v1");
        options.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
