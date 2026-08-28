# ADR 0007: Refactor persistence through operation-specific dual backends

- Status: Accepted
- Date: 2026-08-27
- Decision owners: database/runtime maintainers
- Related: #1368, #1372, #1373
- Extends: ADR 0006

## Context

ADR 0006 correctly keeps production database work on JIT while EF Core's NativeAOT
query path remains experimental. The subsequent linker audit reduced the remaining
first-party diagnostics to 147 unique sites: 45 query-expression diagnostics and 102
diagnostics in the generated EF model. Making individual LINQ expressions static can
reduce the first group, but it cannot make the EF runtime a production-supported
NativeAOT dependency.

The persistence inventory contains 37 direct-query files and 344 query operators.
A big-bang replacement would duplicate model conversions, transaction behavior,
concurrency handling, retries, and PostgreSQL-specific semantics before parity could
be demonstrated. At the same time, retaining caller-supplied expressions and direct
service-level DbContext queries prevents a second implementation from being introduced
incrementally.

The existing repository APIs return materialized values rather than IQueryable. That
is a useful seam, but their assembly ownership and generic query helpers still bind the
surface to the EF-oriented Configuration project.

## Decision

Conduit will use a staged strangler refactor for runtime persistence:

1. Move one domain slice at a time into `ConduitLLM.Persistence.Abstractions`.
   Contracts expose complete named operations and materialized values. They do not
   accept IQueryable, expression trees, ordering delegates, or DbContext types.
2. Keep an EF Core implementation as the production default and behavioral reference
   while JIT images remain the supported rollback lane.
3. Add a typed-Npgsql implementation only for an extracted slice. Every alternate
   implementation must run the same PostgreSQL contract suite and a published
   NativeAOT process probe before it can be selected by a native host.
4. Keep EF migrations and schema ownership in the standalone JIT migrator. Runtime
   implementations share the same tables and persisted contracts; they do not own
   competing migration systems.
5. Migrate slices in dependency order: startup/authentication/configuration reads,
   billing and transactional writes, task leasing/media state, then reporting and
   retention workloads. Reporting may remain JIT until its aggregates have an explicit
   typed operation and measured native implementation.
6. Remove EF from a native service graph only after all operations required by that
   service's documented feature matrix have alternate implementations and native
   process coverage. This ADR does not authorize NativeAOT production promotion.

The first vertical slice is global settings. It is startup-critical, has a small fixed
query surface, and exercises ordered reads, key/id lookups, inserts, updates, upserts,
and deletes without requiring dynamic query composition.

The second vertical slice is IP-filter access policy. It is read on the request path,
has fixed global and virtual-key scopes, and extends the shared contract suite with
audit metadata, nullable columns, foreign-key scoping, and optimistic concurrency.
Provider and provider-key credentials are kept together as the next slice because
their primary-key selection and encryption behavior form one transactional boundary.

That provider/credential slice is now extracted as a single boundary. Provider reads
hydrate credential graphs, JSONB settings retain generated metadata, and the typed-
Npgsql credential writes use one provider-row lock order. The shared PostgreSQL
contract deliberately forces a failed disabled-key promotion and proves the old
primary survives the transaction rollback.

The next slice is the Gateway virtual-key runtime boundary. It hydrates a complete key
and group snapshot, preserves pending-spend balance checks, and owns atomic group balance
plus ledger writes. Native Gateway builds select its typed-Npgsql implementation only for
`IVirtualKeyRuntimeService`; JIT builds and Admin management CRUD retain the existing EF
services. This is the first service-host selection of an alternate adapter, but the
native feature matrix remains excluded until downstream provider, logging, task, and media
dependencies have equivalent boundaries and process coverage.

## Options considered

1. **Wait for production-supported EF NativeAOT.** Rejected as the only plan: it
   preserves the safe JIT lane but makes no progress on application-owned dynamic
   query boundaries.
2. **Rewrite every repository with typed Npgsql now.** Rejected: the 37-file/344-site
   migration is too broad to prove parity or roll back safely as one change.
3. **Refactor all EF queries for precompilation first.** Rejected as the primary path:
   it cannot remove the generated-model diagnostics or change upstream support status.
4. **Extract operation-specific contracts and add alternate backends by slice.**
   Accepted: it preserves production behavior, creates measurable checkpoints, and
   permits EF removal from a future native graph without requiring an all-at-once
   application rewrite.

## Verification and promotion gates

Each slice must provide:

- an architecture test proving its abstraction assembly is free of EF, Npgsql,
  Functions, and service implementation references;
- a single behavioral contract exercised against EF and the alternate backend on real
  PostgreSQL;
- a published NativeAOT process test that executes the alternate implementation;
- unchanged JIT unit/integration behavior and public/persisted contracts;
- an explicit production registration change and rollback plan before traffic uses the
  alternate backend.

The extracted global-settings, IP-filter, and provider/credential slices remain
registered to EF in Gateway and Admin. Their Npgsql implementations are evidence for
the seam and are not selected in production by this decision. The virtual-key runtime
slice is selected only by a native Gateway build; rollback is the JIT artifact, whose
request-time and management contracts both continue to resolve to the legacy EF service.

## Consequences

- Persistence migration becomes a sequence of reviewable vertical slices rather than
  a package replacement.
- During migration, EF and typed-Npgsql implementations coexist and must share contract
  tests. This adds short-term code but bounds semantic drift.
- Generic repository convenience APIs may remain for unextracted slices, but new
  consumers must not add expression-based operations to them.
- ADR 0006's production-JIT constraint, canary requirements, and forward-only migration
  rollback policy remain in force.
