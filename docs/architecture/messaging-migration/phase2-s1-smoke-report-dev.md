# Phase 2 S1 Functional Smoke — Dev-Environment Run (2026-07-17)

> **UPDATE 2026-07-18: W1 and W2 are FIXED and re-verified live** — see
> [Fix verification](#fix-verification-2026-07-18) at the end of this report. The
> matrix below records the original pre-fix run.

**Scope:** Runbook S1 (functional smoke) from
[`phase2-parity-gate-runbook.md`](phase2-parity-gate-runbook.md), executed locally in the
Docker dev environment against `dev` @ `e26dc515`, on **both** backends via the
`ConduitLLM:Messaging:Backend` switch. This is *not* the full parity gate — S2–S6
(load, fault-injection, rollback drill) still require staging. But S1 alone produced
**two cutover-blocking Wolverine defects and two pre-existing bugs**.

**Environment:** `start-dev.ps1` stack (Postgres/RabbitMQ/Redis in Docker), mock
OpenAI-compatible provider (`host.docker.internal:9099`) for deterministic chat/image
responses, webhook sink recording receipt timestamps and returning 500 on `/fail`.
Real-provider egress was validated once via SambaNova before its credits ran out; all
messaging-path tests use the mock (the bus, not the provider, is under test).

## Result matrix

| S1 check | MassTransit (baseline) | Wolverine |
|---|---|---|
| Chat completion via Gateway | ✅ 200 | ✅ 200 |
| Spend debit — batch tier (Redis) | ✅ exact ($0.006 = 46×$100/M + 7×$200/M; $0.02 mock) | ✅ exact ($0.02) — rides Redis, not the bus |
| Spend event path (`SpendUpdateRequested` → `SpendUpdateProcessor`) | ✅ 5 events, publish order preserved, `IdempotencyKey = spend:{RequestId}` written, duplicate republish skipped ("already applied") | ❌ **BLOCKED** — see W1/W2 |
| Admin→Gateway cache invalidation (model mapping, provider key) | ✅ handlers fired | ✅ `ModelMappingChanged` consumed over Postgres transport |
| Admin→Gateway `BatchSpendFlushRequestedEvent` | ✅ handler fired | ❌ delivered but **unprocessable** — W2 |
| Media generation orchestration | ✅ video task consumed (failed at fake provider as designed, webhook fired) | ❌ **BLOCKED** — W1 (image) / W2 (video) |
| Webhook failure → deferred retry → dead-letter | ⚠️ delivered/retried, but see M1 | ❌ never reached webhook stage (orchestrator blocked); `WebhookDeliveryConsumer` deps predict W2 as well |
| Message loss | none (RabbitMQ redelivery pending = drain-before-flip case) | none — everything parked durably in `wolverine_incoming_envelopes` (7 rows, 0 dead-letters) |

## Wolverine blocking defects

### W1 — Admin + Gateway share one Wolverine agent cluster; strict-ordered listeners never start
Both hosts bootstrap into the same `wolverine` schema, forming one node cluster
(`wolverine_nodes`: admin = node 1, gateway = node 2). The Admin node won leadership and,
as leader, tried to assign the exclusive (strict-ordering) listener agents. Admin's
runtime doesn't declare those listeners (`ListenAsConduitGateway` is Gateway-only), so
assignment fails permanently:

```
Wolverine.Runtime.Agents.InvalidAgentException:
'wolverine-listener://postgresql/image-generation-events' is not a known exclusive listener Uri
```

**Effect:** no node ever listens on `spend-update-events` or `image-generation-events`
whenever the Admin node holds leadership — the financial queue is dead. Messages
accumulate durably but are never consumed.

**Direction:** separate the node/agent cluster per application (per-service durability
schema, e.g. `wolverine_gateway` vs `wolverine_admin`) while keeping the shared queue
schema for cross-service messaging — or otherwise ensure exclusive-listener agents can
only be assigned to nodes that declare them.

### W2 — Handler codegen fails for high-risk consumers (`ServiceLocationPolicy.NotAllowed`)
Wolverine's dynamic codegen rejects any bridged handler whose dependency graph needs
service location. Observed at message-delivery time (so it does NOT fail at startup):

- `BatchSpendFlushRequestedEvent` → `InvalidServiceLocationException` (scoped
  `IEnumerable<IEventHandler<T>>` + `IServiceScopeFactory`); envelope parked.
- `VideoGenerationRequested` → same, via opaque lambda-factory registrations
  (`IModelCostService`, typed-HttpClient `IWebhookNotificationService`, scoped
  `ICostCalculationService`).

By dependency shape, `SpendUpdateProcessor`, `ProviderCredentialEventHandler`, and
`WebhookDeliveryConsumer` (typed HttpClient) hit the same wall. The cache-invalidation
handlers with plain constructor graphs (e.g. `ModelMappingCacheInvalidationHandler`)
work fine.

**Direction:** either configure the bridge registration so handler resolution happens
inside an explicit scope owned by the bridge (not codegen'd service location), or relax
`ServiceLocationPolicy` deliberately, or register the offending services without opaque
lambda factories. Needs a decision + tests (the in-memory dual-backend tests in #928
did not catch this — worth understanding why: likely the test harness resolves handlers
differently or the affected handlers weren't exercised through Wolverine codegen).

## Pre-existing bugs surfaced (both backends)

### M1 — MassTransit deferred webhook retry has never worked (missing scheduler)
`WebhookDeliveryConsumer` schedules its 4s/8s/16s deferred retries via
`context.ScheduleSend(...)` (same call before and after the #921 migration — parity
preserved), but the bus never configures a message scheduler:

```
The payload was not found: MassTransit.MessageSchedulerContext
```

The RabbitMQ delayed-exchange plugin IS enabled and `UseDelayedRedelivery` works — the
one-liner `cfg.UseDelayedMessageScheduler()` was simply never added. Net behavior today:
each delivery cycle does the in-consumer HTTP retries (2s/4s/8s), the scheduling throw
faults the consume, transport-level retry redelivers (observed 16 HTTP attempts across
4 cycles), then 5/15/30-min delayed redelivery. `RetryCount` never increments, so the
"max 3 deferred retries then dead-letter" logic is unreachable. The runbook's S1
expectation ("retries at ~4s/8s/16s") describes behavior that has never existed on the
baseline. Wolverine's Postgres-native scheduling would fix this — once W1/W2 are fixed.

### M2 — Admin's `ProviderUpdated`/`ProviderDeleted` events are dead letters by construction
`ProviderCredentialsController.Models.cs` defines local classes `ProviderUpdated` and
`ProviderDeleted` in `ConduitLLM.Admin.Controllers`. Same-namespace resolution makes
`UpdateProvider`/`DeleteProvider` publish these instead of the canonical
`ConduitLLM.Core.Events` records the Gateway subscribes to. Provider update/delete
cache-invalidation events have therefore never been delivered on either backend.
MassTransit dropped them silently; Wolverine at least logs
`No routes can be determined for ... (ConduitLLM.Admin.Controllers.ProviderUpdated)`.
Fix: delete the local duplicates and publish the Core events.

### Minor
- `POST /v1/images/generations/async` never populates `ImageGenerationRequested.WebhookUrl`
  — the event supports webhooks but the public API can't request them (videos can).
- `UpdateProviderRequest` null `BaseUrl` wipes the stored BaseUrl on rename-only updates.
- Admin API `POST /api/Model` / `POST /api/ModelSeries` insert empty cascade rows
  (`Model.Series`/`ModelSeries.Author` initialized to `new ...()` → EF inserts empty
  parent graphs; unique-name conflicts on the second use). Smoke worked around via SQL.

## Evidence highlights

- Spend exactly-once (MassTransit): ledger rows 4–8 in publish order
  `spend:mt-seq-001…005`, duplicate republish of `mt-seq-001` produced
  "Duplicate balance adjustment … skipping" + "already applied - republishing
  notification only", row count and balance unchanged.
- Wolverine durability: `wolverine_incoming_envelopes` = 7, `wolverine_dead_letters` = 0
  after all failures — nothing lost, matching the durable-parking design.
- Wolverine topology provisioned correctly: all 6 queues × (queue + scheduled) tables in
  `wolverine_queues`, OTel meters `Wolverine:conduit-gateway`/`conduit-admin`, health
  check Healthy.
- Drain-before-flip is real: a MassTransit delayed-redelivery webhook envelope was still
  pending in the delayed exchange at flip time and is stranded until the backend returns.

## Gate impact (#929 / #930)

S1 alone is **FAIL for cutover**: W1 and W2 must be fixed (and S1 re-run green on
Wolverine) before the load/fault scenarios S2–S6 are worth executing. The MassTransit
baseline remains the production-safe default; the backend flag flips cleanly in both
directions (rollback path exercised twice in this run).

## Fix verification (2026-07-18)

Both blockers were fixed and the failed scenarios re-run live on Wolverine.

**W1 fix** — per-service durability schema
(`WolverineMessagingExtensions.AddConduitWolverine`): the durability/node schema now
defaults to `wolverine_{service}` (`wolverine_conduit_gateway` /
`wolverine_conduit_admin`, overridable via `ConduitLLM:Messaging:Wolverine:SchemaName`)
while the transport queue schema stays shared (`wolverine_queues`) for cross-service
delivery.
**Verified:** each host is node 1 of its own cluster; the Gateway starts listeners on
**all** queues including the exclusive strict-ordered `spend_update_events` and
`image_generation_events`; zero `InvalidAgentException`.

**W2 fix** — bridge codegen (`AddEventBridge` + `AddConduitWolverine`): each closed
`WolverineHandlerBridge<TEvent>` is registered scoped and marked
`AlwaysUseServiceLocationFor`, and `ServiceLocationPolicy` is set to `AllowedButWarn`
(the marking alone still throws under `NotAllowed` — the policy gates the frames the
marking generates). Bridges resolve wholesale from the per-message scope — the same
semantics the MassTransit bridge always had.
**Verified live, zero `InvalidServiceLocationException`:**

- `BatchSpendFlushRequestedEvent` (Admin → Gateway): handler executed — "Batch spend
  flush request … completed successfully".
- `VideoGenerationRequested` → `WebhookDeliveryConsumer`: full ladder ran — and the
  **deferred retry actually works on Wolverine** (broken on the MassTransit baseline,
  see M1): "scheduling retry 1/3 in 2s / 2/3 in 4s / 3/3 in 8s" with `RetryCount`
  incrementing via native Postgres scheduled messages, then "Max retries (3) exceeded"
  recorded gracefully (consumer-managed terminal state — no dead-letter by design; the
  runbook's DLQ expectation applies only to unhandled faults).
- `ImageGenerationRequested`: consumed from the exclusive queue, orchestrator ran,
  mock image stored to R2, task `completed`.
- `ModelMappingChanged` invalidation and durable recovery: envelopes parked by the
  pre-fix codegen failures were recovered and processed after the fixed build deployed —
  final state 0 pending / 0 dead-lettered / all `Handled`.

**`SpendUpdateProcessor` (financial):** not exercised end-to-end in dev — the chat spend
tier legitimately rides Redis batching, and the tier-2 event fallback requires a Redis
outage the dev request path doesn't survive. Confidence rests on:
`BatchSpendFlushRequestedHandler` (identical constructor shape and registration
machinery) executing through the same bridge; the spend listener confirmed running; and
the 59-test messaging suite. **The S2 staging scenario (10k+ spend events under load) is
the definitive proof and remains mandatory.**

**Additional pre-existing bug fixed en route:** `ImagesController.Async` never set
`ExtensionData["VirtualKey"]` in task metadata, so **every** async image generation
failed with "Virtual key not found in task metadata" (both backends; the video
controller sets it correctly). Fixed by mirroring the video controller.

**New minor findings (pre-existing, both backends), not fixed:**
- Media orchestrator cost lookup uses `modelInfo.ModelId` which resolves to the literal
  string `"unknown"` in this flow → image/video generation bills **$0** (spend event
  never publishes for media). Needs its own issue — this silently disables media billing
  wherever it reproduces.
- The media orchestrator re-resolves the mapped `ProviderModelId` as if it were a model
  alias — generation only works when alias == provider model id.
- Model-mapping capability flags read stale/false from cache on cold start until a
  `ModelMappingChanged` invalidation repopulates the entry.

**Gate impact after fixes:** S1 is **green on Wolverine** for everything S1 can prove in
dev. #929's S2–S6 (spend ordering under load, crash fault-injection, rollback drill)
remain the outstanding gate work before #930 cutover.
