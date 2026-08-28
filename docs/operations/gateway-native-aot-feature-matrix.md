# Gateway NativeAOT feature matrix

This page defines the support contract for the first Conduit Gateway NativeAOT image. The contract is intentionally narrower than the JIT image: only behavior exercised by the native process gate is supported. The JIT service keeps its existing behavior.

The running Gateway exposes the same information at `GET /health/runtime-capabilities`. Operators should use that endpoint, rather than inferring support from a successful publish.

## Supported in the first native image

| Area | NativeAOT contract | Process-gate evidence |
|---|---|---|
| Host topology | One native Admin and two native Gateway processes start together. | Each process is launched and its liveness/capability endpoint returns 200. |
| PostgreSQL / Wolverine | Wolverine 6.14 PostgreSQL persistence and transport initialize in every host. Static application assembly selection and bounded generic metadata avoid runtime-codegen startup failures. | Real PostgreSQL migration, two-host startup, queue assignment, and representative static handler-adapter dispatch. The spend probe is expected to stop at the excluded EF query boundary after dispatch. |
| Redis / SignalR | Redis SignalR backplane and the Redis data-protection key ring initialize. | Backplane subscriptions and `Conduit-DataProtection-Keys` are observed through Redis. |
| SignalR protocol surface | JSON is the only protocol. Negotiate routes for all eight Gateway hubs are present on both Gateway hosts. | The native probe negotiates every hub route on both processes. |
| Authentication boundary | Requests without a virtual key are rejected before the EF-backed data plane. | `GET /v1/models` returns 401 without credentials. |
| Typed request persistence | Virtual-key lookup plus global and per-key IP policy use typed Npgsql repositories in the native Gateway. | With default-deny IP filtering enabled, a seeded virtual key negotiates all eight hub routes on both native Gateway processes. |
| Authenticated model discovery | Model list, retrieval, and effective capability metadata use the fixed-query typed Npgsql model-routing graph. | A seeded alias is listed and retrieved on both native Gateways, and its canonical/provider capability graph is returned by the metadata endpoint. |
| Operations | Liveness, Prometheus metrics, forwarded-header middleware, and OpenTelemetry startup are supported. | Native liveness and metrics endpoints are exercised. |

SignalR MessagePack is deliberately not registered by a NativeAOT publish and is unreachable to the native linker. A normal JIT build still references and enables MessagePack by default, and still honors `SIGNALR_MESSAGEPACK_ENABLED=false`.

## Explicit exclusions

The following features are excluded from the first native image and appear in `excluded_features`:

- `signalr-messagepack`
- `ef-core-query-data-plane`
- `authenticated-signalr-connections`
- `redis-virtual-key-cache-and-rate-limits`
- `provider-routing-and-streaming`
- `s3-media-api-workflows`
- `readiness-and-database-health`

EF Core 10 can generate the compiled `ConduitDbContext` model, but its NativeAOT query precompiler rejects Conduit's repository abstractions as dynamic LINQ. Extracted request-time operations now bypass those queries for global settings, IP filters, provider/credential reads, virtual keys, and model discovery/routing metadata. Request logging, tasks, and media persistence remain unextracted. Those boundaries still block end-to-end provider HTTP/SSE, cancellation, authenticated SignalR, and S3 media workflows. Those behaviors remain fully available in the JIT image.

Gateway request-time consumers now depend on `IVirtualKeyRuntimeService`, while
Admin-style key management remains behind the broader `IVirtualKeyService`. Both
contracts resolve to the same scoped implementation in JIT builds. Native builds now
resolve the request-time contract to `StoreBackedVirtualKeyRuntimeService`, while the
management contract remains on the legacy service. The native process gate proves this
request-time path through authenticated SignalR negotiation and IP policy. It does not
yet claim authenticated hub connections or an authenticated HTTP data plane, which
remain excluded downstream.

