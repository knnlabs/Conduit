# SSE production validation

The Gateway no longer captures response bodies. Streaming accounting is published by controllers and finalized independently of client transport.

## Required ingress settings

Use [`nginx/gateway-sse.conf`](../nginx/gateway-sse.conf) or its platform equivalent for `POST /v1/chat/completions`:

- response buffering, caching, compression, and transformation disabled;
- upstream idle timeout longer than the longest supported stream;
- HTTP connection semantics left to the proxy and Kestrel;
- health checks remove an instance before termination;
- the termination grace period is at least `UsageTracking__GracefulShutdownSeconds` (default 45 seconds).

The Gateway uses a separate `UsageTracking__AccountingFinalizationTimeoutSeconds` budget (default 10 seconds) after client cancellation.

## Validation matrix

Run every scenario through the production ingress, not directly against Kestrel:

| Scenario | Expected result |
|---|---|
| HTTP/1.1 and HTTP/2 | First frame arrives before `[DONE]`; frames remain ordered |
| Disconnect before first provider chunk | No client rewrite; reservation remains safe according to invocation state |
| Disconnect after several chunks | Observed usage settles, or the started reservation remains indeterminate |
| Provider failure before headers | Normal JSON error and status code |
| Provider failure after headers | At most one SSE error; never `[DONE]` |
| Two Gateway replicas | Independent streams complete and shared spend settlement is idempotent |
| Rolling termination | Instance stops admission, drains, and finalizes or retains reservations before exit |

## Soak command

```powershell
$env:CONDUIT_BASE_URL = "https://gateway.example.com"
$env:CONDUIT_API_KEY = "condt_..."
$env:CONDUIT_MODEL = "your-model-alias"
$env:CONDUIT_STREAM_CONCURRENCY = "200"
$env:CONDUIT_STREAM_REQUESTS_PER_WORKER = "10"
node scripts/test/sse-soak.mjs
```

During the run, compare managed heap and large-object-heap size to active streams rather than total emitted bytes. Confirm memory returns after completion and inspect:

- `conduit_streams_active`
- `conduit_streams_total{outcome}`
- `conduit_stream_time_to_provider_first_chunk_seconds`
- `conduit_stream_time_to_client_first_flush_seconds`
- `conduit_stream_chunks_total{provider}`
- `conduit_stream_bytes_total{provider}`
- `conduit_stream_client_disconnects_total{phase}`
- `conduit_stream_accounting_finalization_seconds{outcome}`
- `conduit_stream_usage_evidence_total{source}`

Declare p50/p95/p99 first-flush and proxy-overhead SLOs before rollout. A failed validation must reject or disable streaming; it must never restore response buffering.
