# Admin → Gateway Cache-Invalidation Trace (I0.3 / #912)

**Question:** when an admin edits config in the Admin API, how does the Gateway API
invalidate its caches — via **MassTransit** (the domain bus being migrated) or via a
**direct Redis pub/sub** (out of scope)? And is there a latent cross-service
staleness bug?

## Two independent buses

Conduit runs **two** distinct fan-out mechanisms. Only the first is in scope for #909.

1. **MassTransit domain bus** (in scope). Admin publishes domain events
   (`GlobalSettingChanged`, `ProviderUpdated`, `ModelMappingChanged`,
   `ModelCostChanged`, `IpFilterChanged`, `ProviderKeyCredential*`, …). Gateway
   instances run `IConsumer<T>` handlers that invalidate the relevant cache region.
   Transport is RabbitMQ in multi-instance mode, in-memory otherwise.

2. **Redis pub/sub cache bus** (out of scope — stays). `RedisVirtualKeyCache`,
   `RedisModelCostCache`, `RedisProviderToolCache` publish/subscribe on their own
   Redis channels for immediate cross-instance invalidation of those specific caches.
   This is **not** the MassTransit bus and is explicitly out of scope (epic non-goal).

## The traced path (GlobalSettingChanged)

```
Admin edit (AdminGlobalSettingService / LLMCacheManagementService)
  → IPublishEndpoint.Publish(GlobalSettingChanged)         [MassTransit]
  → fan-out to all bound instances
      ├─ Admin GlobalSettingCacheInvalidationHandler  (keeps Admin cache coherent)
      └─ Gateway GlobalSettingCacheInvalidationHandler (invalidates Gateway cache)
```

`GlobalSettingCacheInvalidationHandler` is registered as a consumer in **both** the
Admin (`Program.cs`) and Gateway (`Program.Messaging.cs`) hosts, so a single published
event invalidates both sides. Provider / model-mapping / model-cost / ip-filter edits
follow the same shape through their respective Gateway handlers.

**Conclusion: the Admin → Gateway path runs over MassTransit, not Redis pub/sub.** It
therefore moves wholesale to the abstraction in Phase 1 and to Wolverine in Phase 2.
The Redis pub/sub cache bus is parallel and untouched.

## Staleness risk

**Yes — a real, latent gap exists, and it is the durability gap the migration closes,
not a wiring bug.**

- Publishing is **fire-and-forget with swallowed failures**
  (`EventPublishingControllerBase.PublishEventFireAndForget` /
  `EventPublishingServiceBase.PublishEventAsync` both `catch … log … swallow`). If the
  broker is briefly unavailable, or the process crashes between the DB commit and the
  publish, the invalidation event is **lost** and Gateway caches stay stale until TTL
  or the next write. There is **no transactional outbox** today.
- `Gateway/Services/SettingsRefreshService` is a **no-op**: `RefreshProvidersAsync`,
  `RefreshModelMappingsAsync`, and `RefreshAllSettingsAsync` all just log and
  `return Task.CompletedTask` (provider/model config is read directly from the DB now).
  So there is **no periodic reconciliation** that would paper over a dropped event —
  the bus is the only path. This raises the stakes on publish durability.

## Decision

- **Bundle the durability fix into I2.4 (#927).** Once on Wolverine's Postgres
  transactional outbox, the invalidation publish commits in the **same transaction** as
  the business write, closing the crash-between-commit-and-publish window for the
  Admin→Gateway path. Idempotent consumers (cache invalidation is naturally idempotent)
  make at-least-once delivery safe.
- **No separate fix in Phase 1.** Phase 1 preserves today's exact (fire-and-forget,
  swallow) semantics so it stays a zero-behavior-change checkpoint; the swallow is
  flagged in the `IEventBus` MassTransit adapter for #927 to address.
- The `SettingsRefreshService` no-op is **left as-is** (correct — config is
  DB-driven); noted here only because it removes a would-be safety net, reinforcing the
  outbox decision.
