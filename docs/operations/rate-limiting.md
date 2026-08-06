# Rate limiting

Conduit limits what a caller can do in four dimensions, at three scopes, on both the HTTP and
SignalR data planes. This page describes what exists, how enforcement actually behaves, and what a
client sees when it is throttled.

Limits are **defence in depth and cost control**, not authorization. A key that is over its limit
is told to come back later; a key that is not permitted to do something is rejected outright by
authentication or model access rules, which are separate mechanisms.

## What can be limited

| Dimension | Meaning | Configured on |
|---|---|---|
| **RPM** | Requests per rolling minute | Virtual key, group |
| **RPD** | Requests per rolling 24 hours | Virtual key, group |
| **TPM** | Prompt + completion tokens per rolling minute | Virtual key, group, per model |
| **Max parallel requests** | Requests in flight at once | Virtual key, group |
| **Per-model RPM / TPM** | Overrides for one model alias | Virtual key |

Every field is nullable and **blank means unlimited**. A key with nothing configured takes no
round-trip to the limit store at all.

Request counting alone cannot police cost: a key sending ten 100k-token prompts a minute costs a
hundred times one sending 1k-token prompts, and RPM cannot tell them apart. That is what TPM is
for. Concurrency is likewise distinct from RPM — a key well inside its per-minute allowance can
still hold hundreds of streaming connections open.

## Scopes, and how they combine

Three scopes apply **together**, and the tightest governs:

1. **Key** — the virtual key's own ceilings.
2. **Group** — shared by every key in the `VirtualKeyGroup`. A per-key limit says nothing about
   what a tenant can do in aggregate: fifty keys at 100 RPM each is 5,000 RPM.
3. **Model** — a per-alias override on the key, so the same key can call a cheap chat model freely
   while being held to a handful of requests a minute against an expensive video model.

Per-model rules match the **alias the caller sends**, since that is what a caller controls and
therefore what an operator is limiting. A trailing `*` matches by prefix; an exact alias always
wins, and among prefix rules the longest wins. A request naming a model no rule covers is subject
to the key and group ceilings only.

Every applicable window is evaluated in **one atomic operation**, all-or-nothing. A request the
group rejects has not consumed the key's quota, and one the daily window rejects has not burned a
minute slot.

### Priority tiers within a group

A group's windows are shared by every key in it, so a noisy batch key can spend the whole
allowance and crowd out a critical key in the same group. `rateLimitPriority` on a virtual key
addresses that: **0 = low, 1 = normal (the default, and what null means), 2 = high**.

A low-priority key is admitted against `floor(groupLimit × threshold)` instead of the full group
ceiling — by default 80% of it. Once the group's shared window passes the threshold, low-priority
keys are shed with a 429 while normal- and high-priority keys still have the remaining headroom.
This applies to every group dimension: `group:RPM`, `group:RPD`, `group:TPM` and
`group:concurrency`. High currently behaves the same as normal; it is reserved for future
refinement.

Mechanically the reduced ceiling is checked **inside the same atomic operation** as every other
window, against the group's shared fill — there is no separate read of "how full is the group",
so the decision cannot race the fill it is based on, and there is no shedding mode that could
flap. A shed request is reported under a distinct scope (`group:RPM:saturation` etc.), so a
caller can tell "your tenant is busy" from "you are over your own limit".

Edge cases worth knowing:

- A group with no ceiling has nothing to shed against; priority is inert there.
- A very small group ceiling can floor to zero for low-priority keys (e.g. a group RPM of 1 at
  the default threshold), which excludes them from that window entirely — the whole allowance is
  inside the reserved headroom.
- The key's own ceilings are never reduced; priority only narrows what a key may take of its
  group's shared allowance.

## Enforcement semantics

### Rolling windows, not calendar ones

All windows are rolling. "Requests per day" means the last 24 hours from now, not since midnight.
A key exhausted at 23:50 recovers gradually as its oldest requests age out — not in ten minutes.

### Token limits are estimate-then-reconcile

A request's token cost is only known after the provider answers, but admission has to be decided
before the provider is called, or the limit polices nothing. So:

