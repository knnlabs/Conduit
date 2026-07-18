# ADR-001: The `IEventBus` / `IEventHandler<T>` messaging abstraction (I0.2 / #911)

- **Status:** Accepted
- **Date:** 2026-06-24
- **Context:** epic #909 — replace MassTransit with Wolverine behind a Conduit-owned abstraction
- **Supersedes:** direct use of MassTransit `IPublishEndpoint` / `IConsumer<T>` in domain code

## Context

MassTransit v9+ went commercial; v8 (Apache-2.0) is frozen to critical-only fixes
through end of 2026. Conduit's domain-event bus is spread across 5 projects (~48 publish
sites, ~36 consumers, 45 event types, 4 tuned endpoints). Today the domain depends
directly on MassTransit types, so the library is load-bearing in the domain. We want to
move to Wolverine **without** repeating that mistake, and with **zero behavior change**
at the cutover boundary.

## Decision

Introduce a small, Conduit-owned abstraction and make all domain code depend on it
instead of on a messaging library. The abstraction is implemented first over MassTransit
(Phase 1, shippable, no behavior change) and later over Wolverine (Phase 2), selectable
by a config flag.

### Contracts

```csharp
IEventBus.PublishAsync<TEvent>(TEvent @event, CancellationToken)          // publish
IEventHandler<TEvent>.HandleAsync(TEvent @event, IEventContext context)   // consume
IEventContext { CancellationToken; MessageId; CorrelationId;
                TryGetHeader(...); PublishAsync(...); SchedulePublishAsync(...) }
```

- **`IEventBus`** — one type-routed publish method. Replaces direct
  `IPublishEndpoint.Publish` and is the single seam used by
  `EventPublishingControllerBase` / `EventPublishingServiceBase`.
- **`IEventHandler<TEvent>`** — one handler method. A class may implement it for several
  event types (existing handlers do). Throwing signals retry/redelivery — same as today's
  consumers that re-throw to trigger MassTransit retry.
- **`IEventContext`** — exposes exactly the consume-time capabilities the existing
  consumers use, so they port 1:1:
  - `CancellationToken` — used throughout;
  - `MessageId` — `WebhookDeliveryConsumer` de-dup key;
  - `CorrelationId` — propagation;
  - `TryGetHeader` — `SpendUpdatedHandler` reads `Model`/`Provider` headers;
  - `PublishAsync` — follow-on events (e.g. `SpendUpdateProcessor` → `SpendUpdated`);
  - `SchedulePublishAsync` — webhook deferred retry (`ScheduleSend` today).

### Policy as data — `EndpointPolicy`

The four tuned endpoints are described declaratively (`EndpointPolicy` + `RetryPolicy`,
`CircuitBreakerPolicy`, `RateLimitPolicy`). Each backend translates the **same** descriptor
into its own primitives. The descriptor covers all four shapes:

| Endpoint | Shape captured by the descriptor |
|---|---|
| webhook-delivery | prefetch 100, concurrency 75, exponential retry, circuit breaker, rate-limit 100/s, quorum + `x-delivery-limit`/`x-max-length`/`x-overflow` |
| video-generation-events | cfg prefetch/concurrency, partition-key ordering (no SAC), incremental retry, circuit breaker, consume-topology, quorum |
| image-generation-events | cfg prefetch/concurrency, single-active-consumer, incremental retry, circuit breaker, quorum |
| spend-update-events | prefetch 10, concurrency 1, single-active-consumer (strict order), immediate retry, quorum + `x-max-length` |

### Placement

The abstraction lives in **`ConduitLLM.Configuration`** (`ConduitLLM.Configuration.Messaging`).
Rationale: `Configuration` is the **lowest** project that already contains publish sites
(`ProviderService`, `CacheConfigurationService`) and is referenced — directly or
transitively — by every project that publishes or consumes (Core, Security, Providers,
Gateway, Admin). The interface files carry **no** `using MassTransit`; the MassTransit
adapter sits beside them under `Messaging/MassTransit/` and is the only place that
references the library. This keeps the **type-level** dependency inverted (domain code
never names a library type) even while the package reference is transitively present
until Phase 3 removes it.

### Mapping existing seams

| Today | Becomes |
|---|---|
| `EventPublishingControllerBase.PublishEventFireAndForget` | wraps `IEventBus.PublishAsync` (keeps fire-and-forget + swallow) |
| `EventPublishingServiceBase.PublishEventAsync` | wraps `IEventBus.PublishAsync` |
| `IConsumer<T>.Consume(ConsumeContext<T>)` | `IEventHandler<T>.HandleAsync(T, IEventContext)` via a generic bridge |
| `BatchInvalidationEventHandler<T>` / `ResilientEventHandlerBase<T>` | re-based onto `IEventHandler<T>` |
| `ReceiveEndpoint(...)` tuned config | `EndpointPolicy` descriptor + per-backend translator |

## Consequences

- **+** A third-party bus is never again load-bearing in domain code; swapping transports
  is an adapter + config change.
- **+** The fire-and-forget swallow gap is now visible in one adapter and can be closed by
  the outbox (#927) without touching call sites.
- **+** Policies are testable data, shared verbatim by both backends.
- **−** One extra indirection (bridge) on the consume path; negligible cost, and it is the
  natural place to add cross-cutting concerns.
- **−** The abstraction lives in `Configuration` rather than a dedicated project — a
  pragmatic trade to avoid a new project and reference churn mid-migration; can be
  extracted later if desired.

## Alternatives considered

- **Dedicated `ConduitLLM.Messaging` project** — cleaner separation, but adds a new
  project + reference wiring across the solution for no behavioral gain during the
  migration. Deferred.
- **Keep using MassTransit types, swap library in place** — rejected; that is exactly the
  load-bearing-library coupling we are eliminating.
- **Roll-your-own bus** — rejected; billing-grade reliability (ordering, outbox) should
  not be hand-rolled.
