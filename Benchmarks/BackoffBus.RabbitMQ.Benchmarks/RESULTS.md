# Local benchmark results

These results were collected on July 29, 2026 with:

- RabbitMQ 4.3.4 running locally in Docker;
- Erlang 27.3.4.15;
- .NET 10 Release build;
- persistent messages and publisher confirms;
- classic durable queues;
- 10,000 immediate messages per throughput run.

Results are environment-specific and should be treated as comparative,
not as universal RabbitMQ capacity numbers.

## Publisher channel pool

The baseline used one publisher channel protected by a global
`SemaphoreSlim`.

| Configuration | Publish throughput |
|---|---:|
| Original baseline | 309.56 msg/s |
| Pool, 1 channel | 341.79 msg/s |
| Pool, 2 channels | 758.88 msg/s |
| Pool, 4 channels | 2,632.96 msg/s |
| Pool, 8 channels | 1,209.87 msg/s |

Four channels were optimal on this machine. Eight channels added broker
and disk contention.

## Adaptive confirm batching

With four publisher channels, batches of up to 100 messages, a minimum
pipeline size of 32, and 512 concurrent callers:

| Metric | Result |
|---|---:|
| Best observed publish throughput | 15,021.67 msg/s |
| Best observed completion throughput | 14,250.31 msg/s |
| Final verification completion throughput | 13,229.04 msg/s |
| Final p50 end-to-end latency | 28.29 ms |
| Final p95 end-to-end latency | 52.46 ms |
| Final p99 end-to-end latency | 64.26 ms |

At low caller concurrency, groups smaller than 32 automatically use
individual confirms without waiting for a batch. With 16 concurrent
callers, the final adaptive configuration completed 1,931.17 msg/s.

## Delay bucket strategy

One thousand messages were scheduled 7.5 seconds into the future.

| Strategy | Broker behavior | Drift p50 | Drift p99 |
|---|---|---:|---:|
| Ceiling | One 8-second delay queue hop | 511.54 ms | 527.08 ms |
| Floor | Multiple smaller queue hops | 754.98 ms | 1,000.94 ms |

Under a scheduled-message backlog, `Ceiling` was both more predictable
and more efficient. `Floor` remains available for low-volume scenarios
where repeated hops are acceptable.

The final one-second accuracy verification delivered 1,000 messages
with no early or duplicate deliveries:

| Metric | Drift |
|---|---:|
| p50 | 10.92 ms |
| p95 | 79.82 ms |
| p99 | 81.35 ms |
| maximum | 81.96 ms |
