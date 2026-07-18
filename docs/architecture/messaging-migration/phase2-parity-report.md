# Phase 2 Parity Report (I2.6 / #929)

**Status: COMPLETE — all scenarios executed on both backends, 2026-07-18.**

Executes `phase2-parity-gate-runbook.md` on the dedicated `e909` gate environment
(Docker: real Postgres 16, RabbitMQ, Redis, Prometheus/Grafana/Jaeger; api + admin
built from `dev` HEAD `77416598`). The runbook's "staging" is realized as this
isolated local stack — the same containers, brokers, and configuration used by both
backends; only the host differs from a true remote staging deployment.

## Environment & harness

- Backend switch: `CONDUIT_MESSAGING_BACKEND` → `ConduitLLM__Messaging__Backend`
  (MassTransit baseline first, then Wolverine candidate; identical images).
- Wolverine: PostgreSQL transport, `AutoProvision=true`, per-service durability
  schemas (`wolverine_conduit_gateway` / `_admin`), shared `wolverine_queues`.
- Load harness (committed in `scripts/test/parity-gate/`):
  - `mock-provider.js` — OpenAI-compatible chat/images + MiniMax-compatible video
    API on `host.docker.internal:9099`; deterministic usage/cost; failure injection
    for S5.
  - `webhook-sink.js` — receipt recorder on `:9098` with per-delivery-key
    fail-first-N injection for deferral timing.
  - `seed-parity.js` — 20 virtual-key groups / 200 keys via Admin API.
  - `load-media.js` — S2: per-key **strictly sequential** async image generation
    (spend publish order per key == sequence order; media billing always rides
    `spend-update-events`, bypassing Redis batch tier); S3: async video generation
    carrying `webhook_url`.
  - `reconcile-spend.js` — joins driver log to `VirtualKeyGroupTransactions` via
    `IdempotencyKey = spend:{taskId}` and checks exactly-once, zero loss, per-key
    ordering (ledger `Id` asc vs seq), per-group money to the cent.
- Spend economics: `PerImage` cost $0.05/image → 10,000 events = $500.00 expected
  ledger debit total across 20 groups.

## Scenario results (side-by-side)

### S1 — Functional smoke (absorbs #923)

