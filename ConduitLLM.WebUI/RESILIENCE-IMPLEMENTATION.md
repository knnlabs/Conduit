# Resilience Implementation for ConduitLLM.WebUI

This document describes the comprehensive error handling and resilience patterns implemented in the WebUI project.

## Overview

The WebUI project now includes robust resilience patterns to handle failures when communicating with the Admin API and other external services. This implementation uses Polly for retry policies, circuit breakers, and timeouts.

## Components

### 1. Resilience Policies (`AdminApiResiliencePolicies.cs`)

Provides a centralized location for defining resilience policies:

- **Retry Policy**: Exponential backoff with 3 retries (2, 4, 8 seconds)
- **Circuit Breaker**: Opens after 5 failures, stays open for 30 seconds
- **Timeout Policy**: 30-second timeout per request
- **Combined Policy**: Wraps retry, circuit breaker, and timeout policies

### 2. HTTP Client Configuration

All HTTP clients are configured with resilience policies:

```csharp
services.AddHttpClient<AdminApiClient>()
    .AddAdminApiResiliencePolicies();
```

### 3. Enhanced Error Handling (`AdminApiClient.ErrorHandling.cs`)

The AdminApiClient now includes:

- `ExecuteWithErrorHandlingAsync<T>()` - Wraps HTTP calls with comprehensive error handling
- `HandleHttpResponseAsync<T>()` - Handles specific HTTP status codes appropriately
- Detailed error logging with context
- Graceful degradation on failures

### 4. Health Checks

- **AdminApiHealthCheck**: Monitors Admin API connectivity and circuit breaker state
- Endpoints:
  - `/health` - Full health check report
  - `/health/ready` - Critical components only

### 5. Resilience Monitoring

- **ResilienceLoggingMiddleware**: Logs slow requests and resilience events
- **CircuitBreakerStatus Component**: Visual indicator when circuit breaker is open

## Usage Examples

### Basic Usage (Automatic)

All AdminApiClient methods automatically benefit from resilience policies:

```csharp
// This call automatically retries on transient failures
var virtualKeys = await _adminApiClient.GetAllVirtualKeysAsync();
```

### Custom Error Handling

For specific error handling needs:

```csharp
protected async Task<T?> GetWithCustomHandling<T>(string endpoint) where T : class
{
    return await ExecuteWithErrorHandlingAsync(
        $"Get {endpoint}",
        async () => await GetAsync<T>(endpoint),
        defaultValue: null
    );
}
```

### Different Policies for Different Operations

The policies adapt based on HTTP method:
- GET requests: Full retry count (safe to retry)
- DELETE requests: Limited retries (potentially idempotent)
- POST/PUT requests: Single retry only (not idempotent)

## Configuration

### Retry Configuration

Configured via `ResiliencePolicyOptions`:
- `RetryCount`: Number of retry attempts (default: 3)
- `CircuitBreakerThreshold`: Failures before opening (default: 5)
- `TimeoutSeconds`: Request timeout (default: 30)

### Customizing Policies

```csharp
builder.AddResiliencePolicies(options =>
{
    options.RetryCount = 5;
    options.CircuitBreakerThreshold = 3;
    options.TimeoutSeconds = 60;
});
```

## Error Responses

### Circuit Breaker Open
- HTTP Status: 503 (Service Unavailable)
- Response: `{"error": "Service temporarily unavailable", "code": "CIRCUIT_BREAKER_OPEN"}`

### Request Timeout
- HTTP Status: 504 (Gateway Timeout)
- Response: `{"error": "Request timeout", "code": "REQUEST_TIMEOUT"}`

## Monitoring

### Health Check Response Example

```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "admin_api",
      "status": "Healthy",
      "description": "Admin API is responsive",
      "duration": 123.45,
      "data": {
        "responseTime": "< 1s",
        "lastCheck": "2024-01-01T00:00:00Z"
      }
    }
  ],
  "totalDuration": 125.67
}
```

### Logging

All resilience events are logged:
- Retry attempts with delay and reason
- Circuit breaker state changes
- Timeout occurrences
- Slow request warnings (> 5 seconds)

## Benefits

1. **Improved Reliability**: Automatic retry of transient failures
2. **Failure Isolation**: Circuit breaker prevents cascading failures
3. **Better User Experience**: Graceful degradation instead of errors
4. **Operational Visibility**: Health checks and detailed logging
5. **Performance Protection**: Timeouts prevent hanging requests

## Testing

To test resilience patterns:

1. **Simulate Admin API failure**: Stop the Admin API service
2. **Simulate slow responses**: Add delays to Admin API endpoints
3. **Monitor health endpoint**: Check `/health` during failures
4. **Check circuit breaker**: Verify UI shows circuit breaker status

## Future Enhancements

1. **Bulkhead Isolation**: Limit concurrent requests per endpoint
2. **Request Hedging**: Send parallel requests for critical operations
3. **Dynamic Configuration**: Adjust policies based on runtime metrics
4. **Distributed Tracing**: Add correlation IDs for request tracking
5. **Metrics Dashboard**: Real-time visualization of resilience metrics