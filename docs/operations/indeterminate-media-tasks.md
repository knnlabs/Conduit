# Reconciling indeterminate media tasks

An image or video task becomes `indeterminate` when Conduit started a provider
invocation but lost the definitive outcome. Automatic retry is disabled because
the provider may have completed and charged for the original operation.

The customer spend reservation is released on this path. The resolutions below
never charge the customer. Do not use them as a substitute for a future billing
correction workflow.

## Detection

Grafana raises **Indeterminate Media Task** when
`media_task_indeterminate_total` increases. The metric's `source` label identifies
whether the task was detected by lease recovery, cancellation, or an exception.

List unresolved tasks with a master key:

```bash
curl -sS "https://CONDUIT_ADMIN/v1/admin/tasks?state=indeterminate&page=1&pageSize=50" \
  -H "Authorization: Bearer MASTER_KEY"
```

The response intentionally excludes task payload and metadata because those fields
may contain a virtual key or customer input.

## Provider reconciliation

For each task:

1. Record the task ID, provider operation ID when present, model, and timestamps.
2. Search the provider dashboard or API for an operation in that time window.
3. Confirm one of these outcomes:
   - the provider did not accept or execute the operation;
   - the provider definitively failed it; or
   - the provider completed it or the outcome is still unknown.
4. Save the provider evidence in the incident record and put a concise reference in
   the resolution `reason`.

If the provider completed the operation, or the outcome remains unknown, do not
retry. Leave the task indeterminate and escalate the incident. The Admin API does
not offer a `completed` state flip because it cannot attach a valid result or make
an explicit billing correction.

## Retry after confirmed non-execution

Only retry after the provider confirms it did not perform the work:

```bash
curl -sS -X POST \
  "https://CONDUIT_ADMIN/v1/admin/tasks/TASK_ID/resolve" \
  -H "Authorization: Bearer MASTER_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "resolution": "retry",
    "reason": "Provider incident ABC-123 confirms the operation was never accepted"
  }'
```

The API returns `202 Accepted` with a `dispatchId`. Admin publishes a durable
reconciliation command; Gateway idempotently moves the task back to pending and
publishes the stored media request through its transactional handler outbox. A
redelivered command with the same dispatch ID is safe, and duplicate generation
events are rejected by the task claim.

Verify that the task leaves the indeterminate listing and reaches `processing`,
then a terminal state. Also check
`media_task_operator_retries_total{task_type=...}` and logs containing the dispatch
ID.

## Close as failed without charge

When the provider definitively failed and retry is not appropriate:

```bash
curl -i -X POST \
  "https://CONDUIT_ADMIN/v1/admin/tasks/TASK_ID/resolve" \
  -H "Authorization: Bearer MASTER_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "resolution": "failed_no_charge",
    "reason": "Provider incident ABC-123 confirms a terminal failure",
    "providerOperationId": "optional-provider-operation-id"
  }'
```

A successful resolution returns `204 No Content`, marks the task failed and
non-retryable, and publishes the normal task lifecycle update. No customer charge
or refund is created.

## Audit and safety

- Both actions require `MasterKeyPolicy` and are recorded by Admin operation and
  audit logging.
- Retry is rejected after the task's retry limit.
- Never put a master key, virtual key, prompt, or provider secret in `reason`.
- Do not update `AsyncTasks` directly; doing so bypasses durable dispatch, cache
  invalidation, lifecycle events, and audit logging.
