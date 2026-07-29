# BackoffBus sandboxes

The sandbox projects demonstrate the same integration-event contracts
with two transport architectures.

## Shared contracts

`BackoffBus.Sandbox.IntegrationEvents` contains only integration-event
contracts. It has no handlers and no host-specific dependencies.

## InMemory

The InMemory sandbox runs publishing and handling in one API process:

- `BackoffBus.Sandbox.InMemory.Application` contains event handlers.
- `BackoffBus.Sandbox.InMemory.Api` hosts the API and the in-memory
  consumers.

Run it with:

```powershell
dotnet run --project .\Presentation\Sandbox\InMemory\BackoffBus.Sandbox.InMemory.Api
```

Swagger is available at `https://localhost:7242`.

## RabbitMQ

The RabbitMQ sandbox separates publishing from consumption:

- `BackoffBus.Sandbox.RabbitMQ.Application` contains event handlers.
- `BackoffBus.Sandbox.RabbitMQ.Api` publishes events.
- `BackoffBus.Sandbox.RabbitMQ.Worker` consumes and handles events.

Start RabbitMQ:

```powershell
docker compose up -d rabbitmq
```

Then run the API and Worker:

```powershell
dotnet run --project .\Presentation\Sandbox\RabbitMQ\BackoffBus.Sandbox.RabbitMQ.Api
dotnet run --project .\Presentation\Sandbox\RabbitMQ\BackoffBus.Sandbox.RabbitMQ.Worker
```

Swagger is available at `https://localhost:7243`.
