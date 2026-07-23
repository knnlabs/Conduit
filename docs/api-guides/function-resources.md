# Function resources

Function configuration, discovery, and execution use structured JSON throughout their public
contracts. JSON Schema and provider settings are JSON objects, not JSON-encoded strings.

## Canonical execution

Gateway and Admin publish the same `FunctionExecutionDto` lifecycle fields:

```json
{
  "id": "85f88aa3-a3e3-43d3-9e12-f33c4f6dcb36",
  "function_id": 7,
  "status": "completed",
  "input": { "query": "weather" },
  "output": { "answer": "sunny" },
  "error": null,
  "created_at": "2026-07-23T20:00:00Z",
  "started_at": "2026-07-23T20:00:00Z",
  "completed_at": "2026-07-23T20:00:01Z",
  "duration_ms": 1000,
  "cost": {
    "estimated": 0.001,
    "actual": 0.001,
    "currency": "USD",
    "breakdown": {}
  }
}
```

The Gateway renders snake_case. Admin renders the same fields in camelCase. `status` uses the
shared `ExecutionState` string enum. Duration is always an integer number of milliseconds.

Admin responses extend the base schema with an `admin` object containing virtual-key, retry,
lease, webhook, and progress diagnostics. Those operational details are not exposed by Gateway.

## Configuration and discovery

`providerSettings` and `parameterSchema` in Admin configuration requests and responses are
open-ended JSON objects. Gateway function discovery returns `parameter_schema` as the same kind of
object, both in the function list and the per-function parameter endpoint.
