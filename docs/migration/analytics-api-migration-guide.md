# Analytics API Migration Guide

## Overview

This guide helps developers migrate from the deprecated logging and cost endpoints to the new unified Analytics API. The new API consolidates multiple services into a single, efficient interface with improved caching and performance monitoring.

## Migration Timeline

- **Deprecation Date**: May 2024
- **Removal Date**: TBD (6 months after deprecation)
- **Recommended Migration**: Immediate

## Deprecated Endpoints

The following endpoints have been deprecated and replaced:

### LogsController (Deprecated)
- `GET /api/logs` → `GET /api/analytics/logs`
- `GET /api/logs/{id}` → `GET /api/analytics/logs/{id}`
- `GET /api/logs/models` → `GET /api/analytics/models`

### AdminLogService (Deprecated)
- Entire service consolidated into AnalyticsService

### AdminCostDashboardService (Deprecated)
- Entire service consolidated into AnalyticsService

## New Unified Analytics API

### Key Improvements

1. **Unified Interface**: Single controller for all analytics operations
2. **Improved Performance**: 
   - Paginated queries for large datasets
   - Multi-tier caching (1/5/15 minute durations)
   - Metrics tracking for cache hit rates
3. **Enhanced Monitoring**:
   - Cache performance metrics
   - Operation duration tracking (P95, average)
   - Memory usage monitoring
4. **Better Documentation**: Comprehensive Swagger/OpenAPI documentation

### Endpoint Mapping

| Old Endpoint | New Endpoint | Changes |
|-------------|--------------|---------|
| `GET /api/logs` | `GET /api/analytics/logs` | Added pagination support, improved filtering |
| `GET /api/logs/{id}` | `GET /api/analytics/logs/{id}` | No changes |
| `GET /api/logs/models` | `GET /api/analytics/models` | Cached for better performance |
| `GET /api/cost-analytics/summary` | `GET /api/analytics/costs/summary` | Unified response format |
| `GET /api/cost-analytics/trends` | `GET /api/analytics/costs/trends` | Enhanced trend analysis |
| N/A | `GET /api/analytics/summary` | New comprehensive summary endpoint |
| N/A | `GET /api/analytics/export` | New export functionality |
| N/A | `GET /api/analytics/metrics/cache` | New cache monitoring |
| N/A | `GET /api/analytics/metrics/operations` | New performance metrics |

## Migration Steps

### 1. Update API Client Endpoints

**Old:**
```typescript
const logs = await fetch('/api/logs?page=1&pageSize=50');
const costs = await fetch('/api/cost-analytics/summary');
```

**New:**
```typescript
const logs = await fetch('/api/analytics/logs?page=1&pageSize=50');
const costs = await fetch('/api/analytics/costs/summary');
```

### 2. Update Response Handling

The new API uses consistent response formats:

**Paginated Responses:**
```typescript
interface PagedResult<T> {
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  items: T[];
}
```

**Cost Dashboard Response:**
```typescript
interface CostDashboardDto {
  timeFrame: string;
  startDate: string;
  endDate: string;
  totalCost: number;
  last24HoursCost: number;
  last7DaysCost: number;
  last30DaysCost: number;
  topModelsBySpend: DetailedCostDataDto[];
  topProvidersBySpend: DetailedCostDataDto[];
  topVirtualKeysBySpend: DetailedCostDataDto[];
}
```

### 3. Leverage New Features

#### Pagination
The new API supports efficient pagination for large datasets:
```typescript
// Fetch logs with pagination
const response = await fetch('/api/analytics/logs?page=2&pageSize=100');
```

#### Export Functionality
Export analytics data in CSV or JSON format:
```typescript
// Export last 30 days as CSV
const csvData = await fetch('/api/analytics/export?format=csv');

// Export filtered data as JSON
const jsonData = await fetch('/api/analytics/export?format=json&model=gpt-4');
```

#### Performance Monitoring
Monitor API performance and cache effectiveness:
```typescript
// Get cache metrics
const cacheMetrics = await fetch('/api/analytics/metrics/cache');
// Returns: { TotalHits, TotalMisses, HitRate, CacheMemoryMB, ... }

// Get operation metrics
const operationMetrics = await fetch('/api/analytics/metrics/operations');
// Returns: { GetLogsAsync_avg_ms, GetLogsAsync_p95_ms, ... }
```

### 4. Update WebUI Components

