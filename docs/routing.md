# Model routing

*Audience: operators tuning how requests are served, and anyone debugging why a request went to
one provider and not another. This doc owns the full routing logic; for the objects it operates on
(aliases, mappings, providers), start with [Core concepts](./concepts.md).*

When a client sends a chat request, the `model` field names an **alias**, not a provider. If that
alias is served by more than one [mapping](./concepts.md#the-core-objects), Conduit has a choice to
make: which provider should handle this request, and what should happen if that provider fails.
Routing is how it decides.

Two things are worth stating up front:

- **Only chat completions are routed.** Embeddings, images, audio, and every other request type
  resolve deterministically to a single mapping — see [Other request types](#other-request-types).
- **Routing only matters when an alias fans out.** An alias with one mapping always resolves to that
  one target; scoring and failover simply have nothing to choose between.

## When routing applies

For a chat request, Conduit gathers **every enabled mapping** for the alias whose provider is also
enabled. Those are the eligible candidates. If exactly one is eligible, it is used. If several are,
they go through scoring.

## How candidates are scored

Conduit scores each eligible mapping and serves the best, keeping the rest as an ordered fallback
list. A mapping's score blends several factors:

- **Cost, speed, and quality** — the three headline factors, combined by a weighted **routing
  policy**. The default `Balanced` policy weights them **0.40 / 0.30 / 0.30**. A cost-sensitive
  deployment might raise the cost weight; a latency-sensitive one might favor speed.
- **Priority and weight** — each mapping carries a **priority** (lower wins; default `100`) and a
  **weight** (default `1.0`, range `0.1`–`2.0`). Priority lets you express a hard preference order;
  weight nudges the balance between otherwise-comparable mappings.
- **Provider health** — mappings whose provider has been throwing errors are scored down, so routing
  steers away from a struggling upstream on its own. (How health is tracked is covered in
  [Monitoring](./monitoring.md#provider-health).)
- **Prompt-cache affinity** — if a recent request primed a provider's prompt cache, routing gives
  that provider a bounded nudge so follow-up requests can reuse the cache. The nudge is capped: an
  affinity route is only preferred when its score is within **0.10** of the best eligible score, so
  cache stickiness never overrides a materially better option. Affinity uses a sliding **30-minute**
  lifetime by default.

All of these are **defaults you can change.** Weights, cache-affinity behavior, and the like are set
globally or per-alias through the Admin routing configuration — treat the Admin UI as the source of
truth for the values in effect on your deployment.

## Session affinity

Independent scoring can send consecutive turns of one conversation to different providers. When you
want a conversation pinned to a provider, attach a session identifier:

```json
{
  "model": "support-agent",
  "session_id": "conversation-123",
  "messages": [{ "role": "user", "content": "Continue my case" }]
}
```

A client may instead send the `X-Conduit-Session-Id` header; the JSON body wins if both are present.
If neither is supplied, Conduit derives an affinity key from the first system (or developer) message
and the first user message, so a stable prompt naturally sticks without any client change.

Session values are never stored or logged in the clear: Conduit HMACs both explicit and derived
identifiers with the authenticated virtual key before using them, and raw session IDs and prompt
content are never persisted or used as metric labels.

## Failover and circuit breaking

The scored candidate list is also a **failover** list. If the chosen provider fails in a way that
another provider might survive, Conduit retries the next candidate:

- **Retried:** timeouts, network failures, and HTTP `408`, `429`, and `5xx` responses.
- **Not retried:** validation and authentication `4xx` responses — a bad request will fail the same
  way everywhere, so there is nothing to gain.

**Streaming has one hard rule:** a streaming request can fail over only *before its first response
chunk*. Once any output has been sent to the client, it is never replayed onto a different provider.

To stop hammering a provider that is down, each mapping has a **circuit breaker**: five retriable
failures within 60 seconds opens the circuit for 30 seconds, after which a single half-open probe is
allowed through to test recovery.

## Prompt-cache controls

Providers that support prompt caching can be driven explicitly or left on automatic. **Caller-
provided cache settings always take precedence** — modes, lifetimes, keys, and content breakpoints
you send are honored as-is. A managed policy only fills in values you omit, and it **fails open**: if
its configuration or injection is invalid, the request proceeds uncached rather than erroring.

What a provider actually supports varies, and the provider adapter validates and shapes the controls
it accepts. As illustrative examples: OpenRouter supports automatic and explicit control with `5m`
and `1h` lifetimes; OpenAI offers automatic provider-managed caching plus explicit control on newer
models; some providers cache automatically with nothing to configure.

## Turning routing off and configuring it

- **Global kill switch** — a single runtime setting disables scored chat routing. With it off, each
  alias collapses to one mapping and the scoring path is skipped entirely. This is the lever to pull
  if routing ever misbehaves in production.
- **Global defaults** — the default policy (weights, cache affinity) applies to every alias that has
  no policy of its own.
- **Per-alias policies** — any alias can override the defaults with its own weights and affinity
  settings.

All three are managed through the Admin routing endpoints (and the WebAdmin UI). Because they are
[runtime configuration](./configuration.md#runtime-configuration-in-the-admin-ui), they change live —
no redeploy required.

## Other request types

Everything that is not a chat completion — embeddings, image generation, audio, rerank, and so on —
uses **deterministic resolution**: the alias resolves to a single mapping, which is used directly.
There is no scoring and no automatic failover for these request types. If you need redundancy for a
non-chat model, that is a configuration choice (which mapping the alias points at), not something
routing decides per request.

## Where to go next

- **[Core concepts](./concepts.md)** — aliases, mappings, providers, and the request lifecycle
  routing sits inside.
- **[Configuration](./configuration.md)** — where routing policies and the kill switch live.
- **[Monitoring](./monitoring.md)** — provider health and error tracking, which routing reads from.
