# Admin API Client Guide

This comprehensive guide covers all aspects of using the Conduit Admin API clients across different platforms and integration scenarios.

## Overview

The Conduit Admin API provides programmatic access to all administrative functionality through multiple client implementations:

- **.NET/C# Client** - Direct HTTP client implementation for .NET applications
- **TypeScript/JavaScript Client** - Full-featured Node.js/browser client with advanced features
- **WebUI Integration** - Next.js integration patterns for React applications
- **Direct API Integration** - Raw HTTP API usage for any platform

## Table of Contents

1. [.NET Client Usage](#net-client-usage)
2. [TypeScript/JavaScript Client Usage](#typescriptjavascript-client-usage)
3. [WebUI Integration Patterns](#webui-integration-patterns)
4. [Direct API Integration](#direct-api-integration)
5. [Authentication & Security](#authentication--security)
6. [Configuration](#configuration)
7. [Error Handling](#error-handling)
8. [Best Practices](#best-practices)
9. [Deployment Considerations](#deployment-considerations)

---

## .NET Client Usage

The .NET client provides direct HTTP communication with the Admin API, implementing service interfaces for seamless integration.

### Architecture

The WebUI project uses the Admin API client through a direct implementation pattern:

```
WebUI Component → Admin API Client → HTTP → Admin API → Repository → Database
```

### Implementation

The `AdminApiClient` class directly implements all service interfaces:

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
    
    // Direct interface implementation
    public async Task<VirtualKeyDto?> GetVirtualKeyByIdAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/virtualkeys/{id}");
            
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<VirtualKeyDto>(_jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving virtual key with ID {VirtualKeyId}", id);
            return null;
        }
    }
}
```

### Service Registration

Register the Admin API client in `Program.cs`:

```csharp
// Register the Admin API client
builder.Services.AddAdminApiClient(builder.Configuration);

// Add caching decorator
builder.Services.Decorate<IAdminApiClient, CachingAdminApiClient>();

// Register direct interface implementations
services.AddScoped<IVirtualKeyService>(sp => sp.GetRequiredService<IAdminApiClient>());
services.AddScoped<ICostDashboardService>(sp => sp.GetRequiredService<IAdminApiClient>());
services.AddScoped<IGlobalSettingService>(sp => sp.GetRequiredService<IAdminApiClient>());
```

### Implemented Interfaces

| Interface | Purpose |
|-----------|---------|
| `IGlobalSettingService` | Manage global application settings |
| `IVirtualKeyService` | Manage virtual API keys |
| `IRequestLogService` | Access and analyze request logs |
| `IModelCostService` | Manage and retrieve model cost information |
| `IProviderHealthService` | Monitor LLM provider health |
| `IProviderCredentialService` | Manage provider credentials |
| `IIpFilterService` | Manage IP whitelist/blacklist filtering |
| `ICostDashboardService` | Retrieve cost analytics and dashboard data |
| `IRouterService` | Configure and manage the LLM router |

### Example Usage

```csharp
public class AdminService
{
    private readonly IVirtualKeyService _virtualKeyService;
    
    public AdminService(IVirtualKeyService virtualKeyService)
    {
        _virtualKeyService = virtualKeyService;
    }
    
    public async Task<IEnumerable<VirtualKeyDto>> GetActiveKeysAsync()
    {
        return await _virtualKeyService.GetAllAsync(isEnabled: true);
    }
}
```

---

## TypeScript/JavaScript Client Usage

The TypeScript client provides a comprehensive, feature-rich interface for the Admin API with full type safety.

### Installation

```bash
npm install @knn_labs/conduit-admin-client
# or
yarn add @knn_labs/conduit-admin-client
# or
pnpm add @knn_labs/conduit-admin-client
```

### Basic Usage

```typescript
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

// Initialize the client
const client = new ConduitAdminClient({
  masterKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY!,
  adminApiUrl: process.env.CONDUIT_ADMIN_API_URL!,
});

// Or use the convenience factory method
const client = ConduitAdminClient.fromEnvironment();
```

### Service Areas

The TypeScript client organizes functionality into logical service areas:

#### Virtual Key Management

```typescript
// Create a virtual key
const { virtualKey, keyInfo } = await client.virtualKeys.create({
  keyName: 'Development Key',
  allowedModels: 'gpt-4,gpt-3.5-turbo,claude-3-opus',
  maxBudget: 50,
  budgetDuration: 'Monthly',
  metadata: JSON.stringify({
    environment: 'development',
    team: 'engineering',
  }),
  rateLimitRpm: 60,
  rateLimitRpd: 1000,
});

// List virtual keys with filtering
const keysList = await client.virtualKeys.list({
  pageSize: 10,
  isEnabled: true,
  sortBy: {
    field: 'createdAt',
    direction: 'desc',
  },
});

// Validate a key
const validation = await client.virtualKeys.validate(virtualKey);
if (validation.isValid) {
  console.log(`Budget remaining: $${validation.budgetRemaining}`);
}

// Update and manage keys
await client.virtualKeys.update(keyInfo.id, { maxBudget: 100 });
await client.virtualKeys.resetSpend(keyInfo.id);
```

#### Provider Management

```typescript
// Test provider connections
await client.providers.testConnection({ 
  providerName: 'openai', 
  apiKey: 'sk-...' 
});

// Get health status
const health = await client.providers.getHealthStatus();
console.log(`Healthy providers: ${health.healthyProviders}/${health.totalProviders}`);

// Configure health monitoring
await client.providers.updateHealthConfiguration('openai', {
  isEnabled: true,
  checkIntervalSeconds: 60,
  timeoutSeconds: 10,
  unhealthyThreshold: 3,
  healthyThreshold: 2,
});
```

#### Model Mappings

```typescript
// Create model mappings
await client.modelMappings.create({
  modelId: 'gpt-4',
  providerId: 'openai',
  providerModelId: 'gpt-4',
  priority: 100,
});

// Bulk model discovery
const discoveredModels = await client.modelMappings.bulkDiscover(['openai', 'anthropic']);
console.log(`Discovered ${discoveredModels.length} models`);
```

#### Settings Management

```typescript
// Update router configuration
await client.settings.updateRouterConfiguration({
  routingStrategy: 'least-cost',
  fallbackEnabled: true,
  maxRetries: 3,
  loadBalancingEnabled: true,
  circuitBreakerEnabled: true,
});

// Set custom settings
await client.settings.setSetting('RATE_LIMIT_WINDOW', '60', {
  description: 'Rate limit window in seconds',
  dataType: 'number',
  category: 'RateLimiting',
});
```

#### IP Filtering

```typescript
// Create IP filters
await client.ipFilters.createAllowFilter(
  'Office Network',
  '192.168.1.0/24',
  'Company infrastructure'
);

// Check IP access
const ipCheck = await client.ipFilters.checkIp('192.168.1.100');
console.log(`IP is ${ipCheck.isAllowed ? 'allowed' : 'blocked'}`);

// Bulk operations
const companyRanges = [
  { name: 'Office Network', cidr: '192.168.1.0/24' },
  { name: 'VPN Range', cidr: '10.0.0.0/16' },
  { name: 'Cloud Servers', cidr: '172.16.0.0/12' },
];

for (const range of companyRanges) {
  await client.ipFilters.createAllowFilter(range.name, range.cidr);
}
```

#### Cost Analytics

```typescript
// Get cost summaries
const dateRange = {
  startDate: new Date(2024, 0, 1).toISOString(),
  endDate: new Date().toISOString(),
};

const costSummary = await client.analytics.getCostSummary(dateRange);
console.log(`Total cost: $${costSummary.totalCost.toFixed(2)}`);

// Cost breakdown by model
const costByModel = await client.analytics.getCostByModel(dateRange);
costByModel.models
  .sort((a, b) => b.totalCost - a.totalCost)
  .slice(0, 5)
  .forEach((model) => {
    console.log(`${model.modelId}: $${model.totalCost.toFixed(2)}`);
  });

// Request log analysis
const failedRequests = await client.analytics.getRequestLogs({
  status: 'error',
  startDate: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
  endDate: new Date().toISOString(),
});
```

#### System Management

```typescript
// Create system backups
const backup = await client.system.createBackup({
  description: 'Pre-update backup',
  includeKeys: true,
  includeProviders: true,
  includeSettings: true,
  includeLogs: false,
});

// Health checks
const health = await client.system.getHealth();
console.log(`System health: ${health.status}`);

// Maintenance operations
await client.virtualKeys.performMaintenance({
  cleanupExpiredKeys: true,
  resetDailyBudgets: true,
});
```

### Advanced Configuration

```typescript
const client = new ConduitAdminClient({
  masterKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY!,
  adminApiUrl: process.env.CONDUIT_ADMIN_API_URL!,
  options: {
    timeout: 30000,
    retries: {
      maxRetries: 5,
      retryDelay: 1000,
      retryCondition: (error) => {
        return error.response?.status >= 500 || error.code === 'ECONNABORTED';
      },
    },
    logger: {
      debug: (msg, ...args) => console.debug(`[DEBUG] ${msg}`, ...args),
      info: (msg, ...args) => console.info(`[INFO] ${msg}`, ...args),
      warn: (msg, ...args) => console.warn(`[WARN] ${msg}`, ...args),
      error: (msg, ...args) => console.error(`[ERROR] ${msg}`, ...args),
    },
    headers: {
      'X-Custom-Header': 'value',
    },
  },
});
```

---

## WebUI Integration Patterns

The WebUI project demonstrates advanced integration patterns using Next.js and React.

### Client Configuration

The WebUI uses centralized SDK configuration with environment-based routing:

```typescript
// src/lib/server/sdk-config.ts
export const SDK_CONFIG = {
  get masterKey() { 
    return process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY ?? '';
  },
  
  get adminBaseURL() {
    return process.env.NODE_ENV === 'production' 
      ? 'http://admin:8080' 
      : (process.env.CONDUIT_ADMIN_API_BASE_URL ?? 'http://localhost:5002');
  },
  
  timeout: 30000,
  maxRetries: 3,
  
  signalR: {
    enabled: false // Disabled for server-side usage
  }
};

export function getServerAdminClient(): ConduitAdminClient {
  if (!adminClient) {
    adminClient = new ConduitAdminClient({
      baseUrl: SDK_CONFIG.adminBaseURL,
      masterKey: SDK_CONFIG.masterKey,
      timeout: SDK_CONFIG.timeout,
      retries: SDK_CONFIG.maxRetries,
    });
  }
  return adminClient;
}
```

### API Route Patterns

Next.js API routes provide server-side integration:

```typescript
// app/api/admin/virtualkeys/route.ts
import { getServerAdminClient } from '@/lib/server/adminClient';
import { NextRequest, NextResponse } from 'next/server';

export async function GET(request: NextRequest) {
  try {
    const client = getServerAdminClient();
    const searchParams = request.nextUrl.searchParams;
    
    const keys = await client.virtualKeys.list({
      isEnabled: searchParams.get('isEnabled') === 'true',
      pageSize: parseInt(searchParams.get('pageSize') ?? '10'),
    });
    
    return NextResponse.json(keys);
  } catch (error) {
    console.error('Failed to fetch virtual keys:', error);
    return NextResponse.json(
      { error: 'Failed to fetch virtual keys' },
      { status: 500 }
    );
  }
}

export async function POST(request: NextRequest) {
  try {
    const client = getServerAdminClient();
    const body = await request.json();
    
    const result = await client.virtualKeys.create(body);
    return NextResponse.json(result);
  } catch (error) {
    console.error('Failed to create virtual key:', error);
    return NextResponse.json(
      { error: 'Failed to create virtual key' },
      { status: 500 }
    );
  }
}
```

### React Component Integration

React components consume API routes with proper error handling:

```typescript
// components/virtualkeys/VirtualKeysTable.tsx
import { useEffect, useState } from 'react';
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

interface VirtualKeysTableProps {
  // Component props
}

export function VirtualKeysTable(props: VirtualKeysTableProps) {
  const [keys, setKeys] = useState<VirtualKeyDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function fetchKeys() {
      try {
        setLoading(true);
        const response = await fetch('/api/admin/virtualkeys');
        
        if (!response.ok) {
          throw new Error('Failed to fetch virtual keys');
        }
        
        const data = await response.json();
        setKeys(data.items || []);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Unknown error');
      } finally {
        setLoading(false);
      }
    }

    fetchKeys();
  }, []);

  if (loading) return <div>Loading...</div>;
  if (error) return <div>Error: {error}</div>;

  return (
    <table>
      {/* Table implementation */}
    </table>
  );
}
```

### Custom Hooks for API Integration

```typescript
// hooks/useVirtualKeysApi.ts
import { useState, useEffect, useCallback } from 'react';
import { VirtualKeyDto } from '@knn_labs/conduit-admin-client';

export function useVirtualKeysApi() {
  const [keys, setKeys] = useState<VirtualKeyDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchKeys = useCallback(async (filters?: any) => {
    setLoading(true);
    setError(null);
    
    try {
      const params = new URLSearchParams(filters);
      const response = await fetch(`/api/admin/virtualkeys?${params}`);
      
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      
      const data = await response.json();
      setKeys(data.items || []);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setLoading(false);
    }
  }, []);

  const createKey = useCallback(async (keyData: any) => {
    try {
      const response = await fetch('/api/admin/virtualkeys', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(keyData),
      });
      
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      
      const newKey = await response.json();
      setKeys(prev => [...prev, newKey.keyInfo]);
      return newKey;
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
      throw err;
    }
  }, []);

  return {
    keys,
    loading,
    error,
    fetchKeys,
    createKey,
    // ... other operations
  };
}
```

---

## Direct API Integration

For platforms without dedicated clients, use direct HTTP API calls.

### Authentication

All Admin API requests require master key authentication:

```bash
curl -X GET "http://localhost:5002/api/virtualkeys" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json"
```

### Common Endpoints

#### Virtual Keys

```bash
# List virtual keys
curl -X GET "http://localhost:5002/api/virtualkeys" \
  -H "X-Master-Key: your-master-key"

# Create virtual key
curl -X POST "http://localhost:5002/api/virtualkeys" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "keyName": "Test Key",
    "allowedModels": "gpt-4,gpt-3.5-turbo",
    "maxBudget": 100,
    "budgetDuration": "Monthly"
  }'

# Get virtual key details
curl -X GET "http://localhost:5002/api/virtualkeys/{id}" \
  -H "X-Master-Key: your-master-key"

# Update virtual key
curl -X PUT "http://localhost:5002/api/virtualkeys/{id}" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "maxBudget": 200
  }'

# Delete virtual key
curl -X DELETE "http://localhost:5002/api/virtualkeys/{id}" \
  -H "X-Master-Key: your-master-key"
```

#### Provider Management

```bash
# List providers
curl -X GET "http://localhost:5002/api/providers" \
  -H "X-Master-Key: your-master-key"

# Test provider connection
curl -X POST "http://localhost:5002/api/providers/test-connection" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "providerName": "openai",
    "apiKey": "sk-...",
    "baseUrl": "https://api.openai.com/v1"
  }'

# Get provider health
curl -X GET "http://localhost:5002/api/provider-health" \
  -H "X-Master-Key: your-master-key"
```

#### Settings

```bash
# Get global settings
curl -X GET "http://localhost:5002/api/settings" \
  -H "X-Master-Key: your-master-key"

# Update setting
curl -X PUT "http://localhost:5002/api/settings/RATE_LIMIT_WINDOW" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "value": "60",
    "description": "Rate limit window in seconds"
  }'
