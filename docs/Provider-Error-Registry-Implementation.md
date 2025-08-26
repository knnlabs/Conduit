# Provider Error Registry Implementation

## Overview
This document describes the implementation of a unified provider error registry that tracks provider API errors in Redis and automatically disables keys when fatal errors occur.

## Implementation Date
2025-08-25

## Components Implemented

### 1. Core Models and Enums
**File**: `ConduitLLM.Core/Models/ProviderErrorModels.cs`
- `ProviderErrorType` enum - Categorizes errors as Fatal, Warnings, or Transient
- `ProviderErrorInfo` class - Detailed error information
- `DisablePolicy` class - Rules for when to disable keys
- `AlertPolicy` class - Rules for when to send alerts
- `ErrorThresholdConfiguration` - Static configuration for error thresholds

### 2. Error Tracking Service
**File**: `ConduitLLM.Core/Services/ProviderErrorTrackingService.cs`
- Redis-based implementation for tracking provider errors
- Automatic key disabling based on error policies
- Provider-level error summaries
- Global error feed for monitoring

**File**: `ConduitLLM.Core/Interfaces/IProviderErrorTrackingService.cs`
- Service interface defining all error tracking operations

### 3. MassTransit Events
**File**: `ConduitLLM.Configuration/Events/ProviderKeyEvents.cs`
- `ProviderKeyDisabledEvent` - Published when a key is disabled
- `ProviderKeyReenabledEvent` - Published when a key is re-enabled
- `ProviderErrorAlertEvent` - Published when error thresholds are exceeded

### 4. Provider Error Classification
**File**: `ConduitLLM.Providers/BaseLLMClient.cs`
- Added `ClassifyHttpError` method for base error classification
- Added `RefineErrorClassification` virtual method for provider-specific refinement
- Added `ExtractErrorMessageAsync` for parsing error messages
- Added `SendRequestWithKeyTracking` for key attribution

**File**: `ConduitLLM.Providers/Providers/OpenAI/OpenAIClient.ErrorHandling.cs`
- OpenAI-specific error refinement
- Detects insufficient quota errors from 403 responses
- Parses OpenAI-specific error messages

### 5. HTTP Request Extensions
**File**: `ConduitLLM.Providers/Extensions/HttpRequestExtensions.cs`
- Extension methods for tracking key context in HTTP requests
- Enables error attribution to specific keys

## Redis Schema

### Per-Key Error Tracking
```redis
provider:errors:key:{keyId}:fatal -> Hash
{
  "error_type": "InvalidApiKey",
  "count": 5,
  "first_seen": "2024-01-20T10:30:00Z",
  "last_seen": "2024-01-20T10:35:00Z",
  "last_error_message": "Invalid API key",
  "last_status_code": 401,
  "disabled_at": "2024-01-20T10:35:00Z"
}

provider:errors:key:{keyId}:warnings -> Sorted Set
[{"type":"RateLimitExceeded","message":"...","timestamp":"..."}]
```

### Provider-Level Summaries
```redis
provider:errors:provider:{providerId}:summary -> Hash
{
  "total_errors": 150,
  "fatal_errors": 2,
  "warnings": 148,
  "disabled_keys": "[1,2,3]",
  "last_error": "2024-01-20T10:35:00Z"
}
```

### Global Error Feed
```redis
provider:errors:recent -> Sorted Set (last 1000 entries)
```

## Error Types and Policies

### Fatal Errors (Auto-Disable)
1. **InvalidApiKey** (401) - Immediate disable
2. **InsufficientBalance** (402) - Disable after 2 occurrences in 5 minutes
3. **AccessForbidden** (403) - Disable after 3 occurrences in 10 minutes

### Warnings (Track Only)
1. **RateLimitExceeded** (429) - Alert after 10 in 5 minutes
2. **ModelNotFound** (404)
3. **ServiceUnavailable** (503) - Alert after 5 in 10 minutes

### Transient Errors
1. **NetworkError** - Connection failures
2. **Timeout** - Request timeouts
3. **Unknown** - Unclassified errors

## Key Features

### Automatic Key Disabling
- Keys are automatically disabled when fatal error thresholds are met
- Disabled keys require manual re-enabling by admin
- Events are published for real-time UI updates

### Provider-Specific Error Detection
- Each provider can override `RefineErrorClassification` for custom error parsing
- OpenAI example: Detects insufficient quota from 403 responses
- Extensible for other providers (Anthropic, Google, etc.)

### Error Attribution
- HTTP requests track which key credential was used
- Errors are attributed to specific keys for accurate tracking
- Supports multiple keys per provider

### Redis-Based Storage
- Horizontally scalable across multiple Conduit instances
- 30-day TTL on warnings
- Permanent storage of fatal errors until cleared

## Integration Points

### With Retry Policies
The existing Polly retry policies can be enhanced to track errors:
```csharp
onRetry: async (outcome, timespan, retryAttempt, context) =>
{
    if (outcome.Result != null)
    {
        var (keyId, providerId) = outcome.Result.RequestMessage.GetKeyCredentialContext();
        if (keyId.HasValue)
        {
            // Track error without disabling until final retry
            await errorTracker.TrackErrorAsync(...);
        }
    }
}
```

### With Admin API (To Be Implemented)
Endpoints for error monitoring and key management:
- `GET /api/provider-errors/recent` - Get recent errors
- `GET /api/provider-errors/summary` - Get provider summaries
- `GET /api/provider-errors/keys/{keyId}` - Get key error details
- `POST /api/provider-errors/keys/{keyId}/clear` - Clear errors and re-enable

### With WebUI (To Be Implemented)
- Error dashboard showing provider health
- Disabled keys with error reasons
- Manual re-enable interface with confirmation

## Next Steps

### Phase 2: Provider Integration
1. Update remaining providers (Anthropic, Google, etc.) with error refinement
2. Integrate error tracking into HTTP request flow
3. Hook into retry policies for error capture

### Phase 3: Admin API
1. Create `ProviderErrorsController`
2. Add DTOs for API responses
3. Update Admin SDK with error endpoints

### Phase 4: WebUI Integration
1. Create error dashboard page
2. Enhance key management with error badges
3. Add real-time updates via SignalR

### Phase 5: Testing
1. Unit tests for error classification
2. Integration tests with Redis
3. Manual testing with actual provider errors

## Configuration Required

### Environment Variables
```bash
# Redis connection for error tracking
REDIS_CONNECTION_STRING=localhost:6379

# Error tracking settings
PROVIDER_ERROR_TRACKING__ENABLED=true
PROVIDER_ERROR_TRACKING__AUTO_DISABLE_KEYS=true
```

### Dependency Injection
```csharp
// In Program.cs or Startup.cs
services.AddSingleton<IProviderErrorTrackingService, ProviderErrorTrackingService>();
```

## Testing Recommendations

### Manual Testing Scenarios
1. Use invalid API key - Should disable immediately
2. Use key with no balance - Should disable after 2 attempts
3. Trigger rate limits - Should track as warnings
4. Re-enable disabled key - Should clear error history

### Monitoring
- Check Redis for error data: `redis-cli KEYS "provider:errors:*"`
- Monitor application logs for error tracking
- Verify events are published via MassTransit

## Security Considerations
- Error messages are stored in Redis - ensure no sensitive data in messages
- API keys are never logged or stored in error records
- Admin authentication required for re-enabling keys

## Performance Impact
- Minimal - Error tracking is async and fire-and-forget
- Redis operations are fast and non-blocking
- No impact on successful request flow