# Error Tracking Architecture

This document describes the technical architecture of Conduit's provider error tracking system for developers.

## Overview

The error tracking system monitors provider API failures, classifies them by severity, and automatically disables keys that consistently fail. It uses Redis for fast, ephemeral storage and MassTransit for event-driven updates.

## Components

```
┌─────────────────┐     ┌──────────────────────┐     ┌─────────────────┐
│  Provider       │────▶│ ContextAwareLLMClient│────▶│ ILLMClient      │
│  Client Factory │     │ (Decorator)          │     │ (Actual Client) │
└─────────────────┘     └──────────┬───────────┘     └─────────────────┘
                                   │
                    Error occurs   │
                                   ▼
                        ┌──────────────────────┐
                        │ ProviderErrorTracking│
                        │ Service              │
                        └──────────┬───────────┘
                                   │
              ┌────────────────────┼────────────────────┐
              │                    │                    │
              ▼                    ▼                    ▼
      ┌───────────────┐   ┌───────────────┐   ┌───────────────┐
      │ Redis Error   │   │ Key/Provider  │   │ MassTransit   │
      │ Store         │   │ Disable Logic │   │ Events        │
      └───────────────┘   └───────────────┘   └───────────────┘
```

### Core Services

| Component | Location | Responsibility |
|-----------|----------|----------------|
| `ProviderErrorTrackingService` | `ConduitLLM.Core/Services/` | Core tracking logic, threshold evaluation, key disabling |
| `RedisErrorStore` | `ConduitLLM.Core/Services/` | Redis persistence layer |
| `ContextAwareLLMClient` | `ConduitLLM.Core/Decorators/` | Captures errors and invokes tracking |
| `ProviderKeyContext` | `ConduitLLM.Core/Services/` | AsyncLocal context for key identification |

### Models

| Model | Location | Purpose |
|-------|----------|---------|
| `ProviderErrorInfo` | `ConduitLLM.Core/Models/` | Error occurrence details |
| `ProviderErrorType` | `ConduitLLM.Core/Models/` | Error classification enum |
| `DisablePolicy` | `ConduitLLM.Core/Models/` | Key disabling rules |
| `AlertPolicy` | `ConduitLLM.Core/Models/` | Warning alert rules |

## Data Flow

### 1. Error Capture

```csharp
// ContextAwareLLMClient wraps all LLM operations
public class ContextAwareLLMClient : ILLMClient
{
    public async Task<ChatCompletionResponse> CreateChatCompletionAsync(...)
    {
        using (ProviderKeyContext.Set(keyId, providerId))
        {
            try
            {
                return await _innerClient.CreateChatCompletionAsync(...);
            }
            catch (LLMCommunicationException ex)
            {
                var errorType = ClassifyError(ex.StatusCode);
                await _errorTracking.TrackErrorAsync(new ProviderErrorInfo
                {
                    KeyCredentialId = keyId,
                    ProviderId = providerId,
                    ErrorType = errorType,
                    ErrorMessage = ex.Message,
                    HttpStatusCode = (int?)ex.StatusCode
                });
                throw;
            }
        }
    }
}
```

### 2. Error Classification

HTTP status codes map to error types:

```csharp
private static ProviderErrorType ClassifyError(HttpStatusCode? statusCode)
{
    return statusCode switch
    {
        HttpStatusCode.Unauthorized => ProviderErrorType.InvalidApiKey,
        HttpStatusCode.PaymentRequired => ProviderErrorType.InsufficientBalance,
        HttpStatusCode.Forbidden => ProviderErrorType.AccessForbidden,
        HttpStatusCode.TooManyRequests => ProviderErrorType.RateLimitExceeded,
        HttpStatusCode.NotFound => ProviderErrorType.ModelNotFound,
        HttpStatusCode.ServiceUnavailable => ProviderErrorType.ServiceUnavailable,
        HttpStatusCode.BadGateway => ProviderErrorType.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout => ProviderErrorType.Timeout,
        HttpStatusCode.InternalServerError => ProviderErrorType.ServiceUnavailable,
        _ => ProviderErrorType.Unknown
    };
}
```

