# Usage Tracking System

## Overview

The Usage Tracking System replaces the previous crude character÷4 token estimation approach with actual usage data extracted directly from provider responses. This middleware-based solution intercepts API responses, extracts real usage metrics, and updates virtual key spending with accurate cost calculations.

### Purpose and Benefits

- **Accurate Billing**: Tracks actual token usage from provider responses instead of estimating
- **Multi-Provider Support**: Handles different response formats (OpenAI, Anthropic, etc.)
- **Cached Token Support**: Properly accounts for Anthropic's cached token pricing
- **Batch Processing**: Reduces database writes through Redis-based batching
- **Real-time Metrics**: Exposes Prometheus metrics for monitoring
- **Streaming Support**: Handles both standard and streaming responses

### High-Level Architecture

```
Request Flow:
    Client Request
         ↓
    Authentication Middleware
         ↓
    UsageTrackingMiddleware (intercepts response)
         ↓
    API Controller
         ↓
    Provider Service
         ↓
    Response with Usage Data
         ↓
    UsageTrackingMiddleware (extracts & processes)
         ↓
    Client Response

Processing Flow:
    Extract Usage → Calculate Cost → Batch Update → Log Request
```

## Components

### UsageTrackingMiddleware

**Location**: `ConduitLLM.Http/Middleware/UsageTrackingMiddleware.cs`

**Responsibility**: Core middleware that intercepts HTTP responses and extracts usage data.

**Placement in Pipeline**: Registered after authentication but before response compression.

#### How It Intercepts Responses

1. **Request Filtering**: Only processes `/v1` API endpoints with successful responses (status < 400)
2. **Response Buffering**: Replaces response stream with MemoryStream for inspection
3. **Content Detection**: Identifies streaming vs non-streaming responses
4. **Usage Extraction**: Parses JSON response for usage data
5. **Cost Calculation**: Computes costs based on model and usage
6. **Spend Update**: Queues update via batch service or direct update as fallback
7. **Response Forwarding**: Copies original response to client

#### Provider Detection Mechanism

The middleware uses HttpContext.Items to access provider information:
- `ProviderType`: Used for metrics labeling
- `VirtualKeyId`: Required for spend tracking
- `IsStreamingRequest`: Indicates SSE responses

### Provider Response Parsing

The system handles multiple provider response formats through the `ExtractUsage` method:

#### OpenAI Format
```json
{
  "usage": {
    "prompt_tokens": 9,
    "completion_tokens": 12,
    "total_tokens": 21
  },
  "model": "gpt-4"
}
```

**Tracked Fields**:
- `prompt_tokens` → `Usage.PromptTokens`
- `completion_tokens` → `Usage.CompletionTokens`
- `total_tokens` → `Usage.TotalTokens`

#### Anthropic Format
```json
{
  "usage": {
    "input_tokens": 2095,
    "output_tokens": 503,
    "cache_creation_input_tokens": 100,
    "cache_read_input_tokens": 50
  },
  "model": "claude-3-5-sonnet-20241022"
}
```

**Tracked Fields**:
- `input_tokens` → `Usage.PromptTokens` (overrides OpenAI format if both exist)
- `output_tokens` → `Usage.CompletionTokens`
- `cache_creation_input_tokens` → `Usage.CachedWriteTokens`
- `cache_read_input_tokens` → `Usage.CachedInputTokens`

#### Image Generation Format
```json
{
  "usage": {
    "images": 2
  },
  "model": "dall-e-3"
}
```

**Tracked Fields**:
- `images` → `Usage.ImageCount`

#### Streaming Response Handling

For Server-Sent Events (SSE) streaming:
1. Middleware detects `Content-Type: text/event-stream`
2. Skips JSON parsing to avoid breaking the stream
3. Relies on SSE writer to store usage in `HttpContext.Items["StreamingUsage"]`
4. Processes accumulated usage after stream completion

### Cost Calculation Flow

1. **Response Interception**: Middleware captures response after controller execution
2. **Usage Extraction**: Parses response JSON for usage object
3. **Model Resolution**: Extracts model name from response
4. **Cost Calculation**: Calls `ICostCalculationService.CalculateCostAsync(model, usage)`
5. **Spend Update**: Queues update to batch service or falls back to direct update

