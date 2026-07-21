# Gateway API boundary

WebAdmin owns its Gateway integration under `src/lib/gateway-api`. Application code must not import
the published Gateway or Common packages or reach into the SDK workspace. The public Gateway SDK is
maintained independently for external consumers.

`src/generated/gateway-api.ts` is regenerated from the authoritative Gateway OpenAPI document.
Ordinary JSON operations flow through an `openapi-fetch` client parameterized by its generated
`paths` type. The local client intentionally exposes only the capabilities WebAdmin uses: ephemeral-key creation,
discovery, function execution, chat streaming, image generation, asynchronous video generation,
cancellation, and media upload.

Browser calls obtain short-lived virtual keys through WebAdmin's ephemeral-key route and send them
as opaque Bearer tokens. Chat parsing preserves standard chunks plus reasoning, tool, metrics,
final-metrics, and error SSE events. Video progress uses the local SignalR client with polling as a
fallback. File uploads retain local validation and progress reporting. SSE parsing, XHR upload
progress, and SignalR are explicit transport exceptions; their HTTP paths are still checked against
the generated contract. Credential, upload, function-execution, and video-task state responses are
validated with Zod.

Run `npm run check:api-boundary` to enforce both local API boundaries. Offline OpenAPI generation
and CI fail if either WebAdmin generated type file drifts from its authoritative contract.
