using BackoffBus.Extensions;
using BackoffBus.Sandbox.InMemory.Application.Extensions;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BackoffBus InMemory Sandbox",
        Version = "v1",
        Description =
            "Single-process sandbox using the BackoffBus in-memory provider."
    });

    var xmlFilename =
        $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(
        AppContext.BaseDirectory,
        xmlFilename);
    options.IncludeXmlComments(xmlPath);
});

builder.Services
    .AddInMemorySandboxApplication()
    .UseInMemory();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "BackoffBus InMemory Sandbox v1");
        options.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
