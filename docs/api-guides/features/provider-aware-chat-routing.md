# Provider-aware chat routing

Chat-completion aliases may map to more than one enabled provider. Conduit scores eligible mappings with the alias routing policy, using configured price, speed, quality, mapping weight, priority, health, and bounded prompt-cache affinity. Other APIs keep their existing deterministic mapping behavior.

The default `Balanced` policy weights cost, speed, and quality at `0.40`, `0.30`, and `0.30`. Cache affinity uses a sliding 30-minute lifetime and is accepted only when its route is no more than `0.10` below the best eligible score. Administrators can change global defaults or an alias policy through the Admin routing endpoints. `Routing.Chat.Enabled` is the emergency global switch.

## Session affinity

Chat requests can include an optional `session_id`:

```json
{
  "model": "support-agent",
  "session_id": "conversation-123",
  "messages": [{ "role": "user", "content": "Continue my case" }]
}
```

Clients may instead send `X-Conduit-Session-Id`. The JSON body takes precedence when both are present. If neither is supplied, Conduit derives affinity from the first system or developer message and the first user message.

Conduit HMACs both explicit and derived values with the authenticated virtual-key hash before using Redis. Raw session IDs and prompt content are never persisted or used as metric labels.

## Failover

Timeouts, network failures, HTTP 408/429, and 5xx responses may retry on another eligible mapping. Validation and authentication 4xx responses are not retried. Streaming requests can fail over only before their first response chunk; emitted output is never replayed. Five retryable failures in 60 seconds open a mapping circuit for 30 seconds, after which one half-open probe is allowed.

## Prompt-cache controls

Prompt-cache policies use schema v3 and the generic `Automatic` and `Explicit` strategies. Provider adapters validate and shape supported controls. Caller-provided cache modes, lifetimes, keys, and content breakpoints take precedence; managed policy fills only missing values and fails open if configuration or injection is invalid.

OpenRouter supports automatic and explicit controls with `5m` and `1h` lifetimes. OpenAI supports automatic provider-managed caching and explicit controls for GPT-5.6-or-later models with the supported `30m` lifetime. Groq caching is automatic and observable, with no managed controls.
