# Gateway route namespaces

The Gateway reserves direct `/v1/...` routes for resources that follow the
OpenAI API contract. Conduit-specific extensions live below `/v1/conduit/...`.

## OpenAI-compatible routes

- `/v1/models`
- `/v1/chat/completions`
- `/v1/completions`
- `/v1/embeddings`
- `/v1/audio/...`
- `/v1/images/generations`

## Conduit extension route map

| Previous prefix or route | Canonical route |
|---|---|
| `/v1/auth/...` | `/v1/conduit/auth/...` |
| `/v1/batch/...` | `/v1/conduit/batch/...` |
| `/v1/discovery/...` | `/v1/conduit/discovery/...` |
| `/v1/downloads/...` | `/v1/conduit/downloads/...` |
| `/v1/functions/...` | `/v1/conduit/functions/...` |
| `/v1/images/generations/async` | `/v1/conduit/images/generations/async` |
| `/v1/images/generations/{taskId}` | `/v1/conduit/images/generations/{taskId}` |
| `/v1/images/generations/{taskId}/status` | `/v1/conduit/images/generations/{taskId}/status` |
| `/v1/media/...` | `/v1/conduit/media/...` |
| `/v1/models/{modelId}/metadata` | `/v1/conduit/models/{modelId}/metadata` |
| `/v1/rerank` | `/v1/conduit/rerank` |
| `/v1/tasks/...` | `/v1/conduit/tasks/...` |
| `/v1/videos/...` | `/v1/conduit/videos/...` |

The old extension routes are intentionally not retained as aliases. This keeps
the generated contract unambiguous and prevents new non-OpenAI resources from
leaking into the reserved namespace.
