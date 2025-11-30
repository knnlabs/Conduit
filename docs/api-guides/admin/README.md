# Admin API Overview

The ConduitLLM Admin API provides administrative endpoints for managing and configuring the Conduit platform. It is separate from the Core LLM API and is used by the WebAdmin and other admin tools.

## Quick Navigation

- **[Getting Started](./getting-started.md)** - Authentication, setup, and quick start guide
- **[TypeScript SDK](./typescript-sdk.md)** - Complete SDK guide with examples
- **[API Reference](./api-reference.md)** - Complete endpoint documentation

## What You Can Do

- **Virtual Keys**: Create and manage API keys with budgets and rate limits
- **Provider Configuration**: Add and configure LLM providers (OpenAI, Anthropic, etc.)
- **Model Mappings**: Route model requests to specific providers
- **Usage Monitoring**: Track costs, usage, and request logs
- **IP Filtering**: Control network access to the platform
- **System Management**: Global settings and system configuration

## Architecture

The Admin API is designed as a separate service with these key components:

```
WebAdmin / Admin Tools
       ↓
Admin API Client
       ↓
Admin API (HTTP)
       ↓
Business Logic Services
       ↓
PostgreSQL Database
```

### Benefits

1. **Decoupled Components**: Independent development and deployment
2. **Clean API Contracts**: Well-defined interfaces between components
3. **Enhanced Security**: Administrative functions isolated from user-facing API
4. **Flexible Deployment**: Different deployment scenarios for different environments

## Base URL

- **Development**: `http://localhost:5002/api`
- **Production**: Configure based on your deployment

## Authentication

All Admin API requests require authentication using the master key:

```http
Authorization: Bearer your_master_key_here
```

Or using the `X-API-Key` header:

```http
X-API-Key: your_master_key_here
```

See [Getting Started](./getting-started.md) for detailed authentication setup.

## Available SDKs

### TypeScript/Node.js

```bash
npm install @knn_labs/conduit-admin-client
```

```typescript
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

const adminClient = new ConduitAdminClient({
  masterKey: 'your_master_key_here',
  adminApiUrl: 'http://localhost:5002'
});

// Manage virtual keys
const keys = await adminClient.virtualKeys.list();
```

See [TypeScript SDK Guide](./typescript-sdk.md) for complete documentation.

## Main Features

### Virtual Keys Management

Create and manage API keys for users and applications:

- Set budgets and track spending
- Configure rate limits (RPM/RPD)
- Define allowed models
- Set expiration dates
- Add custom metadata

### Provider Configuration

Configure LLM providers and credentials:

- Add multiple providers (OpenAI, Anthropic, Groq, etc.)
- Manage API keys securely
- Configure provider-specific settings
- Monitor provider health
- Test provider connectivity

### Model Mappings

Control how model requests are routed:

- Map model aliases to provider models
- Configure multiple providers per model (failover)
- Set priority for load balancing
- Enable/disable specific mappings

### Usage Analytics

Monitor platform usage and costs:

- Real-time usage tracking
- Cost analytics by model, key, or provider
- Request logs with detailed metadata
- Budget alerts and notifications

### IP Filtering

Control access based on IP addresses:

- Allowlist/blocklist IP ranges
- CIDR notation support
- Per-key IP restrictions
- Global IP filtering

## Docker Compose Example

```yaml
services:
  admin:
    image: ghcr.io/knnlabs/conduit-admin:latest
    environment:
      DATABASE_URL: postgresql://user:password@postgres:5432/conduitdb
      CONDUIT_API_TO_API_BACKEND_AUTH_KEY: your_master_key
    ports:
      - "5002:8080"
    depends_on:
      - postgres

  webadmin:
    image: ghcr.io/knnlabs/conduit-webadmin:latest
    environment:
      CONDUIT_ADMIN_API_URL: http://admin:8080
      CONDUIT_API_TO_API_BACKEND_AUTH_KEY: your_master_key
      CONDUIT_USE_ADMIN_API: "true"
    ports:
      - "3000:8080"
    depends_on:
      - admin
```

## Security Considerations

- **Master Key Authentication**: Use strong, randomly generated keys
- **Network Isolation**: Restrict Admin API access in production
- **HTTPS Only**: Always use HTTPS in production environments
- **API Key Management**: Securely handle provider credentials
- **Audit Logging**: Monitor all administrative operations

## Related Documentation

- **[Gateway API](../core/)** - User-facing LLM API documentation
- **[SDK Documentation](../sdk/)** - SDK integration guides
- **[Architecture Docs](../../architecture/)** - System design and patterns
- **[Development Guide](../../development/)** - Contributing to Conduit

## Support

For questions or issues:
- **GitHub Issues**: https://github.com/knnlabs/Conduit/issues
- **Documentation**: https://docs.conduit.ai
