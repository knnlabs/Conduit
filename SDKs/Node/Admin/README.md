# Conduit Admin Client

A TypeScript client library for the Conduit Admin API, providing programmatic access to all administrative functionality.

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
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

// Initialize the client
const client = new ConduitAdminClient({
  masterKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY!,
  adminApiUrl: process.env.CONDUIT_ADMIN_API_URL!,
});

// Or use the convenience factory method
const client = ConduitAdminClient.fromEnvironment();

// Create a virtual key
const { virtualKey, keyInfo } = await client.virtualKeys.create({
  keyName: 'My API Key',
  allowedModels: 'gpt-4,gpt-3.5-turbo',
  maxBudget: 100,
  budgetDuration: 'Monthly',
});

console.log(`Created key: ${virtualKey}`);
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

### 📊 Analytics
- Cost summaries and trends
- Request logs with filtering
- Usage metrics
- Performance monitoring

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

// Providers
await client.providers.testConnection({ providerName: 'openai', apiKey: 'sk-...' });
const health = await client.providers.getHealthStatus();

// Model Mappings
await client.modelMappings.create({
  modelId: 'gpt-4',
  providerId: 'openai',
  providerModelId: 'gpt-4',
  priority: 100,
});

// Settings
await client.settings.setSetting('RATE_LIMIT_WINDOW', '60');
await client.settings.updateRouterConfiguration({ routingStrategy: 'least-cost' });

// IP Filters
await client.ipFilters.createAllowFilter('Office', '192.168.1.0/24');
const allowed = await client.ipFilters.checkIp('192.168.1.100');

// Cost Analytics
const summary = await client.analytics.getTodayCosts();
const logs = await client.analytics.getRequestLogs({ status: 'error' });

// System
const backup = await client.system.createBackup();
const health = await client.system.getHealth();
```

## Advanced Configuration

```typescript
const client = new ConduitAdminClient({
  masterKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY!,
  adminApiUrl: process.env.CONDUIT_ADMIN_API_URL!,
  options: {
    timeout: 30000, // 30 seconds
    retries: 3,
    logger: {
      debug: console.debug,
      info: console.info,
      warn: console.warn,
      error: console.error,
    },
    headers: {
      'X-Custom-Header': 'value',
    },
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

## Documentation

- [API Documentation](./docs/API.md) - Complete API reference
- [Examples](./examples) - Usage examples
- [Next.js Integration](./examples/next-app) - Next.js app example
- [Stub Functions](./docs/STUBS.md) - Features requiring API implementation

## TypeScript Support

This library is written in TypeScript and provides full type definitions for all API operations.

## Contributing

See [CONTRIBUTING.md](./CONTRIBUTING.md) for development setup and guidelines.

## License

MIT