**Implementation**:
```csharp
// Extract usage data
var usage = ExtractUsage(usageElement);

// Calculate cost based on model and usage
var cost = await costCalculationService.CalculateCostAsync(model, usage);

// Update spend using batch service
if (batchSpendService.IsHealthy)
{
    batchSpendService.QueueSpendUpdate(virtualKeyId, cost);
}
else
{
    // Fallback to direct database update
    await virtualKeyService.UpdateSpendAsync(virtualKeyId, cost);
}
```

### Batch Spend Updates

**Purpose**: Reduce database write pressure by batching multiple spend updates together.

**Benefits**:
- Reduces database round-trips
- Improves throughput for high-volume scenarios
- Provides automatic fallback to direct updates
- Prevents data loss through Redis persistence

#### How It Works

1. **Queueing**: Updates are queued to Redis with group-based keys
2. **Accumulation**: Redis atomically increments spend values
3. **Batch Processing**: Background service flushes updates periodically
4. **Event Notification**: Raises `SpendUpdatesCompleted` event for cache invalidation
5. **Fallback Mechanism**: Direct database updates when batch service is unhealthy

#### Redis Key Structure
```
pending_spend:group:{virtualKeyGroupId}  # Accumulated spend by group
key_usage:group:{groupId}:key:{keyId}    # Individual key usage tracking
```

#### Configuration
```json
{
  "BatchSpending": {
    "FlushIntervalSeconds": 10,
    "RedisTtlMinutes": 30
  }
}
```

### Monitoring

#### Prometheus Metrics Available

| Metric Name | Type | Labels | Description |
|------------|------|--------|-------------|
| `conduit_usage_tracking_requests_total` | Counter | `endpoint_type`, `status` | Total usage tracking requests |
| `conduit_usage_tracking_tokens_total` | Counter | `model`, `provider_type`, `token_type` | Total tokens tracked |
| `conduit_usage_tracking_cost_dollars` | Counter | `model`, `provider_type`, `endpoint_type` | Total cost tracked in dollars |
| `conduit_usage_tracking_failures_total` | Counter | `reason`, `endpoint_type` | Usage tracking failures |
| `conduit_usage_extraction_time_seconds` | Histogram | `endpoint_type` | Time to extract usage from response |

#### Example Prometheus Queries

```promql
# Total tokens processed by model
sum by (model) (rate(conduit_usage_tracking_tokens_total[5m]))

# Cost per provider over time
sum by (provider_type) (rate(conduit_usage_tracking_cost_dollars[1h]))

# Failure rate by endpoint
rate(conduit_usage_tracking_failures_total[5m]) / rate(conduit_usage_tracking_requests_total[5m])

# P99 extraction time
histogram_quantile(0.99, rate(conduit_usage_extraction_time_seconds_bucket[5m]))
```

#### Alerting Recommendations

```yaml
# High failure rate alert
- alert: HighUsageTrackingFailureRate
  expr: rate(conduit_usage_tracking_failures_total[5m]) > 0.05
  for: 5m
  annotations:
    summary: "High usage tracking failure rate: {{ $value | humanizePercentage }}"

# Slow extraction time alert  
- alert: SlowUsageExtraction
  expr: histogram_quantile(0.99, rate(conduit_usage_extraction_time_seconds_bucket[5m])) > 0.5
  for: 5m
  annotations:
    summary: "Slow usage extraction: {{ $value }}s P99"
```

## Adding New Providers

### Response Format Requirements

New providers must return usage data in their response with:
1. A `usage` object containing token/unit counts
2. A `model` field identifying the model used
3. Consistent field names across streaming and non-streaming responses

### Integration Steps

1. **Update Usage Extraction** (`UsageTrackingMiddleware.ExtractUsage`):
```csharp
// Add provider-specific field mappings
if (usageElement.TryGetProperty("new_provider_prompt_field", out var promptTokens))
    usage.PromptTokens = promptTokens.GetInt32();
```

2. **Add Provider Type** (if new):
```csharp
public enum ProviderType
{
    // ... existing providers
    NewProvider = 11
}
```

3. **Configure Cost Calculation** (`CostCalculationService`):
   - Add model-to-cost mappings in database
   - Support provider-specific pricing models