1. At admission, prompt tokens are counted from the bound request and a completion budget is added.
   A request that declares no `max_tokens` would otherwise be able to reserve the whole window, so
   it reserves a configured default instead; a request declaring an enormous ceiling is clamped.
2. When the response is billed, the reservation is corrected to actual usage across every token
   window it touched.

Failure modes are bounded rather than eliminated. A request that dies before reconciliation leaves
its estimate standing, over-charging the key until the entry ages out of the rolling minute — never
longer, because window entries carry a TTL. An estimate larger than the whole ceiling is clamped to
it, so such a request produces one 429 rather than a permanent rejection no waiting could resolve.

Only token-shaped requests carry a token window: chat completions, responses and embeddings. Image,
video and audio requests are not measured in tokens and are not subject to TPM.

### Concurrency slots

A slot is held for the lifetime of the HTTP request and released whether it completes, throws, or
the client disconnects. For an **asynchronous job** — video generation, batch — the slot covers the
submit call, not the job.

Slots also expire on their own after `ConcurrencySlotTtlSeconds`, which is what makes a leak
self-healing: a node killed mid-request never releases its slots, but they age out instead of
permanently shrinking the key's capacity. That lifetime must therefore exceed the longest
legitimate request, since streaming responses run for minutes.

### Where each limit is enforced

- **RPM, RPD, concurrency** — `VirtualKeyRateLimitMiddleware`, before the request body is read.
- **TPM and per-model limits** — an endpoint filter on the token-consuming routes, because a token
  estimate needs the prompt and a per-model limit needs the model alias, neither of which exists
  until the request is bound.
- **SignalR method invocations** — `VirtualKeySignalRRateLimitFilter`, per key, RPM and RPD.
- **SignalR connections** — a separate ceiling, `MaxConnectionsPerVirtualKey`.
- **Per-IP, discovery and model-capability limits** — the security layer, independent of virtual
  keys. These cap abuse rather than metering usage, and use fixed windows.

## What a client sees

### Headers

Set on **allowed and rejected responses alike**, so a client can pace itself before it is ever
throttled. They describe whichever window has the least headroom.

| Header | Meaning |
|---|---|
| `X-RateLimit-Limit` | The ceiling of the reported window |
| `X-RateLimit-Remaining` | What is left in it |
| `X-RateLimit-Reset` | Unix seconds at which it frees room, rounded up |
| `X-RateLimit-Scope` | Which limit is being reported |
| `Retry-After` | On a 429, seconds to wait, rounded up |

`Retry-After` and `X-RateLimit-Reset` round **up**, never truncate: a client that waits exactly as
long as it was told must get through, and a 4.2-second wait advertised as 4 sends it back before
capacity exists.

On a denial the reset is the instant the window genuinely frees enough room for *that* request —
which for a token window means enough weight has aged out, not merely the oldest entry.

### Scope values

| Value | Which limit denied |
|---|---|
| `RPM`, `RPD`, `TPM` | The key's own request or token ceiling |
| `concurrency` | The key's in-flight ceiling |
| `group:RPM`, `group:RPD`, `group:TPM`, `group:concurrency` | The group's, shared with sibling keys |
| `group:RPM:saturation` (and the other group scopes) | The group is saturated and this low-priority key was shed before the ceiling itself |
| `model:{alias}:rpm`, `model:{alias}:tpm` | A per-model override on the key |
| `discovery`, `model-capability` | Security-layer per-IP limits |

The scope is what makes a 429 actionable. Without it every rejection looks the same and the only
available response is to back off; with it a caller can tell "slow down" from "a neighbouring key
in your tenant is the problem" from "finish something before starting more".

### Body

`429 Too Many Requests`, in the standard OpenAI error shape:

```json
{
  "error": {
    "message": "RPM rate limit exceeded (600 requests). Retry after 12 seconds.",
    "type": "rate_limit_exceeded",
    "code": "rate_limit_exceeded"
  }
}
```

The body is identical whichever layer rejected the request — only `X-RateLimit-Scope` distinguishes
the reason.

