# Messaging Migration — Inventory & Tracking Matrix (I0.4 / #913)

Living checklist for the MassTransit → Wolverine migration (epic #909). This is the
Phase 1 burn-down: every publish site, consumer, event type, tuned endpoint, and
coupled test file, tagged by risk tier.

**Risk tiers**
- 🟢 **low** — cache-invalidation fan-out (idempotent, no ordering, no money)
- 🟡 **med** — media-generation orchestrators (progress/complete/fail, partitioned)
- 🔴 **high** — financial / strictly-ordered / deferred-delivery (spend + webhook)

> The abstraction lives in `ConduitLLM.Configuration/Messaging` (`IEventBus`,
> `IEventHandler<T>`, `IEventContext`, `EndpointPolicy`) because `Configuration` is
> the lowest project that contains publish sites and is referenced by every project
> that publishes or consumes. The MassTransit adapter sits beside it under
> `Messaging/MassTransit/`.

---

## 1. Publish sites (→ `IEventBus.PublishAsync`)

Two seams currently wrap `IPublishEndpoint`:
`EventPublishingControllerBase.PublishEventFireAndForget` (Core/Controllers) and
`EventPublishingServiceBase.PublishEventAsync` (Core/Services). The rest call
`IPublishEndpoint.Publish` directly (often resolved from a DI scope).

| Location | Events | Tier |
|---|---|---|
| `Core/Controllers/EventPublishingControllerBase.cs` (seam, 2 overloads) | any | 🟢 |
| `Core/Services/EventPublishingServiceBase.cs` (seam, 2 overloads) | any | 🟢 |
| `Admin/Controllers/ProviderCredentialsController.Providers.cs` | ProviderCreated/Updated/Deleted | 🟢 |
| `Admin/Controllers/ProviderCredentialsController.Keys.cs` | ProviderKeyCredential{Created,Updated,Deleted,PrimaryChanged} | 🟢 |
| `Admin/Controllers/ModelController.cs` | ModelUpdated | 🟢 |
| `Admin/Controllers/FunctionConfigurationsController.cs` | FunctionConfigurationChanged | 🟢 |
| `Admin/Controllers/SystemInfoController.cs` | DiscoveryCacheInvalidationRequested, FunctionDiscoveryCacheInvalidationRequested | 🟢 |
| `Admin/Controllers/ProviderErrorsController.cs` | ProviderKeyReenabledEvent | 🟢 |
| `Admin/Controllers/BatchSpendingController.cs` | BatchSpendFlushRequestedEvent | 🔴 |
| `Admin/Services/LLMCacheManagementService.cs` | GlobalSettingChanged | 🟢 |
| `Admin/Services/CacheManagementService.cs` | CacheConfigurationChangedEvent | 🟢 |
| `Admin/Services/Admin{VirtualKey,ModelProviderMapping,ModelCost,IpFilter,GlobalSetting}Service.cs` | (optional publish endpoint) | 🟢 |
| `Gateway/Controllers/ImagesController.cs` | ImageGenerationRequested, ImageGenerationCancelled | 🟡 |
| `Gateway/EventHandlers/SpendUpdateProcessor.cs` | SpendUpdateDeferred, SpendUpdated, SpendThresholdExceeded | 🔴 |
| `Gateway/EventHandlers/BatchSpendFlushRequestedHandler.cs` | BatchSpendFlushCompletedEvent | 🔴 |
| `Gateway/Authentication/VirtualKeySignalRRateLimitFilter.cs` | RateLimitExceeded, ConnectionLimitExceeded | 🟢 |
| `Core/Services/BatchWebhookPublisher.cs` | WebhookDeliveryRequested (batched) | 🔴 |
| `Core/Services/Abstractions/MediaGenerationOrchestrator.cs` | SpendUpdateRequested, WebhookDeliveryRequested | 🟡/🔴 |
| `Core/Services/ImageGenerationOrchestrator.cs` | progress/complete/fail | 🟡 |
| `Core/Services/VideoGenerationOrchestrator.cs` | lifecycle | 🟡 |
| `Core/Services/VideoProgressTrackingOrchestrator.cs` | progress | 🟡 |
| `Core/Services/HybridAsyncTaskService.cs` (+ `.Advanced.cs`) | AsyncTask{Created,Updated,Deleted} | 🟢 |
| `Core/Services/ImageGenerationResilienceService.{Recovery,Failover}.cs` | ProviderRecoveryInitiated, ProviderFailoverReverted/Initiated, ProviderQuarantined | 🟢 |
| `Core/Services/ProviderErrorTrackingService.cs` | ProviderKeyDisabledEvent | 🟢 |
| `Core/Services/Strategies/{Base64,Url}MediaProcessor.cs` | MediaGenerationCompleted | 🟡 |
| `Configuration/ProviderService.cs` | ProviderKeyCredential{Created,Updated,Deleted,PrimaryChanged} | 🟢 |
| `Configuration/Services/CacheConfigurationService.cs` (+ `.Audit.cs`) | CacheConfigurationChangedEvent | 🟢 |
| `Admin/Extensions/ServiceCollectionExtensions.cs` | (optional publish endpoint resolution) | 🟢 |

## 2. Consumers (→ `IEventHandler<T>`)

| Consumer | Event(s) | Endpoint | Tier |
|---|---|---|---|
| `Gateway/EventHandlers/VirtualKeyCacheInvalidationHandler` | VirtualKeyUpdated*, VirtualKeyCreated, VirtualKeyDeleted, SpendUpdated | default | 🟢 |
| `Gateway/EventHandlers/SpendUpdatedHandler` | SpendUpdated | default | 🟢 |
| `Gateway/EventHandlers/ProviderEventHandler` | provider events | default | 🟢 |
| `Gateway/EventHandlers/ProviderCacheInvalidationHandler` | provider events | default | 🟢 |
| `Gateway/EventHandlers/ProviderCredentialCacheInvalidationHandler` | provider credential events | default | 🟢 |
| `Gateway/EventHandlers/ProviderKeyCredentialCacheInvalidationHandler` | provider key events | default | 🟢 |
| `Gateway/EventHandlers/ModelCacheInvalidationHandler` | ModelUpdated | default | 🟢 |
| `Gateway/EventHandlers/DiscoveryCacheInvalidationHandler` | DiscoveryCacheInvalidationRequested | default | 🟢 |
| `Gateway/EventHandlers/AsyncTaskCacheInvalidationHandler` | AsyncTask{Created,Updated,Deleted} | default | 🟢 |
| `Gateway/Consumers/IpFilterCacheInvalidationHandler` | IpFilterChanged | default | 🟢 |
| `Gateway/Consumers/ModelCostCacheInvalidationHandler` | ModelCostChanged | default | 🟢 |
| `Gateway/Consumers/ModelMappingCacheInvalidationConsumer` | ModelMappingChanged | default | 🟢 |
| `Core/Consumers/GlobalSettingCacheInvalidationHandler` | GlobalSettingChanged | default | 🟢 |
| `Core/Consumers/FunctionConfigurationCacheInvalidationHandler` | FunctionConfigurationChanged | default | 🟢 |
| `Core/Consumers/FunctionDiscoveryCacheInvalidationRequestHandler` | FunctionDiscoveryCacheInvalidationRequested | default | 🟢 |
| `Gateway/EventHandlers/MediaLifecycleHandler` | MediaGenerationCompleted | default | 🟢 |
| `Gateway/EventHandlers/ImageGenerationProgressHandler` | ImageGenerationProgress | default | 🟡 |
| `Gateway/EventHandlers/ImageGenerationCompletedHandler` | ImageGenerationCompleted | default | 🟡 |
| `Gateway/EventHandlers/ImageGenerationFailedHandler` | ImageGenerationFailed | default | 🟡 |
| `Gateway/EventHandlers/VideoGenerationStartedHandler` | VideoGenerationStarted | default | 🟡 |
| `Gateway/EventHandlers/VideoGenerationProgressHandler` | VideoGenerationProgress | default | 🟡 |
| `Gateway/EventHandlers/VideoGenerationCompletedHandler` | VideoGenerationCompleted | default | 🟡 |
| `Gateway/EventHandlers/VideoGenerationFailedHandler` | VideoGenerationFailed | default | 🟡 |
| `Core/Services/ImageGenerationOrchestrator` | ImageGenerationRequested | **image-generation-events** | 🟡 |
| `Core/Services/VideoGenerationOrchestrator` | VideoGenerationRequested | **video-generation-events** | 🟡 |
| `Core/Services/VideoProgressTrackingOrchestrator` | VideoProgressCheckRequested | **video-generation-events** | 🟡 |
| `Gateway/EventHandlers/BatchSpendFlushRequestedHandler` | BatchSpendFlushRequestedEvent | default | 🔴 |
| `Gateway/EventHandlers/SpendUpdateProcessor` | SpendUpdateRequested | **spend-update-events** | 🔴 |
| `Gateway/Consumers/WebhookDeliveryConsumer` | WebhookDeliveryRequested | **webhook-delivery** | 🔴 |

**Abstract bases that also implement `IConsumer<T>`** (must move to `IEventHandler<T>`):
`Gateway/EventHandlers/BatchInvalidationEventHandler<T>`,
`Gateway/EventHandlers/ResilientEventHandlerBase<T>`.

## 3. Event types (45)

All are plain `record`s deriving from `ConduitLLM.Core.Events.DomainEvent`
(`EventId`/`Timestamp`/`CorrelationId`) — **no MassTransit coupling**, so they port
unchanged. Grouped files under `Core/Events/DomainEvents.*.cs` plus
`AsyncTaskEvents.cs`, `VideoProgressEvents.cs`, `MediaDeleted.cs`. A parallel set of
`ProviderKeyCredential*` events exists under `Configuration/Events/`. Two non-event
"command" records (`CacheConfigurationChangedEvent`, `BatchSpendFlushRequestedEvent`)
are published/consumed the same way.

## 4. Tuned endpoints (exact current policies — Gateway `Program.Messaging.cs`)

| Endpoint | Prefetch | Concurrency | Ordering | Retry | Circuit breaker | Rate limit | Queue args |
|---|---|---|---|---|---|---|---|
| **webhook-delivery** | 100 | 75 | none | Exponential(3, 1s→30s, step 2s) | 1m / trip 15 / active 10 / reset 5m | 100 / 1s | quorum, x-delivery-limit 10, x-max-length 50000, x-overflow reject-publish |
| **video-generation-events** | cfg | cfg | partition key (no SAC) | Incremental(3, 2s, +5s) | 2m / trip 20 / active 5 / reset 10m | — | quorum, ConfigureConsumeTopology |
| **image-generation-events** | cfg | cfg | x-single-active-consumer | Incremental(3, 1s, +3s) | 1m / trip 15 / active 5 / reset 5m | — | quorum, x-single-active-consumer |
| **spend-update-events** | 10 | **1** | x-single-active-consumer | Immediate(3) | — | — | quorum, x-single-active-consumer, x-max-length 10000 |

Bus-level (non-endpoint) policies: `PrefetchCount = cfg`,
`UseMessageRetry(Incremental(3, 1s, +2s))`, `UseDelayedRedelivery(5m, 15m, 30m)`.
Admin bus: `UseMessageRetry(Exponential(3, 1s→10s, step 2s))`, no tuned endpoints.

`spend-update-events` ordering is **RabbitMQ-native** (`x-single-active-consumer` +
`ConcurrentMessageLimit=1`), not MassTransit's partitioner — portable to any transport
that offers a single-active-consumer / strict-ordered listener.

## 5. Test files coupled to MassTransit (29)

`AddMassTransitTestHarness` / `ITestHarness` / `IBusControl`:
- `Tests/Integration/BatchInvalidationIntegrationTests.cs`
- `Tests/Integration/DiscoveryCacheInvalidationIntegrationTests.cs`

`Mock<IPublishEndpoint>` / `Mock<IBus>` (the bulk):
`Tests/HealthChecks/RabbitMQHealthCheckTests.cs`,
`Tests/Admin/Integration/ModelCostIntegrationTests.cs`,
`Tests/Services/ProviderErrorTrackingServiceTests.cs`,
`Tests/Services/Orchestrators/{VideoGenerationOrchestratorTests,ImageGenerationOrchestratorTests,MediaGenerationOrchestratorTestBase}.cs`,
`Tests/Admin/Services/CacheManagementServiceTests.LLMCache.cs`,
`Tests/Gateway/Services/CachedApiVirtualKeyServiceTests.cs`,
`Tests/Admin/Services/AdminVirtualKeyServiceTests.{Core,Generate,UpdateDelete}.cs`,
`Tests/Admin/Controllers/SystemInfoControllerTests.cs`,
`Tests/Admin/Controllers/ProviderCredentialsControllerTests.cs`,
`Tests/Admin/Controllers/ModelControllerTests.{*,CrudOperations,ProviderOperations,GetOperations}.cs` + `ModelControllerIntegrationTests.cs`,
`Tests/Gateway/EventHandlers/SpendUpdateProcessorTests.cs`,
`Tests/Gateway/EventHandlers/BatchSpendFlushRequestedHandlerTests.cs`,
`Tests/Configuration/Services/CacheConfigurationServiceTests.cs`,
`Tests/Gateway/Controllers/ImagesControllerTests.cs`,
`Tests/Admin/Services/AdminModelCostServiceTests.cs`,
`Tests/Core/Consumers/GlobalSettingCacheInvalidationHandlerTests.cs`,
`Tests/Gateway/Consumers/ModelMappingCacheInvalidationConsumerTests.cs`.

## 6. Health checks & observability

- `Core/HealthChecks/RabbitMQHealthCheck.cs` (injects `IBus`), registered in
  `Gateway/Program.Monitoring.cs` when `useRabbitMq`. Replaced by a Wolverine/bus
  health check in Phase 2 (#931).

## 7. Transport selection

Runtime switch in both `Gateway/Program.Messaging.cs` and `Admin/Program.cs`:
`useRabbitMq = !IsNullOrEmpty(RabbitMQ:Host) && Host != "localhost"` → RabbitMQ,
else in-memory. Phase 2 adds a `Messaging:Backend` flag (`MassTransit` default |
`Wolverine`) that selects the `IEventBus`/handler-host implementation independently of
the transport-selection logic above.
