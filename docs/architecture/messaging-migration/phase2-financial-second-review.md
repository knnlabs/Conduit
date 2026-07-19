# Financial-Path Second Review — PR #949 (I2.4 / #927), Parity Gate #929

Reviewer: independent second review (adversarial), read-only.
Repo: `Conduit-LLM`, branch `dev`, HEAD `77416598`. PR #949 merged as `2a74be19`.

---

## Area 1 — PR #949 diff vs current dev HEAD

**VERDICT: OK**

- `git log 2a74be19..HEAD` over the four financial files (`SpendUpdateProcessor.cs`, `CachedApiVirtualKeyService.cs`, `VirtualKeyGroupRepository.cs`, `EventPublishingServiceBase.cs`) plus the migration and `DomainEvents.VirtualKey.cs` shows **zero post-merge changes** — current HEAD is byte-identical to the reviewed intent for all financial code.
- `WolverineMessagingExtensions.cs` **was** modified post-merge (#928 in-memory transport, #931 metrics, #929 per-service durability schema). Verified the load-bearing line survived: `opts.Policies.UseDurableOutboxOnAllSendingEndpoints()` at `Shared\ConduitLLM.Configuration\Messaging\Wolverine\WolverineMessagingExtensions.cs:164`, still inside the Postgresql-transport branch. The new InMemory branch (`:139-146`, `DurabilityMode.Solo`) has **no outbox** — that is dev/CI-only by design and matches MassTransit in-memory semantics; the `TryPublishEventAsync` + direct-write fallback still covers it.
- Migration `20260717081905_AddIdempotencyKeyToVirtualKeyGroupTransactions.cs` present on HEAD: nullable `character varying(100)` column + filtered unique index `IX_VirtualKeyGroupTransactions_IdempotencyKey` (`filter: "\"IdempotencyKey\" IS NOT NULL"`). Expand-only, backward compatible with previous-release code (ADR-002 compliant). Index name contains "IdempotencyKey", matching the `IsIdempotencyKeyViolation` constraint-name check at `VirtualKeyGroupRepository.cs:311-324`.

Ops note (not a correctness issue): the index is created non-`CONCURRENTLY` (EF default, transactional migration), which takes a SHARE lock and blocks writes to the ledger table for the build duration. On a large `VirtualKeyGroupTransactions` table this is a deploy-window consideration.

---

## Area 2 — Negative-balance semantics / SpendThresholdExceeded downstream

**VERDICT: OK (pre-existing concerns, none introduced by #949)**

The old `newBalance >= 0` throw-after-commit is confirmed gone (`SpendUpdateProcessor.cs:100-175`). Under the old code a legitimately-negative balance threw *after* the debit committed, so each retry re-charged (the latent double-charge bug); worse, `SpendThresholdExceeded` only ever fired at exactly zero. The new code publishes the crossing at `SpendUpdateProcessor.cs:156-170` on `result.Applied && newBalance <= 0 && previousBalance > 0` and never throws for negative balances. This is a genuine fix.

Downstream trace of `SpendThresholdExceeded`:
- **There are no consumers.** Repo-wide grep finds the event referenced only in `BatchInvalidationEventHandler.cs:112` and `BatchCacheInvalidationService.cs:417` — both are *priority-classification* switch arms, not `IEventHandler<SpendThresholdExceeded>` subscriptions. No handler disables keys, no notification fires. `KeyDisabled = false` is always accurate because nothing ever disables.
- Actual enforcement is synchronous, per request: `VirtualKeyValidationHelper.ValidateVirtualKeyAsync` (`Shared\ConduitLLM.Core\Services\VirtualKeyValidationHelper.cs:46-61`) reads the group balance **directly from the DB** (`groupRepository.GetByIdAsync`) on every full validation and returns 402 when `Balance <= 0`. So gating is prompt once a debit commits — key disabling is not needed for enforcement.

Over-spend exposure: a group can go negative by (a) the cost of all requests admitted while balance was still positive (inherent to post-hoc billing) plus (b) **the spend-queue backlog**. `spend-update-events` runs `ConcurrentMessageLimit: 1` + single-active-consumer (`ConduitEndpointPolicies.cs:50-60`); if that single consumer lags, the DB balance stays positive while debits queue, and new requests keep being admitted. Exposure is proportional to backlog depth, not truly "arbitrary", but it is unbounded by any budget mechanism. This is pre-existing architecture, unchanged by #949. Related pre-existing risk: the queue carries `x-max-length = 10000` with no `x-overflow` argument (`ConduitEndpointPolicies.cs:57-60`), and RabbitMQ's default overflow is **drop-head** — under a >10k backlog the *oldest spend events are silently dropped* = lost spend on the MassTransit/RabbitMQ backend. Recommend `x-overflow: reject-publish` (the fallback would then charge directly) as a follow-up.

Many in-flight spend events applying after depletion: the crossing event fires exactly once (on the transition), subsequent debits just deepen the negative balance with no further signal — acceptable given zero consumers, and the 402 gate is already closed by then.

---

## Area 3 — Publish-failure fallback in CachedApiVirtualKeyService.UpdateSpendAsync

**VERDICT: OK**

Code: `Services\ConduitLLM.Gateway\Services\CachedApiVirtualKeyService.cs:186-282`; `TryPublishEventAsync` at `Shared\ConduitLLM.Core\Services\EventPublishingServiceBase.cs:145-181` (returns false on failure, never throws).

Adversarial scenarios, all traced to safe outcomes via the shared key `spend:{RequestId}` (`SpendIdempotency.KeyFor`, `DomainEvents.VirtualKey.cs:140-146`):

1. **Publish reports failure but event was actually accepted (late delivery AFTER fallback applied).** Fallback debits with `spend:{RequestId}` (`CachedApiVirtualKeyService.cs:217` → `:260-267`). Late `SpendUpdateRequested` reaches `SpendUpdateProcessor` → `AdjustBalanceIdempotentAsync` finds the ledger row (`IgnoreQueryFilters` `AnyAsync`, `VirtualKeyGroupRepository.cs:204-215`) → skip, republish notification only. **No double charge.**
2. **Fallback and late event race concurrently.** Both pass the `AnyAsync` pre-check; both attempt the atomic save; Postgres blocks the second INSERT on the in-flight unique-index entry, raises unique violation on the first's commit → `IsIdempotencyKeyViolation` backstop (`VirtualKeyGroupRepository.cs:222-230`) rolls back the loser's *entire* save (balance update included, same SaveChanges transaction) and returns `Applied: false`. **Exactly one debit.**
3. **Reverse order: event delivered first, fallback runs second.** Same dedup, fallback's `UpdateSpendDirectAsync` returns true either way (correct — the charge exists).
4. **Publish succeeded (returned true) then anything fails.** Fallback never runs; single event-path charge.
5. **Publish truly failed AND fallback throws (DB down).** Outer catch (`CachedApiVirtualKeyService.cs:227-231`) returns false; spend is lost only if the publish genuinely never reached the bus. This is the documented residual window ("crash before publish is inherent to post-hoc billing"); if the publish did sneak through, the consumer still applies it. No action available at that point; logged at Error.

The same `requestId` is provably the same value on both paths (single local variable, `CachedApiVirtualKeyService.cs:189`). Amounts are identical by construction, so a dedup skip never masks a different-amount charge.

---

## Area 4 — MediaGenerationOrchestrator.UpdateSpendAsync (billing publish inside handler)

**VERDICT: OK (one factual correction to the review brief, one minor concern)**

- **RequestId stability: confirmed.** `UpdateSpendAsync(vkId, cost, GetRequestId(request), ...)` at `MediaGenerationOrchestrator.cs:187`; `GetRequestId` = `request.TaskId` (image, `ImageGenerationOrchestrator.cs:35`) / `request.RequestId` (video, `VideoGenerationOrchestrator.cs:39`) — both come off the **event message itself**, so any redelivery of the same message carries the same RequestId. A handler retry can never mint a different RequestId; idempotency is not defeated.
- **Correction to the brief:** publish failure does **not** fail the handler. `HandleAsync` wraps the whole pipeline in `catch (Exception ex) → HandleFailureAsync(...)` with no rethrow (`MediaGenerationOrchestrator.cs:212-218`; `HandleFailureAsync` at `:495-544` does not rethrow). So an exception from the spend publish is swallowed: the task is marked Failed and a "failed" webhook fires *even though media was generated and stored*, and on MassTransit the spend is silently lost (no `TryPublish` fallback here). On Wolverine this window is closed differently: `PublishAsync` inside a handler enqueues on the message-context outbox and flushes at handler completion, so acceptance is near-infallible and durable. **Minor concern (MassTransit backend only, pre-existing shape): media spend has no publish-failure fallback, unlike the chat path.**
- **Double generation:** on crash-before-ack redelivery, `ShouldProcessRequest` returns `true` unconditionally (`ImageGenerationOrchestrator.cs:72-76` — no completed-task guard), so the media is generated again at provider cost. The second spend publish carries the same RequestId → deduped → **customer charged exactly once**. Operator eats the duplicate provider cost; not a double-charge or lost-spend defect.
- **Wolverine crash windows:** crash before handler completion → outbox not flushed, spend publish discarded, message redelivered, regenerate + republish same RequestId → one charge. Crash after flush but before ack → redelivery → duplicate spend publish → deduped by ledger key. Both safe.

---

## Area 5 — SpendUpdateProcessor paths

**VERDICT: OK (three latent concerns, none blocking)**

- **Idempotent-skip path** (`SpendUpdateProcessor.cs:130-148`): on duplicate, republishes `SpendUpdated` only. Consumers are `VirtualKeyCacheInvalidationHandler` (cache invalidation — idempotent) and `SpendUpdatedHandler` (SignalR notification — a duplicate toast at worst, `SpendUpdatedHandler.cs:32+`). No financial write downstream of `SpendUpdated`. Correct design: covers crash-after-debit-before-notification.
- **Duplicate + threshold:** `SpendThresholdExceeded` is deliberately *not* republished on duplicate (`:156` guard `result.Applied`). If the first attempt crashed after the debit committed but before the threshold publish (on Wolverine: before outbox flush), the crossing signal is lost permanently — the comment "the crossing was already reported" is not strictly true. **Currently moot (zero consumers, Area 2), but a trap if consumers are ever added.**
- **`previousBalance` staleness:** `previousBalance` is read from a `GetByIdAsync` on a separate context (`:90, :97`) before the adjustment commits in another context. A concurrent direct-write fallback debit (which itself never publishes a threshold event, `UpdateSpendDirectAsync`) can perform the depleting debit, after which the processor sees `previousBalance <= 0` and never fires the crossing. Missed/duplicate threshold events are possible under interleaving — notification-only impact, no monetary effect.
- **SpendUpdateDeferred** (`:55-74`): published when repositories are absent from scope — and **it has no consumer anywhere in the repo** (grep: only the event definition, the processor, and tests). If that branch ever executes, the spend evaporates into an unconsumed event. In practice it is dead code: the Gateway registers both repositories (`Program.Caching.cs:90-100` uses `GetRequiredService`), and the processor only runs in the Gateway. **Latent lost-spend path — should be an error/alert, not a silent defer.**
- **Throw mid-way / redelivery semantics:** any throw before the ledger commit → nothing applied → retry re-runs cleanly. Throw after commit (ambiguous commit, or during the `SpendUpdated` publish) → catch at `:177-183` rethrows → MassTransit: immediate retry ×3 (`ConduitEndpointPolicies.SpendUpdate`, `:56`) → duplicate detected → notification republished. Wolverine: `EndpointRetryHandlerPolicy` translates `Immediate(3)` to `RetryWithCooldown` with zero delays (`WolverineEndpointPolicy.cs:96, :145`) — same shape. Retries exhausted → error queue / Postgres dead-letter; debit (if committed) stays committed, only the notification is lost → stale cache until natural invalidation. No double-charge in any branch.
- **No-RequestId branch** (`:115-125`) uses non-idempotent `AdjustBalanceAsync`; combined with retry-on-throw this is the one remaining double-charge-capable path. All current publishers set RequestId (chat: `Guid.NewGuid()`, media: task id), so it is defensive legacy only. Consider logging it at Warning and/or rejecting empty-RequestId messages outright.

---

## Area 6 — VirtualKeyGroupRepository.AdjustBalanceIdempotentAsync

**VERDICT: OK**

- **Atomicity:** balance mutation and ledger insert happen in **one** `SaveChangesAsync` on one context (`ApplyBalanceAdjustmentAsync`, `VirtualKeyGroupRepository.cs:247-298`); EF Core wraps the UPDATE + INSERT in a single implicit transaction. There is no code path that commits one without the other: a unique violation aborts the whole save, so the catch path (`:222-230`) can never observe balance-updated-without-ledger-row or vice versa.
- **Check-then-insert race:** the `AnyAsync` pre-check (`:204-207`) is read-committed and racy by itself; the filtered unique index is the real guarantee. Loser's INSERT blocks on the winner's uncommitted index entry; violation raised only if the winner committed → correct skip. If the winner *aborts*, the loser's insert proceeds → correct apply. Both orders correct.
- **Ambiguous commit + EnableRetryOnFailure:** the Gateway context uses `EnableRetryOnFailure` (`DatabaseServicesExtensions.cs:37`). If a commit succeeds but its ack is lost and the strategy replays the save, the replay hits the unique index → violation → backstop skip. The idempotency key converts the classic retry-double-apply hazard into a no-op. (The non-keyed `AdjustBalanceAsync` retains that hazard — pre-existing, only reachable on legacy paths.)
- **Soft delete:** dedup check uses `IgnoreQueryFilters` (`:205`) so a soft-deleted ledger row still blocks re-application. Correct. Note the guarantee assumes ledger rows are never *hard-purged* within the redelivery/fallback window — worth stating in any future retention policy.
- **Constraint-name matching** (`:311-324`) walks the full inner-exception chain and matches `PostgresErrorCodes.UniqueViolation` + constraint name containing "IdempotencyKey" — matches the migration's index name; robust to wrapping.
- Duplicate/skip return (`GetCurrentStateAsync`, `:300-309`) reports post-winner state on a fresh context — consistent, since the violation implies the winner committed.

---

## Overall

**Financial-path second review: PASS with concerns**

- (pre-existing, MassTransit backend) `spend-update-events` queue declares `x-max-length=10000` without `x-overflow=reject-publish` → RabbitMQ default drop-head silently discards oldest spend events under extreme backlog. Recommend adding `reject-publish` so the publish fails and the direct-write fallback charges instead. (`ConduitEndpointPolicies.cs:50-60`)
- (latent) `SpendUpdateDeferred` has **no consumer** — the repositories-missing branch in `SpendUpdateProcessor` (`:55-74`) would silently lose spend. Dead in current deployments (Gateway registers the repos), but it should hard-error or alert rather than publish into the void.
- (latent) `SpendThresholdExceeded` has **no consumers** (no key disabling, no notification); depletion enforcement rests entirely on the per-request DB balance check (402). Fine today, but the "skipped on duplicate" logic in the processor means a crash between debit-commit and threshold-publish loses the crossing signal permanently if consumers are ever added.
- (minor, MassTransit backend) Media-generation spend publish (`MediaGenerationOrchestrator.cs:187, :625-634`) has no publish-failure fallback and its failure is swallowed by `HandleFailureAsync` — a genuinely failed publish loses that media spend. Closed on Wolverine by the handler outbox; consider a `TryPublish` + direct-write fallback for backend parity.
- (defensive) The empty-RequestId branch in `SpendUpdateProcessor` (`:115-125`) remains non-idempotent and retry-double-charge-capable; unreachable from current publishers, but worth rejecting outright.
- (ops) The idempotency-index migration builds the unique index non-concurrently — blocks ledger writes during the build on large tables; schedule accordingly.

No double-charge, lost-spend, or ordering defect was found in the code introduced by PR #949 on either backend. The idempotency-key design (ledger-row key, same atomic save, unique-index backstop, shared key across event and fallback paths) holds up under every adversarial interleaving examined.
