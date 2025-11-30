# Admin SDK TypeScript Guide

Complete guide for using the Conduit Admin SDK with TypeScript, including setup, authentication, and comprehensive examples.

## Table of Contents

1. [Quick Start](#quick-start)
2. [Setup & Configuration](#setup--configuration)
3. [Authentication](#authentication)
4. [TypeScript Type Definitions](#typescript-type-definitions)
5. [Virtual Key Management](#virtual-key-management)
6. [Provider Configuration](#provider-configuration)
7. [Advanced Features](#advanced-features)
8. [Production Best Practices](#production-best-practices)

---

## Quick Start

### Installation

```bash
npm install @knn_labs/conduit-admin-client
```

### Basic Usage

```typescript
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

const client = new ConduitAdminClient({
  baseUrl: 'http://localhost:5002',
  masterKey: process.env.CONDUIT_MASTER_KEY!
});

// Get all virtual keys
const keys = await client.virtualKeys.list();

// Create a new virtual key
const newKey = await client.virtualKeys.create({
  keyName: 'My Application',
  maxBudget: 100.00,
  allowedModels: 'gpt-4*,claude-*'
});
```

---

## Setup & Configuration

### Node.js Environment

Install required dependencies:

```bash
npm install @knn_labs/conduit-admin-client
npm install --save-dev typescript @types/node
```

### Environment Variables

Create a `.env` file:

```env
CONDUIT_ADMIN_API_URL=http://localhost:5002
CONDUIT_MASTER_KEY=your_master_key_here
NODE_ENV=development
```

### TypeScript Configuration

Recommended `tsconfig.json`:

```json
{
  "compilerOptions": {
    "target": "ES2020",
    "module": "commonjs",
    "lib": ["ES2020"],
    "outDir": "./dist",
    "rootDir": "./src",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "forceConsistentCasingInFileNames": true,
    "moduleResolution": "node",
    "resolveJsonModule": true
  },
  "include": ["src/**/*"],
  "exclude": ["node_modules", "dist"]
}
```

---

## Authentication

All Admin API requests require authentication using the master key.

### Primary Authentication (Recommended)

```typescript
const client = new ConduitAdminClient({
  baseUrl: 'http://localhost:5002',
  masterKey: process.env.CONDUIT_MASTER_KEY!
});
```

The SDK automatically handles authentication headers:
- Uses `X-API-Key` header by default
- Falls back to `Authorization: Bearer` for compatibility

### Manual Authentication (Lower-Level API)

If not using the SDK:

```typescript
const headers = {
  'X-API-Key': process.env.CONDUIT_MASTER_KEY!,
  'Content-Type': 'application/json'
};

const response = await fetch('http://localhost:5002/api/virtualkeys', {
  headers
});
```

---

## TypeScript Type Definitions

The SDK provides full TypeScript types for all operations. Key interfaces:

### Virtual Key Types

```typescript
interface VirtualKeyDto {
  id: number;
  keyName: string;
  allowedModels: string;
  maxBudget: number;
  currentSpend: number;
  budgetDuration: 'Daily' | 'Weekly' | 'Monthly';
  budgetStartDate: string;
  isEnabled: boolean;
  expiresAt?: string;
  createdAt: string;
  updatedAt: string;
  metadata?: string;
  rateLimitRpm?: number;
  rateLimitRpd?: number;
}

interface CreateVirtualKeyRequest {
  keyName: string;
  allowedModels?: string;
  maxBudget?: number;
  budgetDuration?: 'Daily' | 'Weekly' | 'Monthly';
  expiresAt?: string;
  metadata?: string;
  rateLimitRpm?: number;
  rateLimitRpd?: number;
}
```

### Provider Types

```typescript
interface ProviderDto {
  id: number;
  name: string;
  providerType: string;
  isEnabled: boolean;
  priority: number;
  createdAt: string;
  updatedAt: string;
}

interface ProviderCredentialDto {
  id: number;
  providerId: number;
  apiKey?: string;
  apiEndpoint?: string;
  organizationId?: string;
  isEnabled: boolean;
}
```

### Model Mapping Types

```typescript
interface ModelProviderMappingDto {
  id: number;
  modelAlias: string;
  providerId: number;
  providerModelId: string;
  isEnabled: boolean;
  priority: number;
}
```

---

## Virtual Key Management

### List All Virtual Keys

```typescript
const keys = await client.virtualKeys.list();

keys.forEach(key => {
  console.log(`${key.keyName}: $${key.currentSpend}/${key.maxBudget}`);
});
```

### Create a Virtual Key

```typescript
const newKey = await client.virtualKeys.create({
  keyName: 'Production App',
  allowedModels: 'gpt-4*,claude-*',
  maxBudget: 500.00,
  budgetDuration: 'Monthly',
  rateLimitRpm: 60,
  rateLimitRpd: 10000,
  metadata: JSON.stringify({ team: 'engineering', project: 'api' })
});

console.log(`Created key: ${newKey.virtualKey}`);
console.log(`Key ID: ${newKey.keyInfo.id}`);
```

### Update a Virtual Key

```typescript
await client.virtualKeys.update(keyId, {
  maxBudget: 1000.00,
  allowedModels: 'gpt-4*,claude-*,gemini-*',
  isEnabled: true
});
```

### Delete a Virtual Key

```typescript
await client.virtualKeys.delete(keyId);
```

### Get Usage Statistics

```typescript
const key = await client.virtualKeys.get(keyId);
const usagePercent = (key.currentSpend / key.maxBudget) * 100;

console.log(`Usage: ${usagePercent.toFixed(2)}%`);
console.log(`Spend: $${key.currentSpend} / $${key.maxBudget}`);
```

### Validate a Virtual Key

```typescript
const validation = await client.virtualKeys.validate('condt_yourkeyhere');

if (validation.isValid) {
  console.log(`Valid key for: ${validation.keyName}`);
  console.log(`Allowed models: ${validation.allowedModels?.join(', ')}`);
} else {
  console.log(`Invalid: ${validation.reason}`);
}
```

---

## Provider Configuration

### List Providers

```typescript
const providers = await client.providers.list();

providers.forEach(provider => {
  console.log(`${provider.name} (${provider.providerType}): ${provider.isEnabled ? 'enabled' : 'disabled'}`);
});
```

### Create a Provider

```typescript
const provider = await client.providers.create({
  name: 'Production OpenAI',
  providerType: 'OpenAI',
  isEnabled: true,
  priority: 100
});
```

### Add Provider Credentials

```typescript
const credential = await client.providers.addCredential(providerId, {
  apiKey: process.env.OPENAI_API_KEY!,
  apiEndpoint: 'https://api.openai.com/v1',
  isEnabled: true
});
```

### Test Provider Connection

```typescript
const result = await client.providers.testConnection(providerId);

if (result.success) {
  console.log(`Connected to ${result.providerName}`);
  console.log(`Available models: ${result.modelsAvailable?.join(', ')}`);
} else {
  console.error(`Connection failed: ${result.message}`);
}
```

### Configure Model Mappings

```typescript
// Map "gpt-4" to a specific provider
const mapping = await client.modelMappings.create({
  modelAlias: 'gpt-4',
  providerId: openaiProviderId,
  providerModelId: 'gpt-4-0613',
  isEnabled: true,
  priority: 100
});
```

---

## Advanced Features

### Health Monitoring

```typescript
// Configure provider health checks
await client.providers.configureHealthCheck(providerId, {
  isEnabled: true,
  checkIntervalSeconds: 300,
  timeoutSeconds: 30,
  unhealthyThreshold: 3,
  healthyThreshold: 2,
  testModel: 'gpt-3.5-turbo'
});

// Get health status
const health = await client.providers.getHealthStatus(providerId);
console.log(`Provider health: ${health.isHealthy ? 'healthy' : 'unhealthy'}`);
```

### Cost Management

```typescript
// Get cost analytics
const dashboard = await client.analytics.getCostDashboard({
  startDate: '2025-01-01',
  endDate: '2025-01-31'
});

console.log(`Total cost: $${dashboard.totalCost}`);
console.log(`Total requests: ${dashboard.totalRequests}`);

// Top models by cost
dashboard.costsByModel.forEach(item => {
  console.log(`  ${item.model}: $${item.cost}`);
});
```

### Request Logs

```typescript
// Query recent logs
const logs = await client.logs.query({
  startDate: new Date(Date.now() - 24 * 60 * 60 * 1000), // Last 24 hours
  endDate: new Date(),
  virtualKeyId: keyId,
  pageSize: 100
});

logs.items.forEach(log => {
  console.log(`${log.modelId}: ${log.totalTokens} tokens, $${log.cost}`);
});
```

### IP Filtering

```typescript
// Add IP filter
await client.ipFilters.create({
  name: 'Office Network',
  cidrRange: '203.0.113.0/24',
  filterType: 'Allow',
  isEnabled: true,
  description: 'Main office IP range'
});

// Check if IP is allowed
const check = await client.ipFilters.checkIp('203.0.113.50');
console.log(`IP allowed: ${check.isAllowed}`);
```

---

## Production Best Practices

### Error Handling

```typescript
import { ConduitAdminClient, ConduitError } from '@knn_labs/conduit-admin-client';

try {
  const keys = await client.virtualKeys.list();
} catch (error) {
  if (error instanceof ConduitError) {
    console.error(`API Error ${error.status}: ${error.message}`);
    if (error.status === 401) {
      console.error('Authentication failed - check master key');
    } else if (error.status === 429) {
      console.error('Rate limited - retry later');
    }
  } else {
    console.error('Unexpected error:', error);
  }
}
```

### Retry Logic

The SDK includes automatic retry logic for transient failures:

```typescript
const client = new ConduitAdminClient({
  baseUrl: 'http://localhost:5002',
  masterKey: process.env.CONDUIT_MASTER_KEY!,
  retryConfig: {
    maxRetries: 3,
    retryDelay: 1000,
    backoffMultiplier: 2
  }
});
```

### Connection Pooling

Reuse the client instance to benefit from HTTP connection pooling:

```typescript
// Create once, use everywhere
export const adminClient = new ConduitAdminClient({
  baseUrl: process.env.CONDUIT_ADMIN_API_URL!,
  masterKey: process.env.CONDUIT_MASTER_KEY!
});

// Use in multiple modules
import { adminClient } from './config';
const keys = await adminClient.virtualKeys.list();
```

### Security Best Practices

1. **Never hardcode credentials**
   ```typescript
   // ❌ BAD
   const client = new ConduitAdminClient({
     masterKey: 'actual-key-here'
   });
   
   // ✅ GOOD
   const client = new ConduitAdminClient({
     masterKey: process.env.CONDUIT_MASTER_KEY!
   });
   ```

2. **Use environment-specific URLs**
   ```typescript
   const baseUrl = process.env.NODE_ENV === 'production'
     ? 'https://admin.conduit.production.com'
     : 'http://localhost:5002';
   ```

3. **Implement proper logging**
   ```typescript
   const client = new ConduitAdminClient({
     baseUrl: process.env.CONDUIT_ADMIN_API_URL!,
     masterKey: process.env.CONDUIT_MASTER_KEY!,
     onRequest: (url, options) => {
       console.log(`API Request: ${options.method} ${url}`);
     },
     onResponse: (url, response) => {
       console.log(`API Response: ${response.status} ${url}`);
     }
   });
   ```

### Monitoring

Set up monitoring for production usage:

```typescript
// Health check endpoint
app.get('/health', async (req, res) => {
  try {
    const info = await adminClient.system.getInfo();
    res.json({ status: 'healthy', version: info.version });
  } catch (error) {
    res.status(503).json({ status: 'unhealthy', error: error.message });
  }
});

// Metrics collection
setInterval(async () => {
  try {
    const dashboard = await adminClient.analytics.getCostDashboard({
      startDate: new Date(Date.now() - 60 * 60 * 1000), // Last hour
      endDate: new Date()
    });
    metrics.recordCost(dashboard.totalCost);
    metrics.recordRequests(dashboard.totalRequests);
  } catch (error) {
    console.error('Metrics collection failed:', error);
  }
}, 60000); // Every minute
```

---

## Complete Example: Virtual Key Lifecycle

```typescript
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

const client = new ConduitAdminClient({
  baseUrl: process.env.CONDUIT_ADMIN_API_URL!,
  masterKey: process.env.CONDUIT_MASTER_KEY!
});

async function manageVirtualKey() {
  // 1. Create a new virtual key
  const created = await client.virtualKeys.create({
    keyName: 'Demo Application',
    allowedModels: 'gpt-4*,claude-*',
    maxBudget: 100.00,
    budgetDuration: 'Monthly',
    rateLimitRpm: 60,
    metadata: JSON.stringify({ environment: 'production' })
  });
  
  console.log(`Created virtual key: ${created.virtualKey}`);
  const keyId = created.keyInfo.id;
  
  // 2. Validate the key works
  const validation = await client.virtualKeys.validate(created.virtualKey);
  console.log(`Key valid: ${validation.isValid}`);
  
  // 3. Monitor usage
  const key = await client.virtualKeys.get(keyId);
  console.log(`Current spend: $${key.currentSpend} / $${key.maxBudget}`);
  
  // 4. Update if needed
  if (key.currentSpend > key.maxBudget * 0.8) {
    await client.virtualKeys.update(keyId, {
      maxBudget: key.maxBudget * 1.5
    });
    console.log('Increased budget by 50%');
  }
  
  // 5. Get usage logs
  const logs = await client.logs.query({
    virtualKeyId: keyId,
    startDate: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000), // Last 7 days
    endDate: new Date(),
    pageSize: 100
  });
  
  console.log(`Total requests: ${logs.totalCount}`);
  console.log(`Average cost: $${logs.items.reduce((sum, l) => sum + l.cost, 0) / logs.items.length}`);
  
  // 6. Clean up (if demo)
  // await client.virtualKeys.delete(keyId);
}

manageVirtualKey().catch(console.error);
```

---

## Related Documentation

- **[Gateway API Guide](./core-api-guide.md)** - Using the Core LLM API
- **[Admin API Reference](../api-reference/admin-api-endpoints.md)** - Complete endpoint reference
- **[Admin Architecture](../architecture/admin-api-integration.md)** - Integration patterns
- **[SDK Documentation](../development/)** - SDK best practices

---

## Support

For issues or questions:
- GitHub Issues: https://github.com/knnlabs/Conduit/issues
- Documentation: https://docs.conduit.ai
