# Batch Operations with Idempotency

## Overview

Batch operations in Conduit support idempotency to prevent duplicate processing. This is critical for billing operations where duplicate requests could result in double charges.

## What is Idempotency?

Idempotency ensures that making the same request multiple times has the same effect as making it once. If a request fails due to network issues, you can safely retry it without worrying about duplicate processing.

## How It Works

1. **Client generates token**: Create a unique identifier for the operation
2. **Include in request**: Add `X-Idempotency-Token` header
3. **Server checks**: System checks if token has been processed
4. **Return cached result**: If duplicate detected, returns previous result
5. **Store result**: On first processing, result is cached for 24 hours

## API Endpoints Supporting Idempotency

### Batch Spend Updates

**Endpoint**: `POST /v1/batch/spend-updates`

**Headers**:
```
Authorization: Bearer <your-api-key>
Content-Type: application/json
X-Idempotency-Token: <unique-token>
```

**Example Request**:
```bash
curl -X POST https://api.conduit.com/v1/batch/spend-updates \
  -H "Authorization: Bearer sk-..." \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Token: op-spend-2024-01-15-12345" \
  -d '{
    "updates": [
      {
        "virtualKeyId": 123,
        "amount": 10.50,
        "model": "gpt-4",
        "providerType": "OpenAI",
        "metadata": {
          "requestId": "req-abc123"
        }
      },
      {
        "virtualKeyId": 124,
        "amount": 5.25,
        "model": "claude-3-opus",
        "providerType": "Anthropic"
      }
    ]
  }'
```

**Example Response**:
```json
{
  "operationId": "batch-op-789",
  "operationType": "spend_update",
  "totalItems": 2,
  "statusUrl": "/v1/batch/operations/batch-op-789",
  "taskId": "batch-op-789",
  "message": "Batch operation started. Subscribe to TaskHub with the taskId for real-time updates."
}
```

### Duplicate Request

If you retry the same request with the same idempotency token:

```bash
# Same request with same token
curl -X POST https://api.conduit.com/v1/batch/spend-updates \
  -H "Authorization: Bearer sk-..." \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Token: op-spend-2024-01-15-12345" \
  -d '<same payload>'
```

**Response**: Returns the *same* `operationId` and result as the first request. No duplicate processing occurs.

## Generating Idempotency Tokens

### Best Practices

1. **Use UUIDs**: Most reliable approach
   ```javascript
   const token = crypto.randomUUID();
   // Example: "550e8400-e29b-41d4-a716-446655440000"
   ```

2. **Use deterministic IDs**: Based on business context
   ```javascript
   const token = `spend-update-${accountId}-${timestamp}`;
   // Example: "spend-update-123-1705312800000"
   ```

3. **Use hash of payload**: For exact duplicate detection
   ```javascript
   const token = hashPayload(JSON.stringify(payload));
   // Example: "sha256-a3f8b9c..."
   ```

### Token Requirements

- **Uniqueness**: Must be unique per distinct operation
- **Consistency**: Same operation should use same token
- **Length**: 1-255 characters
- **Characters**: Alphanumeric, hyphens, underscores

### Bad Practices

❌ **Don't use**: Incrementing integers (conflicts across clients)
❌ **Don't use**: Current timestamp only (not unique enough)
❌ **Don't reuse**: Tokens across different operation types

## Code Examples

### JavaScript/TypeScript

```typescript
import { randomUUID } from 'crypto';

async function performBatchSpendUpdate(updates: SpendUpdate[]) {
  const idempotencyToken = randomUUID();

  const response = await fetch('https://api.conduit.com/v1/batch/spend-updates', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json',
      'X-Idempotency-Token': idempotencyToken,
    },
    body: JSON.stringify({ updates }),
  });

  if (!response.ok) {
    throw new Error(`Batch operation failed: ${response.statusText}`);
  }

  return await response.json();
}

// Safe to retry - idempotency prevents duplicates
async function performBatchWithRetry(updates: SpendUpdate[], maxRetries = 3) {
  const idempotencyToken = randomUUID();

  for (let attempt = 0; attempt < maxRetries; attempt++) {
    try {
      const response = await fetch('https://api.conduit.com/v1/batch/spend-updates', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${apiKey}`,
          'Content-Type': 'application/json',
          'X-Idempotency-Token': idempotencyToken,
        },
        body: JSON.stringify({ updates }),
      });

      if (response.ok) {
        return await response.json();
      }

      if (response.status >= 500) {
        // Server error - retry with same token
        await delay(Math.pow(2, attempt) * 1000);
        continue;
      }

      // Client error - don't retry
      throw new Error(`Batch operation failed: ${response.statusText}`);
    } catch (error) {
      if (attempt === maxRetries - 1) throw error;
      await delay(Math.pow(2, attempt) * 1000);
    }
  }
}
```

### Python

```python
import uuid
import requests
from typing import List, Dict

