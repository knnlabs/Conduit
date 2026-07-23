# Configuration

*Audience: operators deploying and tuning Conduit. This doc explains **where** configuration lives
and the shape of each area; it is not an exhaustive list of every setting. For the objects being
configured, see [Core concepts](./concepts.md).*

Conduit's configuration lives in two places, and knowing which is which is most of the job:

| | Deploy-time configuration | Runtime configuration |
|---|---|---|
| Lives in | The **environment** (env vars / secrets) | The **database**, edited through the Admin UI |
| Set by | Your deployment / container platform | Operators, live, in WebAdmin |
| Changes take effect | On restart | Immediately, no redeploy |
| Examples | Database URL, keys, storage, migration mode | Providers, virtual keys, model mappings, costs, routing policies |

The rule of thumb: **infrastructure and secrets are deploy-time; everything about how Conduit routes,
prices, and gates requests is runtime.**

## Deploy-time configuration (the environment)

Conduit ships **no `appsettings.json`** — every process setting is read from environment variables.
Hierarchical settings use a **double underscore** as the section separator, so a setting documented
as `Section:Sub:Value` is set as `Section__Sub__Value` in the environment.

The authoritative, current list of variables is **`.env.example`** in the repo root — it is kept in
step with the code, and copying it is the intended way to start a deployment. What follows is a map
of the areas, not a copy of that file:

- **Authentication and secrets** — the single backend/master key that lets WebAdmin and internal
  services reach the Admin and Gateway APIs, and a separate key that guards the health endpoints
  (see [Monitoring](./monitoring.md#securing-the-health-endpoints)). Human admins sign in to WebAdmin
  through Clerk, configured with its own keys.
- **Database** — a single **PostgreSQL** connection URL (`DATABASE_URL`) is required; Conduit is
  Postgres-only. Schema changes are governed by a **migration mode** (`CONDUIT_MIGRATION_MODE`) that
  chooses whether a booting service applies migrations itself, waits for an external migration to
  finish first, or skips the step — the mechanism behind zero-downtime deploys.
- **Cache (Redis)** — a Redis connection enables distributed caching and is **required for
  distributed rate limiting**; the Gateway will not start rate limiting without it. Redis also backs
  the real-time backplane and ephemeral keys. Redis 7.4 or newer enables per-connection field TTLs
  for SignalR monitoring; older supported servers continue to use the periodic stale-connection
  cleanup. The bundled Compose deployment pins Redis 7.4.2. Set
  `SignalR__ConnectionMonitor__EnableHashFieldExpiration=false` to force the compatibility path.
- **Messaging** — Conduit's internal events run on **Wolverine over PostgreSQL** (they reuse the
  database; there is no separate message broker to run). This replaced an earlier RabbitMQ-based
  transport — if you see RabbitMQ referenced in older material, it no longer applies.
- **Media storage** — generated media (images, audio, video) is stored in memory for local dev or in
  an **S3-compatible bucket** (AWS S3 or Cloudflare R2, auto-detected) for real deployments. Two
  cautions worth calling out: automatic **media cleanup runs only on the Admin service**, and it
  ships in **dry-run mode** — nothing is actually deleted until you turn dry-run off, so storage will
  grow unbounded until you do.
- **Security** — IP filtering, rate limiting, failed-auth banning, and security headers are each
  configurable per service (the Admin and Gateway APIs have their own prefixes). One setting is easy
  to miss: correct client-IP handling behind a load balancer or CDN requires enabling **trusted-proxy
  / forwarded-headers** support — it is **off by default**, and without it IP-based rules act on the
  proxy's address, not the caller's.
- **Observability** — metrics and tracing export via OpenTelemetry, with a Prometheus scrape endpoint
  and configurable OTLP target. Covered in [Monitoring](./monitoring.md).

> A number of older variables (legacy Redis, cache toggles, database-recreate escape hatches) are
> **deprecated or ignored** and log a warning on boot if set. Trust `.env.example` and the startup
> warnings over any list — including this one — for what is still live.

### Scheduled media lifecycle cleanup

The Admin service is the single owner of scheduled media cleanup. Each cycle acquires a PostgreSQL
distributed lock before running explicit expiration, orphan, and retention-policy cleanup, so a
multi-instance deployment does not run the same cycle concurrently. The Gateway only records media
lifecycle metadata; it does not schedule cleanup.

Set `MediaLifecycle__Enabled=true` to start the scheduler and configure its polling interval with
`MediaLifecycle__ScheduleIntervalMinutes`. The three phases can be controlled independently with
`MediaLifecycle__EnableExpirationCleanup`, `MediaLifecycle__EnableOrphanCleanup`, and
`MediaLifecycle__EnableRetentionCleanup`. Cleanup defaults to `MediaLifecycle__DryRunMode=true`;
set it to `false` only after reviewing the status endpoint and logs. All phases share
`MediaLifecycle__MonthlyDeleteBudget`, batch-size, rate-limit, and dry-run safeguards.

The Admin media cleanup status endpoint reports both the aggregate cycle and the last outcome of
each phase. Prometheus metrics use a `cleanup_type` label with `expiration`, `orphan`, or
`retention`. When `MediaLifecycle__TestVirtualKeyGroups` is set, expiration and retention are
limited to those groups and orphan cleanup is skipped because an orphan no longer has group
ownership that can be scoped safely.

## Runtime configuration (in the Admin UI)

Everything about *how Conduit behaves per request* is data in Postgres, created and edited live
through the Admin API and the WebAdmin UI — no restart, no redeploy. This is where you manage:

- **Providers** and their **API keys** — the upstream connections and credentials.
- **Model mappings** — which alias points at which provider and provider model.
- **Model costs** — how each model is priced for billing.
- **Virtual keys** and **key groups** — access control and prepaid balances.
- **Routing policies** — global defaults and per-alias overrides, plus the routing kill switch
  (see [Model routing](./routing.md#turning-routing-off-and-configuring-it)).
- **IP filters, prompt-cache policy, and other global settings.**

The clearest illustration of the deploy-vs-runtime split is the **routing kill switch**: it reads
like an infrastructure toggle, but it is a *runtime* setting stored in the database, so you can flip
routing off during an incident without touching a deployment.

## Where to go next

- **[Core concepts](./concepts.md)** — what providers, keys, mappings, and virtual keys are.
- **[Model routing](./routing.md)** — the routing policies and switch you configure at runtime.
- **[Monitoring](./monitoring.md)** — the health-endpoint and observability settings referenced above.
