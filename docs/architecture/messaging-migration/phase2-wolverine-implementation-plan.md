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

## I2.1 — Bootstrap (#924) — ✅ implemented

- `WolverineFx` + `WolverineFx.Postgresql` 6.14.0 referenced from `ConduitLLM.Configuration`
  (same placement as the MassTransit adapter; flows transitively to Gateway/Admin).
- `AddConduitWolverine(configuration, connectionString, serviceName)`
  (`ConduitLLM.Configuration.Messaging.Wolverine`) wraps `UseWolverine`:
  `UsePostgresqlPersistenceAndTransport` on the service's existing EF connection
  ("CoreAPI"/"AdminAPI"), `Policies.AutoApplyTransactions()`, and
  **`Discovery.DisableConventionalDiscovery()`** — without it Wolverine's conventions
  would pick up Conduit's `*Handler`/`*Consumer` classes (whose `HandleAsync` takes an
  `IEventContext` Wolverine can't resolve). Dispatch is exclusively via the explicit
  bridges added in #925.
- Config keys: `ConduitLLM:Messaging:Backend` (`MassTransit` default; unrecognized values
  **throw at boot**), `…:Wolverine:SchemaName` (default `wolverine`),
  `…:Wolverine:AutoProvision` (default `true` → `AutoCreate.CreateOrUpdate`; prod sets
  `false` and provisions by script).
- Staging note (#924 as landed): with the flag set, the Wolverine host boots **alongside**
  MassTransit, which remains the active `IEventBus` backend; #925 turns the flag into the
  either/or composition-root switch shown above.
- Acceptance: app boots with Wolverine configured but **inactive** (flag still `MassTransit`).

## I2.2 — IEventBus / handler host on Wolverine (#925) — ✅ implemented

- `WolverineEventBus : IEventBus` (scoped, like the MassTransit adapter — inside a handler
  scope Wolverine's `IMessageBus` is the active message context, so follow-on publishes are
  correlation-aware and outbox-eligible). `PublishBatchAsync` = sequential publishes
  (Wolverine has no batch API; the batch is an optimization, not a semantic guarantee).
- `WolverineEventContext : IEventContext` → `Envelope.Id`/`CorrelationId`/`Headers`,
  cascading `PublishAsync`, and `SchedulePublishAsync` →
  `PublishAsync(evt, new DeliveryOptions { ScheduledTime = … })` (the interface form of
  `ScheduleAsync`, which is an extension method; unspecified `DateTime.Kind` treated as UTC).
  The `CancellationToken` is captured from the handler-method parameter (Wolverine does not
  expose it on the context).
- `WolverineHandlerBridge<TEvent>.Handle(TEvent, IMessageContext, CancellationToken)` —
  sequential dispatch to all `IEventHandler<TEvent>`, exceptions propagate to Wolverine's
  retry. Closed bridge types registered per event type via `AddEventBridge` /
  `Discovery.IncludeType` (conventional discovery stays off). One shared
  `BridgedEventTypes` list per extension class drives BOTH backends' registration
  (`CacheInvalidationMessagingExtensions`, `SharedCacheInvalidationMessagingExtensions`,
  `MediaGenerationMessagingExtensions`).
- Composition roots are now the either/or switch from the plan; handler registrations are
  backend-neutral and shared. `RabbitMQHealthCheck` (injects MassTransit `IBus`) is gated
  to the MassTransit backend (#931 replaces it). `BatchWebhookPublisher` registers
  unconditionally on Wolverine (it publishes via `IEventBus`).
- **Gotcha:** Wolverine 6 split the Roslyn runtime compiler out of the core package —
  without `WolverineFx.RuntimeCompilation` + `opts.UseRuntimeCompilation()`, hosts throw at
  startup in the default `TypeLoadMode.Dynamic`. Pre-generated static codegen
  (`codegen write` + `TypeLoadMode.Static`) is a cutover-time optimization (#930).
- Local queues are durable (`UseDurableLocalQueues`, Postgres-backed). Publishes route to
  this process's bridge handlers; **cross-service queue topology is #926 scope** — parity
  with today's in-memory MassTransit mode, where cross-service invalidation likewise rides
  the (separate, retained) Redis pub/sub cache bus.
- Acceptance met: in-memory pilot (`WolverineBridgePilotTests`) publishes via `IEventBus`
  through the bridge to an `IEventHandler` with envelope metadata; adapter/bridge/context
  unit tests mirror the MassTransit set.

## I2.3 — Map the 4 tuned endpoints (#926) — ✅ implemented (behavioral parity gate = #929)

Implemented as `WolverineEndpointPolicy` (descriptor translation) +
`ConduitMessagingTopology` in `ConduitLLM.Core.Messaging` (the declared event→queue
topology Postgres point-to-point queues need where RabbitMQ derived it from exchanges):

| Descriptor | Wolverine translation (as landed) |
|---|---|
| `SingleActiveConsumer` / `ConcurrentMessageLimit = 1` (spend, image) | `ListenToPostgresqlQueue(name).ListenWithStrictOrdering()` — cluster-wide single active listener + sequential handling |
| `ConcurrentMessageLimit` (webhook 75) | `.MaximumParallelMessages(n)`; `PrefetchCount` has no Postgres analogue |
| `Retry` shapes | `EndpointRetryHandlerPolicy : IHandlerPolicy` scopes `RetryWithCooldown(...)` to each endpoint's message types (Wolverine's failure DSL is global-or-generic; the descriptors carry runtime `Type` lists). Cooldowns via `ComputeRetryCooldowns`: Immediate → zeros; Incremental → min + step·n; Exponential → min + step·(2ⁿ−1) capped at max |
| `DelayedRedeliveryIntervals` | `ScheduleRetry(...)` after inline attempts (then Postgres dead-letter storage) |
| `CircuitBreaker` | listener `CircuitBreaker` (TrackingPeriod/FailurePercentageThreshold/MinimumThreshold/PauseTime) |
| `RateLimit` (webhook 100/s) | NOT translated — app-level webhook rate limiting/circuit breaking retained (as this plan allowed) |
| `QuorumQueue` / `QueueArguments` | RabbitMQ-native, not applicable |
| deferred retry (`SchedulePublishAsync`) | already native + durable since #925 |

**Topology** (single source of truth, applied identically on both hosts; a rule for a
type a host never publishes is inert): 4 tuned queues + `gateway-events` (all other
Gateway-consumed events) + `admin-events` (shared cache events; shared types fan out to
BOTH service queues). Gateway listens tuned + `gateway-events`; Admin listens
`admin-events`. Instances of a service compete on its queue — the same
one-consumer-per-event semantics as today's per-service RabbitMQ queues (per-instance
cache fan-out remains the Redis pub/sub cache bus's job). The Gateway bridge-extension
`BridgedEventTypes` lists now derive from the topology so routing and bridge
registration cannot drift (unit-tested: every bridged type routed exactly once).

Acceptance: translation + topology invariants unit-tested; live ordering/deferral/
concurrency behavior vs the MassTransit baseline is measured in I2.6/#929 (needs real
Postgres + load).

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
