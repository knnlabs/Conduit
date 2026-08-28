# NativeAOT audit and CI baselines

Gateway runtime feature support, exclusions, process gates, and the third-party risk ledger are defined in [gateway-native-aot-feature-matrix.md](gateway-native-aot-feature-matrix.md). A successful publish alone is not a claim that the full JIT data plane is supported.

NativeAOT readiness is measured separately from the normal JIT developer loop. The
production services remain framework-dependent until the persistence and runtime
parity phases are complete.

## Local analyzer audit

From the repository root, run:

```powershell
./scripts/aot/aot-audit.ps1
```

This is the single local audit command. It rebuilds the Admin and Gateway service
graphs with `ConduitAotAudit=true`, enabling AOT and trim analyzers, reflection-disabled
JSON defaults, generated configuration binding, and generated request delegates. It
does not set `PublishAot` or run the native linker.

Results are written to `artifacts/aot-audit/`:

- `diagnostics.json` contains every unique first-party warning with its code,
  project, source file, line, column, and message.
- `summary.md` reports counts by warning code and project.
- `admin.log` and `gateway.log` retain the raw build output.

The checked-in `scripts/aot/warning-baseline.json` is a ratchet grouped by project,
warning code, and source file. Counts may decrease without changing the baseline;
new groups or increased counts fail. After deliberately reviewing a changed warning
inventory, regenerate it with:

```powershell
./scripts/aot/aot-audit.ps1 -UpdateBaseline
```

Do not update the baseline to hide a regression. The later NativeAOT phases should
normally only reduce it.

## Analyzer ratchet

The 2026-08-12 post-phase audit contains **0** unique first-party diagnostics,
down from 509 before the epic-level cleanups. The first cleanup removed all 139
`MaxLengthAttribute` `IL2026` diagnostics by using statically analyzable string
validation and an explicit collection-count validator. The next cleanup removed 12
paired `IL2026`/`IL3050` diagnostics from the closed pricing-configuration shapes by
adding source-generated JSON metadata. The Bedrock Converse cleanup removed another
10 paired diagnostics by replacing dynamic JSON conversion with typed content handling
and generated metadata. The OpenAI-compatible mapping cleanup removed 10 more by using
typed tool-call contracts, generated annotation metadata, and explicit content-array
handling. The media-cleanup cache cleanup removed another eight paired diagnostics
with a dedicated source-generated Redis context. Regression tests preserve validation,
provider-wire behavior, and legacy cache contracts. Model-capability persistence then
removed eight more with generated configuration metadata while retaining legacy reads.
The async-task boundary cleanup removed another 22 paired diagnostics by generating
the persisted metadata/cache contracts and replacing anonymous result payloads with
statically described models or JSON DOM values. It also makes progress updates work
against the `JsonElement` values produced when persisted results are read back.
The shared security-cache cleanup removed another eight paired diagnostics by passing
generated contracts through its generic cache helpers and deleting an unused dynamic
value helper.
The multimodal content-helper cleanup removed another eight paired diagnostics by
limiting its supported shapes to strings, JSON elements, typed content parts, and
enumerables instead of reflectively serializing arbitrary objects to inspect them.
The Vertex service-account cleanup removed another eight paired diagnostics with a
generated credential/token/JWT contract, including explicit metadata for polymorphic
JWT string and integer claim values.
The shared Redis-cache cleanup removed another six paired diagnostics by requiring
source-generated metadata at its generic read/write boundary and registering the
Gateway provider, tool, and parsed-pricing cache shapes.
The streaming cleanup removed another six paired diagnostics by requiring generated
metadata for generic SSE/custom-stream parsing and registering the OpenAI-compatible
and MiniMax stream contracts.
The prompt-cache marker cleanup removed another six paired diagnostics by converting
known HTTP content through generated metadata and cloning existing JSON DOM nodes.
The provider-wire cleanup removed another 26 diagnostics by generating Cloudflare
image, Replicate prediction, OpenAI-compatible audio/chunk, and MiniMax chat/video
contracts, while making Replicate diagnostic formatting reflection-free.
The function-subsystem cleanup removed all 46 remaining Functions diagnostics by
generating built-in provider, pricing, execution-record, MCP, structured-JSON, and
hybrid-cache contracts; model-capability cache callers now provide primitive metadata.
The Core-boundary cleanup removed all 76 remaining Core diagnostics by generating
closed cache, pricing, webhook, error, rate-limit, and orchestration contracts; generic
HTTP and cache paths now resolve configured metadata, and ephemeral-key services pass
their generated contracts into the shared base class.
The configuration cleanup removed all 23 remaining Configuration diagnostics with
generated persistence contracts, statically analyzable collection/range validation,
the checked-in compiled EF model boundary, and removal of a redundant LINQ conversion.
The final provider cleanup removed all 12 remaining Providers diagnostics and wires the
generated provider/Core resolvers through generic HTTP calls. OpenAI-compatible request
maps now use statically described dictionaries, and provider response/stream contracts
are registered end to end.
The final Security cleanup removed its last paired diagnostic with a generated
middleware error-response contract.
The final Admin cleanup removed all 73 remaining diagnostics by routing persisted
and HTTP JSON through generated contracts, replacing reflective options validation,
and removing redundant query conversion.