The persistence side of that seam is also explicit: `IVirtualKeyRuntimeStore` returns
backend-neutral key/group snapshots and performs atomic balance-plus-ledger updates.
Its fixed-query EF reference adapter and typed Npgsql adapter pass the same PostgreSQL
contract, including concurrent writers, idempotent redelivery, and conflict detection,
and the Npgsql adapter runs from the published native persistence probe. The native
Gateway now selects that adapter for virtual-key lookup, hydrated group limits, balance
validation, and direct spend fallback. The same native-only registration replaces the
global-setting, IP-filter, provider, and provider-credential repositories with their
already parity-tested typed-Npgsql implementations. A complete model-routing runtime
graph now has fixed-query EF and typed-Npgsql stores plus real-PostgreSQL and native
probe coverage. Native Gateway registers a read-only adapter over that store while JIT
and Admin retain the full EF repository; persisted route policy is read through the same
store. Logging, task, and media dependencies remain EF-backed, so the remaining
exclusions remain authoritative.

The compiled model is checked in under `Shared/ConduitLLM.Configuration/Data/CompiledModels`. Regenerate it whenever the EF model changes:

```powershell
$env:ConduitEfTooling = 'true'
$env:CONDUIT_EF_COMPILED_MODEL = 'true'
$env:DATABASE_URL = 'postgresql://user:password@localhost:5432/conduitdb'
dotnet ef dbcontext optimize --project Shared/ConduitLLM.Configuration --startup-project Shared/ConduitLLM.Configuration --context ConduitDbContext --output-dir Data/CompiledModels --namespace ConduitLLM.Configuration.Data.CompiledModels --nativeaot --configuration Release
```

The generator currently needs the three `Model = ConduitLLM.Configuration.Entities.Model` aliases retained in generated files because the domain entity name collides with an EF internal type.

## Native process gate

CI runs `scripts/aot/gateway-native-parity.ps1` after native publishing. It requires PostgreSQL, Redis, the standalone migrator, and the published native artifacts. The gate provisions a default-deny global IP policy, a virtual key with its own allowlist, and a complete model-routing graph. It negotiates every hub and exercises authenticated model list/retrieval on both native Gateways, then verifies effective capability metadata. The supported-boundary probe intentionally does not provision S3 or provider credentials because provider execution and media paths are downstream of the remaining excluded persistence boundaries.

The separate `scripts/test/wolverine-two-host-smoke.ps1 -NativeArtifactDirectory <artifact>` gate exercises cross-host Wolverine delivery with native Admin/Gateway processes.

## Third-party NativeAOT risk ledger

| Dependency | Version | Upstream reference | Remaining risk | Removal condition |
|---|---:|---|---|---|
| EF Core | 10.0.10 | [NativeAOT epic #29754](https://github.com/dotnet/efcore/issues/29754), [precompiled queries #25009](https://github.com/dotnet/efcore/issues/25009) | Dynamic repository query composition is rejected by the experimental precompiler. Query-backed features are excluded and observable. | The EF precompiler accepts Conduit's query shapes, or the repository layer is converted to precompilable/static queries; then enable and process-test the data plane before removing exclusions. |
| Wolverine / JasperFx | Wolverine 6.14.0, JasperFx 2.13.x | [Wolverine #2769](https://github.com/JasperFx/wolverine/issues/2769), [#2757](https://github.com/JasperFx/wolverine/issues/2757) | Wolverine still closes router/serializer generics and locates its generated handler registry and adapters reflectively. Conduit supplies narrow runtime directives and explicit application assembly selection. | Remove each directive only after the pinned dependency no longer uses runtime generic construction/exported-type discovery and the two-host native gate remains green. |
| MessagePack-CSharp | 3.1.4 | [NativeAOT support #1503](https://github.com/MessagePack-CSharp/MessagePack-CSharp/issues/1503), [generator issue #2283](https://github.com/MessagePack-CSharp/MessagePack-CSharp/issues/2283) | The SignalR MessagePack package/protocol is excluded from native builds; JSON remains supported. | Add a generated resolver for every hub payload, process-test all hubs with MessagePack, then remove the conditional package/protocol exclusion. |
| AWS SDK for .NET S3 | 4.0.x | [trim-safe runtime dependency work #4354](https://github.com/aws/aws-sdk-net/issues/4354), [AOT tracking #2486](https://github.com/aws/aws-sdk-net/issues/2486) | S3 construction publishes, but authenticated media workflows are unreachable until EF query support exists and are not claimed supported. | Restore native S3 upload/download/delete tests after the EF-backed media data plane is enabled; only then mark supported. |

First-party linker warnings remain tracked by the epic's later warning-cleanup tasks. This ledger records third-party runtime/AOT boundaries specific to protocol and infrastructure parity.
