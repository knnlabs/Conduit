# Provider HTTP Resilience

How ConduitLLM protects calls to upstream LLM providers. Implemented with
[Microsoft.Extensions.Http.Resilience](https://learn.microsoft.com/dotnet/core/resilience/http-resilience)
(Polly v8) in `Shared/ConduitLLM.Providers/Http/`.

## Named clients

Provider clients never construct `HttpClient` directly — they request named clients whose names
come from **`ProviderHttpClientNames`**, the single source of truth shared by registration
(`HttpClientExtensions.AddLLMProviderHttpClients`) and the request side (`BaseLLMClient`).
Three named clients exist per provider type:

| Suffix | Used for | Default operation class |
|---|---|---|
| `*LLMClient` | chat, embeddings, images | `chat` |
| `*AuthVerification` | key/auth verification | `auth` |
| `*VideoClient` | video generation | `video` |

`AddProviderServices()` registers all of them (idempotently) — any host that can create provider
clients automatically gets the resilience pipeline in both Gateway and Admin.

> **Why this matters:** `IHttpClientFactory` silently returns an *unconfigured* client for any
> unknown name. A registration/request name mismatch disables every policy with no error — that
> exact bug shipped for a long time. `ProviderHttpClientRegistrationTests` pins the names as
> string literals; if you change a prefix, the tests fail before production traffic does.

## Pipeline

Each named client gets, outermost → innermost:

1. **Total timeout** — caps all attempts plus backoff delays.
2. **Retry** — transient errors (5xx, 408, connect failures, per-attempt timeouts) and 429.
   Honors `Retry-After` up to a cap (default 5s); a `Retry-After` **beyond** the cap declines the
   retry entirely — failing fast beats holding an interactive request open. Otherwise
   exponential backoff with jitter. Zero-retry budgets (auth, video) omit the strategy.
3. **Circuit breaker** — opens on outage signals only: 5xx, 408, connect failures, attempt
   timeouts. **Not** 429 (rate limiting is not an outage; breaking on it amplifies quota
   incidents) and not auth-class 4xx (key problems belong to error tracking).
   State is partitioned per **named client × request URI authority**
   (`SelectPipelineByAuthority`), so two Provider rows pointing at `api.openai.com` share
   failure signal while OpenAI-compatible providers on different hosts break independently.
4. **Per-attempt timeout** — bounds a single hung connection. For streaming requests (sent with
   `ResponseHeadersRead`) this is effectively a **time-to-first-token** bound, because the HTTP
   call completes when response headers arrive.

**The pipeline never cancels an in-progress response stream** — every strategy acts on
`SendAsync`, which completes at headers for streaming. Stream lifetime is governed by the
streaming idle watchdog (see `ProviderResilienceOptions.Streaming`).

## Operation classes and budgets

Timeouts are resolved per request from the operation class: the
`ConduitHttpOptions.OperationClass` request option when set, else the named client's default.
Budgets ship with these defaults (all configurable):

| Class | Attempt timeout | Total budget | Retries |
|---|---|---|---|
| `chat` / `chat-stream` | 100s | 120s | 2 |
| `images` | 180s | 240s | 2 |
| `auth` | 10s | 30s | 0 |
| `video` | 600s | 1800s | 0 |

Video and auth get zero retries deliberately: retrying an accepted video generation job risks
double-generation and double cost, and a wrong API key stays wrong.

## Configuration

Section `Conduit:ProviderHttp` (see `ProviderResilienceOptions` for all fields):

```jsonc
"Conduit": {
  "ProviderHttp": {
    "Retry": { "BaseDelaySeconds": 0.5, "MaxDelaySeconds": 5, "RetryAfterCapSeconds": 5 },
    "CircuitBreaker": {
      "Enabled": true, "FailureRatio": 0.5, "MinimumThroughput": 10,
      "SamplingDurationSeconds": 30, "BreakDurationSeconds": 30
    },
    "Budgets": {
      "Chat":   { "AttemptTimeoutSeconds": 100, "TotalTimeoutSeconds": 120,  "MaxRetryAttempts": 2 },
      "Images": { "AttemptTimeoutSeconds": 180, "TotalTimeoutSeconds": 240,  "MaxRetryAttempts": 2 },
      "Auth":   { "AttemptTimeoutSeconds": 10,  "TotalTimeoutSeconds": 30,   "MaxRetryAttempts": 0 },
      "Video":  { "AttemptTimeoutSeconds": 600, "TotalTimeoutSeconds": 1800, "MaxRetryAttempts": 0 }
    },
    "Streaming": { "IdleReadTimeoutSeconds": 90 }
  }
}
```

`CircuitBreaker:Enabled = false` is the escape hatch if a chronically flaky provider trips the
breaker in ways that hurt more than help.

## Error tracking

`ProviderErrorTrackingRetryHook` runs inside the retry strategy's `OnRetry`: 429s are tracked on
every retry; fatal-class errors (401/402/403) only on the final retry (avoiding duplicates).
Key/provider attribution flows through `ProviderKeyContext` (AsyncLocal, set by
`ContextAwareLLMClient`) into `IProviderErrorTrackingService`, which auto-disables failing keys.

## Known limitation (until the streaming timeout fix lands)

`BaseLLMClient` still sets `HttpClient.Timeout = 120s`, which sits outside the pipeline and both
truncates the images total budget (240s → 120s) and kills SSE streams that run longer than 120s.
The follow-up PR moves all budgets into the pipeline (`HttpClient.Timeout = Infinite`) and adds
the streaming idle-read watchdog.
