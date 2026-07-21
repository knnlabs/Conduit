# Admin contract-read migration

WebAdmin is incrementally replacing the compatibility URL transport with direct operations on the
generated `openapi-fetch` `Client<paths>`. This changes only compile-time coupling to the Admin
contract; UI-facing services, DTOs, authentication, callbacks, retry behavior, and error behavior stay
stable.

## Completed pilot

The model-author and model-series read slice is contract-native:

- `GET /api/ModelAuthor`
- `GET /api/ModelAuthor/{id}`
- `GET /api/ModelAuthor/{id}/series`
- `GET /api/ModelSeries`
- `GET /api/ModelSeries/{id}`
- `GET /api/ModelSeries/{id}/models`

These methods call literal generated paths and pass numeric IDs as generated path parameters. A shared
base-client executor supplies `X-Master-Key`, custom headers, timeouts, caller cancellation, callbacks,
logging, retries, structured errors, and bodyless-response behavior. Boundary checks reject generic
`client['get']` usage in the two migrated services.

Author and series create, update, and delete methods intentionally remain on the compatibility
transport.

## Next candidate

The next candidate slice is the routine model and model-identifier reads in `FetchModelService`.
Model mutations, other Admin services, Gateway operations, streaming, and SignalR should remain
separate migrations so each slice can preserve and verify its existing behavior independently.
