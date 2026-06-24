# Phase 3 — Decommission & Close-out (runbook)

Covers epic #909 issues **#932** and **#934**. (Documentation, #933, lands as code/doc
edits to `CLAUDE.md` and this folder.)

## I3.1 — Remove MassTransit & RabbitMQ apparatus (#932)

**Precondition (hard gate): prod stable on Wolverine for the agreed soak (#930).** Until
then the MassTransit backend is the live rollback path and MUST stay. This is why removal
is **not** done in the Phase-1/2 PR — pulling it early would delete the rollback the epic
deliberately keeps live.

When the gate is met, removal is mechanical and `grep`-verifiable:

1. **Packages** — drop `MassTransit` and `MassTransit.RabbitMQ` from `ConduitLLM.Core`,
   `ConduitLLM.Configuration`, `ConduitLLM.Security`, `ConduitLLM.Functions`,
   `ConduitLLM.Gateway`, `ConduitLLM.Admin` `.csproj`. (`MassTransit.Redis` already removed
   in #914.)
2. **Adapter** — delete `Shared/ConduitLLM.Configuration/Messaging/MassTransit/`
   (`MassTransitEventBus`, `MassTransitConsumerBridge`, `MassTransitEventContext`,
   `MassTransitEndpointPolicy`, `MassTransitMessagingExtensions`).
3. **Host wiring** — delete the MassTransit branch of `Gateway/Program.Messaging.cs` and
   the `AddMassTransit(...)` block in `Admin/Program.cs`; drop the `Messaging:Backend` flag
   (Wolverine becomes unconditional). Keep `AddConduitWolverine()`.
4. **Health/observability** — remove `Core/HealthChecks/RabbitMQHealthCheck.cs` and its
   registration in `Gateway/Program.Monitoring.cs` (replaced by the Wolverine health check
   from #931).
5. **Infra** — remove the RabbitMQ service from `docker-compose.yml` /
   `docker-compose.dev.yml`, its env (`CONDUITLLM__RABBITMQ__*`), and any
   `RabbitMqConfiguration` plumbing that is now unused. Keep the dead-code
   `ResilientEventHandlerBase`/`ResilientSpendUpdateProcessor` removal here too (they were
   never registered).
6. **Verify** — `grep -ri masstransit` and `grep -ri rabbitmq` come back clean (outside
   historical docs); `dotnet build` + full suite green.

What stays (explicit non-goals): the Redis pub/sub cache bus
(`RedisVirtualKeyCache`/`RedisModelCostCache`/`RedisProviderToolCache`) and the SignalR
Redis backplane — neither is the MassTransit bus.

The **interface layer keeps its place**: `IEventBus`/`IEventHandler<T>`/`IEventContext`/
`EndpointPolicy` remain in `ConduitLLM.Configuration.Messaging` with no third-party
`using` — the whole point of the abstraction is that the domain never again names a bus
library.

## I3.2 — Documentation & runbook (#933)

- Update `CLAUDE.md` "Event-Driven Architecture" section to describe Wolverine + Postgres
  transport/outbox instead of MassTransit + RabbitMQ.
- Update `docs/architecture/events/*` and any deployment/runbook references.
- Record the completed migration in project memory.

## I3.3 — (Stretch) Redis Streams transport-swap spike (#934)

Proves the portability claim end-to-end: point the same `IEventHandler<T>` handlers at
`WolverineFx.Redis` (Streams) in a throwaway spike, changing only Wolverine transport
configuration (no handler or domain edits). Document the config delta and any ordering
caveats (Redis Streams consumer-group ordering vs the Postgres sequential listener).
Acceptance: short report confirming the swap is configuration-only — the abstraction's
final validation.
