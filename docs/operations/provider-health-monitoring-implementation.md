# Provider Health Monitoring Implementation

## Overview
This document describes the implementation of provider health monitoring in Conduit, which completes the event-driven architecture for real-time navigation state updates.

## Components

### Provider Health Monitoring Service
Located in `ConduitLLM.Admin/Services/ProviderHealthMonitoringService.cs`, this background service:
- Runs health checks every 5 minutes (configurable via `ProviderHealth:DefaultCheckIntervalMinutes`)
- Checks all enabled providers with monitoring enabled
- Performs provider-specific health checks (OpenAI, Anthropic, Google)
- Saves health records to the database
- Publishes `ProviderHealthChanged` events when status changes

### Health Check Methods
- **OpenAI**: Calls `/v1/models` endpoint to verify API connectivity
- **Anthropic**: Sends a minimal request to `/v1/messages` endpoint
- **Google**: Calls models endpoint with API key validation
- **Default**: Validates that API credentials exist

### Event Publishing
When a provider's health status changes (Online → Offline or vice versa), the service publishes:
```csharp
ProviderHealthChanged {
    ProviderId: int,
    ProviderName: string,
    IsHealthy: bool,
    Status: string // Includes status type, message, and response time
}
```

## Configuration

### Environment Variables
```bash
# Provider Health Monitoring Configuration
export CONDUITLLM__PROVIDERHEALTH__ENABLED=true                    # Enable/disable monitoring
export CONDUITLLM__PROVIDERHEALTH__DEFAULTCHECKINTERVALMINUTES=5   # Check interval
export CONDUITLLM__PROVIDERHEALTH__DEFAULTTIMEOUTSECONDS=10        # Health check timeout
export CONDUITLLM__PROVIDERHEALTH__DEFAULTRETRYATTEMPTS=2          # Retry attempts
```

### Default Configuration
```json
{
  "ProviderHealth": {
    "Enabled": true,
    "DefaultCheckIntervalMinutes": 5,
    "DefaultTimeoutSeconds": 10,
    "DefaultRetryAttempts": 2,
    "DetailedRecordRetentionDays": 30,
    "SummaryRecordRetentionMonths": 12
  }
}
```

## Database Schema
The service uses existing provider health tables:
- `ProviderHealthConfiguration`: Stores monitoring settings per provider
- `ProviderHealthRecord`: Stores health check results with timestamps

## Integration with Real-Time Updates

### Event Flow
1. ProviderHealthMonitoringService performs health check
2. If status changes, publishes ProviderHealthChanged event
3. Core API's ProviderHealthChangedNotificationConsumer receives event
4. SignalR hub pushes update to connected WebUI clients
5. Navigation state updates instantly without polling

### WebUI Impact
- Navigation items that depend on provider availability update in real-time
- Users see immediate feedback when providers go offline/online
- No more 30-second delays for navigation state changes

## Error Handling
- Each provider health check is isolated - one failure doesn't affect others
- Exceptions are caught and logged with appropriate error categories
- Failed health checks result in "Offline" status with error details

## Performance Considerations
- Health checks run on a timer, not impacting request handling
- Minimal HTTP requests (one per provider per interval)
- Event publishing only occurs on status changes
- Database operations are optimized with bulk queries

## Future Enhancements
1. Add more provider-specific health endpoints
2. Implement custom health check URLs per provider
3. Add webhook notifications for critical provider failures
4. Implement provider-specific retry policies
5. Add metrics collection for health check performance