### 3. Error Type Categories

```csharp
public enum ProviderErrorType
{
    // Fatal Errors (1-9) - Auto-disable keys
    InvalidApiKey = 1,
    InsufficientBalance = 2,
    AccessForbidden = 3,

    // Warning Errors (10-19) - Track but don't disable
    RateLimitExceeded = 10,
    ModelNotFound = 11,
    ServiceUnavailable = 12,

    // Transient Errors (20-29)
    NetworkError = 20,
    Timeout = 21,
    Unknown = 99
}

// Fatal check: errors 1-9 are fatal
public bool IsFatal => (int)ErrorType <= 9;
```

### 4. Threshold Evaluation

```csharp
// From FatalErrorPolicies static class
InvalidApiKey:
    DisableImmediately = true
    RequiresManualReenable = true

InsufficientBalance:
    DisableImmediately = false
    RequiredOccurrences = 2
    TimeWindow = 5 minutes

AccessForbidden:
    DisableImmediately = false
    RequiredOccurrences = 3
    TimeWindow = 10 minutes
```

### 5. Key Disabling

When thresholds are exceeded:

```csharp
public async Task DisableKeyAsync(int keyId, string reason)
{
    // 1. Disable key in database
    var key = await _keyRepository.GetByIdAsync(keyId);
    key.IsEnabled = false;
    await _keyRepository.UpdateAsync(key);

    // 2. Check if this disables entire provider
    var allKeys = await _keyRepository.GetByProviderIdAsync(key.ProviderId);
    if (allKeys.All(k => !k.IsEnabled))
    {
        await DisableProviderAsync(key.ProviderId, reason);
    }

    // 3. Update Redis tracking
    await _errorStore.MarkKeyDisabledAsync(keyId, DateTime.UtcNow, reason);

    // 4. Publish event for UI updates
    await _bus.Publish(new ProviderKeyDisabledEvent
    {
        KeyId = keyId,
        ProviderId = key.ProviderId,
        Reason = reason,
        DisabledAt = DateTime.UtcNow,
        IsAutomatic = true
    });
}
```

## Redis Schema

### Key Patterns

```
provider:errors:key:{keyId}:fatal
    Type: Hash
    Fields:
        count           - Integer error count
        error_type      - String (e.g., "InvalidApiKey")
        last_seen       - ISO 8601 datetime
        first_seen      - ISO 8601 datetime
        last_error_message - String (truncated to 500 chars)
        last_status_code   - Integer HTTP status
        disabled_at     - ISO 8601 datetime (if disabled)
    TTL: None (persists until cleared)

provider:errors:key:{keyId}:warnings
    Type: Sorted Set
    Score: Unix timestamp
    Values: JSON serialized warning data
    TTL: 30 days
    Max entries: 100 (trimmed on each add)

provider:errors:provider:{providerId}:summary
    Type: Hash
    Fields:
        total_errors    - Integer
        fatal_errors    - Integer
        warnings        - Integer
        last_error      - ISO 8601 datetime
        disabled_keys   - JSON array of key IDs
        provider_disabled_at - ISO 8601 datetime (if disabled)
        provider_disable_reason - String
    TTL: None

provider:errors:recent
    Type: Sorted Set
    Score: Unix timestamp
    Values: JSON {keyId, providerId, type, message, timestamp}
    Max entries: 1000
```

### Data Examples

Fatal error hash:
```
HGETALL provider:errors:key:42:fatal
1) "count"
2) "3"
3) "error_type"
4) "InvalidApiKey"
5) "last_seen"
6) "2025-01-15T10:30:00Z"
7) "last_error_message"
8) "Invalid API key provided"
9) "last_status_code"
10) "401"
```

Warning entry (JSON in sorted set):
```json
{
  "type": "RateLimitExceeded",
  "message": "Rate limit exceeded. Retry after 30s",
  "timestamp": "2025-01-15T10:30:00Z",
  "statusCode": 429
}
```

