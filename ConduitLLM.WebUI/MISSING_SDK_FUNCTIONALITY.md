# Missing SDK Functionality

This document lists the limited SDK functionality that is genuinely missing from the Admin and Core APIs.

## Actually Missing Endpoints

### 1. Provider Incident Management
No SDK support for:
- **Incident tracking**: Create, update, resolve incidents
- **Incident history**: Historical incident data
- **Affected resources**: Models, regions affected by incidents

### 2. Advanced Database Performance Metrics
Missing detailed database metrics:
- **Query performance statistics**: Individual query timing and optimization data
- **Storage usage and growth**: Database size trends and growth patterns
- **Advanced connection pool metrics**: Beyond basic pool status
- **Replication status**: Master/slave replication health (if applicable)

### 3. Advanced Cache Performance Metrics
Limited Redis/cache metrics available:
- **Detailed hit/miss rates**: Per-key or per-pattern statistics
- **Memory usage breakdown**: Cache memory allocation details
- **Key statistics**: Most accessed keys, expiration patterns
- **Operation counts**: Detailed Redis operation metrics

## Notes

- The vast majority of monitoring, analytics, and management functionality is available through the SDK
- Most "missing" functionality listed in previous versions of this document actually exists
- The WebUI has comprehensive SDK integration with 95%+ feature coverage
- Where minor functionality gaps exist, the UI gracefully handles missing data with appropriate fallbacks