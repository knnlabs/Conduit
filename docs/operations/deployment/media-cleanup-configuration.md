# Media cleanup production configuration

The Admin API is the only owner of scheduled media cleanup. Configure it before enabling
deletions; otherwise generated objects can outlive their database owner and storage usage can
grow without an operational limit.

## Storage contract

Gateway writes media and Admin deletes it, so both services must resolve the same storage backend,
endpoint, bucket, region, and credentials. For S3-compatible storage, set:

```dotenv
CONDUIT_MEDIA_STORAGE_TYPE=S3
CONDUIT_S3_ENDPOINT=https://<account>.r2.cloudflarestorage.com
CONDUIT_S3_ACCESS_KEY_ID=<access-key>
CONDUIT_S3_SECRET_ACCESS_KEY=<secret-key>
CONDUIT_S3_BUCKET_NAME=conduit-media
CONDUIT_S3_REGION=auto
# Optional public origin/CDN:
# CONDUIT_S3_PUBLIC_BASE_URL=https://media.example.com
```

Do not give Admin a different bucket from Gateway. The storage configuration guard blocks
destructive operations when Admin resolves the in-memory backend while persisted media rows
exist. The cleanup status page shows the resolved backend so operators can detect other
cross-service configuration mistakes before enabling deletion.

Direct CDN requests do not pass through the media API and therefore do not update
`MediaRecord.LastAccessedAt`. See [configuration.md](../../configuration.md) for the
recent-access retention decision and warning behavior.

## Scheduler environment variables

ASP.NET configuration uses double underscores as section separators:

| Variable | Default | Purpose |
| --- | ---: | --- |
| `MediaLifecycle__Enabled` | `false` | Starts the Admin cleanup scheduler |
| `MediaLifecycle__DryRunMode` | `true` | Reports candidates without mutating rows or storage |
| `MediaLifecycle__ScheduleIntervalMinutes` | `60` | Delay between scheduled cycles |
| `MediaLifecycle__EnableExpirationCleanup` | `true` | Processes explicit expiration timestamps |
| `MediaLifecycle__EnableReconciliation` | `true` | Finds storage objects without database rows |
| `MediaLifecycle__EnableQuotaCleanup` | `true` | Enforces per-group storage quotas |
| `MediaLifecycle__EnableRetentionCleanup` | `true` | Applies group retention policies |
| `MediaLifecycle__EnableSoftDelete` | `true` | Tombstones tracked media before permanent purge |
| `MediaLifecycle__SoftDeleteGracePeriodDays` | `7` | Fallback recovery window |
| `MediaLifecycle__TestVirtualKeyGroups__N` | unset | Restricts progressive rollout to listed group IDs |
| `MediaLifecycle__CleanupPageSize` | `1000` | Maximum database candidates loaded per page |
| `MediaLifecycle__MaxRecordsPerRun` | `10000` | Shared candidate ceiling per scheduled cycle |
| `MediaLifecycle__MaxBatchSize` | `1000` | Maximum candidates grouped for deletion |
| `MediaLifecycle__BudgetReservationStride` | `10` | Maximum permanent deletes reserved per provider call |
| `MediaLifecycle__MonthlyDeleteBudget` | `500000` | Monthly permanent-delete safety ceiling |
| `MediaLifecycle__BudgetFailureMode` | `FailClosed` | Behavior when the shared counter is unavailable |
| `MediaLifecycle__BudgetAlertThresholdPercent` | `90` | Health-alert threshold for budget utilization |
| `MediaLifecycle__R2OperationTimeoutSeconds` | `30` | Per-provider-call timeout |
| `MediaLifecycle__DeleteThrottleMaxRetries` | `5` | Retry limit for throttling and timeouts |
| `MediaLifecycle__DeleteThrottleInitialBackoffMs` | `1000` | Initial exponential-backoff delay |

`FailClosed` is the production-safe budget mode. `FailOpen` permits deletion when the budget
backend fails and should be used only after explicitly accepting that accounting risk.

