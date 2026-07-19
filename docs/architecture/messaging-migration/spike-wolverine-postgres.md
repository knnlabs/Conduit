# Spike: Wolverine + PostgreSQL transport — Go/No-Go (I0.1 / #910)

**Goal:** retire the only genuine technical unknowns before committing — (a) per-key
**strict ordering** (mirrors `spend-update-events`), (b) **deferred/scheduled** delivery
(mirrors `WebhookDeliveryConsumer.ScheduleSend`), (c) **throughput** comfortable at the
observed ~17 msg/s spend rate.

## Verdict: ✅ GO

Wolverine on the PostgreSQL transport supports all three required behaviours with
first-class, documented features. Package availability and version compatibility were
verified against this solution's toolchain (`WolverineFx` 6.14.0 + `WolverineFx.PostgreSQL`
restore cleanly from nuget.org on .NET 10).

### (a) Per-key strict ordering — ✅

- `ListenToPostgresQueue("spend-update").Sequential()` (or `MaxDegreeOfParallelism = 1`
  on a single listener) gives the strict-ordered, one-at-a-time processing that today's
  `x-single-active-consumer` + `ConcurrentMessageLimit = 1` provides.
- Spend ordering today is **per virtual key** and is enforced by single-active-consumer
  on one queue, **not** by MassTransit's partitioner (which is not registered). A single
  sequential Postgres listener reproduces this exactly. If true per-key parallelism is
  ever wanted, Wolverine message groups / a partitioned listener can shard by key while
  preserving per-key order — but that is **not** required for parity.

### (b) Deferred / scheduled delivery — ✅

- Wolverine has native scheduling: `bus.ScheduleAsync(message, delay)` /
  `context.ReScheduleAsync(time)` and the `DeliverAt`/`ScheduledTime` envelope option,
  backed by the Postgres durability tables (durable scheduled messages survive restart —
  an improvement over MassTransit's `ScheduleSend`, which today depends on a message
  scheduler that is **not** configured in `Program.Messaging.cs`).
- The webhook retry (`context.ScheduleSend(retryTime, msg)`) maps to
  `IEventContext.SchedulePublishAsync(deliveryTime, evt)`; the MassTransit adapter keeps
  `ScheduleSend`, the Wolverine adapter uses `ScheduleAsync`.

### (c) Throughput — ✅

- The Postgres transport comfortably exceeds tens of msgs/sec; Wolverine batches and
  polls the queue tables. ~17 msg/s for spend, and the 1000+/min webhook target, are
  well within range for the Postgres transport already operated here. (Hard load
  numbers are captured against real infra in the parity gate I2.6/#929, not the spike.)

## Notes carried forward

- **Ordering is transport-native**, so it survives a later swap to Redis Streams
  (Wolverine v5+) with a config delta only (proven in stretch I3.3/#934).
- **Durability**: enabling the Postgres transactional outbox closes the fire-and-forget
  swallow gap identified in I0.3/#912 — folded into I2.4/#927.
- **No sagas / no request-response / no mediator** in current usage, so none of
  Wolverine's heavier features are on the critical path.

Per the issue's acceptance criteria, the throwaway spike code is **not** committed; this
report is the durable artifact. The production implementation is built behind the
`Messaging:Backend` flag in Phase 2.
