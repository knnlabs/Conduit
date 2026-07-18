# Database Migration Infrastructure

Runtime migration handling for the single EF Core context (`ConduitDbContext`,
PostgreSQL only). Behavior is governed by `CONDUIT_MIGRATION_MODE` — the full
operational guide is
[docs/operations/deployment/migration-deployment-strategy.md](../../../docs/operations/deployment/migration-deployment-strategy.md),
and the schema-compatibility rules are
[ADR-002](../../../docs/architecture/adr-002-expand-contract-migration-policy.md).

## Components

| File | Role |
|---|---|
| `MigrationStartupOptions.cs` | `CONDUIT_MIGRATION_MODE` (`Apply`/`Wait`/`Skip`) + timeout env parsing; warns on removed legacy variables |
| `SimpleMigrationService.cs` | Applies migrations + seeds default data under a blocking session-scoped `pg_advisory_lock(7891011)` |
| `MigrationWaitService.cs` | Wait mode: background poll of pending migrations; flips readiness when the schema is current |
| `MigrationReadinessState.cs` | Process-wide "schema is current" flag |
| `../HealthChecks/PendingMigrationsReadinessCheck.cs` | Gates `/health/ready` (tag `ready`) on that flag |
| `MigrationExtensions.cs` | DI registration (`AddDatabaseMigration`) + startup dispatch (`RunDatabaseMigrationAsync`) |
| `MigrationCommand.cs` | `migrate` CLI verb: standalone migrator + Wolverine schema provisioning (release-hook entry point) |
| `ExecutionStrategyExtensions.cs` | `ExecuteInTransactionAsync` — explicit transactions compatible with `EnableRetryOnFailure` |
| `ConfigurationDbContextFactory.cs` | Design-time factory; also resolves `DATABASE_URL` for the migrate verb |

## Design invariants

- **Migrations only.** Never `EnsureCreated` — it bypasses `__EFMigrationsHistory` and
  historically corrupted production databases
  (see `scripts/migrations/fix-production-migrations.ps1`).
- **One session for lock + migration.** The advisory lock and `MigrateAsync` share a
  dedicated `NpgsqlConnection`; if the migrator dies, the lock dies with the
  connection. The migration context deliberately has no retrying execution strategy.
- **Losers block, then verify.** Contending instances block on `pg_advisory_lock`
  (waiter timeout `CONDUIT_MIGRATION_LOCK_TIMEOUT_SECONDS`), then re-check
  `GetPendingMigrationsAsync()` and find nothing to do.
- **Seeding runs under the lock** and is idempotent.
