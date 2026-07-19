# Conduit Admin SDK

Official TypeScript/JavaScript client for the Conduit Admin API, providing programmatic access to all administrative functionality.

> **Note**: This client is part of the Conduit multi-platform SDK collection located at `SDKs/Node/Admin/`. For other platforms (Python, Go, .NET), see the [main SDKs directory](../../README.md).

## Installation

```bash
npm install @knn_labs/conduit-admin-client
# or
yarn add @knn_labs/conduit-admin-client
# or
pnpm add @knn_labs/conduit-admin-client
```

## Quick Start

```typescript
import { FetchConduitAdminClient } from '@knn_labs/conduit-admin-client';

// Initialize the client
const client = new FetchConduitAdminClient({
  masterKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY!,
  adminApiUrl: process.env.CONDUIT_ADMIN_API_URL!,
});

// Or use the convenience factory method
const client = FetchConduitAdminClient.fromEnvironment();

// Create a virtual key
const virtualKey = await client.virtualKeys.create({
  keyName: 'My API Key',
  allowedModels: 'gpt-4,gpt-3.5-turbo',
  maxBudget: 100,
  budgetDuration: 'Monthly',
});

console.log(`Created key: ${virtualKey.id}`);

// Get analytics
const metrics = await client.analytics.getUsageMetrics({
  startDate: '2025-01-01',
  endDate: '2025-01-31'
});
```

## Environment Variables

The client expects the following environment variables:

- `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` - Your Conduit master API key (required)
- `CONDUIT_ADMIN_API_URL` - The Admin API base URL (required)
- `CONDUIT_API_URL` - The Conduit API base URL (optional)

## Features

### 🔑 Virtual Key Management
- Full CRUD operations for API keys
- Budget tracking and spend management
- Refund functionality with audit trail
- Rate limiting configuration
- Key validation and search

### 🔌 Provider Management
- Configure LLM provider credentials
- Test connections
- Health monitoring
- Automatic failover configuration

### 🔄 Model Mappings
- Route models to providers
- Priority-based routing
- Load balancing configuration

### ⚙️ Settings Management
- Global configuration
- Audio provider settings
- Router configuration
- Custom settings with categories

### 🛡️ IP Filtering
- Allow/deny lists
- CIDR range support
- Bypass rules for admin UI

### 💰 Cost Management
- Model pricing configuration
- Cost calculations
- Budget alerts
- Usage tracking

### 📊 Analytics & Metrics
- Cost summaries and trends
- Request logs with filtering
- Usage metrics
- Performance monitoring
- System health metrics
- Database pool metrics

### 🖥️ System Management
- Health checks
- Backup and restore
- Notifications
- Maintenance tasks

## Service Examples

```typescript
// Virtual Keys
const keys = await client.virtualKeys.list({ isEnabled: true });
await client.virtualKeys.update(keyId, { maxBudget: 500 });
const usage = await client.virtualKeys.getUsage(keyId);

// Providers
const providers = await client.providers.list();
await client.providers.addKey(providerId, {
  key: 'sk-...',
  providerAccountGroup: 'production'
});
const health = await client.providers.getHealth(providerId);

// Model Mappings
await client.modelMappings.create({
  modelProviderId: providerId,
  modelId: modelId,
  priority: 100,
});

// Settings
await client.settings.updateSetting('RATE_LIMIT_WINDOW', '60');
const config = await client.settings.getRouterConfiguration();

// IP Filters
await client.ipFilter.createAllowFilter('Office', '192.168.1.0/24');
const allowed = await client.ipFilter.checkIp('192.168.1.100');

// Model Costs
const cost = await client.modelCost.create({
  costName: 'GPT-4 Standard',
  modelProviderMappingIds: [1, 2, 3],
  inputCostPerMillionTokens: 30.0,
  outputCostPerMillionTokens: 60.0
});

// Analytics
const costMetrics = await client.analytics.getCostMetrics({
  startDate: '2025-01-01',
  endDate: '2025-01-31'
});
const logs = await client.analytics.getRequestLogs({ status: 'error' });

// Metrics
const systemMetrics = await client.metrics.getAllMetrics();
const dbMetrics = await client.metrics.getDatabasePoolMetrics();
const memory = await client.metrics.getMemoryUsage();

// Notifications
const unread = await client.notifications.getUnreadNotifications();
await client.notifications.markAsRead(notificationId);
const stats = await client.notifications.getNotificationStatistics();

// System
const backup = await client.system.createBackup({ includeMedia: false });
const health = await client.system.getHealth();
const info = await client.system.getInfo();
```

## Advanced Configuration

