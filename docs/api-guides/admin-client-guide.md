# Admin API Client Guide

This comprehensive guide covers all aspects of using the Conduit Admin API clients across different platforms and integration scenarios.

## Overview

The Conduit Admin API provides programmatic access to all administrative functionality through multiple client implementations:

- **.NET/C# Client** - Direct HTTP client implementation for .NET applications
- **TypeScript/JavaScript Client** - Full-featured Node.js/browser client with advanced features
- **WebUI Integration** - Next.js integration patterns for React applications
- **Direct API Integration** - Raw HTTP API usage for any platform

## Documentation Structure

The Admin API client documentation has been organized by platform and usage pattern:

### 🔧 Client Implementations
- **[TypeScript Setup](./typescript-setup.md)** - TypeScript client setup and configuration
- **[TypeScript Client](./typescript-client.md)** - Complete TypeScript client implementation
- **[TypeScript Examples](./examples.md)** - Comprehensive usage examples

### 🎯 Specific Use Cases
- **[Virtual Keys Management](./typescript-virtual-keys.md)** - Virtual key operations and examples
- **[Provider Management](./typescript-providers.md)** - Provider configuration and health monitoring
- **[Analytics & Monitoring](./typescript-analytics.md)** - Usage analytics and cost tracking

### 🚀 Integration Patterns
- **[Next.js Integration](../sdk-nextjs-integration-guide.md)** - Next.js specific patterns
- **[WebUI Integration](../api-reference/webui-api-reference.md)** - WebUI API reference

## Quick Start by Platform

### .NET Client Usage

The WebUI project uses the Admin API client through a direct implementation pattern:

```
WebUI Component → Admin API Client → HTTP → Admin API → Repository → Database
```

**Implementation:**
```csharp
public partial class AdminApiClient : IVirtualKeyService, IRequestLogService, 
    ICostDashboardService, IGlobalSettingService, IProviderHealthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AdminApiClient> _logger;
    
    public AdminApiClient(HttpClient httpClient, ILogger<AdminApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    
    // Implementation directly calls Admin API endpoints
    public async Task<List<VirtualKeyDto>> GetAllVirtualKeysAsync()
    {
        var response = await _httpClient.GetAsync("/api/virtualkeys");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<VirtualKeyDto>>();
    }
}
```

**Key Features:**
- Direct service interface implementation
- Automatic HTTP client configuration
- Integrated logging and error handling
- Dependency injection support

### TypeScript/JavaScript Client

```bash
npm install @conduit/admin-sdk
# or use our comprehensive TypeScript client
```

**Basic Setup:**
```typescript
import { ConduitAdminApiClient } from './typescript-client';

const adminClient = new ConduitAdminApiClient(
    'http://localhost:5002',
    'your_master_key_here'
);

// Use the client
const virtualKeys = await adminClient.getAllVirtualKeys();
const providers = await adminClient.getAllProviderCredentials();
```

**Features:**
- Full TypeScript support with type definitions
- Automatic retry logic with exponential backoff
- Comprehensive error handling
- Multiple authentication methods

### WebUI Integration

**Server-Side Usage:**
```typescript
// pages/api/admin/keys.ts
import { getServerAdminClient } from '@/lib/sdk/server-admin-client';

export default async function handler(req: NextRequest) {
  const adminClient = getServerAdminClient();
  const keys = await adminClient.getAllVirtualKeys();
  return Response.json({ data: keys });
}
```

**Client-Side Usage:**
```typescript
// components/VirtualKeysList.tsx
'use client';

import { useQuery } from '@tanstack/react-query';

export function VirtualKeysList() {
  const { data: keys } = useQuery({
    queryKey: ['virtual-keys'],
    queryFn: async () => {
      const response = await fetch('/api/admin/virtual-keys');
      return response.json();
    },
  });

  return (
    <div>
      {keys?.data?.map(key => (
        <div key={key.id}>{key.keyName}</div>
      ))}
    </div>
  );
}
```

### Direct API Integration

