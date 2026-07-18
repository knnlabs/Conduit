# Migration Deployment Strategy

How ConduitLLM applies EF Core (and Wolverine) schema changes, per environment. This is
the single authoritative doc for migration behavior; the schema-compatibility rules that
make it safe live in [ADR-002: Expand/Contract Migration Policy](../../architecture/adr-002-expand-contract-migration-policy.md).

## The moving parts

| Piece | What it does |
|---|---|
| `CONDUIT_MIGRATION_MODE` | Per-service startup behavior: `Apply` (default), `Wait`, or `Skip` |
| `migrate` CLI verb | Standalone migrator on both service images: `dotnet ConduitLLM.<Gateway|Admin>.dll migrate` |
| Advisory lock | Blocking, session-scoped `pg_advisory_lock(7891011)` serializes all migrators |
| Readiness gate | `pending_migrations` health check (tag `ready`) holds `/health/ready` at 503 until the schema is current |
| CI artifact | `migration-validation` workflow uploads an idempotent SQL script per change |

## Modes (`CONDUIT_MIGRATION_MODE`)

### `Apply` (default)
The service migrates inline at startup, before serving traffic, under the blocking
advisory lock. Concurrent instances queue on the lock; losers wake, find nothing
pending, and continue. Seeding of default data (media retention policy) runs under the
same lock, so it cannot double-seed.

- Right for: development, docker-compose, single-writer deployments.
- `CONDUIT_MIGRATION_LOCK_TIMEOUT_SECONDS` (default `300`, `0` = wait forever) bounds
  how long a *waiting* instance blocks on the lock. The lock winner is never timed out —
  raise this before shipping a known-long migration (large backfill, index build).

### `Wait`
The service never migrates. It starts serving immediately with `/health/ready` failing,
polls `GetPendingMigrationsAsync()` (5s → 15s backoff), and flips ready once the schema
contains every migration the binary knows about — i.e. once the external migrator has
run. An unreachable database is not fatal; readiness simply stays down.

- Right for: production services on a platform whose release hook runs `migrate`.
- `CONDUIT_MIGRATION_WAIT_TIMEOUT_SECONDS` (default `0` = poll forever): if set and the
  schema never becomes current, the service logs critical and stops.

### `Skip`
No migration work, no schema checks, readiness unaffected. For tests and break-glass
operations only.

An unrecognized mode value fails startup deliberately — a misconfigured production
service must not silently migrate.

> **Removed variables**: `CONDUIT_SKIP_DATABASE_INIT` and `FORCE_RECREATE_DB_ON_FAILURE`
> no longer exist. Setting them logs a loud warning and has no effect. Use
> `CONDUIT_MIGRATION_MODE=Skip`/`Wait`, and recreate dev databases explicitly
> (`./scripts/dev.ps1 -Clean`).

## Production: release-hook flow (recommended)

1. Platform release hook runs the image with the `migrate` command
   (Railway custom start/release command, Fly.io `release_command`, etc.):
   `dotnet ConduitLLM.Admin.dll migrate` — the ENTRYPOINT is already `dotnet <dll>`, so
   the hook command is just `migrate`. Either service image works; they share the migrator.
2. The verb applies EF migrations under the advisory lock, seeds default data, and (when
   `ConduitLLM:Messaging:Backend=Wolverine` on the Postgresql transport) provisions
   Wolverine's durability/queue schema via JasperFx resource setup. Exit 0 on success,
   1 on failure — a failed migration fails the *release*, not the running fleet.
3. Gateway and Admin services run with `CONDUIT_MIGRATION_MODE=Wait`. Old instances keep
   serving (safe, per ADR-002); new instances gate readiness until the hook finishes.

**Wolverine note**: production sets `ConduitLLM:Messaging:Wolverine:AutoProvision=false`;
the `migrate` verb is the one explicit schema-management moment and provisions
regardless of that flag. Dev keeps `AutoProvision=true` (services self-provision).

**Manual/DBA alternative**: every change to the Configuration project uploads a
`conduit-migrations-idempotent.sql` artifact (CI workflow `migration-validation`).
It is rerunnable (`--idempotent`) and can be reviewed and applied by hand; Wait-mode
services notice and become ready regardless of *how* the schema became current.

## Development / docker-compose

Nothing to configure. Both services default to `Apply`; the advisory lock serializes
them; `./scripts/dev.ps1` works unchanged. To exercise the production flow locally:

```powershell
docker compose up -d postgres redis rabbitmq
docker compose run --rm -e CONDUIT_MIGRATION_MODE=Wait api      # /health/ready = 503
docker compose run --rm admin migrate                            # applies schema
# api's readiness flips to 200 without a restart
```

## Failure modes

| Scenario | Behavior |
|---|---|
| Migration throws (Apply) | Startup fails; service exits. Fix forward. |
| Migration throws (verb) | Exit 1; release aborted; fleet untouched. |
| Migrator dies mid-migration | Session lock releases with the dropped connection; next migrator retakes it. Each EF migration runs transactionally. |
| Waiter exceeds lock timeout | `TimeoutException`, startup fails — raise `CONDUIT_MIGRATION_LOCK_TIMEOUT_SECONDS`. |
| Wait-mode service, DB down | Polls forever (or until wait timeout); readiness stays down. |
| Old code vs new schema | Safe **only** because of the expand/contract policy (ADR-002). |
