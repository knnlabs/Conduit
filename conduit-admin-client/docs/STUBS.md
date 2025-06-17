# Stub Functions Documentation

This document lists all stub functions in the Conduit Admin Client library that require implementation in the Admin API.

## Overview

Stub functions are placeholders for functionality that currently exists only in the WebUI or requires new Admin API endpoints. Each stub throws a `NotImplementedError` with a descriptive message.

## Virtual Keys

### `VirtualKeyService.getStatistics()`

**Purpose**: Retrieve aggregated statistics about virtual keys.

**Current Implementation**: The WebUI calculates statistics client-side by fetching all keys.

**Suggested API Endpoint**: `GET /api/virtualkeys/statistics`

**Response Structure**:
```typescript
{
  totalKeys: number;
  activeKeys: number;
  expiredKeys: number;
  totalSpend: number;
  averageSpendPerKey: number;
  keysNearBudgetLimit: number;
  keysByDuration: {
    Total: number;
    Daily: number;
    Weekly: number;
    Monthly: number;
  }
}
```

### `VirtualKeyService.bulkCreate()`

**Purpose**: Create multiple virtual keys in a single request.

**Current Implementation**: Not available in WebUI.

**Suggested API Endpoint**: `POST /api/virtualkeys/bulk`

**Request Structure**:
```typescript
{
  keys: CreateVirtualKeyRequest[]
}
```

**Response Structure**:
```typescript
{
  created: CreateVirtualKeyResponse[];
  failed: {
    index: number;
    error: string;
    request: CreateVirtualKeyRequest;
  }[]
}
```

### `VirtualKeyService.exportKeys()`

**Purpose**: Export virtual keys to CSV or JSON format.

**Current Implementation**: Not available in WebUI.

**Suggested API Endpoint**: `GET /api/virtualkeys/export?format={csv|json}`

**Response**: File download (CSV or JSON)

## Provider Management

### `ProviderService.getUsageStatistics()`

**Purpose**: Get detailed usage statistics for a provider.

**Current Implementation**: Partially available through health monitoring.

**Suggested API Endpoint**: `GET /api/providercredentials/{id}/statistics`

### `ProviderService.bulkTest()`

**Purpose**: Test multiple provider connections simultaneously.

**Current Implementation**: Not available in WebUI.

**Suggested API Endpoint**: `POST /api/providercredentials/test/bulk`

## Model Mappings

### `ModelMappingService.importMappings()`

**Purpose**: Import model mappings from CSV/JSON file.

**Current Implementation**: Not available in WebUI.

**Suggested API Endpoint**: `POST /api/modelprovidermapping/import`

### `ModelMappingService.suggestOptimalMapping()`

**Purpose**: AI-powered suggestion for optimal model-to-provider mapping.

**Current Implementation**: Not available.

**Suggested API Endpoint**: `POST /api/modelprovidermapping/suggest`

## Analytics

### `AnalyticsService.getDetailedCostBreakdown()`

**Purpose**: Get detailed cost breakdown with time-series data.

**Current Implementation**: Basic cost dashboard available, but not detailed breakdown.

**Suggested API Endpoint**: `GET /api/costdashboard/detailed`

### `AnalyticsService.predictFutureCosts()`

**Purpose**: Predict future costs based on usage patterns.

**Current Implementation**: Not available.

**Suggested API Endpoint**: `POST /api/costdashboard/predict`

### `AnalyticsService.exportAnalytics()`

**Purpose**: Export analytics data to various formats.

**Current Implementation**: Not available.

**Suggested API Endpoint**: `GET /api/costdashboard/export`

## Real-time Features

### `AnalyticsService.streamRequestLogs()`

**Purpose**: Stream real-time request logs via Server-Sent Events.

**Current Implementation**: Not available.

**Suggested API Endpoint**: `GET /api/logs/stream` (SSE endpoint)

### `HealthService.streamHealthStatus()`

**Purpose**: Stream real-time health status updates.

**Current Implementation**: Not available.

**Suggested API Endpoint**: `GET /api/providerhealth/stream` (SSE endpoint)

## System Management

### `SystemService.scheduledBackup()`

**Purpose**: Configure scheduled automatic backups.

**Current Implementation**: Only manual backups available.

**Suggested API Endpoint**: `POST /api/databasebackup/schedule`

### `SystemService.getAuditLog()`

**Purpose**: Retrieve detailed audit log of all admin actions.

**Current Implementation**: Not available.

**Suggested API Endpoint**: `GET /api/audit`

## IP Filtering

### `IpFilterService.bulkImport()`

**Purpose**: Import IP filter rules from CSV.

**Current Implementation**: Not available.

**Suggested API Endpoint**: `POST /api/ipfilter/import`

### `IpFilterService.testRules()`

**Purpose**: Test IP filter rules without applying them.

**Current Implementation**: Not available.

**Suggested API Endpoint**: `POST /api/ipfilter/test`

## Complete List of Stub Functions

### Settings Service
- `getSystemConfiguration()` - Comprehensive system configuration export
- `exportSettings()` - Export all settings to JSON/ENV format
- `importSettings()` - Import settings from file
- `validateConfiguration()` - Validate all configuration settings

### Analytics Service
- `getDetailedCostBreakdown()` - Detailed cost analysis with filters
- `predictFutureCosts()` - ML-based cost prediction
- `exportAnalytics()` - Export analytics data to various formats
- `detectAnomalies()` - Detect usage anomalies
- `streamRequestLogs()` - Real-time request log streaming (SSE)
- `generateReport()` - Generate PDF/HTML reports

### System Service
- `getAuditLogs()` - Retrieve audit logs with filtering
- `scheduledBackup()` - Configure automatic backups
- `getScheduledBackups()` - List scheduled backup configurations
- `exportAuditLogs()` - Export audit logs to CSV/JSON
- `getDiagnostics()` - System diagnostics and recommendations
- `streamSystemMetrics()` - Real-time system metrics (SSE)

### Model Cost Service
- `bulkUpdate()` - Update costs for multiple models
- `getHistory()` - Cost history for a model
- `estimateCosts()` - Estimate costs for scenarios
- `compareCosts()` - Compare costs between models
- `importCosts()` - Import cost data from file
- `exportCosts()` - Export cost data

## Implementation Priority

Based on typical usage patterns, here's the suggested implementation priority:

1. **High Priority**
   - Virtual Key Statistics
   - Detailed Cost Breakdown
   - Export functionality (keys, analytics, settings)
   - Audit logging

2. **Medium Priority**
   - Bulk operations (create, test, import)
   - Real-time streaming (logs, metrics)
   - System diagnostics
   - Cost history and comparison

3. **Low Priority**
   - Predictive analytics
   - AI-powered suggestions
   - Advanced scheduling
   - Anomaly detection

## Notes for Implementation

When implementing these endpoints in the Admin API:

1. **Consistency**: Follow existing API patterns for request/response structures
2. **Performance**: Consider pagination for large data sets
3. **Security**: Ensure proper authentication and authorization
4. **Caching**: Implement appropriate cache headers for read operations
5. **Documentation**: Update OpenAPI/Swagger documentation

## Contributing

To implement any of these stub functions:

1. Add the endpoint to the Admin API
2. Update the corresponding service in this library
3. Remove the stub function and NotImplementedError
4. Add tests for the new functionality
5. Update this documentation