Executed 2026-07-17/18 on both backends; report:
`phase2-s1-smoke-report-dev.md`. **Green on both backends** after W1/W2 fixes
(PR #962) and #955–#960 fixes (PRs #963–#968). Carried forward unchanged.

### S2 — Spend ordering + exactly-once under load (FINANCIAL)

10,000 spend events, ~17 msg/s sustained, 200 keys / 20 groups, per-key
sequential submission.

| Measurement | MassTransit (baseline) | Wolverine (candidate) |
|---|---|---|
| Sustained submit rate | **17.0/s** (600.2s wall, 10,000/10,000 completed, 0 errors) | **17.0/s** (600.1s wall, 10,000/10,000 completed, 0 errors) |
| Events applied (ledger rows, keyed) | **10,000** (`unexpectedRows: 0`) | **10,000** (`unexpectedRows: 0`) |
| Exactly-once (distinct IdempotencyKey == rows == tasks) | **PASS** (10,000 == 10,000 == 10,000) | **PASS** (10,000 == 10,000 == 10,000) |
| Zero loss (missing spend) | **PASS** (0 missing) | **PASS** (0 missing) |
| Per-key ordering violations | **0** across 200 keys × 50 seq | **0** across 200 keys × 50 seq |
| Per-group money reconciliation ($0.000001 tolerance) | **PASS** — $500.000000 applied == expected, all 20 groups | **PASS** — $500.000000 applied == expected, all 20 groups |
| Spend queue depth during run / drain-to-zero | max **11**, drained to 0 (137 samples @5s) | max **6**, drained to 0 |
| Task latency P50 / P95 / P99 | 428 / 462 / 1,039 ms | 433 / 858 / 923 ms |

Wolverine numbers captured with the W3/W4 fixes (PR #974) applied on top of dev
HEAD `77416598`.

### S3 — Webhook throughput + deferral timing

Async video tasks each producing one webhook delivery to the sink;
target ≥1,000 deliveries/min for ≥10 min.

| Measurement | MassTransit | Wolverine |
|---|---|---|
| Sustained delivery throughput | **1,080/min (18.0/s) for 10 min**, 10,800/10,800 delivered; supplemental run: 962/min ×10 min, 10,000/10,000 | **1,081/min (18.0/s) for 10 min**, 10,800/10,800 delivered |
| Zero loss (sink receipts == tasks, dedup `{TaskId}:{EventType}`) | **PASS** — 0 missing, 0 duplicate keys | **PASS** — 0 missing, 0 duplicate keys |
| Deferred retry gaps (sink 500 on first two attempts) | **2.13s → 4.27s**, success on attempt 3; consistent across all 5 probes | **2.2s → 4.3s**, success on attempt 3; all 5 probes (native Postgres scheduled messages) |
| Rate limiting (≤ ~100/s egress, app-level) | PASS (load ≤18/s, well under limiter; `webhook-delivery` depth max 1) | PASS (`webhook_delivery` depth max 5) |

### S4 — Crash fault-injection (FINANCIAL)

Procedure: 17/s image load, `docker kill` (SIGKILL) the Gateway at ~750 tasks
submitted, ~43s outage, restart, drain, audit every submitted task
(`s4-audit.js`: final task state × ledger row).

| Measurement | MassTransit (expected loss — documents the delta) | Wolverine |
|---|---|---|
| SIGKILL Gateway mid-load: lost spend after drain | **0 lost** — 1,793/1,793 submitted tasks completed AND billed exactly once (quorum-queue requeue + #949 idempotency covered this crash class; the fire-and-forget publish window did not bite in this run) | **0 lost** — 1,777/1,777 submitted tasks completed AND billed exactly once (~43s outage) |
| Kill in processing window: redelivery + idempotent single application | **PASS** — in-flight tasks at kill redelivered on restart; 0 duplicate ledger rows; 0 stuck tasks | **PASS** — 0 duplicate ledger rows; 0 stuck tasks |
| Durable envelope recovery on restart | n/a (no inbox) | **Observed**: durability agent logged "recover 5 incoming messages from the inbox to postgresql://image_generation_events/" + recoverable outbox messages for `gateway_events`; all recovered work completed and billed once |

Note on the MassTransit "expected loss": the baseline survived this particular
crash class cleanly because the submitted `ImageGenerationRequested` events were
already durable in RabbitMQ quorum queues and the ledger idempotency (#949)
absorbed redelivery. The MassTransit loss windows that remain are the
fire-and-forget publish gap (publish accepted by the API but process dies before
the broker confirms) and spend-queue drop-head under >10k backlog — both closed
by Wolverine's transactional outbox/inbox, neither reliably reproducible by
process-kill timing in this environment.

Driver-side: 207 submissions failed at the HTTP layer during the outage (Gateway
down — request-path loss, identical on both backends by design; billing is
post-hoc). All 7 tasks whose status polling was cut off by the outage completed
and billed.

### S5 — Backpressure & circuit breaker

Procedure: mock provider forced to 100% HTTP 500 (`failRate=1`), 200 image tasks
at 10/s, then heal and re-drive.

| Measurement | MassTransit | Wolverine |
|---|---|---|
| Failing provider under load: breaker pause/resume | **Breaker never engages — by design.** Provider failures are converted to task-`failed` inside the orchestrator (`LLMCommunicationException` → `HandleFailureAsync`, no rethrow); the message acks, consumption continues (queue 0/0/0), no dead-letters. 200/200 failed gracefully. | **Identical**: 200/200 failed gracefully, 0 `wolverine_dead_letters`, queue drained, no breaker engagement |
| Failure visibility (metrics) | Failures visible as task failures + `Tracked ServiceUnavailable error` log events; endpoint breaker sees nothing (no handler faults) | Identical (task failures + error logs; no handler faults for the breaker/`wolverine-execution-failure` to see) |
| Health check reflects stress / recovers | Bus health unchanged (no transport faults); recovery immediate — 50/50 completed the moment the provider healed | Bus health `Healthy` throughout; recovery immediate — 50/50 |

The listener circuit breaker on both backends protects against *handler/transport*
faults, not provider errors — media handlers absorb provider errors into task
state. Parity requires Wolverine to show the same graceful absorption.

### S6 — Rollback drill (Wolverine → MassTransit)

Executed under live load (5/s video submissions with webhooks):

- **Time-to-rollback: 9s** from `docker compose up` with `Backend=MassTransit`
  to Gateway `/health/ready` 200. Client-visible disruption: 31 submit errors in
  a **6.0s** window (process restart gap); everything before and after flowed.
  This is the #930 rollback SLA baseline.
- **Clean startup + RabbitMQ rebind**: 39 MassTransit consumer-bridge endpoints
  rebound; deliveries continued to the sink immediately after restart
  (867/869 of the drill's webhooks delivered on RabbitMQ).
- **Drain procedure**: 2 webhook envelopes were in `wolverine_queues.*` at the
  flip instant. They are durable, not lost — they sat in the table during the
  MassTransit period and **delivered as soon as the backend flipped back to
  Wolverine** (sink total reconciled to the exact submitted count). Procedure
  for #930: check `wolverine_queues.wolverine_queue_*` depths are 0 before
  flipping (drain-first), or accept that residual rows deliver on flip-back;
  under sustained rollback, replay them by briefly starting one Wolverine-backed
  instance or moving the rows' payloads manually.

## Financial-path second review (blocking)

Full report: [`phase2-financial-second-review.md`](phase2-financial-second-review.md).
Adversarial review of the #949 diff (verified unchanged on HEAD), negative-balance
semantics, publish-failure fallback interleavings, media spend publish, processor
retry semantics, and `AdjustBalanceIdempotentAsync` atomicity.

**Verdict: PASS with concerns** — no double-charge, lost-spend, or ordering defect
in the migration-introduced code on either backend. Concerns (all pre-existing or
latent, none blocking):

- (pre-existing) `spend-update-events` `x-max-length=10000` without
  `x-overflow=reject-publish` → RabbitMQ drop-head can silently lose the oldest
  spend events under >10k backlog — a MassTransit-side risk that the Wolverine
  Postgres transport does not share.
- (latent) `SpendUpdateDeferred` and `SpendThresholdExceeded` have **no consumers**;
  the deferred branch would silently lose spend if ever reached — should hard-error.
- (minor) Media spend publish failures are swallowed with no fallback
  (`MediaGenerationOrchestrator`) — lost media spend on MassTransit; closed on
  Wolverine by the handler outbox.
- (defensive) Empty-RequestId branch in `SpendUpdateProcessor` is non-idempotent;
  unreachable today, should be rejected outright.
- (ops) The idempotency-index migration builds non-concurrently — schedule
  accordingly on large ledgers.

## Wolverine defects found and fixed by this gate (W3/W4 — PR #974)

The first Wolverine S2 attempt ran at ~5 msg/s with task latency P50 **53s**
(baseline: 428ms). Two compounding defects, both fixed on the gate branch and
re-verified live before the candidate numbers below were captured:

- **W3 — SAC mistranslated to strict ordering.** `WolverineEndpointPolicy` mapped
  `SingleActiveConsumer` → `ListenWithStrictOrdering()` (exclusive + sequential),
  but MassTransit's `x-single-active-consumer` keeps concurrent handling —
  `image-generation-events` runs 50-parallel on the RabbitMQ defaults. Now only
  `ConcurrentMessageLimit == 1` gets strict ordering; SAC alone maps to
  `ExclusiveNodeWithParallelism`.
- **W4 — Postgres queue polling at idle defaults.** The Postgres queue listener
  defaults to 20 messages/poll on the 5s `ScheduledJobPollingTime` cadence — a
  ~4 msg/s ceiling per queue (measured as 20-message pulses every ~4-5s).
  Listeners now poll every 250ms with `PrefetchCount` mapped to
  `MaximumMessagesToReceive`; the untuned `gateway_events`/`admin_events` queues
  are tuned identically.

Correctness was unaffected in both defective configurations (the partial slow run
reconciled exactly-once/ordered/zero-loss) — these were pure throughput defects,
which is precisely what S2's load target exists to catch.

## New findings during gate execution (pre-existing on dev HEAD, both backends)

1. **Polymorphic `PricingConfiguration` parsed case-sensitively → $0 billing.**
   `CostCalculationService.PricingModels.cs` deserializes `PerImage`, `PerVideo`,
   `PerSecondVideo`, `InferenceSteps`, and `TieredTokens` configs with default
   (case-sensitive) `JsonSerializer` options, while the documented and
   WebAdmin-produced JSON is camelCase (`{"baseRate": 0.05}`) — the config binds
   zeros and media bills **$0.00** silently. Only `RulesBased` passes
   `PropertyNameCaseInsensitive`. Filed + fixed separately (issue/PR referenced in
   the gate PR). Gate workaround: cost row stored PascalCase.
2. **Video generation broken through the caching decorator.**
   `ContextAwareLLMClient.CreateVideoAsync` throws
   `NotSupportedException: The underlying client PromptCachingLLMClient does not
   support video generation` — every async video task fails at the provider call
   regardless of backend. Webhooks still fire (`failed` events), which the S3 load
   exploits, but video generation itself is dead on dev HEAD. Needs its own issue.
3. **`PUT /api/modelcosts/{id}` 500s on its own GET payload** (round-trip update
   fails with "error occurred while saving the entity changes"), which also blocks
   the documented cache-invalidation path for cost edits. Needs its own issue.
4. **ModelCost caching is two-layered** (in-process + Redis `conduit-tasks:ModelCosts:*`)
   — out-of-band DB edits require flushing both; only the Admin API update path
   invalidates properly (and see finding 3).

## Known/accepted deltas

Per the runbook: `PrefetchCount` (no Postgres analogue), app-level webhook rate
limit, `CorrelationId` auto-generation, sequential `PublishBatchAsync`, and the
accepted I2.4 residual risks. Additionally from S1: MassTransit deferred webhook
retry never worked (missing `UseDelayedMessageScheduler`) — Wolverine's working
deferral is an improvement, not a regression (finding M1).

## Gate result

Wolverine, with the W3/W4 listener fixes (PR #974) applied, matches or exceeds
the MassTransit baseline on every scenario: identical spend correctness
(exactly-once / zero-loss / per-key ordering / money to the cent at 17 msg/s),
identical webhook throughput (1,080+/min) and deferral timing (2.2s/4.3s ladder),
identical graceful provider-failure behavior, superior crash durability
(observed inbox/outbox recovery), and a 9-second rehearsed rollback with a
documented drain procedure.

**Condition:** PR #974 (W3/W4) must merge to `dev` before #930 — the gate
numbers were captured with those fixes; without them Wolverine runs at ~5 msg/s.

```
Gate result: PASS (conditional on PR #974 merged)
Financial 2nd review: independent adversarial review, 2026-07-18 — PASS with
  concerns (all pre-existing/latent; see phase2-financial-second-review.md)
Sign-off to cut over (#930): Nick Nassiri (merge of PR #978), 2026-07-18
```