The first-party analyzer inventory is now empty. Treat the generated
`diagnostics.json` as the source of truth for the fast analyzer lane. This does not
mean that the native linker inventory is empty: the linker analyzes additional
closed generic instantiations and EF expression trees that are not reached by the
regular compiler analyzers.

## Native publish lane

`.github/workflows/native-aot.yml` publishes both services for `linux-x64` after
merges to `master`, on a weekly schedule, and on manual dispatch. The workflow:

1. Native-publishes both service projects without changing normal build properties.
2. Inventories the real linker diagnostics and rejects increases above the checked-in
   `scripts/aot/linker-warning-baseline.json` ratchet.
3. Moves `.dbg`/`.pdb` files out of runtime directories into a symbols artifact.
4. Launches each native executable using the infrastructure-free OpenAPI entry path.
5. Retains publish time, executable and runtime size, OpenAPI readiness time, peak
   working set, generated OpenAPI documents, and process logs.

Native smoke results are measurements during the readiness epic. A known runtime
failure is visible in the report and job summary without discarding successful
publish artifacts. JIT build/test and Docker validation jobs are unchanged.

The publish lane writes `linker-diagnostics.json` with each de-duplicated first-party
`IL2026`, `IL3050`, `IL207x`, or `IL209x` diagnostic and `linker-summary.md` with
counts by service, project, and warning code. The evaluator is also independently
callable against existing publish logs:

```powershell
./scripts/aot/evaluate-native-linker-warnings.ps1 `
  -ReportDirectory artifacts/native-aot/reports `
  -BaselinePath scripts/aot/linker-warning-baseline.json
```

EF Core 10's compiled-model generator emits closed enum and array mappings through
APIs annotated for arbitrary runtime types. The 22 affected generated `Create`
methods carry exact `IL3050` exceptions with an owner, upstream issue, and removal
condition. `scripts/aot/normalize-compiled-model.ps1` reapplies and verifies that
bounded generated-file set after regeneration.

The 2026-08-28 `win-x64` native publish contains **0** first-party linker diagnostics.
The checked-in linker baseline is therefore empty: any new first-party `IL2026`,
`IL3050`, `IL207x`, or `IL209x` diagnostic now fails the native publish lane.

This closeout removed the eight supported Gateway metrics diagnostics by introducing
the fixed-shape `IGatewayMetricsStore`; its EF reference and typed-Npgsql adapters pass
the same real-PostgreSQL aggregate contract. The remaining 29 emitted diagnostics came
from 25 EF query methods on the Admin management/reporting path that ADR 0006 explicitly
excludes from the supported NativeAOT data plane. Those methods now carry individual
`IL2026` exceptions with the database/runtime owner, `dotnet/efcore#29754`, and a
removal condition requiring either a fixed-shape native store or trim-safe EF query
construction. No assembly-, type-, or warning-category suppression is used.

The zero first-party inventory satisfies the local linker criterion; production
NativeAOT promotion still requires a successful `linux-x64` release publish plus the
full feature-parity, digest-pinned benchmark, ordered soak, and rollback gates described
in the feature matrix and promotion policy.
