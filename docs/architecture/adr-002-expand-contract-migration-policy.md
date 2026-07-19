# ADR-002: Expand/Contract Migration Policy

**Status**: Accepted
**Date**: 2026-07-17
**Related**: [Migration Deployment Strategy](../operations/deployment/migration-deployment-strategy.md)

## Context

ConduitLLM applies EF Core migrations once per deploy (the `migrate` verb run by the
platform release hook, or an Apply-mode instance winning the advisory lock). During a
rolling deploy there is always a window where the **new schema is live while old-code
instances still serve traffic** — the release hook migrates before the new instances
replace the old ones, and in Apply mode the first new container migrates while its
siblings are still on the previous release.

No amount of locking, leader election, or startup coordination can make that window
safe if a migration is incompatible with the previous release's code. A dropped or
renamed column takes down every old instance mid-request. Coordination mechanisms
serialize *migrators*; they do nothing for *old readers*.

## Decision

Every migration merged to `dev` MUST be backward-compatible with the code of the
previous release ("expand" steps only). Destructive/"contract" steps ship one release
later, after no deployed code references the old shape.

**Allowed in any release (expand):**
- Creating tables, views, indexes (prefer `CREATE INDEX CONCURRENTLY` for large tables)
- Adding nullable columns, or non-null columns with a database default
- Widening types (e.g. `varchar(50)` → `text`), relaxing constraints
- Backfilling data (idempotent, batched for large tables)
- Adding new constraints as `NOT VALID` + later `VALIDATE CONSTRAINT`

**Deferred one release (contract) — only after the release that stopped using the old shape is fully rolled out:**
- Dropping tables or columns
- Renaming tables or columns (model a rename as: add new → dual-write/read in code → backfill → drop old)
- Narrowing types, adding `NOT NULL` to existing columns without a default
- Tightening constraints that existing rows or old code could violate

**Review rule:** a PR that adds a migration states in its description which category the
migration is in. Contract steps reference the release/PR that removed the last code
dependency on the old shape. Reviewers reject expand-release PRs containing drops or
renames.

## Consequences

- Rolling deploys and the release-hook flow are safe by policy: old code always runs
  correctly against the new schema.
- Removals take two releases and a small amount of discipline (tracking pending
  contract steps). We accept this cost; it is the industry-standard trade for
  zero-downtime schema changes.
- Rollback of the *code* is always safe within one release, because the schema the
  previous release ran against is still compatible. Rolling back a *migration* is not
  supported — fix forward.