**Authentication:**
```bash
# Primary method (recommended)
curl -H "X-API-Key: your_master_key" \
     https://api.conduit.im/api/virtualkeys

# Alternative method
curl -H "Authorization: Bearer your_master_key" \
     https://api.conduit.im/api/virtualkeys
```

**Common Operations:**
```bash
# List virtual keys
curl -H "X-API-Key: your_master_key" \
     "https://api.conduit.im/api/virtualkeys"

# Create virtual key
curl -X POST \
     -H "X-API-Key: your_master_key" \
     -H "Content-Type: application/json" \
     -d '{"keyName":"Test Key","virtualKeyGroupId":1}' \
     "https://api.conduit.im/api/virtualkeys"

# Get provider health
curl -H "X-API-Key: your_master_key" \
     "https://api.conduit.im/api/providerhealth/summary"
```

## Architecture Overview

### .NET Implementation
```
WebUI Components
       ↓
Admin API Client (HTTP)
       ↓
Admin API Service
       ↓
Repository Pattern
       ↓
Entity Framework
       ↓
PostgreSQL Database
```

### TypeScript SDK Implementation
```
Client Application
       ↓
TypeScript Admin Client
       ↓ 
HTTP with Retry Logic
       ↓
Admin API Endpoints
       ↓
Business Logic Services
       ↓
Database Access Layer
```

### WebUI Integration Pattern
```
React Components
       ↓
Next.js API Routes
       ↓
Server Admin Client
       ↓
Admin API (HTTP)
       ↓
Backend Services
```

## Authentication & Security

### Master Key Authentication

All Admin API clients use master key authentication:

```typescript
// Environment configuration
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your_master_key_here

// TypeScript client
const client = new ConduitAdminApiClient(
    process.env.ADMIN_API_URL,
    process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY
);

// .NET client (configured via DI)
services.AddHttpClient<AdminApiClient>(client => {
    client.DefaultRequestHeaders.Add("X-API-Key", masterKey);
});
```

### Security Best Practices

1. **Environment Variables**: Store master keys in environment variables
2. **Server-Side Only**: Never expose master keys in client-side code
3. **HTTPS Only**: Always use HTTPS in production
4. **Access Control**: Implement proper authorization in your application
5. **Audit Logging**: Log all administrative operations

## Error Handling Patterns

### TypeScript Client
```typescript
try {
    const result = await adminClient.createVirtualKey(request);
    return result;
} catch (error) {
    if (error.status === 429) {
        // Rate limited - client automatically retries
        console.log('Request was rate limited, retrying...');
    } else if (error.status === 403) {
        // Authentication error
        throw new Error('Invalid master key');
    } else {
        // Other errors
        console.error('Unexpected error:', error.message);
        throw error;
    }
}
```

### .NET Client
```csharp
try
{
    var result = await adminClient.GetAllVirtualKeysAsync();
    return result;
}
catch (HttpRequestException ex) when (ex.Message.Contains("429"))
{
    // Rate limited
    await Task.Delay(TimeSpan.FromSeconds(1));
    return await adminClient.GetAllVirtualKeysAsync();
}
catch (HttpRequestException ex) when (ex.Message.Contains("403"))
{
    // Authentication error
    throw new UnauthorizedAccessException("Invalid master key", ex);
}
```

### WebUI Integration
```typescript
// Server-side error handling
export async function GET() {
  try {
    const adminClient = getServerAdminClient();
    const data = await adminClient.getAllVirtualKeys();
    return Response.json({ data });
  } catch (error) {
    console.error('Admin API error:', error);
    return Response.json(
      { error: 'Failed to fetch virtual keys' },
      { status: 500 }
    );
  }
}
```

## Configuration

### Environment Variables
```bash
# Required
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your_master_key_here
ADMIN_API_URL=http://localhost:5002

# Optional
ADMIN_API_TIMEOUT=30000
ADMIN_API_RETRY_ATTEMPTS=3
ADMIN_API_RETRY_DELAY=1000
```