## Safe rollout runbook

1. Deploy with `MediaLifecycle__Enabled=true` and `MediaLifecycle__DryRunMode=true`.
2. Confirm the cleanup status page reports the expected S3 backend and a persistent Redis budget
   backend. Investigate any storage guard, database, or budget-store error.
3. Review dry-run candidates in logs and the
   `conduit_admin_media_cleanup_dry_run_files_total`,
   `conduit_admin_media_cleanup_dry_run_bytes_total`, and
   `conduit_admin_media_cleanup_dry_run_records_total` metrics. Dry runs never increment the
   actual files-deleted or bytes-freed counters.
4. Set one or more `MediaLifecycle__TestVirtualKeyGroups__N` values for a progressive rollout.
   The status page must show the orange test-scope warning and the exact group list.
5. Set `MediaLifecycle__DryRunMode=false`, then observe at least one full cycle. Test scope limits
   purge, expiration, quota, and retention. Reconciliation is intentionally skipped because an
   untracked object no longer has group ownership.
6. Remove every test-group setting to expand cleanup to all groups. Confirm the status warning
   disappears.

Use large-batch approval for additional production control:

```dotenv
MediaLifecycle__RequireManualApprovalForLargeBatches=true
MediaLifecycle__LargeBatchThreshold=100
MediaLifecycle__LargeBatchApprovalExpirationHours=24
```

## Budget sizing and Cloudflare R2

`MonthlyDeleteBudget` is a Conduit safety ceiling, not a provider invoice forecast. Cloudflare's
current [R2 pricing documentation](https://developers.cloudflare.com/r2/pricing/) classifies
`DeleteObject` as a free operation; `ListObjects`, used by reconciliation, is a Class A operation
with its own monthly allowance. Provider pricing can change, so verify the linked pricing page
when setting a production limit.

Choose a ceiling from expected lifecycle volume:

```text
monthly delete budget >=
  expected expirations
  + expected retention purges
  + expected quota evictions
  + reconciliation contingency
```

Keep enough contingency for retries and emergency cleanup, but low enough to cap an accidental
policy expansion. Conduit reserves budget before each permanent-delete stride, reports utilization
in the status page, and emits a health-monitoring alert at
`BudgetAlertThresholdPercent` (90% by default).

## Status and alert reference

The WebAdmin media cleanup status page reports:

- scheduler, dry-run, soft-delete, and progressive test-scope state;
- resolved storage backend and public-base-URL warning;
- Redis or in-memory budget backend, failure mode, utilization, and last backend failure;
- last overall run and per-phase outcomes;
- pending large-batch approvals and reconciliation drift.

Failed operations and budget-threshold events are published from Admin through the durable
Conduit event bus. Gateway converts them to health alerts, making them available to the
health-monitoring SignalR hub and configured external notification channels. The cleanup status
page also shows the same conditions through its normal polling path.

Key Prometheus series:

- `conduit_admin_media_cleanup_runs_total{cleanup_type,triggered_by,status}`
- `conduit_admin_media_cleanup_last_run_succeeded{cleanup_type}`
- `conduit_admin_media_cleanup_files_deleted_total{cleanup_type}`
- `conduit_admin_media_cleanup_bytes_freed_total{cleanup_type}`
- `conduit_admin_media_cleanup_records_tombstoned_total{cleanup_type}`
- `conduit_admin_media_cleanup_dry_run_files_total{cleanup_type}`
- `conduit_admin_media_cleanup_dry_run_bytes_total{cleanup_type}`
- `conduit_admin_media_cleanup_dry_run_records_total{cleanup_type}`
- `conduit_admin_media_cleanup_errors_total{cleanup_type,error_type}`
- `conduit_admin_media_cleanup_pending_approvals`
- `conduit_admin_media_cleanup_untracked_objects`
- `conduit_admin_media_cleanup_untracked_bytes`

Alert on repeated `last_run_succeeded == 0`, any sustained cleanup errors, an active
test scope that was not planned, and budget utilization at or above the configured threshold.