```

### Python Example

```python
import requests
import json
from typing import Dict, Any, Optional

class ConduitAdminClient:
    def __init__(self, base_url: str, master_key: str):
        self.base_url = base_url.rstrip('/')
        self.master_key = master_key
        self.session = requests.Session()
        self.session.headers.update({
            'X-Master-Key': master_key,
            'Content-Type': 'application/json'
        })
    
    def _request(self, method: str, endpoint: str, data: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        url = f"{self.base_url}/{endpoint.lstrip('/')}"
        
        response = self.session.request(
            method,
            url,
            json=data if data else None
        )
        
        response.raise_for_status()
        return response.json() if response.content else {}
    
    def list_virtual_keys(self, is_enabled: Optional[bool] = None) -> Dict[str, Any]:
        params = {}
        if is_enabled is not None:
            params['isEnabled'] = str(is_enabled).lower()
        
        endpoint = f"api/virtualkeys"
        if params:
            endpoint += "?" + "&".join(f"{k}={v}" for k, v in params.items())
        
        return self._request('GET', endpoint)
    
    def create_virtual_key(self, key_data: Dict[str, Any]) -> Dict[str, Any]:
        return self._request('POST', 'api/virtualkeys', key_data)
    
    def get_virtual_key(self, key_id: int) -> Dict[str, Any]:
        return self._request('GET', f'api/virtualkeys/{key_id}')
    
    def update_virtual_key(self, key_id: int, updates: Dict[str, Any]) -> Dict[str, Any]:
        return self._request('PUT', f'api/virtualkeys/{key_id}', updates)
    
    def delete_virtual_key(self, key_id: int) -> None:
        self._request('DELETE', f'api/virtualkeys/{key_id}')

# Usage example
client = ConduitAdminClient(
    base_url='http://localhost:5002',
    master_key='your-master-key'
)

# List virtual keys
keys = client.list_virtual_keys(is_enabled=True)
print(f"Found {len(keys.get('items', []))} active keys")

# Create a new key
new_key = client.create_virtual_key({
    'keyName': 'Python Test Key',
    'allowedModels': 'gpt-4,gpt-3.5-turbo',
    'maxBudget': 50,
    'budgetDuration': 'Monthly'
})
print(f"Created key: {new_key['virtualKey']}")
```

---

## Authentication & Security

### Master Key Authentication

The Admin API uses master key authentication for all operations:

- **Header**: `X-Master-Key: your-master-key`
- **Environment Variable**: `CONDUIT_API_TO_API_BACKEND_AUTH_KEY`
- **Security**: Use HTTPS in production, rotate keys regularly

### Authorization Levels

1. **Master Key**: Full administrative access to all operations
2. **WebUI Backend**: Uses `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` for server-to-server communication
3. **Virtual Keys**: Limited access for end-user applications (Core API only)

**CRITICAL**: The `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` is used for server-to-server authentication between backend services. The WebUI uses Clerk for human administrator authentication.

### Security Best Practices

1. **Environment Variables**: Store all credentials in environment variables
2. **Network Security**: Use HTTPS and proper network isolation
3. **Key Rotation**: Regularly rotate master keys
4. **Logging**: Monitor authentication failures and suspicious activity
5. **Access Control**: Limit Admin API access to authorized systems only

---

## Configuration

### Environment Variables

#### Required Variables

- `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` - Master key for Admin API authentication
- `CONDUIT_ADMIN_API_URL` - Admin API base URL
- `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` - Backend service authentication key

#### Optional Variables

- `CONDUIT_USE_ADMIN_API` - Enable/disable Admin API usage (default: true)
- `ADMIN_API_TIMEOUT` - Request timeout in seconds (default: 30)
- `ADMIN_API_MAX_RETRIES` - Maximum retry attempts (default: 3)

### Configuration Files

#### .NET (appsettings.json)

```json
{
  "AdminApi": {
    "BaseUrl": "http://localhost:5002",
    "TimeoutSeconds": 30,
    "UseAdminApi": true,
    "MaxRetries": 3
  }
}
```

#### Node.js (.env)

```bash
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your-master-key
CONDUIT_ADMIN_API_URL=http://localhost:5002
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=backend-auth-key
NODE_ENV=development
```

#### Docker Compose

```yaml
services:
  webui:
    environment:
      CONDUIT_API_TO_API_BACKEND_AUTH_KEY: alpha
      CONDUIT_ADMIN_API_URL: http://admin:8080
      CONDUIT_API_TO_API_BACKEND_AUTH_KEY: backend-key
    depends_on:
      - admin

  admin:
    environment:
      AdminApi__MasterKey: alpha
      AdminApi__AllowedOrigins__0: http://webui:8080
    ports:
      - "5002:8080"
```

---

## Error Handling

### Typed Error Classes (TypeScript)

```typescript
import { 
  ValidationError, 
  AuthenticationError, 
  NotFoundError,
  RateLimitError,
  NotImplementedError 
} from '@knn_labs/conduit-admin-client';

try {
  await client.virtualKeys.getById(999);
} catch (error) {
  if (error instanceof NotFoundError) {
    console.error('Virtual key not found');
  } else if (error instanceof AuthenticationError) {
    console.error('Invalid master key');
  } else if (error instanceof RateLimitError) {
    console.error(`Rate limited. Retry after ${error.retryAfter}s`);
  } else if (error instanceof ValidationError) {
    console.error('Validation failed:', error.details);
  } else if (error instanceof NotImplementedError) {
    console.error('Feature requires Admin API implementation');
  }
}
```

### .NET Error Handling

```csharp
try
{
    var virtualKey = await _adminApiClient.GetVirtualKeyByIdAsync(id);
    return virtualKey;
}
catch (HttpRequestException ex) when (ex.Message.Contains("404"))
{
    _logger.LogWarning("Virtual key {KeyId} not found", id);
    return null;
}
catch (HttpRequestException ex) when (ex.Message.Contains("401"))
{
    _logger.LogError("Authentication failed for Admin API request");
    throw new UnauthorizedAccessException("Invalid master key");
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to retrieve virtual key {KeyId}", id);
    return null; // Graceful degradation
}
```

### HTTP Status Code Handling

| Status Code | Meaning | Client Behavior |
|-------------|---------|-----------------|
| 200 | Success | Return data |
| 201 | Created | Return created resource |
| 400 | Bad Request | Throw ValidationError |
| 401 | Unauthorized | Throw AuthenticationError |
| 403 | Forbidden | Throw AuthorizationError |
| 404 | Not Found | Return null/NotFoundError |
| 429 | Rate Limited | Throw RateLimitError with retry info |
| 500 | Internal Error | Log and retry/fail gracefully |
| 501 | Not Implemented | Throw NotImplementedError |

---

## Best Practices

### Client Configuration

1. **Connection Pooling**: Use singleton clients to share HTTP connections
2. **Timeout Configuration**: Set appropriate timeouts for your use case
3. **Retry Logic**: Implement exponential backoff for transient failures
4. **Caching**: Cache frequently accessed data with appropriate TTL

### Performance Optimization

1. **Pagination**: Use pagination for large data sets
2. **Filtering**: Apply server-side filtering to reduce data transfer
3. **Batch Operations**: Use bulk APIs when available
4. **Connection Management**: Reuse HTTP clients and connections

### Development Workflow

1. **Environment Separation**: Use different API URLs for dev/staging/production
2. **Logging**: Implement comprehensive logging for debugging
3. **Testing**: Mock API clients for unit testing
4. **Monitoring**: Track API performance and error rates

### Code Organization

#### TypeScript Project Structure

```
src/
├── clients/
│   ├── admin-client.ts          # Client initialization
│   └── types.ts                 # Type definitions
├── services/
│   ├── virtual-keys.service.ts  # Business logic
│   ├── providers.service.ts
│   └── analytics.service.ts
├── utils/
│   ├── error-handler.ts         # Error handling utilities
│   └── logger.ts                # Logging utilities
└── config/
    └── environment.ts           # Environment configuration
```

#### .NET Project Structure

```
MyProject/
├── Clients/
│   ├── IAdminApiClient.cs       # Client interface
│   ├── AdminApiClient.cs        # Client implementation
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs
├── Services/
│   ├── VirtualKeyService.cs     # Business logic
│   └── ProviderService.cs
├── Models/
│   └── Dtos/                    # Data transfer objects
└── Configuration/
    └── AdminApiOptions.cs       # Configuration options
```

---

## Deployment Considerations

### Local Development

For local development, run both services on the same machine:

```bash
# Terminal 1: Start Admin API
cd ConduitLLM.Admin
dotnet run

# Terminal 2: Start WebUI (with API access)
cd ConduitLLM.WebUI
CONDUIT_USE_ADMIN_API=true \
CONDUIT_ADMIN_API_URL=http://localhost:5002 \
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your-key \
npm run dev

# Or WebUI with direct DB access
CONDUIT_USE_ADMIN_API=false npm run dev
```

### Docker Environment

Deploy services in separate containers with proper networking:

```yaml
version: '3.8'

services:
  admin:
    image: conduit-admin:latest
    environment:
      - AdminApi__MasterKey=production-master-key
      - AdminApi__AllowedOrigins__0=http://webui:8080
      - ConnectionStrings__DefaultConnection=Host=db;Database=conduit;Username=conduit;Password=password
    ports:
      - "5002:8080"
    depends_on:
      - db
    networks:
      - conduit-network

  webui:
    image: conduit-webui:latest
    environment:
      - CONDUIT_ADMIN_API_URL=http://admin:8080
      - CONDUIT_API_TO_API_BACKEND_AUTH_KEY=production-master-key
      - CONDUIT_API_TO_API_BACKEND_AUTH_KEY=backend-key
      - NODE_ENV=production
    ports:
      - "3000:3000"
    depends_on:
      - admin
    networks:
      - conduit-network

  db:
    image: postgres:15
    environment:
      - POSTGRES_DB=conduit
      - POSTGRES_USER=conduit
      - POSTGRES_PASSWORD=password
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - conduit-network

networks:
  conduit-network:
    driver: bridge

volumes:
  postgres-data:
```

### Production Considerations

1. **Load Balancing**: Use load balancers for high availability
2. **SSL/TLS**: Enable HTTPS with proper certificates
3. **Monitoring**: Implement health checks and monitoring
4. **Backup**: Regular database and configuration backups
5. **Security**: Network isolation, firewall rules, security scanning
6. **Scaling**: Horizontal scaling with Redis cache and RabbitMQ

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: conduit-admin
spec:
  replicas: 3
  selector:
    matchLabels:
      app: conduit-admin
  template:
    metadata:
      labels:
        app: conduit-admin
    spec:
      containers:
      - name: admin
        image: conduit-admin:latest
        ports:
        - containerPort: 8080
        env:
        - name: AdminApi__MasterKey
          valueFrom:
            secretKeyRef:
              name: conduit-secrets
              key: master-key
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: conduit-secrets
              key: database-connection
---
apiVersion: v1
kind: Service
metadata:
  name: conduit-admin-service
spec:
  selector:
    app: conduit-admin
  ports:
  - port: 8080
    targetPort: 8080
  type: ClusterIP
```

---

## Conclusion

This guide covers all aspects of using the Conduit Admin API clients across different platforms and deployment scenarios. Choose the appropriate client implementation based on your platform and integration requirements:

- **Use .NET Client** for ASP.NET Core applications requiring direct repository-style access
- **Use TypeScript Client** for Node.js applications, React applications, or when you need advanced features
- **Use WebUI Patterns** as a reference for Next.js/React integration
- **Use Direct API** for other platforms or custom implementations

For additional help, refer to the specific client documentation and example projects in the repository.