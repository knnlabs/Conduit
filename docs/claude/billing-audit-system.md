# Billing Audit System

## Overview

The Billing Audit System provides comprehensive tracking and auditing of all billing-related events in ConduitLLM. It captures detailed information about successful billing, failures, and edge cases to ensure complete visibility into the billing process and help identify potential revenue loss.

## Architecture

### Components

1. **BillingAuditEvent Entity** (`ConduitLLM.Configuration/Entities/BillingAuditEvent.cs`)
   - Stores audit events with comprehensive metadata
   - Uses PostgreSQL JSONB for flexible data storage
   - Indexed for efficient querying

2. **BillingAuditService** (`ConduitLLM.Configuration/Services/BillingAuditService.cs`)
   - Implements batch processing for high-volume writes
   - Runs as a hosted service with periodic flushing
   - Provides query methods for analysis

3. **UsageTrackingMiddleware Integration** (`ConduitLLM.Http/Middleware/UsageTrackingMiddleware.cs`)
   - Logs all billing decisions automatically
   - Fire-and-forget pattern to avoid blocking requests
   - Captures context for every billing event

4. **Admin API** (`ConduitLLM.Admin/Controllers/BillingAuditController.cs`)
   - REST endpoints for querying audit data
   - Anomaly detection capabilities
   - Export functionality (JSON, CSV)

## Event Types

The system tracks the following billing event types:

| Event Type | Description | Impact |
|------------|-------------|---------|
| `UsageTracked` | Successful usage tracking and billing | Normal operation |
| `UsageEstimated` | Usage was estimated due to missing data | Potential inaccuracy |
| `ZeroCostSkipped` | Zero cost calculated, no billing occurred | Expected for free models |
| `MissingCostConfig` | Model has no cost configuration | Configuration issue |
| `MissingUsageData` | No usage data in response | Provider issue |
| `SpendUpdateFailed` | Failed to update spend (Redis/DB) | Infrastructure issue |
| `ErrorResponseSkipped` | Error response not billed (4xx/5xx) | Policy decision |
| `StreamingUsageMissing` | Streaming response missing usage data | Provider limitation |
| `NoVirtualKey` | No virtual key found for request | Authentication issue |
| `JsonParseError` | JSON parsing error prevented tracking | Data format issue |
| `UnexpectedError` | Unexpected error during tracking | Unknown issue |

## Database Schema

### Table: BillingAuditEvents

```sql
CREATE TABLE "BillingAuditEvents" (
    "Id" uuid NOT NULL DEFAULT gen_random_uuid(),
    "Timestamp" timestamp with time zone NOT NULL,
    "EventType" integer NOT NULL,
    "VirtualKeyId" integer NULL,
    "Model" character varying(100) NULL,
    "RequestId" character varying(100) NULL,
    "CalculatedCost" numeric NULL,
    "FailureReason" text NULL,
    "UsageJson" jsonb NULL,
    "MetadataJson" jsonb NULL,
    "ProviderType" character varying(50) NULL,
    "HttpStatusCode" integer NULL,
    "RequestPath" character varying(500) NULL,
    "IsEstimated" boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_BillingAuditEvents" PRIMARY KEY ("Id")
);
```

### Indexes

- `IX_BillingAuditEvents_Timestamp` - For time-based queries
- `IX_BillingAuditEvents_EventType` - For filtering by event type
- `IX_BillingAuditEvents_VirtualKeyId` - For per-key analysis
- `IX_BillingAuditEvents_Model` - For model-specific queries
- `IX_BillingAuditEvents_RequestId` - For request correlation
- Composite: `IX_BillingAuditEvents_Timestamp_EventType` - For time-filtered event queries
- JSONB GIN: `IX_BillingAuditEvents_UsageJson` - For usage data queries
- JSONB GIN: `IX_BillingAuditEvents_MetadataJson` - For metadata queries

## Configuration

### Service Registration

The billing audit service is registered as both a singleton service and a hosted service:

```csharp
// In ConduitLLM.Admin/Extensions/ServiceCollectionExtensions.cs
services.AddSingleton<IBillingAuditService, BillingAuditService>();
services.AddHostedService<BillingAuditService>(provider => 
    provider.GetRequiredService<IBillingAuditService>() as BillingAuditService);
```

### Batch Processing Settings

```csharp
private const int BatchSize = 100;           // Events per batch
private const int FlushIntervalSeconds = 10; // Seconds between flushes
```

## Usage

### Logging Events

The system automatically logs billing events through the middleware. Manual logging is also possible:

