# Admin API boundary

WebAdmin owns its Admin HTTP integration under `src/lib/admin-api`. Application code must not import
`@knn_labs/conduit-admin-client` or reach into `SDKs/Node/Admin`.

`src/generated/admin-api.ts` is regenerated from
`Services/ConduitLLM.Admin/openapi-admin.json` by the repository's offline OpenAPI generator. The
local feature adapters use those generated wire types while preserving the domain-oriented methods
used by WebAdmin's hooks and components.

Browser operations still obtain a fresh ephemeral master key from
`/api/auth/ephemeral-master-key`, create a zero-retry Admin client, and call the externally reachable
Admin URL. Server operations use `CONDUIT_API_TO_API_BACKEND_AUTH_KEY`. Both paths send
`X-Master-Key`. Gateway operations use their separate local boundary and ephemeral virtual-key flow.

The retired Admin Node package is no longer built or published by this repository; existing npm
versions remain available. Run `npm run check:admin-boundary` to enforce the dependency boundary.
Contract generation and CI also fail when the WebAdmin-local generated types drift from the
authoritative Admin document.
