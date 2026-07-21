# Gateway API boundary

WebAdmin owns its Gateway integration under `src/lib/gateway-api`. Application code must not import
the published Gateway or Common packages or reach into the SDK workspace. The public Gateway SDK is
maintained independently for external consumers.

`src/generated/gateway-api.ts` is regenerated from the authoritative Gateway OpenAPI document. The
local client intentionally exposes only the capabilities WebAdmin uses: ephemeral-key creation,
discovery, function execution, chat streaming, image generation, asynchronous video generation,
cancellation, and media upload.

Browser calls obtain short-lived virtual keys through WebAdmin's ephemeral-key route and send them
as opaque Bearer tokens. Chat parsing preserves standard chunks plus reasoning, tool, metrics,
final-metrics, and error SSE events. Video progress uses the local SignalR client with polling as a
fallback. File uploads retain local validation and progress reporting.

Run `npm run check:admin-boundary` to enforce both local API boundaries. Offline OpenAPI generation
and CI fail if either WebAdmin generated type file drifts from its authoritative contract.