### TypeScript Configuration
```typescript
// Client configuration options
const client = new ConduitAdminApiClient(baseUrl, masterKey, {
    timeout: 30000,
    retryAttempts: 3,
    retryDelay: 1000,
    enableLogging: true
});
```

### .NET Configuration
```csharp
// In Program.cs or Startup.cs
services.AddHttpClient<AdminApiClient>(client => {
    client.BaseAddress = new Uri(configuration["AdminApiUrl"]);
    client.DefaultRequestHeaders.Add("X-API-Key", configuration["MasterKey"]);
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

## Best Practices

### Performance
- **Connection Pooling**: Reuse HTTP clients when possible
- **Caching**: Cache frequently accessed data (virtual keys, providers)
- **Pagination**: Use pagination for large datasets
- **Filtering**: Apply server-side filtering to reduce data transfer

### Reliability
- **Retry Logic**: Implement exponential backoff for transient failures
- **Circuit Breakers**: Protect against cascading failures
- **Health Checks**: Monitor Admin API health
- **Graceful Degradation**: Handle API unavailability gracefully

### Monitoring
- **Logging**: Log all administrative operations
- **Metrics**: Track API usage and performance
- **Alerting**: Set up alerts for failures and anomalies
- **Audit Trails**: Maintain audit logs for compliance

### Development
- **Type Safety**: Use TypeScript for better development experience
- **Testing**: Write comprehensive tests for client integrations
- **Documentation**: Document all custom implementations
- **Version Control**: Pin client library versions

## Deployment Considerations

### Development Environment
```bash
# Local development
ADMIN_API_URL=http://localhost:5002
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=dev_master_key
```

### Production Environment
```bash
# Production configuration
ADMIN_API_URL=https://admin-api.yourdomain.com
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=prod_master_key_from_vault
ADMIN_API_TIMEOUT=15000
```

### Container Deployment
```dockerfile
# Dockerfile
ENV ADMIN_API_URL=https://admin-api.yourdomain.com
ENV CONDUIT_API_TO_API_BACKEND_AUTH_KEY_FILE=/run/secrets/master_key

# Use secret management
COPY --from=secrets /run/secrets/master_key /run/secrets/master_key
```

### Health Monitoring
```typescript
// Health check endpoint
export async function GET() {
  try {
    const adminClient = getServerAdminClient();
    await adminClient.getSystemInfo();
    return Response.json({ status: 'healthy' });
  } catch (error) {
    return Response.json(
      { status: 'unhealthy', error: error.message },
      { status: 503 }
    );
  }
}
```

## Migration Guide

### From Direct HTTP to TypeScript Client
```typescript
// Before: Direct HTTP calls
const response = await fetch('/api/virtualkeys', {
  headers: { 'X-API-Key': masterKey }
});
const keys = await response.json();

// After: TypeScript client
const adminClient = new ConduitAdminApiClient(baseUrl, masterKey);
const keys = await adminClient.getAllVirtualKeys();
```

### From Legacy SDK to New SDK
```typescript
// Update imports
import { ConduitAdminApiClient } from './typescript-client';

// Update initialization
const client = new ConduitAdminApiClient(
  process.env.ADMIN_API_URL,
  process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY
);

// Update method calls (most remain the same)
const keys = await client.getAllVirtualKeys();
```

## Troubleshooting

### Common Issues
1. **Authentication Failures**: Verify master key configuration
2. **Connection Timeouts**: Check network connectivity and API health
3. **Rate Limiting**: Implement proper retry logic
4. **CORS Issues**: Configure CORS for browser-based applications

### Debug Tools
```typescript
// Enable debug logging
const client = new ConduitAdminApiClient(baseUrl, masterKey, {
    enableLogging: true,
    logLevel: 'debug'
});

// Check client configuration
console.log('Client config:', client.getConfiguration());

// Test connection
const health = await client.getSystemInfo();
console.log('API health:', health);
```

## Support

For questions or issues with Admin API clients:
- Review the TypeScript implementation examples
- Check the WebUI integration patterns
- See the comprehensive API reference documentation
- Verify authentication and configuration setup