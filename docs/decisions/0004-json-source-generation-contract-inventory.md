# JSON source-generation contract inventory

Status: Accepted

Issue: #1357

## Decision

Conduit uses explicit `JsonSerializerContext` metadata for its highest-value
HTTP, messaging, Redis, SignalR, and provider contracts. Metadata generation
keeps existing host serializer policies authoritative, so this change does not
alter property names, enum representation, null/default handling, or
polymorphism.

Reflection remains available as a fallback for lower-volume and deliberately
dynamic graphs. The focused compatibility project disables reflection
globally, which ensures every graph listed below is complete.

## Inventory

| Contract family | Context and owner | Wire options | Producer / consumer | Compatibility coverage |
| --- | --- | --- | --- | --- |
| OpenAI-compatible chat and errors | `CoreHttpJsonContext` / Core | `snake_case`, omit nulls; existing host converters remain authoritative | Gateway endpoints and SDK clients | `gateway-chat-response.json`; production Gateway options also exercised |
| Gateway discovery, models, files, uploads, and cancellation | `GatewayHttpJsonContext` and `ConfigurationHttpJsonContext` / Gateway and Configuration | `snake_case`, omit nulls | Gateway endpoints / API clients | `gateway-model-list.json`; reflection-disabled metadata closure |
| Admin errors, representative lists, and batch-spend status | `AdminHttpJsonContext` / Admin | `camelCase`, case-insensitive reads | Admin minimal APIs / WebAdmin and API clients | `admin-problem-details.json`; reflection-disabled metadata closure |
| Wolverine topology messages | `CoreMessagingJsonContext` / Core | Existing Pascal-case CLR names, numeric enums, and explicit JSON attributes | Gateway and Admin publishers / Wolverine handlers | `wolverine-spend-update.json`; runtime resolver-chain serialization |
| Gateway Redis cache and invalidation payloads | `GatewayRedisJsonContext` / Gateway | Existing Pascal-case names, case-insensitive reads | Gateway instances across a rolling deployment | `redis-model-cost-legacy.json` old-to-new and new-to-old; `redis-virtual-key-invalidation.json` |
| Redis webhook reliability state | `CoreRedisJsonContext` / Core | Existing Pascal-case names, case-insensitive reads | Webhook circuit-breaker instances | `redis-webhook-circuit-state.json` |
| Live SignalR notifications | `ConfigurationSignalRJsonContext` / Configuration | `camelCase`, case-insensitive reads | Admin notification hubs / WebAdmin | `signalr-virtual-key-created.json`; reflection-disabled metadata closure |
| Bedrock requests, responses, model discovery, and streams | `ProvidersJsonContext` / Providers | `camelCase`, omit nulls, case-insensitive reads | Bedrock client / AWS Bedrock | `bedrock-converse-response.json`; existing provider tests |
| OpenRouter catalog | `ProvidersJsonContext` / Providers | `camelCase`, omit nulls, case-insensitive reads | OpenRouter client / OpenRouter API | Reflection-disabled metadata closure; existing provider tests |

Batch-spend Redis accumulation uses scalar Redis values and hashes rather than
JSON documents. Its durable flush event is included in
`CoreMessagingJsonContext`.

## Compatibility boundaries

- The removed queued SignalR polymorphic envelope and its
  `DefaultJsonTypeInfoResolver` modifier are not present in the current
  Gateway/Admin path. The live strongly typed notification DTOs are generated
  without introducing a discriminator or changing the wire contract.
- Converted graphs do not require reflective discovery for arbitrary CLR
  objects. Existing OpenAI-compatible extension slots accept only supported
  JSON primitives, `JsonElement`, or source-generated bounded collection
  shapes; the reflection-disabled suite exercises those paths. Bedrock
  tool-choice marker objects now use `JsonElement` instead of unbounded
  `object`.
- The existing non-generic `JsonStringEnumConverter` remains a host fallback
  for endpoints outside this inventory. Converted contexts either preserve
  numeric enum behavior or contain no enum requiring the converter; they do not
  add a new runtime converter-factory dependency.
- Redis fixtures model a previous release using an explicit reflection resolver.
  They prove both previous-release payloads read on the new release and
  new-release payloads read with the previous serializer policy.

## Reflection-disabled verification

`ConduitLLM.SerializationTests` sets
`JsonSerializerIsReflectionEnabledByDefault=false`. It checks representative
fixtures in both directions and verifies that an unregistered type fails.

Run:

```powershell
dotnet test Tests/ConduitLLM.SerializationTests/ConduitLLM.SerializationTests.csproj -c Release
```

## Benchmark

BenchmarkDotNet, .NET 10.0.10, Intel Core i5-12400F, 8 measured iterations:

| Serializer metadata | Mean | Allocated | Ratio |
| --- | ---: | ---: | ---: |
| Reflection | 774.0 ns | 920 B | 1.00 |
| Source-generated | 737.9 ns | 920 B | 0.95 |

The representative chat response is approximately 4.7% faster with unchanged
per-operation allocation.

Run:

```powershell
dotnet run --project Tests/ConduitLLM.Benchmarks/ConduitLLM.Benchmarks.csproj -c Release -- --filter *JsonSourceGeneration*
```
