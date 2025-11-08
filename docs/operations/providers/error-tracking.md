# Provider Error Tracking and Monitoring Guide

This guide documents how Conduit tracks and monitors provider-specific errors, including rate limits, insufficient balance/credits, and other critical failures.

## Table of Contents
- [Overview](#overview)
- [Current State](#current-state)
- [Error Categories](#error-categories)
- [Implementation Gaps](#implementation-gaps)
- [Recommendations](#recommendations)
- [Implementation Guide](#implementation-guide)
- [Monitoring and Alerting](#monitoring-and-alerting)

## Overview

Provider error tracking is essential for maintaining service reliability and understanding when LLM providers experience issues. This includes both availability problems (provider is down) and business-critical errors (insufficient credits, rate limits).

## Current State

### ✅ What's Working

1. **Error Detection and Enhancement**
   - Each provider client (Anthropic, OpenAI, etc.) has `ExtractEnhancedErrorMessage` methods
   - Errors are properly categorized and user-friendly messages are generated
   - Common error patterns are detected:
     ```csharp
     // From AnthropicClient.cs
     protected string ExtractEnhancedErrorMessage(Exception ex)
     {
         var msg = ex.Message;
         
         // Check for insufficient balance/credit errors
         if (msg.Contains("credit balance", StringComparison.OrdinalIgnoreCase) ||
             msg.Contains("insufficient", StringComparison.OrdinalIgnoreCase) ||
             msg.Contains("quota", StringComparison.OrdinalIgnoreCase))
         {
             return "Anthropic API error: " + extractedMessage;
         }
         
         // Check for rate limit errors
         if (msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
             msg.Contains("429", StringComparison.OrdinalIgnoreCase))
         {
             return Constants.ErrorMessages.RateLimitExceeded;
         }
     }
     ```

2. **Health Monitoring Infrastructure**
   - `ProviderHealthMonitoringService` performs periodic health checks
   - Hysteresis prevents notification flapping (3 consecutive failures required)
   - Health status tracked: Online/Offline/Unknown
   - Error categories: Network, Timeout, Authentication, Unknown

3. **Metrics Collection**
   - Business metrics service with Prometheus integration:
     - `conduit_provider_health` - Provider health status (1=healthy, 0=unhealthy)
     - `conduit_provider_errors_total` - Total provider errors by type
     - `conduit_provider_latency_seconds` - Provider API latency
     - `conduit_model_requests_total` - Requests per model/provider with status

### ❌ What's Missing

1. **No Persistent Error Storage**
   - `RequestLogs` table has `StatusCode` but no `ErrorMessage` field
   - Errors only logged to application logs, not database
   - No historical error analysis capability

2. **No Request-Level Error Tracking**
   - Health monitoring only performs synthetic checks
   - Actual API errors during requests aren't captured in metrics
   - Master key requests bypass logging entirely

3. **No Error-Specific Alerting**
   - Alert infrastructure exists but isn't configured for provider errors
   - No notifications for insufficient balance or high error rates

## Error Categories

### Business-Critical Errors
These errors require immediate attention as they prevent service operation:

1. **Insufficient Balance/Credits**
   - Provider: Anthropic, OpenAI, Google
   - Impact: Complete service outage for that provider
   - Example: "Your credit balance is too low to access the Anthropic API"

2. **Quota Exceeded**
   - Provider: All
   - Impact: Service degradation or outage
   - Example: "Monthly quota exceeded"

### Operational Errors
These errors indicate temporary issues that may self-resolve:

1. **Rate Limits**
   - HTTP 429 responses
   - Retry-after headers should be respected
   - Example: "Rate limit exceeded. Please retry after X seconds"

2. **Authentication Failures**
   - Invalid or expired API keys
   - Wrong API version
   - Example: "Invalid API key provided"

3. **Model Not Found**
   - Requested model doesn't exist or isn't available
   - May indicate configuration issues
   - Example: "Model 'gpt-5' not found"

### Infrastructure Errors

1. **Network Errors**
   - Connection timeouts
   - DNS resolution failures
   - SSL/TLS errors

2. **Provider Downtime**
   - 5xx errors from provider
   - Service unavailable responses

## Implementation Gaps

### 1. Database Schema
The `RequestLog` entity needs an error tracking field:

```sql
ALTER TABLE "RequestLogs" 
ADD COLUMN "ErrorMessage" VARCHAR(1000),
ADD COLUMN "ErrorCategory" VARCHAR(50),
ADD COLUMN "ProviderErrorCode" VARCHAR(100);
```

### 2. Request Tracking Middleware
The `LlmRequestTrackingMiddleware` needs to capture errors:

```csharp
// Current implementation only logs successful requests
if (context.Response.StatusCode >= 400)
{
    var errorResponse = await ReadResponseBody(context);
    log.ErrorMessage = ExtractErrorMessage(errorResponse);
    log.ErrorCategory = DetermineErrorCategory(errorResponse);
}
```

### 3. Metrics Integration
Provider errors should increment Prometheus counters:

```csharp
// In error handling code
ProviderErrors.WithLabels(provider, errorCategory).Inc();

// Error categories: 
// - insufficient_balance
// - rate_limit
// - authentication
// - model_not_found
// - network
// - timeout
// - unknown
```

## Recommendations

### 1. Enable Provider Health Monitoring

Create health monitoring configurations for each provider:

```bash
# For each provider
curl -X POST http://localhost:5002/api/providerhealth/configurations \
  -H "X-Master-Key: alpha" \
  -H "Content-Type: application/json" \
  -d '{
    "providerName": "anthropic",
    "monitoringEnabled": true,
    "checkIntervalMinutes": 5,
    "timeoutSeconds": 30,
    "alertingEnabled": true,
    "alertThresholds": {
      "errorRatePercent": 10,
      "consecutiveFailures": 3
    }
  }'
```

### 2. Implement Request-Level Error Tracking

Modify the request tracking to capture all requests, including those using master keys:

```csharp
public class EnhancedRequestTrackingMiddleware
{
    private async Task TrackRequest(HttpContext context)
    {
        // Track ALL requests, not just virtual key requests
        var isVirtualKey = context.Request.Headers.ContainsKey("X-Virtual-Key");
        var isMasterKey = context.Request.Headers.ContainsKey("Authorization");
        
        if (isVirtualKey || isMasterKey)
        {
            // Capture request details
            // Parse response for errors
            // Log to database with error details
        }
    }
}
```

### 3. Create Dedicated Error Tracking Table

```sql
CREATE TABLE "ProviderErrors" (
    "Id" BIGSERIAL PRIMARY KEY,
    "Timestamp" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "Provider" VARCHAR(50) NOT NULL,
    "Model" VARCHAR(100),
    "ErrorCategory" VARCHAR(50) NOT NULL,
    "ErrorCode" VARCHAR(100),
    "ErrorMessage" TEXT,
    "HttpStatusCode" INT,
    "RequestId" VARCHAR(100),
    "VirtualKeyId" INT,
    "Cost" DECIMAL(10,6) DEFAULT 0,
    "RetryCount" INT DEFAULT 0,
    "Resolved" BOOLEAN DEFAULT FALSE,
    "ResolvedAt" TIMESTAMPTZ,
    "ResolutionNotes" TEXT
);

CREATE INDEX idx_provider_errors_timestamp ON "ProviderErrors"("Timestamp");
CREATE INDEX idx_provider_errors_category ON "ProviderErrors"("Provider", "ErrorCategory");
CREATE INDEX idx_provider_errors_unresolved ON "ProviderErrors"("Resolved") WHERE "Resolved" = FALSE;
```

### 4. Implement Error-Specific Alerts

```csharp
public class ProviderErrorAlertService
{
    private readonly IAlertNotificationService _alertService;
    
    public async Task CheckAndAlertAsync()
    {
        // Check for insufficient balance errors in last hour
        var criticalErrors = await _errorRepository.GetErrorsAsync(
            category: "insufficient_balance",
            since: DateTime.UtcNow.AddHours(-1)
        );
        
        if (criticalErrors.Any())
        {
            await _alertService.SendAlertAsync(new ProviderAlert
            {
                Severity = AlertSeverity.Critical,
                Title = "Provider Insufficient Balance",
                Message = $"{criticalErrors.Count} insufficient balance errors detected",
                Provider = criticalErrors.First().Provider,
                ActionRequired = "Add credits to provider account immediately"
            });
        }
    }
}
```

## Implementation Guide

### Phase 1: Database Updates
1. Add error fields to `RequestLogs` table
2. Create `ProviderErrors` table
3. Update Entity Framework models

### Phase 2: Middleware Enhancement
1. Modify `LlmRequestTrackingMiddleware` to capture errors
2. Parse error responses and categorize them
3. Store error details in database

### Phase 3: Metrics Integration
1. Update error handling to increment Prometheus counters
2. Add error category labels
3. Create Grafana dashboards for error visualization

### Phase 4: Alerting Configuration
1. Create alert rules for critical errors
2. Configure notification channels (Slack, email, webhooks)
3. Set up escalation policies

### Phase 5: Dashboard Integration
1. Add error metrics to admin dashboard
2. Create provider health status page
3. Show real-time error rates and categories

## Monitoring and Alerting

### Key Metrics to Monitor

1. **Error Rate by Provider**
   ```promql
   rate(conduit_provider_errors_total[5m])
   ```

2. **Insufficient Balance Alerts**
   ```promql
   conduit_provider_errors_total{error_type="insufficient_balance"}
   ```

3. **Rate Limit Pressure**
   ```promql
   rate(conduit_provider_errors_total{error_type="rate_limit"}[5m]) > 0.1
   ```

### Alert Examples

```yaml
groups:
  - name: provider_errors
    rules:
      - alert: InsufficientBalance
        expr: increase(conduit_provider_errors_total{error_type="insufficient_balance"}[5m]) > 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Provider {{ $labels.provider }} has insufficient balance"
          description: "Add credits immediately to restore service"
      
      - alert: HighErrorRate
        expr: rate(conduit_provider_errors_total[5m]) > 0.1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High error rate for provider {{ $labels.provider }}"
          description: "Error rate: {{ $value }} errors/sec"
```

### Dashboard Panels

1. **Provider Health Overview**
   - Current status (Online/Offline/Degraded)
   - Error rate trends
   - Response time percentiles

2. **Error Distribution**
   - Pie chart of error categories
   - Time series of errors by type
   - Provider comparison

3. **Critical Alerts**
   - Insufficient balance warnings
   - Authentication failures
   - Rate limit violations

## Best Practices

1. **Error Recovery**
   - Implement exponential backoff for rate limits
   - Automatic failover to alternate providers
   - Queue requests during temporary outages

2. **Cost Management**
   - Monitor credit usage trends
   - Set up budget alerts before credits run out
   - Implement spending limits per virtual key

3. **Operational Excellence**
   - Regular review of error patterns
   - Proactive credit top-ups
   - Provider diversity to avoid single points of failure

## Conclusion

While Conduit has robust infrastructure for provider health monitoring, it lacks request-level error tracking and alerting for business-critical errors like insufficient balance. Implementing the recommendations in this guide will provide comprehensive visibility into provider health and enable proactive issue resolution.