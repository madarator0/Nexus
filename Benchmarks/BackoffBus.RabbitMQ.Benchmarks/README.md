# BackoffBus RabbitMQ benchmark

This console project measures the real RabbitMQ delivery path rather
than isolated in-process method calls.

Start RabbitMQ and run the benchmark in Release mode.

Scheduling accuracy:

```powershell
dotnet run --project Benchmarks\BackoffBus.RabbitMQ.Benchmarks -c Release -- `
  --count 1000 `
  --delay-ms 5000
```

Immediate-message throughput:

```powershell
dotnet run --project Benchmarks\BackoffBus.RabbitMQ.Benchmarks -c Release -- `
  --count 10000 `
  --delay-ms 0 `
  --publisher-concurrency 16 `
  --publisher-channels 4 `
  --publisher-batch-size 100 `
  --publisher-batch-minimum-size 32
```

The report includes:

- publish and completion throughput;
- publish latency;
- end-to-end latency;
- schedule drift, where a positive value means late delivery;
- early and duplicate delivery counts;
- p50, p95, p99, minimum, and maximum values.

The default queue is `backoff-bus.benchmark`. Use `--queue` to isolate
runs from another benchmark process. Successful runs fully consume
their messages, but the durable benchmark topology remains in RabbitMQ.

Run with `--help` to list all options.

See [RESULTS.md](RESULTS.md) for the local comparison recorded while
implementing the publisher pool, adaptive confirms, and delay strategy.

For a useful batching comparison, publisher concurrency must be high
enough to fill a batch:

```powershell
dotnet run --project Benchmarks\BackoffBus.RabbitMQ.Benchmarks -c Release -- `
  --count 10000 `
  --delay-ms 0 `
  --publisher-concurrency 512 `
  --publisher-channels 4 `
  --publisher-batch-size 100 `
  --publisher-batch-minimum-size 32 `
  --prefetch 512
```