```typescript
const client = new FetchConduitAdminClient({
  masterKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY!,
  adminApiUrl: process.env.CONDUIT_ADMIN_API_URL!,
  options: {
    // Request timeout (default: 30000ms)
    timeout: 60000,

    // Retry configuration
    retries: {
      maxRetries: 3,
      retryDelay: 1000,
      retryCondition: (error) => error.code === 'NETWORK_ERROR'
    },

    // Custom headers
    headers: {
      'X-Custom-Header': 'value',
    },

    // Request/response callbacks
    onRequest: (config) => {
      console.log('Request:', config.url);
    },
    onResponse: (response) => {
      console.log('Response:', response.status);
    },
    onError: (error) => {
      console.error('Error:', error.message);
    },

    // Custom logger
    logger: {
      debug: console.debug,
      info: console.info,
      warn: console.warn,
      error: console.error,
    },

    // Cache provider (optional)
    cache: {
      get: async (key) => { /* ... */ },
      set: async (key, value, ttl) => { /* ... */ },
      delete: async (key) => { /* ... */ },
      clear: async () => { /* ... */ }
    }
  },
});
```

## Error Handling

The client provides typed error classes for different scenarios:

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
  } else if (error instanceof NotImplementedError) {
    console.error('This feature requires Admin API implementation');
  }
}
```

## TypeScript Support

This library is written in TypeScript and provides full type definitions for all API operations:

```typescript
import type {
  VirtualKeyDto,
  CreateVirtualKeyDto,
  ProviderDto,
  ModelCostDto,
  UsageMetricsDto
} from '@knn_labs/conduit-admin-client';

const createKey = async (): Promise<VirtualKeyDto> => {
  const request: CreateVirtualKeyDto = {
    keyName: 'my-key',
    allowedModels: 'gpt-4'
  };
  return await client.virtualKeys.create(request);
};
```

## Available Services

All services are accessed through the client instance:

- `client.virtualKeys` - Virtual key management
- `client.providers` - Provider configuration
- `client.analytics` - Usage and cost analytics
- `client.metrics` - System performance metrics
- `client.notifications` - System notifications
- `client.system` - System health and configuration
- `client.settings` - Application settings
- `client.security` - Security and audit logs
- `client.configuration` - Cache, routing, and performance config
- `client.monitoring` - System monitoring and alerts
- `client.ipFilter` - IP whitelist/blacklist management
- `client.media` - Media file management
- `client.modelCost` - Model pricing configuration
- `client.models` - Model management
- `client.modelSeries` - Model series management
- `client.modelAuthors` - Model author management
- `client.modelMappings` - Model-to-provider mappings

## Next.js Integration

For Next.js applications, use the optimized Next.js export:

```typescript
import { FetchConduitAdminClient } from '@knn_labs/conduit-admin-client/nextjs';

// Server Components
export default async function Page() {
  const client = new FetchConduitAdminClient({
    masterKey: process.env.CONDUIT_MASTER_KEY!,
    adminApiUrl: process.env.NEXT_PUBLIC_ADMIN_API_URL!
  });

  const virtualKeys = await client.virtualKeys.list();
  return <div>{/* render keys */}</div>;
}

// API Routes
export async function GET() {
  const client = new FetchConduitAdminClient({
    masterKey: process.env.CONDUIT_MASTER_KEY!,
    adminApiUrl: process.env.NEXT_PUBLIC_ADMIN_API_URL!
  });

  const data = await client.analytics.getUsageMetrics({
    startDate: '2025-01-01',
    endDate: '2025-01-31'
  });

  return Response.json(data);
}
```

## Migration from v1.x

See [MIGRATION.md](./MIGRATION.md) for detailed migration instructions from v1.x to v2.0.

**Key breaking changes in v2.0:**
- Removed legacy service classes (use `client.serviceName` pattern instead)
- Removed Zod dependency (validation is now built-in)
- Removed backward compatibility service aliases (`ModelCostService`, `SecurityService`, `ConfigurationService`)
- Simplified export surface (internal utilities no longer exported)
- Consolidated constants (6 files → 1)

## Documentation

- [Migration Guide](./MIGRATION.md) - v1.x to v2.0 migration
- [Examples](./examples) - Usage examples
- [Next.js Integration](./examples/next-app) - Next.js app example
- [Stub Functions](./docs/STUBS.md) - Features requiring API implementation

## Contributing

See [CONTRIBUTING.md](./CONTRIBUTING.md) for development setup and guidelines.

## License

MIT - see LICENSE file for details

## Support

- GitHub Issues: https://github.com/nickna/Conduit/issues
- Documentation: https://github.com/nickna/Conduit/tree/master/SDKs/Node/Admin
