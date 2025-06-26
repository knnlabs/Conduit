---
sidebar_position: 1
title: Admin API Overview
description: Comprehensive guide to Conduit's administrative API for system management
---

# Admin API Overview

The Conduit Admin API is a dedicated service for administrative operations, provider management, virtual key administration, and system configuration. It operates independently from the Core API to ensure administrative functions don't impact user-facing LLM operations.

## Architecture Separation

The Admin API is architecturally separated from the Core API:

- **Admin API** (Port 5002): Administrative operations only
- **Core API** (Port 5000): User-facing LLM requests only
- **WebUI** (Port 5001): Uses Admin API exclusively
- **Database**: Shared PostgreSQL with different access patterns

## Key Features

### Administrative Operations
- Virtual key management and analytics
- Provider credential configuration
- Usage monitoring and billing
- System health and diagnostics

### Event-Driven Coordination
- Real-time cache invalidation
- Cross-service data consistency
- Async task coordination
- Provider health monitoring

### Production-Ready
- Independent scaling capabilities
- Comprehensive health checks
- Detailed audit logging
- Performance monitoring

## Authentication

### Master Key Authentication

The Admin API uses master key authentication for administrative access:

```bash
# Set master key in environment
export CONDUITLLM__MASTERKEY=your-secure-master-key

# Use in API requests
curl -H "Authorization: Bearer your-secure-master-key" \
  http://localhost:5002/api/admin/virtual-keys
```

### Virtual Key Authentication (Limited)

Some Admin API endpoints support virtual key authentication for user-specific operations:

```bash
# Use virtual key for user-specific data
curl -H "Authorization: Bearer condt_your_virtual_key" \
  http://localhost:5002/api/admin/virtual-keys/usage
```

## Base URL and Endpoints

### Development Environment
```
Admin API: http://localhost:5002
WebUI: http://localhost:5001
Health Checks: http://localhost:5002/health
```

### Production Environment
```
Admin API: https://admin-api.yourdomain.com
WebUI: https://admin.yourdomain.com
Health Checks: https://admin-api.yourdomain.com/health
```

## Core Endpoint Categories

### Virtual Key Management
- `/api/admin/virtual-keys` - CRUD operations
- `/api/admin/virtual-keys/{id}/usage` - Usage analytics
- `/api/admin/virtual-keys/{id}/spending` - Cost tracking
- `/api/admin/virtual-keys/bulk` - Bulk operations

### Provider Management
- `/api/admin/providers` - Provider configuration
- `/api/admin/providers/health` - Health monitoring
- `/api/admin/providers/capabilities` - Model discovery
- `/api/admin/providers/credentials` - Credential management

### System Administration
- `/api/admin/system/health` - System diagnostics
- `/api/admin/system/metrics` - Performance metrics
- `/api/admin/system/configuration` - System settings
- `/api/admin/system/export` - Configuration export

### Analytics and Reporting
- `/api/admin/analytics/usage` - Usage statistics
- `/api/admin/analytics/costs` - Cost analysis
- `/api/admin/analytics/performance` - Performance metrics
- `/api/admin/analytics/providers` - Provider analytics

## Request/Response Format

### Standard Request Format

```json
{
  "requestId": "uuid-v4",
  "timestamp": "2024-01-15T10:30:00Z",
  "data": {
    // Request-specific data
  }
}
```

### Standard Response Format

```json
{
  "success": true,
  "timestamp": "2024-01-15T10:30:00Z",
  "requestId": "uuid-v4",
  "data": {
    // Response data
  },
  "errors": [],
  "metadata": {
    "totalCount": 100,
    "pageSize": 20,
    "currentPage": 1
  }
}
```

### Error Response Format

```json
{
  "success": false,
  "timestamp": "2024-01-15T10:30:00Z",
  "requestId": "uuid-v4",
  "data": null,
  "errors": [
    {
      "code": "VALIDATION_ERROR",
      "message": "Invalid virtual key configuration",
      "field": "maxBudget",
      "details": "Budget must be greater than 0"
    }
  ]
}
```