## MassTransit Events

### ProviderKeyDisabledEvent

Fired when a key is automatically or manually disabled:

```csharp
public class ProviderKeyDisabledEvent
{
    public int KeyId { get; set; }
    public int ProviderId { get; set; }
    public string Reason { get; set; }
    public string ErrorType { get; set; }
    public DateTime DisabledAt { get; set; }
    public bool IsAutomatic { get; set; } = true;
}
```

### ProviderKeyReenabledEvent

Fired when a key is manually re-enabled:

```csharp
public class ProviderKeyReenabledEvent
{
    public int KeyId { get; set; }
    public int ProviderId { get; set; }
    public string ReenabledBy { get; set; }
    public string Reason { get; set; }
    public DateTime ReenabledAt { get; set; }
}
```

### ProviderErrorAlertEvent

Fired when warning threshold is exceeded:

```csharp
public class ProviderErrorAlertEvent
{
    public int ProviderId { get; set; }
    public string ProviderName { get; set; }
    public string ErrorType { get; set; }
    public int ErrorCount { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public string AlertMessage { get; set; }
    public DateTime AlertedAt { get; set; }
    public string Severity { get; set; } // "Warning", "Error", "Critical"
}
```

## Admin API Endpoints

Base route: `/api/provider-errors`

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/recent` | GET | Get recent errors (params: providerId?, keyId?, limit) |
| `/summary` | GET | Get provider error summaries |
| `/keys/{keyId}` | GET | Get detailed key error info |
| `/keys/{keyId}/clear` | POST | Clear errors and optionally re-enable |
| `/keys/{keyId}/disable` | POST | Manually disable a key |
| `/stats` | GET | Get error statistics (params: hours) |
| `/providers/{providerId}/key-errors` | GET | Get error counts per key |

### Response DTOs

```csharp
public class ProviderErrorDto
{
    public int KeyCredentialId { get; set; }
    public string? KeyName { get; set; }
    public int ProviderId { get; set; }
    public string? ProviderName { get; set; }
    public string ErrorType { get; set; }
    public string ErrorMessage { get; set; }
    public int? HttpStatusCode { get; set; }
    public DateTime OccurredAt { get; set; }
    public bool IsFatal { get; set; }
    public string? ModelName { get; set; }
}

public class ErrorStatisticsDto
{
    public int TotalErrors { get; set; }
    public int FatalErrors { get; set; }
    public int Warnings { get; set; }
    public int DisabledKeys { get; set; }
    public Dictionary<string, int> ErrorsByType { get; set; }
    public Dictionary<string, int> ErrorsByProvider { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public DateTime GeneratedAt { get; set; }
}
```

## Retry Policy Integration

Errors are tracked during retry policies to avoid duplicate counting:

```csharp
// From ResiliencePolicies.ErrorTracking.cs
public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicyWithErrorTracking()
{
    return Policy<HttpResponseMessage>
        .HandleResult(r => IsTransientError(r.StatusCode))
        .Or<HttpRequestException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: DecorrelatedJitterBackoff,
            onRetryAsync: async (outcome, timespan, retryCount, context) =>
            {
                // Only track on final retry to avoid duplicates
                if (retryCount == 3)
                {
                    await TrackErrorFromRetry(outcome, context);
                }
            });
}
```

## Provider Key Context

AsyncLocal context propagates key information across async boundaries:

```csharp
public static class ProviderKeyContext
{
    private static readonly AsyncLocal<ProviderKeyInfo?> _current = new();

    public static ProviderKeyInfo? Current => _current.Value;

    public static IDisposable Set(int keyId, int providerId)
    {
        var previous = _current.Value;
        _current.Value = new ProviderKeyInfo(keyId, providerId);
        return new ContextScope(() => _current.Value = previous);
    }
}

