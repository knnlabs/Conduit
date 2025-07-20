# Admin API Documentation

*Last Updated: 2025-01-20*

Comprehensive documentation for the Conduit Admin API - managing virtual keys, providers, and system configuration.

## Table of Contents
- [Overview](#overview)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Documentation Structure](#documentation-structure)
- [Common Tasks](#common-tasks)

## Overview

The Admin API provides programmatic access to all administrative functions in Conduit:

- **Virtual Key Management** - Create, update, delete API keys with routing rules
- **Provider Configuration** - Manage LLM provider credentials and settings
- **Model Mapping** - Configure how models route to providers
- **Cost Management** - Set pricing and track usage
- **System Monitoring** - Health checks, metrics, and diagnostics
- **User Management** - Control access and permissions

## Quick Start

### Authentication

All Admin API requests require authentication:

```bash
curl -X GET https://api.conduit.ai/api/virtualkeys \
  -H "Authorization: Bearer YOUR_ADMIN_KEY" \
  -H "Content-Type: application/json"
```

### Create Your First Virtual Key

```bash
curl -X POST https://api.conduit.ai/api/virtualkeys \
  -H "Authorization: Bearer YOUR_ADMIN_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Production API Key",
    "monthlySpendLimit": 100.00,
    "rateLimitPerMinute": 60,
    "isActive": true
  }'
```

### TypeScript SDK

```typescript
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

const admin = new ConduitAdminClient({
  apiKey: process.env.CONDUIT_ADMIN_KEY,
  baseURL: 'https://api.conduit.ai'
});

// Create virtual key
const key = await admin.virtualKeys.create({
  name: 'Production API Key',
  monthlySpendLimit: 100.00,
  rateLimitPerMinute: 60,
  isActive: true
});

console.log(`Created key: ${key.key}`);
```

## Architecture

### API Layers

```
┌─────────────────┐
│   WebUI/Client  │
└────────┬────────┘
         │ HTTPS
┌────────▼────────┐
│   Admin API     │ ← Authentication & Authorization
├─────────────────┤
│  Service Layer  │ ← Business Logic & Validation
├─────────────────┤
│   Repository    │ ← Data Access Abstraction
├─────────────────┤
│   PostgreSQL    │ ← Persistent Storage
└─────────────────┘
```

### Key Components

1. **Controllers** - HTTP endpoint handlers
2. **Services** - Business logic implementation
3. **Repositories** - Data access layer
4. **DTOs** - Data transfer objects for API contracts
5. **Validators** - Input validation and sanitization

## Documentation Structure

### Core Documentation
- **[API Reference](./api-reference.md)** - Complete endpoint documentation
- **[Client Guide](./client-guide.md)** - Using the Admin API clients
- **[Examples](./examples.md)** - Code examples by use case
- **[Security](./security.md)** - Authentication and authorization

### Deployment & Operations
- **[Deployment Guide](./deployment.md)** - Configuration and deployment
- **[Troubleshooting](./troubleshooting.md)** - Common issues and solutions

## Common Tasks

### Virtual Key Management

```typescript
// List all virtual keys
const keys = await admin.virtualKeys.list({
  includeInactive: false,
  sortBy: 'createdAt',
  sortDirection: 'desc'
});

// Update spending limit
await admin.virtualKeys.update(keyId, {
  monthlySpendLimit: 500.00
});

// Disable a key
await admin.virtualKeys.update(keyId, {
  isActive: false
});
```

### Provider Configuration

```typescript
// Add OpenAI provider
await admin.providers.create({
  name: 'openai-prod',
  provider: 'OpenAI',
  apiKey: process.env.OPENAI_API_KEY,
  isActive: true
});

// Configure model mapping
await admin.modelMappings.create({
  modelPattern: 'gpt-4*',
  providerId: 'openai-prod',
  priority: 100
});
```

### Usage Monitoring

```typescript
// Get usage by virtual key
const usage = await admin.analytics.getKeyUsage(keyId, {
  startDate: '2025-01-01',
  endDate: '2025-01-31'
});

// Get provider health
const health = await admin.system.getProviderHealth();
```

## API Endpoints Overview

### Virtual Keys
- `GET /api/virtualkeys` - List all keys
- `POST /api/virtualkeys` - Create new key
- `GET /api/virtualkeys/{id}` - Get key details
- `PUT /api/virtualkeys/{id}` - Update key
- `DELETE /api/virtualkeys/{id}` - Delete key

### Providers
- `GET /api/modelprovider` - List providers
- `POST /api/modelprovider` - Add provider
- `PUT /api/modelprovider/{id}` - Update provider
- `DELETE /api/modelprovider/{id}` - Remove provider

### System
- `GET /api/system/info` - System information
- `GET /api/system/health` - Health status
- `POST /api/system/backup` - Trigger backup

[See complete API Reference →](./api-reference.md)

## Security Considerations

1. **API Keys** - Store admin keys securely, rotate regularly
2. **HTTPS Only** - All requests must use TLS
3. **Rate Limiting** - Default 100 requests/minute per key
4. **Audit Logging** - All actions are logged
5. **CORS** - Configure allowed origins properly

## Integration Patterns

### WebUI Integration
The WebUI acts as a proxy to the Admin API:
- Frontend calls `/api/admin/*` endpoints
- WebUI validates session and forwards to Admin API
- Responses are returned to the frontend

### Direct Integration
For backend services and automation:
- Call Admin API directly with admin key
- Implement retry logic for resilience
- Cache responses where appropriate

## Error Handling

All errors follow a consistent format:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Monthly spend limit must be positive",
    "details": {
      "field": "monthlySpendLimit",
      "value": -10
    }
  }
}
```

Common error codes:
- `UNAUTHORIZED` - Invalid or missing API key
- `NOT_FOUND` - Resource doesn't exist
- `VALIDATION_ERROR` - Invalid input
- `RATE_LIMITED` - Too many requests
- `INTERNAL_ERROR` - Server error

## Related Documentation

- [Core API Documentation](../api-reference/core-api.md)
- [WebUI Guide](../webui-guide.md)
- [Provider Integration](../provider-integration.md)
- [Virtual Keys Guide](../virtual-keys.md)

---

*For the latest updates and SDK releases, see the [GitHub repository](https://github.com/knnlabs/Conduit).*