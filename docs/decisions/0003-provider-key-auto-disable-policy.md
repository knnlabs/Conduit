# ADR 0003: Provider-key automatic disable and recovery policy

- Status: Accepted
- Date: 2026-07-23
- Epic: #1194

## Context

Conduit sends requests through provider credentials that can become invalid,
lose permission, or exhaust a shared balance. Continuing to select a known-bad
credential creates repeated user-visible failures, but disabling too broadly or
after one transient response can take healthy capacity out of service.

Provider-key health is distinct from model-route health. Route-level circuit
breaking responds to transient network, timeout, rate-limit, and upstream
service failures and can move chat traffic to another provider mapping.
Provider-key disabling responds to fatal credential or account failures and
removes only the affected credential scope.

The policy must also work under concurrency, where many requests can discover
the same failure at once, and across Gateway replicas whose credential caches
must converge.

## Decision

Conduit classifies provider errors into these numeric ranges:

- `1`–`9`: fatal credential/account errors that can disable a key:
  `InvalidApiKey`, `InsufficientBalance`, and `AccessForbidden`;
- `10`–`19`: warnings such as `RateLimitExceeded`, `ModelNotFound`, and
  `ServiceUnavailable`;
- `20`–`29`: transient `NetworkError` and `Timeout`;
- `99`: `Unknown`.

HTTP `401`, `402`, and `403` map to the three fatal types. A `403` whose response
mentions quota, billing, payment, or credit is refined to
`InsufficientBalance`. HTTP `429`, `5xx`, network failures, and timeouts do not
disable credentials; their retry and circuit behavior remains in the
transient-resilience layer.

### Disable thresholds

Fatal thresholds count distinct request/correlation IDs, not internal retry
attempts:

| Error type | Disable threshold | Recovery |
|---|---:|---|
| `InvalidApiKey` | 2 distinct requests in 60 seconds | Manual credential repair and re-enable |
| `InsufficientBalance` | 2 distinct requests in 5 minutes | Automatic balance reprobe or manual re-enable |
| `AccessForbidden` | 3 distinct requests in 10 minutes | Manual permission repair and re-enable |

The first `InvalidApiKey` response is recorded and logged without disabling the
key. A 30-second Redis `SET NX` guard permits only one concurrent caller to
perform a qualifying disable and publish its event.

Warning and transient classifications are recorded for monitoring but never
disable a credential.

### Disable scope

A fatal error disables the failing `ProviderKeyCredential`, including when it
is the primary key. A disabled primary is demoted and an enabled sibling is
promoted when one exists. The provider remains enabled while any eligible key
remains.

The provider entity is disabled only when the operation leaves it with no
enabled keys. Conduit records the automatic reason
`All provider keys disabled automatically`; this marker distinguishes an
automatic provider disable from an operator's manual provider toggle.

For `InsufficientBalance`, a nonzero `ProviderAccountGroup` means the keys share
one billing account. Every enabled key in that provider and group is disabled
together. Group `0` is ungrouped, so a failure never propagates between group-0
keys. Other fatal types remain scoped to the failing key.

One aggregate status event represents a group transition. Disable and re-enable
events invalidate the provider credential cache on every Gateway replica and
produce a durable admin notification plus a best-effort live system
announcement.

### Same-request failover

After a key-specific client has attributed and tracked a fatal `401`, `402`, or
`403`, the current request may retry another enabled key of the same provider.
The retry layer makes at most two additional attempts and surfaces the original
error if no eligible key succeeds.

An insufficient-balance attempt excludes every remaining key in the same
nonzero account group. Rate limits, timeouts, and `5xx` responses are not
retried by this key layer. A streaming request can change keys only before its
first response chunk is emitted; output is never replayed after it becomes
visible to the caller.

### Recovery

Operators can clear error state and optionally re-enable a repaired key through
the Admin provider-errors API. Re-enabling a balance-disabled key in a nonzero
account group restores the other balance-disabled members of that group.

An automatically disabled provider is restored when its key or group recovers.
A provider disabled manually is never enabled as a side effect of key recovery.

Only `InsufficientBalance` enters automatic half-open recovery. The Gateway
waits one hour before the first real provider probe, scans every five minutes,
and backs failed probes off exponentially up to 24 hours. A successful probe
re-enables the key or account group. A `401` reclassifies the key as
`InvalidApiKey`, removes it from automatic probing, and requires manual repair.

## Consequences

A bad primary credential no longer removes healthy sibling capacity, and a
shared balance failure cannot churn through equivalent keys. Operators receive
one actionable transition instead of a notification storm, while a temporary
single `401` does not permanently disable a credential.

The policy deliberately favors safety over aggressive recovery:
authentication and permission failures require an operator, balance recovery
waits through a cooldown, and a provider's manual disabled state always wins.
Redis is part of the control plane for thresholds, guards, disabled membership,
and reprobe scheduling; the database remains authoritative for whether a key or
provider is enabled.

Operational procedures and Redis key details are in
[Provider key auto-disable operations](../operations/provider-key-auto-disable.md).