// Usage in client factory
using (ProviderKeyContext.Set(key.Id, provider.Id))
{
    // All errors in this scope are attributed to this key
    await client.CreateChatCompletionAsync(...);
}
```

## Adding Support for New Providers

When adding a new provider client:

1. **Extend BaseLLMClient** - Inherit common error handling
2. **Override error classification if needed** - For provider-specific status codes
3. **Ensure factory sets context** - Factory must wrap with ContextAwareLLMClient
4. **Test error scenarios** - Verify classification works correctly

```csharp
// Example: Custom error classification
public class MyProviderClient : BaseLLMClient
{
    protected override ProviderErrorType ClassifyProviderError(
        HttpStatusCode statusCode,
        string responseBody)
    {
        // Provider-specific: they use 200 with error in body
        if (statusCode == HttpStatusCode.OK &&
            responseBody.Contains("insufficient_quota"))
        {
            return ProviderErrorType.InsufficientBalance;
        }

        return base.ClassifyProviderError(statusCode, responseBody);
    }
}
```

## DI Registration

### Gateway API

```csharp
// Program.CoreServices.cs
builder.Services.AddSingleton<IRedisErrorStore, RedisErrorStore>();
builder.Services.AddSingleton<IProviderErrorTrackingService, ProviderErrorTrackingService>();
```

### Admin API

```csharp
// ServiceCollectionExtensions.cs
services.AddSingleton<IRedisErrorStore, RedisErrorStore>();
services.AddScoped<IProviderErrorTrackingService>(sp =>
{
    // Scoped factory for request-level access
    return new ProviderErrorTrackingService(
        sp.GetRequiredService<IRedisErrorStore>(),
        sp.GetRequiredService<IProviderKeyCredentialRepository>(),
        sp.GetRequiredService<IProviderRepository>(),
        sp.GetRequiredService<IBus>(),
        sp.GetRequiredService<ILogger<ProviderErrorTrackingService>>()
    );
});
```

## Testing

### Unit Tests

Location: `ConduitLLM.Tests/Providers/ErrorClassificationTests.cs`

```csharp
[Fact]
public void Classify_Unauthorized_ReturnsInvalidApiKey()
{
    var result = ErrorClassifier.Classify(HttpStatusCode.Unauthorized);
    Assert.Equal(ProviderErrorType.InvalidApiKey, result);
}

[Theory]
[InlineData(ProviderErrorType.InvalidApiKey, true)]
[InlineData(ProviderErrorType.RateLimitExceeded, false)]
public void IsFatal_ReturnsCorrectValue(ProviderErrorType type, bool expected)
{
    var error = new ProviderErrorInfo { ErrorType = type };
    Assert.Equal(expected, error.IsFatal);
}
```

### Test Builders

```csharp
// ProviderErrorInfoBuilder for fluent test setup
var error = new ProviderErrorInfoBuilder()
    .WithKeyCredentialId(42)
    .WithProviderId(1)
    .WithErrorType(ProviderErrorType.InvalidApiKey)
    .WithMessage("Invalid API key")
    .Build();
```

## Design Decisions

### Why Redis?

- **Speed**: Sub-millisecond reads/writes don't impact request latency
- **Ephemeral**: Errors are operational data, not business data
- **TTL Support**: Automatic cleanup of old warnings
- **Atomic Operations**: HINCRBY for counting, ZADD for sorted sets

### Why Not Database?

- High write volume could impact performance
- Error data has short relevance window
- Redis provides better data structures (sorted sets, hashes)
- Easier horizontal scaling

### Non-Blocking Design

Error tracking is fire-and-forget:

```csharp
// Errors in tracking don't fail requests
try
{
    await _errorTracking.TrackErrorAsync(error);
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to track error");
    // Don't rethrow - tracking failure shouldn't break the request
}
```

## Related Documentation

- [Administrator Guide](../admin/provider-error-tracking.md)
- [Operational Runbook](../operations/error-tracking-runbook.md)
- [Provider Architecture](../architecture/provider-system/provider-architecture.md)
