# Admin API routing map

All Admin API resources live below `/v1/admin`, use plural lowercase kebab-case
resource names, and use lowercase kebab-case literal subpaths. `/metrics` is the
only exception because Prometheus-compatible scrape infrastructure consumes it
at the root.

| Previous root | Canonical root |
| --- | --- |
| `/api/admin/auth` | `/v1/admin/auth-tokens` |
| `/api/Analytics` | `/v1/admin/analytics` |
| `/api/batch-spending` | `/v1/admin/batch-spending-jobs` |
| `/api/audit/billing` | `/v1/admin/billing-audits` |
| `/api/Model/bundled-catalog` | `/v1/admin/model-catalogs` |
| `/api/config` | `/v1/admin/routing-configurations` |
| `/api/FunctionConfigurations` | `/v1/admin/function-configurations` |
| `/api/FunctionCosts` | `/v1/admin/function-costs` |
| `/api/FunctionCredentials` | `/v1/admin/function-credentials` |
| `/api/FunctionExecutions` | `/v1/admin/function-executions` |
| `/api/GlobalSettings` | `/v1/admin/global-settings` |
| `/api/health` | `/v1/admin/health-status` |
| `/api/admin/Media` | `/v1/admin/media-assets` |
| `/api/admin/media-cleanup` | `/v1/admin/media-cleanup-jobs` |
| `/api/IpFilter` | `/v1/admin/ip-filters` |
| `/api/admin/media-retention` | `/v1/admin/media-retention-policies` |
| `/api/ModelAuthor` | `/v1/admin/model-authors` |
| `/api/ModelCosts` | `/v1/admin/model-costs` |
| `/api/Model` | `/v1/admin/models` |
| `/api/ModelProviderMapping` | `/v1/admin/model-provider-mappings` |
| `/api/ModelSeries` | `/v1/admin/model-series` |
| `/api/Notifications` | `/v1/admin/notifications` |
| `/api/Pricing` | `/v1/admin/pricing-tools` |
| `/api/prompt-caching` | `/v1/admin/prompt-cache-settings` |
| `/api/ProviderCredentials` | `/v1/admin/providers` |
| `/api/provider-errors` | `/v1/admin/provider-errors` |
| `/api/ProviderSync` | `/v1/admin/provider-sync-jobs` |
| `/api/admin/provider-tools` | `/v1/admin/provider-tools` |
| `/api/SystemInfo` | `/v1/admin/system-metadata` |
| `/v1/admin/tasks` | `/v1/admin/tasks` |
| `/api/VirtualKeyGroups` | `/v1/admin/virtual-key-groups` |
| `/api/VirtualKeys` | `/v1/admin/virtual-keys` |

Resource identifiers occur only in paths. Partial resource changes use `PATCH`
and return the updated resource. Versioned resources publish an `ETag` and
require `If-Match` for update and delete operations.