## Failure behaviour

Rate limiting requires Redis. Windows are shared state; without a shared store there is nothing to
enforce across instances.

When the store cannot be reached, `CONDUIT_RATE_LIMIT_FAILURE_MODE` decides what happens:

| Value | Behaviour |
|---|---|
| `open` (default) | Admit the request. A cache outage does not take the data plane with it. |
| `closed` | Reject with `503 Service Unavailable` and `Retry-After`. |

Fail-closed returns **503, not 429**: the caller has not exceeded anything, the gateway simply
cannot tell, and conflating the two would have clients back off against a quota they may be nowhere
near. It is an explicit opt-in because it converts a cache outage into a data-plane outage for
every key that has a limit configured. Keys with no limits take no round-trip and are unaffected
either way.

Either mode makes the degradation visible:

- `conduit_gateway_rate_limit_degraded_total{scope,mode}` — checks that could not be evaluated.
- `conduit_gateway_rate_limit_errors_total` — the same events, unlabelled.
- A warning log, debounced to one line per `FailureDebounceSeconds`, so a flapping store does not
  bury the signal.

**Alert on the degraded counter.** Without it, "the limiter is not running" is something you
discover in the invoice.

## Configuration

### Per key and per group

Set through the Admin API or the WebAdmin key and group modals. All fields are optional.

- Virtual key: `rateLimitRpm`, `rateLimitRpd`, `rateLimitTpm`, `maxParallelRequests`,
  `rateLimitPriority`, `modelRateLimits`
- Virtual key group: `rateLimitRpm`, `rateLimitRpd`, `rateLimitTpm`, `maxParallelRequests`

Per-model overrides are a map of alias to ceilings; unknown aliases are rejected on write, because
a typo is otherwise silent — the rule never matches, and the operator believes an expensive model
is capped when it is not.

```json
{
  "modelRateLimits": {
    "gpt-5": { "rpm": 1000, "tpm": 200000 },
    "sora-2*": { "rpm": 10 }
  }
}
```

Group limit changes take effect immediately: the edit invalidates the cached entry for every key in
the group.

### Deployment-wide

| Setting | Default | What it does |
|---|---|---|
| `CONDUIT_RATE_LIMIT_FAILURE_MODE` | `open` | Behaviour when the store is unreachable |
| `CONDUIT_RATE_LIMIT_SATURATION_THRESHOLD` | `0.8` | Fraction of each group ceiling low-priority keys are admitted against; `1` disables shedding |
| `RateLimiting__FailureDebounceSeconds` | 10 | How long one failure reads as an ongoing degradation |
| `RateLimiting__DefaultCompletionTokenBudget` | 1024 | Reserved for a request that declares no `max_tokens` |
| `RateLimiting__MaxCompletionTokenReservation` | 32768 | Ceiling on what one request may reserve |
| `RateLimiting__ConcurrencySlotTtlSeconds` | 900 | How long a leaked concurrency slot survives |
| `RateLimiting__ConcurrencyRetryAfterSeconds` | 1 | Advertised wait when the in-flight ceiling is hit |

## Observing current usage

`GET /v1/admin/virtual-keys/{id}/rate-limit-usage` reports what a key is consuming right now
against each of its ceilings, including its group's. The limits live in the database, but what has
been spent against them lives only in the window store — so "is this key near its limit right now?"
is a question nothing else can answer.

Figures are for the rolling minute and rolling 24 hours as of the call. Group figures are absent,
rather than zero, when the key's group has no ceilings — absent means "no group limit", which is
not the same as a group sitting idle.

## Metrics

| Metric | Labels | Meaning |
|---|---|---|
| `conduit_gateway_rate_limit_decisions_total` | `outcome`, `scope` | Admissions and rejections |
| `conduit_gateway_rate_limit_degraded_total` | `scope`, `mode` | Checks that could not be evaluated |
| `conduit_gateway_rate_limit_errors_total` | — | Same events, unlabelled |

A rejection rate that is persistently non-zero for one scope is the signal that a limit is set too
low — or that a caller needs to be told about it.
