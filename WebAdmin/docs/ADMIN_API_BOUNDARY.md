# Admin API boundary

WebAdmin owns its Admin HTTP integration under `src/lib/admin-api`. Application code must not import
`@knn_labs/conduit-admin-client` or reach into `SDKs/Node/Admin`.

`src/generated/admin-api.ts` is regenerated from
`Services/ConduitLLM.Admin/openapi-admin.json` by the repository's offline OpenAPI generator. The
local feature adapters preserve the domain-oriented methods used by WebAdmin's hooks and components,
but ordinary HTTP requests flow through an `openapi-fetch` client parameterized by the generated
`paths` type. Contract route constants use `satisfies keyof paths`, so renamed or removed routes fail
WebAdmin type-checking.

Browser operations still obtain a fresh ephemeral master key from
`/api/auth/ephemeral-master-key`, create a zero-retry Admin client, and call the externally reachable
Admin URL. Server operations use `CONDUIT_API_TO_API_BACKEND_AUTH_KEY`. Both paths send
`X-Master-Key`. Browser clients retain zero retries because each ephemeral key is single-use. Binary
analytics exports use the same transport with `arrayBuffer` parsing rather than a separate fetch path.

Credential issuance, virtual-key issuance and validation, and virtual-key-group state mutations are
validated with Zod before reaching application code. Routine read DTOs rely on generated compile-time
types.

The retired Admin Node package is no longer built or published by this repository; existing npm
versions remain available. Run `npm run check:api-boundary` to enforce the dependency boundary.
Contract generation and CI also fail when the WebAdmin-local generated types drift from the
authoritative Admin document.
