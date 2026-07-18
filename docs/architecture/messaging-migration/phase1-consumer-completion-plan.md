# Phase 1 — Remaining consumer migration (#920, #921) + verification gate (#923)

The cache-invalidation consumers (#919) are migrated and green, which **proves the bridge
pattern end-to-end**. #920 (orchestrators) and #921 (high-risk spend/webhook) follow the
exact same recipe; they are separated out because their message **routing, ordering, and
deferred-delivery behavior can only be validated against real RabbitMQ/Redis/Postgres**
(the integration tests are Redis-gated and do not run in a code-only environment), and the
financial path requires the mandatory second review the epic mandates. They are therefore
specified here as turnkey follow-ups rather than blind-landed.

## The proven recipe (from #919)

For each consumer class:
1. `IConsumer<TEvent>` → `IEventHandler<TEvent>`; `Consume(ConsumeContext<TEvent> ctx)` →
   `HandleAsync(TEvent message, IEventContext context)`; `ctx.Message`→`message`;
   `ctx.Headers.TryGetHeader`→`context.TryGetHeader`; `ctx.ScheduleSend`→`context.SchedulePublishAsync`;
   injected `IPublishEndpoint`→`IEventBus` (`.Publish`→`.PublishAsync`).
2. Register `services.AddEventHandler<TEvent, THandler>()` and `x.AddEventBridge<TEvent>()`.
3. On a **tuned** endpoint, bind the bridge instead of the consumer:
   `e.ConfigureConsumer<MassTransitConsumerBridge<TEvent>>(context)` and apply the policy via
   `MassTransitEndpointPolicy.ApplyResiliencePolicies(e, ConduitEndpointPolicies.X)` plus the
   RabbitMQ-only settings (`SetQuorumQueue`, `SetQueueArgument`, `PrefetchCount`,
   `ConcurrentMessageLimit`, `x-single-active-consumer`) read from the same descriptor.
   MassTransit de-dups consumers already configured on an explicit endpoint, so the trailing
   `ConfigureEndpoints(context)` will not double-bind them.
4. Tests: invoke `HandleAsync` with `TestEventContext`; update harness registrations to bridge + handler.

## I1.6 — Orchestrators (#920)

Classes: `ImageGenerationOrchestrator` (`ImageGenerationRequested`),
`VideoGenerationOrchestrator` (`VideoGenerationRequested`),
`VideoProgressTrackingOrchestrator` (`VideoProgressCheckRequested`), and the
progress/complete/fail handlers (`ImageGeneration{Progress,Completed,Failed}Handler`,
`VideoGeneration{Progress,Completed,Failed}Handler`).

Special considerations:
- **Generic base** `MediaGenerationOrchestrator<TRequest,TResponse,TEventRequest> :
  IConsumer<TEventRequest>` (Core/Services/Abstractions). Convert the base to
  `IEventHandler<TEventRequest>` and its template-method `Consume` → `HandleAsync`; the two
  derived orchestrators inherit the new shape. The base injects `IPublishEndpoint` and
  publishes `SpendUpdateRequested` / `WebhookDeliveryRequested` — migrate to `IEventBus`
  (scoped, so it still flows through the consume scope). This also lands the two media
  processors (`Base64MediaProcessor`, `UrlMediaProcessor`) deferred from #918, which
  publish `MediaGenerationCompleted`.
- **Tuned endpoints**: bind `Bridge<VideoGenerationRequested>` + `Bridge<VideoProgressCheckRequested>`
  on `video-generation-events` (partition-key ordering, no single-active-consumer,
  `ConfigureConsumeTopology = true`) and `Bridge<ImageGenerationRequested>` on
  `image-generation-events` (single-active-consumer) using `ConduitEndpointPolicies.VideoGeneration`
  / `.ImageGeneration`. The progress/complete/fail handlers ride the default endpoints (like #919).
- Tests: `ImageGenerationOrchestratorTests`, `VideoGenerationOrchestratorTests`,
  `MediaGenerationOrchestratorTestBase` already mock `IPublishEndpoint` → switch to
  `Mock<IEventBus>` (publish) and invoke `HandleAsync` with `TestEventContext`.
- Acceptance: media-gen flows pass integration tests via the abstraction (Redis/RabbitMQ env).

## I1.7 — High-risk: SpendUpdateProcessor + WebhookDeliveryConsumer (#921) — financial/ordered

- **SpendUpdateProcessor** (`SpendUpdateRequested`): strict-ordered, DB writes, publishes
  `SpendUpdated`/`SpendUpdateDeferred`/`SpendThresholdExceeded`. Convert to
  `IEventHandler<SpendUpdateRequested>`; injected `IPublishEndpoint` → `IEventBus`. Bind
  `Bridge<SpendUpdateRequested>` on `spend-update-events` with
  `ConduitEndpointPolicies.SpendUpdate` (PrefetchCount 10, ConcurrentMessageLimit 1,
  single-active-consumer, immediate retry ×3) — **ordering is RabbitMQ-native and must be
  preserved exactly**. `BatchSpendFlushRequestedHandler` (`BatchSpendFlushRequestedEvent`,
  publishes the completion event) converts the same way on a default endpoint.
- **WebhookDeliveryConsumer** (`WebhookDeliveryRequested`): converts to
  `IEventHandler<WebhookDeliveryRequested>`. `context.MessageId` (dedup key) and
  `context.ScheduleSend(retryTime, msg)` → `context.SchedulePublishAsync(retryTime, msg)`
  (the `MassTransitEventContext` maps it back to `ScheduleSend`, preserving the deferred
  retry exactly). Bind `Bridge<WebhookDeliveryRequested>` on `webhook-delivery` with
  `ConduitEndpointPolicies.WebhookDelivery` (prefetch 100, concurrency 75, exponential
  retry, circuit breaker, rate-limit 100/s, quorum + delivery-limit/max-length/overflow).
- **BatchWebhookPublisher** (deferred from #918) uses `IPublishEndpoint.PublishBatch`,
  which has no `IEventBus` equivalent. Decision needed: either add
  `IEventBus.PublishBatchAsync(IEnumerable<T>)` to the abstraction (and a MassTransit
  adapter over `PublishBatch`), or have it publish via a loop of `PublishAsync`. Recommended:
  add the batch method to keep the high-throughput optimization.
- Mandatory **second reviewer** + extra ordering/deferral tests; verify spend ordering and
  webhook retry timing are unchanged before merge.

## I1.9 — Phase 1 verification gate (#923)

- `dotnet test` full suite (done for the publish + cache-consumer increments: 2489 unit
  tests green; the 13 Redis-backed failures are environmental, no running Redis).
- Staging smoke: admin edit → gateway invalidation; image/video gen; webhook; spend — on
  real RabbitMQ/Redis. **Requires staging infra**, so it is a CI/staging step, not a
  code-only check.
- Confirm zero behavior change; tag a safe checkpoint with **MassTransit still underneath**.

## Operational note — queue topology

Consolidating per-consumer queues behind one bridge **per event type** changes
auto-generated RabbitMQ queue names (the in-memory transport used in dev/test is
unaffected). Routing remains correct (MassTransit topology is by message type), but the
deploy that lands the consumer migration creates the new bridge-named queues; drain/observe
the old queues during rollout. This is an operational note for the #923 staging step.