def perform_batch_spend_update(updates: List[Dict], api_key: str) -> Dict:
    idempotency_token = str(uuid.uuid4())

    response = requests.post(
        'https://api.conduit.com/v1/batch/spend-updates',
        headers={
            'Authorization': f'Bearer {api_key}',
            'Content-Type': 'application/json',
            'X-Idempotency-Token': idempotency_token,
        },
        json={'updates': updates},
    )

    response.raise_for_status()
    return response.json()

# With retry logic
def perform_batch_with_retry(updates: List[Dict], api_key: str, max_retries: int = 3) -> Dict:
    idempotency_token = str(uuid.uuid4())

    for attempt in range(max_retries):
        try:
            response = requests.post(
                'https://api.conduit.com/v1/batch/spend-updates',
                headers={
                    'Authorization': f'Bearer {api_key}',
                    'Content-Type': 'application/json',
                    'X-Idempotency-Token': idempotency_token,
                },
                json={'updates': updates},
                timeout=30,
            )

            if response.status_code == 202:
                return response.json()

            if response.status_code >= 500:
                # Server error - retry with same token
                time.sleep(2 ** attempt)
                continue

            # Client error - don't retry
            response.raise_for_status()

        except requests.RequestException as e:
            if attempt == max_retries - 1:
                raise
            time.sleep(2 ** attempt)

    raise Exception(f'Batch operation failed after {max_retries} attempts')
```

### C# / .NET

```csharp
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class BatchOperationsClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public BatchOperationsClient(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public async Task<BatchOperationResponse> PerformBatchSpendUpdate(List<SpendUpdate> updates)
    {
        var idempotencyToken = Guid.NewGuid().ToString();

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/batch/spend-updates")
        {
            Headers =
            {
                { "Authorization", $"Bearer {_apiKey}" },
                { "X-Idempotency-Token", idempotencyToken }
            },
            Content = new StringContent(
                JsonSerializer.Serialize(new { updates }),
                Encoding.UTF8,
                "application/json")
        };

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<BatchOperationResponse>(content);
    }

    public async Task<BatchOperationResponse> PerformBatchWithRetry(
        List<SpendUpdate> updates,
        int maxRetries = 3)
    {
        var idempotencyToken = Guid.NewGuid().ToString();

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/v1/batch/spend-updates")
                {
                    Headers =
                    {
                        { "Authorization", $"Bearer {_apiKey}" },
                        { "X-Idempotency-Token", idempotencyToken }
                    },
                    Content = new StringContent(
                        JsonSerializer.Serialize(new { updates }),
                        Encoding.UTF8,
                        "application/json")
                };

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<BatchOperationResponse>(content);
                }

                if ((int)response.StatusCode >= 500)
                {
                    // Server error - retry with same token
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                    continue;
                }

                // Client error - don't retry
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }

        throw new Exception($"Batch operation failed after {maxRetries} attempts");
    }
}
```

## Monitoring Progress

After starting a batch operation, monitor progress using the TaskHub or status endpoint:

### Using Status Endpoint

```bash
curl https://api.conduit.com/v1/batch/operations/batch-op-789 \
  -H "Authorization: Bearer sk-..."
```

**Response**:
```json
{
  "operationId": "batch-op-789",
  "operationType": "spend_update",
  "status": "Running",
  "totalItems": 1000,
  "processedCount": 450,
  "successCount": 448,
  "failedCount": 2,
  "progressPercentage": 45,
  "elapsedTime": "00:00:12",
  "estimatedTimeRemaining": "00:00:15",
  "itemsPerSecond": 37.5,
  "currentItem": "Item 451 of 1000",
  "canCancel": true
}
```

### Using SignalR (Real-time Updates)

```typescript
import * as signalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
  .withUrl('https://api.conduit.com/hubs/task')
  .build();

