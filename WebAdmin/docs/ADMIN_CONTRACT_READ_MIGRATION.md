# Admin contract migration

WebAdmin is incrementally replacing the compatibility URL transport with direct operations on the
generated `openapi-fetch` `Client<paths>`. This changes only compile-time coupling to the Admin
contract; UI-facing services, DTOs, authentication, callbacks, retry behavior, and error behavior stay
stable.

## Completed model-family operations

The model-author, model-series, and model read slices are contract-native:

- `GET /api/ModelAuthor`
- `GET /api/ModelAuthor/{id}`
- `GET /api/ModelAuthor/{id}/series`
- `GET /api/ModelSeries`
- `GET /api/ModelSeries/{id}`
- `GET /api/ModelSeries/{id}/models`
- `GET /api/Model`
- `GET /api/Model/paged`
- `GET /api/Model/{id}`
- `GET /api/Model/search`
- `GET /api/Model/provider/{provider}`
- `GET /api/Model/{id}/identifiers`
- `GET /api/Model/{id}/available-providers`
- `GET /api/Model/{id}/provider-mappings`

The corresponding model-author, model-series, model, identifier, per-model provider-mapping, and
bundled-catalog create/update/delete/import operations are also contract-native. In total, all fourteen
reads and sixteen mutations in these three services use literal generated operations.

These methods call literal generated paths and pass numeric IDs as generated path parameters. A shared
base-client executor supplies `X-Master-Key`, custom headers, timeouts, caller cancellation, callbacks,
logging, retries, structured errors, and bodyless-response behavior. Boundary checks reject generic
HTTP transport calls in all three migrated services. Identifier normalization and paginated provider
enrichment remain local service behavior layered on the generated responses. Bundled-catalog import
uses its bodyless generated operation.

All nine top-level model-provider-mapping HTTP operations are also contract-native: list, get,
create, update, delete, bulk create, bulk delete, bulk enable, and bulk disable. The legacy
`bulkUpdate` helper remains a client-side fan-out over the generated update operation. Boundary
checks reject generic HTTP transport calls in `FetchModelMappingsService` as well.

Model-cost list, get, create, update, delete, array import, and overview operations are also
contract-native. The list adapter retains its local pagination facade, array import normalizes the
generated bulk result, and association IDs come directly from `ModelCostDto`; the nonexistent
`ModelCostMappings` surface has been removed.

Provider credentials and pricing are also contract-native. Provider CRUD, saved and unsaved tests,
credential CRUD, primary selection, validation, simulation, templates, audit queries, and audit
summaries all use generated operations. Provider/key response types are generated aliases; only UI
enums, display helpers, primary-key selection, and connection-test normalization remain local.

Analytics, Global Settings, and IP filtering are also contract-native. Analytics retains its request-log field-name
and binary-export adapters. Global Settings retains its UI aggregation and typed-value helpers while
all eight live settings and cache operations use generated paths; the unused fabricated pagination
method was removed because the API is intentionally unpaginated.

All ten IP-filter operations use generated paths: global, enabled, and virtual-key lists; get;
create; update; delete; settings read/update; and IP checking. The list facade still forwards its
legacy undocumented filters even though the backend ignores them, and settings/update adapters still
preserve partial request bodies even though the backend binds full models. Both mismatches are deferred
to a backend API redesign. Unused client-only synthetic helpers and their DTOs were retired.

All 28 Function Configuration, Credential, Cost, and Execution operations and all nine Provider Tools
operations are contract-native. Function configuration uses the backend `providerSettings` field;
credential and execution types match the wire contract; nullable function-cost fields are normalized
only where the active UI needs stable values. Partial configuration and credential update bodies remain
unchanged even though the controllers currently bind full entities.

Deferred work: purpose-built function update DTOs, credential response redaction, and a broader
Functions Admin API request/response DTO redesign.

## Next candidate

Configuration, Media, monitoring, Gateway
operations, streaming, and SignalR remain separate migrations.
