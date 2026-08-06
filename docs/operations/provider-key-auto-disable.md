# Provider key auto-disable operations

*Audience: operators investigating provider credential failures, restoring
keys, or validating automatic balance recovery. The policy rationale is in
[ADR 0003](../decisions/0003-provider-key-auto-disable-policy.md).*

## What happens when a provider key fails

Conduit classifies each provider error and records it against the exact
`ProviderKeyCredential`. Fatal `401`, `402`, and `403` classifications use
distinct-request thresholds; `429`, `5xx`, timeouts, and network failures stay
in the transient retry/circuit-breaker path.

When a fatal threshold is crossed, Conduit:

1. disables and, if necessary, demotes the affected key;
2. disables the provider only if no enabled key remains;
3. invalidates provider credential caches across Gateway replicas;
4. emits a durable admin notification and a live system announcement; and
5. makes balance-disabled keys eligible for a delayed recovery probe.

For the exact thresholds and blast-radius rules, see
[ADR 0003](../decisions/0003-provider-key-auto-disable-policy.md#disable-thresholds).

## Inspecting errors

Use WebAdmin's **Provider Errors** view for routine investigation. Key
disable/re-enable transitions also appear in the admin notification feed.
Messages include the provider, key or account group, classified error type, and
a bounded copy of the upstream error.

The backing Admin endpoints require the master key. The examples use the
default `X-API-Key` header:

```bash
ADMIN=http://localhost:5002
KEY_ID=123
PROVIDER_ID=45

# Most recent errors; providerId, keyId, and limit are optional.
curl -sS -H "X-API-Key: $CONDUIT_API_TO_API_BACKEND_AUTH_KEY" \
  "$ADMIN/v1/admin/provider-errors/recent?providerId=$PROVIDER_ID&keyId=$KEY_ID&limit=100"

# Fatal history, last status/message, disabled time, and recent warnings for one key.
curl -sS -H "X-API-Key: $CONDUIT_API_TO_API_BACKEND_AUTH_KEY" \
  "$ADMIN/v1/admin/provider-errors/keys/$KEY_ID"

# Provider summaries and currently disabled key IDs.
curl -sS -H "X-API-Key: $CONDUIT_API_TO_API_BACKEND_AUTH_KEY" \
  "$ADMIN/v1/admin/provider-errors/summary"

# Aggregate counts for a bounded window (1-168 hours).
curl -sS -H "X-API-Key: $CONDUIT_API_TO_API_BACKEND_AUTH_KEY" \
  "$ADMIN/v1/admin/provider-errors/stats?hours=24"

# Per-key fatal counts for one provider (1-24 hours).
curl -sS -H "X-API-Key: $CONDUIT_API_TO_API_BACKEND_AUTH_KEY" \
  "$ADMIN/v1/admin/provider-errors/providers/$PROVIDER_ID/key-errors?hours=1"
```

The recent feed and provider summary are historical monitoring views. Clearing
a key removes its per-key fatal/warning state and disabled membership; it does
not rewrite the global recent feed or cumulative provider summary counters.

## Repairing and re-enabling a key

Fix the cause before restoring traffic: rotate an invalid secret, replenish the
account, or correct the provider-side permission.

To clear error state and re-enable the key:

```bash
curl -sS -X POST \
  -H "X-API-Key: $CONDUIT_API_TO_API_BACKEND_AUTH_KEY" \
  -H "Content-Type: application/json" \
  "$ADMIN/v1/admin/provider-errors/keys/$KEY_ID/clear" \
  -d '{
    "reenableKey": true,
    "confirmReenable": true,
    "reason": "Credential rotated and verified"
  }'
```

To clear only the recorded error state without enabling the credential, send
`reenableKey: false`. A re-enable request is rejected unless
`confirmReenable` is also `true`.

If the key was disabled for insufficient balance and belongs to a nonzero
`ProviderAccountGroup`, the operation restores every member of that group whose
recorded fatal error is `InsufficientBalance`. If all keys had automatically
disabled the provider, the provider is restored as well. The operation never
overrides a provider that an operator disabled manually.

You can also disable a key deliberately:

```bash
curl -sS -X POST \
  -H "X-API-Key: $CONDUIT_API_TO_API_BACKEND_AUTH_KEY" \
  -H "Content-Type: application/json" \
  "$ADMIN/v1/admin/provider-errors/keys/$KEY_ID/disable" \
  -d '"Maintenance window"'
```

Manual disables do not enter automatic balance recovery.

## Automatic balance reprobe

The Gateway's `ProviderKeyReprobeService` handles only keys classified as
`InsufficientBalance`. Defaults are:

- enabled;
- scan interval: 5 minutes;
- first probe: 1 hour after disable;
- failed-probe backoff: exponential, capped at 24 hours;
- per-key distributed probe lock: 10 minutes;
- maximum keys per scan: 50.

The probe uses the disabled credential to make a real model-list request.
Success clears its error state and re-enables the key. For a nonzero account
group, the balance-disabled group recovers together. The provider is also
restored only when it carries Conduit's automatic all-keys-disabled marker.

A probe that returns `401` changes the stored classification to
`InvalidApiKey` and stops future automatic probes. Other failures record the
next eligible time and stay in the backoff schedule.

Override the defaults through normal .NET configuration:

```json
{
  "ProviderKeyReprobe": {
    "Enabled": true,
    "ScanInterval": "00:05:00",
    "InitialCooldown": "01:00:00",
    "MaxCooldown": "1.00:00:00",
    "ProbeLockTtl": "00:10:00",
    "BatchSize": 50
  }
}
```

Environment variables use the same section with double underscores, for
example `ProviderKeyReprobe__Enabled=false`.

## Notifications

Each disable creates an unread durable notification of type
`ProviderKeyDisabled` with error severity. Each recovery creates
`ProviderKeyReenabled` with informational severity. Account-group operations
name the group and affected key IDs in one notification.

The Gateway also broadcasts the same text as a best-effort system announcement
to connected admins. A live broadcast failure does not roll back the durable
notification or cause the status event to be redelivered solely for that
failure.

## Redis state

Redis stores health history and coordination state. Do not edit these keys
directly during normal recovery; use the Admin API so database state, cache
invalidation, events, and notifications stay consistent.

| Key | Purpose |
|---|---|
| `provider:errors:recent` | Global sorted feed, capped at 1,000 entries |
| `provider:errors:key:{id}:fatal` | Fatal count, classification, last error, disabled time, and reprobe schedule; 30-day TTL when tracked |
| `provider:errors:key:{id}:fatal:{type}:requests` | Distinct request IDs used by the threshold window; 30-day TTL |
| `provider:errors:key:{id}:warnings` | Last 100 warnings; 30-day TTL |
| `provider:errors:key:{id}:disabling` | 30-second distributed disable guard |
| `provider:errors:key:{id}:reprobing` | Distributed reprobe lock |
| `provider:errors:disabled_keys` | Global set scanned by the reprobe worker |
| `provider:errors:provider:{id}:summary` | Provider totals and automatic provider-disable marker |
| `provider:errors:provider:{id}:disabled_keys` | Disabled credential IDs for the provider |

Clearing a key deletes its fatal, warning, threshold, and guard keys and removes
it from both disabled-key sets.

## Troubleshooting

If traffic still selects a disabled key, verify that `IsEnabled` is false in
the database, the disable event was delivered, and each Gateway consumed the
credential-cache invalidation event. The database is authoritative; Redis
alone does not remove a credential from selection.

If a key never reprobes, confirm its stored fatal type is
`InsufficientBalance`, `ProviderKeyReprobe:Enabled` is true, the initial
cooldown or `next_reprobe_at` has elapsed, and the Gateway worker can acquire
the per-key lock. An `InvalidApiKey` classification intentionally requires
manual action.

If a provider stays disabled after key recovery, determine whether the provider
was disabled manually. Automatic recovery only clears the exact
`All provider keys disabled automatically` marker; it will not override an
operator toggle.

If WebAdmin missed a live message, check the durable notification feed first.
The system announcement is best effort, while the notification record is the
authoritative operator signal.