connection.on('TaskProgress', (taskId, progress, message) => {
  console.log(`Progress: ${progress}% - ${message}`);
});

connection.on('TaskCompleted', (taskId, result) => {
  console.log('Operation completed:', result);
});

await connection.start();
await connection.invoke('SubscribeToTask', 'batch-op-789');
```

## Cache Duration

- **Default TTL**: 24 hours
- **Cleanup**: Automatic (via Redis TTL)
- **Storage**: Redis-based, O(1) lookup performance

After 24 hours, idempotency tokens expire and the cached results are removed.

## Error Handling

### Token Already Processed

If you make a request with a token that was already processed, you'll receive the cached result:

```json
{
  "operationId": "batch-op-789",
  "operationType": "spend_update",
  "totalItems": 2,
  "statusUrl": "/v1/batch/operations/batch-op-789",
  "taskId": "batch-op-789",
  "message": "Batch operation started. Subscribe to TaskHub with the taskId for real-time updates."
}
```

The operation **will not be executed again**.

### Invalid Token

Tokens must be 1-255 characters. Invalid tokens will result in a 400 error:

```json
{
  "error": "Invalid idempotency token format"
}
```

### Redis Unavailable

If Redis is unavailable, the system fails open:
- Idempotency is disabled temporarily
- Operation proceeds normally
- Warning is logged

## Best Practices

### 1. Always Use Idempotency for Billing

```typescript
// ✅ GOOD - Always use idempotency for spend updates
const token = randomUUID();
await batchSpendUpdate(updates, { idempotencyToken: token });

// ❌ BAD - No idempotency = risk of double billing
await batchSpendUpdate(updates);
```

### 2. Retry with Same Token

```typescript
// ✅ GOOD - Same token across retries
const token = randomUUID();
for (let attempt = 0; attempt < 3; attempt++) {
  try {
    return await batchSpendUpdate(updates, { idempotencyToken: token });
  } catch (error) {
    if (attempt === 2) throw error;
  }
}

// ❌ BAD - Different token each retry
for (let attempt = 0; attempt < 3; attempt++) {
  const token = randomUUID(); // New token each time!
  try {
    return await batchSpendUpdate(updates, { idempotencyToken: token });
  } catch (error) {
    if (attempt === 2) throw error;
  }
}
```

### 3. Store Tokens for Reconciliation

```typescript
// ✅ GOOD - Store for audit trail
const token = randomUUID();
await db.saveOperation({ token, payload: updates, timestamp: Date.now() });
const result = await batchSpendUpdate(updates, { idempotencyToken: token });
await db.updateOperation({ token, result });
```

### 4. Use Deterministic Tokens for Scheduled Jobs

```typescript
// ✅ GOOD - Prevents duplicate execution if job runs twice
const token = `daily-spend-update-${accountId}-${date}`;
await batchSpendUpdate(updates, { idempotencyToken: token });
```

## Troubleshooting

### Operation Not Idempotent

**Symptom**: Same token returns different results

**Cause**: Token expired (> 24 hours old)

**Solution**: Generate a new token for operations older than 24 hours

### Unexpected Cached Result

**Symptom**: Operation returns old cached result

**Cause**: Reusing an old token

**Solution**: Generate a new unique token for each distinct operation

### Performance Degradation

**Symptom**: Slow response times with idempotency

**Cause**: Redis latency or connection issues

**Solution**: Check Redis health and connection pooling

## Limitations

1. **TTL**: Cached results expire after 24 hours
2. **Storage**: Redis-based (requires Redis availability)
3. **Scope**: Idempotency is per-token, not per-payload
4. **Size**: Token size limited to 255 characters

## Related Documentation

- [Batch Operations API Reference](/docs/api-reference/batch-operations)
- [Batch Operation Framework](/docs/architecture/patterns/batch-operation-framework.md)
- [Error Handling Guide](/docs/api-guides/error-handling)
- [Rate Limiting](/docs/api-guides/rate-limiting)
