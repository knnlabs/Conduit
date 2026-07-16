# Phase 2 — Wolverine Implementation & Cutover (implementation plan)

Covers epic #909 issues **#924–#931**. Phase 1 (the `IEventBus`/`IEventHandler<T>`
abstraction over MassTransit) is the prerequisite: once every publish site and consumer
goes through the abstraction, swapping the backend is an adapter + config change with no
domain edits. This document is the concrete build plan for the Wolverine backend, kept
behind a default-off config flag so MassTransit remains the active backend (instant
rollback) until the cutover (#930) is signed off.

Package availability verified: `WolverineFx` 6.14.0 + `WolverineFx.PostgreSQL` restore on
the .NET 10 toolchain (see [spike](spike-wolverine-postgres.md)).

## Backend selection (the flag)

Add `ConduitLLM:Messaging:Backend` = `MassTransit` (default) | `Wolverine`. A single
composition-root switch chooses which `IEventBus` + handler host is wired:

```csharp
var backend = builder.Configuration["ConduitLLM:Messaging:Backend"] ?? "MassTransit";
if (backend.Equals("Wolverine", StringComparison.OrdinalIgnoreCase))
    builder.AddConduitWolverine();      // #924/#925
else
    builder.Services.AddMassTransitEventBus();   // existing Phase 1 path
```

Both backends bind the **same** `IEventBus`/`IEventHandler<T>` contracts and consume the
**same** `EndpointPolicy` descriptors (`ConduitEndpointPolicies`), so handlers and call
sites are untouched.

## I2.1 — Bootstrap (#924)

- Add `WolverineFx` + `WolverineFx.PostgreSQL` to Gateway and Admin.
- `builder.Host.UseWolverine(opts => { opts.PersistMessagesWithPostgresql(connString); opts.Policies.AutoApplyTransactions(); })`.
- Provision durability tables via `opts.Services.AddResourceSetupOnStartup()` / `.AutoProvision()` in dev, or an EF migration for prod (reuse the existing Npgsql connection — no new broker).
- Acceptance: app boots with Wolverine configured but **inactive** (flag still `MassTransit`).

## I2.2 — IEventBus / handler host on Wolverine (#925)

- `WolverineEventBus : IEventBus` → `PublishAsync<T>` delegates to Wolverine `IMessageBus.PublishAsync`.
- `WolverineEventContext : IEventContext` → wraps Wolverine's message context:
  `CancellationToken`, `Envelope.Id` → `MessageId`, `Envelope.CorrelationId`, header
  lookup, `PublishAsync` (cascading), and `SchedulePublishAsync` → `bus.ScheduleAsync(evt, deliveryTime)`.
- Handler host: a generic Wolverine handler `WolverineHandlerBridge<TEvent>` (or a
  conventional handler discovered by Wolverine) resolves all `IEventHandler<TEvent>` and
  invokes each — the mirror of `MassTransitConsumerBridge<TEvent>`. Throwing propagates to
  Wolverine's retry/redelivery (configured from the descriptors in #926).
- Acceptance: every event type publishes/consumes via Wolverine in a dev run; the existing
  abstraction unit tests + the in-memory pilot pass against the Wolverine backend.

## I2.3 — Map the 4 tuned endpoints (#926)

Translate `ConduitEndpointPolicies` → Wolverine, the analogue of
`MassTransitEndpointPolicy.ApplyResiliencePolicies`:

| Descriptor | Wolverine translation |
|---|---|
| `SingleActiveConsumer` / `ConcurrentMessageLimit = 1` (spend) | `ListenToPostgresQueue("spend-update-events").Sequential()` (single, ordered listener) |
| `ConcurrentMessageLimit` / `PrefetchCount` | `.MaximumParallelMessages(n)` / buffered listener options |
| `Retry` (Immediate/Incremental/Exponential) | `opts.OnException<…>().RetryWithCooldown(...)` / `policy.RetryTimes` per endpoint |
| `DelayedRedeliveryIntervals` | `.ScheduleRetry(...)` intervals |
| `CircuitBreaker` | `opts.Policies.OnException(...).Pause(...)` / app-level Polly retained where richer |
| `RateLimit` (webhook 100/s) | endpoint throttle, or retain the existing app-level `IWebhookCircuitBreaker`/rate logic |
| deferred retry (`SchedulePublishAsync`) | native `ScheduleAsync` (durable; better than today's unconfigured MT scheduler) |

Acceptance: each endpoint's behavior matches the MassTransit baseline in tests (ordering,
deferral, concurrency).

## I2.4 — Transactional outbox (#927)

- Turn on the Postgres-backed outbox for spend/financial + webhook publishes
  (`AutoApplyTransactions` + durable outbox), closing the fire-and-forget swallow gap
  flagged in `MassTransitEventBus` and [I0.3](admin-gateway-cache-trace.md). The
  invalidation/spend publish commits in the **same transaction** as the business write.
- Add an inbox / idempotency key where consumers are not naturally idempotent (spend is
  keyed by `RequestId`).
- Acceptance: a crash injected between business commit and publish no longer loses the
  event (fault-injection test).

## I2.5 — Port the test suite (#928)

- Dev/CI uses Wolverine **in-memory local queues** (`opts.UseInMemory...` / `Durability =
  DurabilityMode.MediatorOnly` style) so the suite runs without Postgres.
- Extend the Phase 1 test doubles: run the abstraction/handler tests against BOTH backends
  (parameterize the pilot harness). `TestEventContext` already covers handler-level tests
  unchanged.
- Acceptance: suite green on the Wolverine backend in CI.

## I2.6 — Parity & ordering validation (#929) — GATE

- Side-by-side load: spend per-key ordering under ~17 msg/s, webhook deferral timing, 1000+
  webhooks/min throughput, zero message loss. Compare to captured MassTransit behavior.
- **Requires real Postgres + load infra — runs in CI/staging, not a unit environment.**
- Acceptance: documented parity report; sign-off to cut over.

## I2.7 — Staged cutover (#930)

- Flip `ConduitLLM:Messaging:Backend` → `Wolverine` in staging, then prod; keep MassTransit
  switchable for instant rollback for one release cycle. Both backends remain in the build.
- **Operational rollout — performed in the deployment pipeline, not from a code change.**

## I2.8 — Observability (#931)

- Wolverine OpenTelemetry metrics/tracing + health checks; replace `RabbitMQHealthCheck`
  with a bus/queue-depth health check; update dashboards/alerts.
- Acceptance: Wolverine queue depth/failures/retries visible; health endpoint reflects the
  new bus.

## Why this is staged, not done in one PR

#929 (load parity), #930 (prod cutover + soak) and the durability fault-injection in #927
require **real Postgres/RabbitMQ/load infrastructure** that a code-only environment cannot
provide, and #930 is a deployment event. They are therefore specified here and executed in
CI/staging/prod as the epic prescribes (one PR per issue), not blind-landed.