## Event-Driven Operations

### Virtual Key Events

The Admin API publishes events for real-time coordination:

```json
{
  "eventType": "VirtualKeyCreated",
  "keyId": "uuid-v4",
  "keyHash": "abcd1234",
  "timestamp": "2024-01-15T10:30:00Z",
  "data": {
    "keyName": "Production API Key",
    "isEnabled": true,
    "allowedModels": ["gpt-4", "gpt-3.5-turbo"],
    "maxBudget": 100.00
  }
}
```

### Provider Events

```json
{
  "eventType": "ProviderCredentialUpdated",
  "providerId": "uuid-v4",
  "providerName": "openai",
  "timestamp": "2024-01-15T10:30:00Z",
  "data": {
    "isEnabled": true,
    "changedProperties": ["apiKey", "isEnabled"]
  }
}
```

## Health Checks

### Basic Health Check

```bash
curl http://localhost:5002/health
```

Response:
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567"
}
```

### Detailed Health Check

```bash
curl http://localhost:5002/health/ready
```

Response:
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.0123456",
      "description": "Database connection successful"
    },
    "eventbus": {
      "status": "Healthy",
      "duration": "00:00:00.0098765",
      "description": "RabbitMQ connection successful"
    },
    "providers": {
      "status": "Degraded",
      "duration": "00:00:00.0156789",
      "description": "3 of 5 providers healthy",
      "data": {
        "openai": "Healthy",
        "anthropic": "Healthy",
        "google": "Healthy",
        "azure": "Unhealthy",
        "cohere": "Unhealthy"
      }
    }
  }
}
```

## Rate Limiting

### Admin API Rate Limits

```bash
# Rate limiting headers in responses
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 999
X-RateLimit-Reset: 1642267800
X-RateLimit-Window: 3600
```

### Rate Limit Configuration

```bash
# Environment configuration
CONDUITLLM__RATELIMITING__ADMIN__ENABLED=true
CONDUITLLM__RATELIMITING__ADMIN__REQUESTSPERMINUTE=1000
CONDUITLLM__RATELIMITING__ADMIN__BURSTSIZE=100
```

## Pagination

### Standard Pagination

```bash
# Request with pagination
curl "http://localhost:5002/api/admin/virtual-keys?page=1&pageSize=20&sortBy=createdAt&sortOrder=desc"
```

Response:
```json
{
  "success": true,
  "data": {
    "items": [...],
    "pagination": {
      "currentPage": 1,
      "pageSize": 20,
      "totalPages": 5,
      "totalCount": 100,
      "hasNextPage": true,
      "hasPreviousPage": false
    }
  }
}
```

### Filtering and Sorting

```bash
# Advanced filtering
curl "http://localhost:5002/api/admin/virtual-keys?isEnabled=true&hasMaxBudget=true&createdAfter=2024-01-01"

# Multiple sort fields
curl "http://localhost:5002/api/admin/virtual-keys?sortBy=usage&sortOrder=desc&secondarySort=createdAt"
```

## Security Headers

### Standard Security Headers

```http
X-Frame-Options: DENY
X-Content-Type-Options: nosniff
X-XSS-Protection: 1; mode=block
Strict-Transport-Security: max-age=31536000; includeSubDomains
Content-Security-Policy: default-src 'self'
```

### Audit Logging

All Admin API operations are logged:

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "level": "Information",
  "message": "Virtual key created",
  "properties": {
    "operation": "VirtualKey.Create",
    "userId": "admin",
    "keyId": "uuid-v4",
    "keyName": "Production API Key",
    "ipAddress": "192.168.1.100",
    "userAgent": "Mozilla/5.0...",
    "correlationId": "req-abc123"
  }
}
```

## Client Libraries

### Node.js Client

```javascript
import { ConduitAdminClient } from '@conduit/admin-client';