```csharp
// Synchronous (fire-and-forget)
billingAuditService.LogBillingEvent(new BillingAuditEvent
{
    EventType = BillingAuditEventType.UsageTracked,
    VirtualKeyId = virtualKeyId,
    Model = model,
    CalculatedCost = cost,
    UsageJson = JsonSerializer.Serialize(usage)
});

// Asynchronous
await billingAuditService.LogBillingEventAsync(auditEvent);
```

### Querying Events

```csharp
// Get paginated events
var (events, totalCount) = await billingAuditService.GetAuditEventsAsync(
    from: DateTime.UtcNow.AddDays(-7),
    to: DateTime.UtcNow,
    eventType: BillingAuditEventType.SpendUpdateFailed,
    virtualKeyId: 123,
    pageNumber: 1,
    pageSize: 100
);

// Get summary statistics
var summary = await billingAuditService.GetAuditSummaryAsync(
    from: DateTime.UtcNow.AddDays(-30),
    to: DateTime.UtcNow,
    virtualKeyId: 123
);

// Detect anomalies
var anomalies = await billingAuditService.DetectAnomaliesAsync(
    from: DateTime.UtcNow.AddDays(-7),
    to: DateTime.UtcNow
);

// Calculate potential revenue loss
var loss = await billingAuditService.GetPotentialRevenueLossAsync(
    from: DateTime.UtcNow.AddMonths(-1),
    to: DateTime.UtcNow
);
```

## Admin API Endpoints

### Query Events
```
POST /api/audit/billing/query
{
    "from": "2024-01-01T00:00:00Z",
    "to": "2024-01-31T23:59:59Z",
    "eventType": 5,
    "virtualKeyId": 123,
    "pageNumber": 1,
    "pageSize": 100
}
```

### Get Summary
```
GET /api/audit/billing/summary?from=2024-01-01&to=2024-01-31&virtualKeyId=123
```

### Detect Anomalies
```
GET /api/audit/billing/anomalies?from=2024-01-01&to=2024-01-31
```

### Get Revenue Loss
```
GET /api/audit/billing/revenue-loss?from=2024-01-01&to=2024-01-31
```

### Export Data
```
POST /api/audit/billing/export
{
    "from": "2024-01-01T00:00:00Z",
    "to": "2024-01-31T23:59:59Z",
    "format": "csv",  // or "json"
    "eventType": null,
    "virtualKeyId": null
}
```

### Get Event Types
```
GET /api/audit/billing/event-types
```

## Anomaly Detection

The system automatically detects the following anomalies:

1. **High Failure Rate**: When billing failures exceed 5% of total events
2. **Zero Cost Spike**: When zero-cost calculations spike to 3x the average
3. **Missing Model Configuration**: When a model has no cost configuration for >10 requests

## Best Practices

### 1. Regular Monitoring
- Set up alerts for high failure rates
- Review anomaly reports weekly
- Monitor potential revenue loss metrics

### 2. Data Retention
- Consider implementing automatic cleanup for old events
- Archive data older than 90 days to cold storage
- Keep summary statistics for long-term analysis

### 3. Performance Optimization
- The batch processing minimizes database writes
- JSONB indexes enable efficient querying
- Consider partitioning the table by timestamp for very high volumes

### 4. Integration Points
- Can publish MassTransit events for critical failures
- Integrates with existing logging infrastructure
- Compatible with external monitoring tools via Admin API

## Troubleshooting

### Common Issues

1. **Events not appearing immediately**
   - Events are batched and flushed every 10 seconds
   - Check the application logs for flush errors

2. **High memory usage**
   - Reduce BatchSize if processing very large events
   - Ensure the flush timer is running correctly

3. **Query performance**
   - Ensure indexes are created and not fragmented
   - Use time ranges to limit query scope
   - Consider adding additional indexes for common queries

## Future Enhancements

Potential improvements for the billing audit system:

1. **Real-time alerting** - Immediate notifications for critical events
2. **Machine learning** - Advanced anomaly detection using ML models
3. **Data retention policies** - Automatic archival and cleanup
4. **Dashboard integration** - Real-time visualization of billing metrics
5. **Event replay** - Ability to replay events for testing or recovery
6. **Custom event types** - Extensible event type system for custom tracking

## Security Considerations

- All audit events are immutable once written
- Access to audit data requires Admin API authentication
- Sensitive data should not be stored in metadata fields
- Consider encrypting the UsageJson and MetadataJson fields for sensitive deployments

## Related Documentation

- [UsageTrackingMiddleware](./usage-tracking-middleware.md)
- [Virtual Key Management](./virtual-key-management.md)
- [Cost Calculation Service](./cost-calculation-service.md)
- [Admin API Security](./admin-api-security.md)