4. **Test Response Parsing**:
```csharp
[Fact]
public async Task NewProvider_Response_Tracks_Usage()
{
    // Arrange response with provider format
    var response = new { 
        usage = new { /* provider fields */ },
        model = "provider-model"
    };
    
    // Assert usage extracted correctly
}
```

### Testing Guidelines

1. **Unit Tests**: Test usage extraction for the new format
2. **Integration Tests**: Verify end-to-end tracking with mock responses
3. **Load Tests**: Ensure batch processing handles provider volume
4. **Monitoring**: Verify metrics are properly labeled for new provider

## Troubleshooting

### Common Issues

#### No Usage Data in Response
- **Symptom**: Log shows "No usage data found in response"
- **Cause**: Provider not returning usage object or different field name
- **Solution**: Check provider documentation for response format

#### Zero Cost Calculated
- **Symptom**: Usage extracted but cost is $0.00
- **Cause**: Missing model cost configuration
- **Solution**: Add model costs to database via Admin API

#### Batch Service Unhealthy
- **Symptom**: "BatchSpendUpdateService unhealthy" warnings
- **Cause**: Redis connection issues or service not started
- **Solution**: Check Redis connectivity and service health

#### Streaming Usage Not Tracked
- **Symptom**: SSE responses show no usage tracking
- **Cause**: SSE writer not storing usage in HttpContext.Items
- **Solution**: Verify SSE handler sets `StreamingUsage` and `StreamingModel`

### Debug Logging

Enable debug logging for detailed tracking information:

```json
{
  "Logging": {
    "LogLevel": {
      "ConduitLLM.Http.Middleware.UsageTrackingMiddleware": "Debug",
      "ConduitLLM.Configuration.Services.BatchSpendUpdateService": "Debug"
    }
  }
}
```

### Verification Steps

1. **Check Metrics Endpoint**: `http://localhost:5000/metrics`
   - Look for `conduit_usage_tracking_*` metrics
   - Verify counters are incrementing

2. **Inspect Redis Keys**:
```bash
redis-cli
> KEYS pending_spend:*
> GET pending_spend:group:123
```

3. **Database Verification**:
```sql
-- Check recent spend updates
SELECT vk.Id, vk.Name, vk.TotalSpend, vk.UpdatedAt
FROM VirtualKeys vk
WHERE vk.UpdatedAt > NOW() - INTERVAL '1 hour'
ORDER BY vk.UpdatedAt DESC;

-- Check request logs
SELECT ModelName, InputTokens, OutputTokens, Cost, CreatedAt
FROM RequestLogs
WHERE CreatedAt > NOW() - INTERVAL '1 hour'
ORDER BY CreatedAt DESC;
```

4. **Test with Curl**:
```bash
# Make a request and check response headers
curl -i -X POST http://localhost:5000/v1/chat/completions \
  -H "Authorization: Bearer YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{"model": "gpt-4", "messages": [{"role": "user", "content": "Hello"}]}'
```

## Implementation Details

### Middleware Registration

```csharp
// In Program.cs
app.UseUsageTracking(); // After auth, before endpoints
```

### Key Classes and Interfaces

- `UsageTrackingMiddleware`: Main middleware class
- `ICostCalculationService`: Calculates costs based on usage
- `IBatchSpendUpdateService`: Manages batched spend updates
- `IRequestLogService`: Logs request details for auditing
- `Usage`: Model containing token counts and usage metrics

### Performance Considerations

- **Response Buffering**: Uses MemoryStream to avoid blocking
- **Async Processing**: All operations are async/await
- **Fire-and-Forget Batching**: Spend updates don't block response
- **Redis TTL**: Automatic cleanup of stale pending updates
- **Metric Sampling**: Histograms use exponential buckets for efficiency

## Migration from Character-Based Estimation

The system automatically handles the transition:
1. New requests use actual usage tracking
2. Historical data remains unchanged
3. Cost calculations are more accurate going forward
4. No manual intervention required

## Related Documentation

- [Cost Calculation Service](../model-pricing/README.md)
- [Batch Processing Architecture](../architecture/batch-processing.md)
- [Provider Integration Guide](../providers/integration-guide.md)
- [Prometheus Metrics Setup](../prometheus-metrics-setup.md)