const client = new ConduitAdminClient({
  baseUrl: 'http://localhost:5002',
  masterKey: 'your-master-key',
  timeout: 30000
});

// Create virtual key
const virtualKey = await client.virtualKeys.create({
  name: 'Production API Key',
  description: 'API key for production environment',
  maxBudget: 100.00,
  allowedModels: ['gpt-4', 'gpt-3.5-turbo']
});
```

### HTTP Examples

```bash
# Create virtual key
curl -X POST http://localhost:5002/api/admin/virtual-keys \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Production API Key",
    "description": "API key for production environment",
    "maxBudget": 100.00,
    "allowedModels": ["gpt-4", "gpt-3.5-turbo"],
    "rateLimit": {
      "requestsPerMinute": 1000,
      "requestsPerDay": 50000
    }
  }'

# Get virtual key usage
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys/uuid-v4/usage?startDate=2024-01-01&endDate=2024-01-31"

# Update provider credentials
curl -X PUT http://localhost:5002/api/admin/providers/openai/credentials \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "apiKey": "sk-new-api-key",
    "organizationId": "org-123456",
    "isEnabled": true
  }'
```

## Performance Considerations

### Caching Strategy

- Virtual key data cached for 5 minutes
- Provider health cached for 1 minute
- System metrics cached for 30 seconds
- Analytics data cached for 5 minutes

### Connection Pooling

```bash
# Database connection pool settings
CONDUITLLM__DATABASE__ADMIN__MAXPOOLSIZE=50
CONDUITLLM__DATABASE__ADMIN__MINPOOLSIZE=5
CONDUITLLM__DATABASE__ADMIN__CONNECTIONLIFETIME=300
```

### Resource Allocation

```yaml
# Kubernetes resource allocation
resources:
  requests:
    memory: "256Mi"
    cpu: "250m"
  limits:
    memory: "512Mi"
    cpu: "500m"
```

## Monitoring and Observability

### Metrics

Key metrics exposed by the Admin API:

- `admin_api_requests_total` - Total requests
- `admin_api_request_duration_seconds` - Request duration
- `admin_api_virtual_key_operations_total` - Virtual key operations
- `admin_api_provider_operations_total` - Provider operations

### Logging

Structured logging with correlation IDs:

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "level": "Information",
  "message": "Virtual key usage retrieved",
  "properties": {
    "keyId": "uuid-v4",
    "dateRange": "2024-01-01 to 2024-01-31",
    "requestCount": 15420,
    "totalCost": 234.56,
    "correlationId": "req-abc123",
    "duration": 145
  }
}
```

## Error Handling

### Standard Error Codes

| Code | Description | HTTP Status |
|------|-------------|-------------|
| `AUTHENTICATION_FAILED` | Invalid master key | 401 |
| `AUTHORIZATION_FAILED` | Insufficient permissions | 403 |
| `VALIDATION_ERROR` | Request validation failed | 400 |
| `RESOURCE_NOT_FOUND` | Resource does not exist | 404 |
| `CONFLICT` | Resource conflict | 409 |
| `RATE_LIMITED` | Rate limit exceeded | 429 |
| `INTERNAL_ERROR` | Internal server error | 500 |
| `SERVICE_UNAVAILABLE` | Service temporarily unavailable | 503 |

### Error Response Examples

```json
{
  "success": false,
  "errors": [
    {
      "code": "VALIDATION_ERROR",
      "message": "Virtual key name is required",
      "field": "name"
    },
    {
      "code": "VALIDATION_ERROR", 
      "message": "Max budget must be greater than 0",
      "field": "maxBudget"
    }
  ]
}
```

## Next Steps

- **Virtual Keys**: Learn about [virtual key management](virtual-keys)
- **Provider Configuration**: Set up [provider credentials](provider-configuration)
- **Usage & Billing**: Monitor [usage and costs](usage-billing)
- **WebUI Guide**: Use the [administrative interface](webui-guide)