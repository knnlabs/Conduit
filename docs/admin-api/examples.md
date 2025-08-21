# Admin API TypeScript Examples

A comprehensive collection of TypeScript examples for the Conduit Admin API, split into focused guides for easier navigation and maintenance.

## Documentation Structure

The Admin API examples have been organized into the following focused guides:

### 🚀 Getting Started
- **[TypeScript Setup](./typescript-setup.md)** - Authentication, types, and basic configuration
- **[TypeScript Client](./typescript-client.md)** - Complete client implementation with error handling

### 🔑 Core Features
- **[Virtual Keys Management](./typescript-virtual-keys.md)** - Create, manage, and monitor virtual keys
- **[Provider Management](./typescript-providers.md)** - Configure AI providers, model mappings, and IP filtering

### 📊 Analytics & Monitoring
- **[Analytics Guide](./typescript-analytics.md)** - Request logs, cost analytics, and usage monitoring
- **[Advanced Patterns](./typescript-advanced.md)** - Error handling, production patterns, and best practices

## Quick Start

1. **Setup your environment** - Follow the [TypeScript Setup Guide](./typescript-setup.md)
2. **Initialize the client** - Use the [TypeScript Client](./typescript-client.md) 
3. **Manage virtual keys** - Start with [Virtual Keys Management](./typescript-virtual-keys.md)
4. **Configure providers** - Set up AI providers with [Provider Management](./typescript-providers.md)

## Example Workflow

```typescript
import { ConduitAdminApiClient } from './typescript-client';

// Initialize client
const adminClient = new ConduitAdminApiClient(
    'http://localhost:5002',
    'your_master_key_here'
);

// Create a virtual key
const keyResponse = await adminClient.createVirtualKey({
    keyName: 'Development Key',
    maxBudget: 100.00,
    budgetDuration: 'Monthly'
});

// Add a provider
const provider = await adminClient.createProviderCredential({
    providerName: 'openai',
    apiKey: 'sk-...',
    isEnabled: true
});

// Create model mapping
await adminClient.createModelProviderMapping({
    modelId: 'gpt-4',
    providerId: provider.id.toString(),
    providerModelId: 'gpt-4',
    isEnabled: true,
    priority: 100
});
```

## Features Covered

- ✅ **Authentication** - Both X-API-Key and Bearer token methods
- ✅ **Virtual Keys** - Full CRUD operations, validation, budget management
- ✅ **Providers** - Add/remove providers, health monitoring, connection testing
- ✅ **Model Mappings** - Route models to providers with priority settings
- ✅ **IP Filtering** - Access control with allowlists and blocklists
- ✅ **Cost Management** - Budget tracking, cost analytics, usage reports
- ✅ **Request Logs** - Query logs, filter by date/model/key, export data
- ✅ **Audio Configuration** - Audio providers, costs, usage tracking
- ✅ **System Management** - Health checks, backups, system information
- ✅ **Error Handling** - Retry logic, circuit breakers, comprehensive error handling

## TypeScript Support

All examples include:
- **Full Type Safety** - Complete TypeScript interfaces for all API operations
- **Generic Types** - Properly typed responses and parameters
- **Error Types** - Typed error handling with HTTP status codes
- **IntelliSense Support** - Auto-completion for all methods and properties

## Production Ready

These examples are designed for production use and include:
- **Retry Logic** - Automatic retry with exponential backoff
- **Error Handling** - Comprehensive error handling with recovery strategies
- **Rate Limiting** - Respect for API rate limits with automatic backoff
- **Logging** - Structured logging for debugging and monitoring
- **Security** - Proper authentication and credential management

## Contributing

When adding new examples:
1. Follow the established patterns in each guide
2. Include comprehensive error handling
3. Add TypeScript type annotations
4. Provide usage examples
5. Update the appropriate guide's table of contents

## Support

For questions or issues:
- Check the specific guide for your use case
- Review the [Advanced Patterns](./typescript-advanced.md) for complex scenarios
- Refer to the [TypeScript Client](./typescript-client.md) for implementation details