If using the Admin SDK:
```typescript
// Old approach (direct API calls)
const response = await fetch('/api/cost-analytics/summary');

// New approach (using Admin SDK)
import { createAdminClient } from '@conduit/admin-sdk';

const client = createAdminClient({ apiKey: 'your-key' });
const summary = await client.analytics.getCostSummary('daily');
```

## Performance Considerations

### Caching Strategy

The new API implements a multi-tier caching strategy:
- **Short Duration (1 minute)**: Cost summaries, real-time data
- **Medium Duration (5 minutes)**: Model lists, trend data
- **Long Duration (15 minutes)**: Historical analytics, aggregations

### Pagination Best Practices

1. **Default Page Size**: Use 50 items per page for optimal performance
2. **Maximum Page Size**: Limited to 100 items to prevent resource exhaustion
3. **Filtering**: Apply filters to reduce dataset size before pagination

### Monitoring Recommendations

1. **Track Cache Hit Rate**: Monitor `/api/analytics/metrics/cache` regularly
2. **Watch Operation Durations**: Check P95 latencies for performance degradation
3. **Set Alerts**: Configure alerts for:
   - Cache hit rate < 70%
   - P95 latency > 1000ms
   - Memory usage > 100MB

## Troubleshooting

### Common Issues

1. **Missing Data After Migration**
   - Ensure date ranges are properly formatted (ISO 8601)
   - Check that filters are using correct parameter names

2. **Performance Degradation**
   - Monitor cache metrics to ensure cache is working
   - Check if pagination is being used for large datasets
   - Verify database indexes are properly configured

3. **Authentication Errors**
   - Ensure X-API-Key header is included
   - Verify master key is correctly configured

### Debug Endpoints

- `GET /api/analytics/metrics/cache` - Check cache performance
- `GET /api/analytics/metrics/operations` - Monitor operation latencies
- `POST /api/analytics/cache/invalidate` - Force cache refresh if needed

## Support

For questions or issues during migration:
1. Check the [API Documentation](/docs/api-reference/admin-api-endpoints.md)
2. Review the [Analytics Service Implementation](/ConduitLLM.Admin/Services/AnalyticsService.cs)
3. Contact the development team through internal channels

## Appendix: Code Examples

### TypeScript/JavaScript
```typescript
// Complete migration example
class AnalyticsClient {
  private baseUrl = '/api/analytics';
  
  async getLogs(params: LogQueryParams) {
    const query = new URLSearchParams(params);
    const response = await fetch(`${this.baseUrl}/logs?${query}`);
    return response.json();
  }
  
  async getCostSummary(timeframe = 'daily') {
    const response = await fetch(
      `${this.baseUrl}/costs/summary?timeframe=${timeframe}`
    );
    return response.json();
  }
  
  async exportData(format = 'csv', filters = {}) {
    const query = new URLSearchParams({ format, ...filters });
    const response = await fetch(`${this.baseUrl}/export?${query}`);
    return response.blob();
  }
}
```

### C#/.NET
```csharp
// Using the Admin SDK
public class AnalyticsMigration
{
    private readonly IAnalyticsService _analyticsService;
    
    public async Task<PagedResult<LogRequestDto>> GetLogsAsync(
        int page = 1, 
        int pageSize = 50)
    {
        return await _analyticsService.GetLogsAsync(
            page, 
            pageSize, 
            DateTime.UtcNow.AddDays(-7), 
            DateTime.UtcNow);
    }
    
    public async Task<CostDashboardDto> GetCostSummaryAsync()
    {
        return await _analyticsService.GetCostSummaryAsync(
            "daily", 
            DateTime.UtcNow.AddDays(-30), 
            DateTime.UtcNow);
    }
}
```

### Python
```python
import requests
from datetime import datetime, timedelta

class AnalyticsClient:
    def __init__(self, base_url, api_key):
        self.base_url = f"{base_url}/api/analytics"
        self.headers = {"X-API-Key": api_key}
    
    def get_logs(self, page=1, page_size=50, **filters):
        params = {"page": page, "pageSize": page_size, **filters}
        response = requests.get(
            f"{self.base_url}/logs",
            params=params,
            headers=self.headers
        )
        return response.json()
    
    def get_cost_summary(self, timeframe="daily"):
        response = requests.get(
            f"{self.base_url}/costs/summary",
            params={"timeframe": timeframe},
            headers=self.headers
        )
        return response.json()
    
    def export_analytics(self, format="csv", **filters):
        params = {"format": format, **filters}
        response = requests.get(
            f"{self.base_url}/export",
            params=params,
            headers=self.headers
        )
        return response.content
```