# Phase 2 Parity Gate Runbook (I2.6 / #929)

**Status: ready to execute — requires staging infrastructure (real Postgres + RabbitMQ +
Redis + load tooling). This is the sign-off gate for the #930 cutover.**

This gate certifies that the Wolverine backend behaves at parity with the MassTransit
baseline under load. It **absorbs three obligations** carried forward from earlier
issues:

1. The **staging smoke** originally specified for the Phase 1 gate (#923, never run in
   staging — Phase 1 shipped on the code-only gate).
2. The **mandatory second review of the financial path** (#921/#927).
3. The **crash fault-injection** acceptance for the transactional outbox (#927), which
   needs real Postgres.

## Prerequisites

- [ ] `dev` contains #924–#928 and #931 (verify: `git merge-base --is-ancestor` for the
      merge commits; do not trust PR badges — see the stacked-PR stranding incidents).
- [ ] Staging runs both hosts with `ConduitLLM:Messaging:Backend` switchable at deploy
      time; default `MassTransit`.
- [ ] EF migration `AddIdempotencyKeyToVirtualKeyGroupTransactions` applied
      (`dotnet ef database update`); verify the filtered unique index
      `IX_VirtualKeyGroupTransactions_IdempotencyKey` exists.
- [ ] Wolverine schema provisioning decided: staging may use
      `ConduitLLM:Messaging:Wolverine:AutoProvision=true`; production uses scripted
      provisioning (`false`).
- [ ] `ConduitLLM:Messaging:Wolverine:Transport` is `Postgresql` (default) — the
      in-memory mode is for dev/CI only and skips everything this gate certifies.
- [ ] Observability reachable on both hosts: `/metrics` (Prometheus) and
      `/health/ready`. On Wolverine expect the `wolverine_bus` check and the
      `Wolverine:{service}` meters; on MassTransit expect `rabbitmq_comprehensive`
      (Gateway) and the `MassTransit` meter.
- [ ] Load tooling able to drive: virtual-key chat completions (spend), image/video
      generation with webhook URLs, admin config edits. A webhook sink that records
      receipt timestamps (e.g. requestbin-style with logging) is required for S3.

## Standing CI guards (#961)

Two CI checks close the gap that let S1's W1/W2 hide from the in-memory dual-backend
tests (#928); both run on every push/PR in `.github/workflows/ci.yml`:

- **Build-ahead codegen check** (`validate` job): `dotnet run -- codegen preview` for
  Gateway and Admin with `Backend=Wolverine` compiles the handler chain for every
  registered event type up front, so codegen/service-location incompatibilities (W2's
  class) fail the build instead of first message delivery. This is also the checking
  half of the #930 static-codegen (`codegen write` + `TypeLoadMode.Static`) cutover
  optimization — the JasperFx command line is wired into both hosts' `Program`.
- **Two-host listener assertion** (`wolverine-two-host` job): boots the real Admin
  host first (recreating W1's leadership scenario), then the real Gateway, against
  real Postgres, and asserts per-service node clusters, the exclusive
  `spend-update-events` / `image-generation-events` listener agents assigned and
  started, all five Gateway queues listening, and logs free of
  `InvalidAgentException` / `InvalidServiceLocationException`. Runbook:
  `scripts/test/wolverine-two-host-smoke.ps1` (header documents a local-run recipe).

These guards are necessary but not sufficient for cutover — they prove topology and
codegen, not load behavior. S2–S6 below remain mandatory.

## Execution order

Run the **full scenario set twice**: first on `Backend=MassTransit` (baseline capture),
then on `Backend=Wolverine` (candidate). Record the same measurements both times; the
parity report is the side-by-side table.

> Deploy note (from Phase 1): the bridge-named queues replaced per-consumer queues —
> when flipping backends, drain in-flight messages first (see S7 rollback drill).

## Scenarios

### S1 — Functional smoke (absorbs #923)

| Step | Verify |
|---|---|
| Admin edits a global setting / provider / model mapping / model cost / IP filter | Gateway cache invalidates (observe next request behavior or cache logs) — Admin→Gateway invalidation rides the bus on Wolverine (#926) |
| Image generation request (async, with webhook) | Task completes; media stored; webhook delivered; spend debited |
| Video generation request + cancel | Cancel honored; no spend for cancelled work beyond policy |
| Direct webhook path failure (unreachable URL) | Retries at ~4s/8s/16s (2^(n+1)s, max 3), then dead-letter; `wolverine-dead-letter-queue` increments on Wolverine |
| Spend update on a chat completion | Group balance debited once; `SpendUpdated` notification observed; ledger row has `IdempotencyKey = spend:{RequestId}` |

### S2 — Spend ordering + exactly-once under load (FINANCIAL — HIGH RISK)

The spend queue (`spend-update-events`) is single-active-consumer with
`ConcurrentMessageLimit=1` (MassTransit/RabbitMQ) → `ListenWithStrictOrdering()`
(Wolverine/Postgres). Target sustained load: **~17 msg/s** (the tuned production rate),
mixed across ≥ 50 virtual keys in ≥ 10 groups, ≥ 10,000 total spend events.

Measurements (identical on both backends):

- [ ] **Per-key ordering**: for each key, ledger rows' `CreatedAt`/id order matches
      publish order (embed a sequence number in the request description or correlate
      via RequestId → publish log).
- [ ] **Exactly-once**: `SUM(ledger debits per group) == SUM(published amounts per group)`
      to the cent, AND `COUNT(DISTINCT IdempotencyKey) == COUNT(ledger rows with key)`
      (no duplicate application), AND every published RequestId has exactly one ledger row.
- [ ] **Zero loss**: no published RequestId missing from the ledger after drain
      (allow the retry window to complete before reconciling).
- [ ] Throughput ≥ baseline; consumer lag (`wolverine-inbox-count` for the spend queue /
      RabbitMQ queue depth) returns to ~0 after load stops.

### S3 — Webhook throughput + deferral timing

Webhook endpoint: concurrency 75, app-level rate limit 100/s. Target: **1,000+
webhooks/minute** for ≥ 10 minutes.

- [ ] Delivery throughput ≥ 1,000/min sustained on both backends; zero loss
      (sink receipt count == published count, dedup by `{TaskId}:{EventType}`).
- [ ] Deferred retry timing: for a sink returning 500 on first two attempts, observe
      gaps of ~4s then ~8s (`SchedulePublishAsync` → Postgres scheduled messages on
      Wolverine; `wolverine-scheduled-count` rises and drains).
- [ ] Rate limiting holds (≤ ~100/s egress) — app-level, so identical on both backends.

### S4 — Crash fault-injection (#927 acceptance, FINANCIAL)

On **Wolverine** only (this is the durability the migration buys):

- [ ] **Kill the Gateway process** (SIGKILL/container kill) mid-load after publishes are
      accepted but before consumers process them → on restart, the durability agent
      recovers persisted envelopes; reconcile S2 invariants — **no lost spend**.
- [ ] **Kill the Gateway between debit commit and ack**: pause the spend consumer under
      load (breakpoint/toxiproxy on the queue ack path, or kill within the processing
      window), confirm redelivery occurs, and the ledger shows the RequestId applied
      **once** (idempotent skip logged: "already applied - republishing notification").
- [ ] Baseline documentation: the same two kills on MassTransit are *expected* to lose
      and/or double-apply events (fire-and-forget + no inbox) — capture actual behavior
      for the report; this is the delta being purchased.

### S5 — Backpressure & circuit breaker

- [ ] Point image-generation at a deliberately failing provider under load: listener
      circuit breaker pauses/resumes (Wolverine) as the RabbitMQ breaker did; failures
      visible in `wolverine-execution-failure` tagged by exception type.
- [ ] Health check reflects stress: `wolverine_bus` goes Degraded when dead-letters
      accumulate; returns Healthy after replay/purge.

### S6 — Rollback drill (required before #930)

- [ ] Under light load, redeploy with `Backend=MassTransit`. Confirm clean startup,
      RabbitMQ queues rebind, and traffic continues. Document the drain procedure for
      in-flight Wolverine queue rows (wolverine schema tables) — either drain before
      flip or replay after.
- [ ] Record time-to-rollback; this becomes the #930 rollback SLA.

## Financial-path second review (blocking)

An engineer other than the implementer reviews, in staging context:

- [ ] PR #949 diff (`SpendUpdateProcessor`, `AdjustBalanceIdempotentAsync`,
      `CachedApiVirtualKeyService.UpdateSpendAsync` fallback, migration).
- [ ] The negative-balance semantics change: the old `newBalance >= 0` throw-after-commit
      is gone; depletion is signaled via `SpendThresholdExceeded`. Confirm downstream
      consumers (key disabling, notifications) behave under over-spend.
- [ ] The publish-failure fallback path: same RequestId key on both paths — confirm no
      scenario double-charges (event delivered late after fallback applied).
- [ ] `MediaGenerationOrchestrator.UpdateSpendAsync` (billing publish inside handler
      scope — rides the handler outbox on Wolverine; failure fails the handler → retry).

## Parity report + sign-off

Produce `phase2-parity-report.md` with the side-by-side table (scenario × backend ×
measurements), the fault-injection evidence, known deltas, and:

```
Gate result: PASS / FAIL
Financial 2nd review: <name>, <date>
Sign-off to cut over (#930): <name>, <date>
```

## Known/accepted deltas (do not fail the gate on these)

- `PrefetchCount` has no Postgres analogue (documented in I2.3).
- Webhook `RateLimit` is app-level on both backends (never transport-level on Wolverine).
- `CorrelationId` on a bare publish: Wolverine auto-generates, MassTransit leaves null —
  Conduit `DomainEvent`s carry their own, so no user-visible difference (#928 finding).
- `PublishBatchAsync` is sequential on Wolverine (batch was an optimization, not a
  semantic guarantee).
- Residual risks accepted in I2.4: crash-before-publish on the request path loses spend
  on BOTH backends (post-hoc billing); Admin invalidation crash-window (idempotent +
  TTL-backed consumers).
