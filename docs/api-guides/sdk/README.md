# Conduit SDK Documentation

Complete guides for using the Conduit Node.js SDKs in your applications.

## 📦 SDK Packages

Conduit provides TypeScript SDKs for both administrative and core API operations:

- **Admin SDK** (`@knn_labs/conduit-admin-client`) - Administrative operations, virtual key management, provider configuration
- **Core SDK** (`@knn_labs/conduit-gateway-client`) - LLM operations, chat completions, image generation, audio processing

## 📚 Getting Started

### Installation

```bash
# Install Admin SDK (server-side only)
npm install @knn_labs/conduit-admin-client

# Install Core SDK (client or server)
npm install @knn_labs/conduit-gateway-client

# Install Common types (shared between SDKs)
npm install @knn_labs/conduit-common
```

### Quick Example

```typescript
// Admin SDK - Server-side only
import { FetchConduitAdminClient } from '@knn_labs/conduit-admin-client';

const adminClient = new FetchConduitAdminClient({
  baseUrl: 'http://localhost:5002',
  masterKey: process.env.CONDUIT_MASTER_KEY!
});

const keys = await adminClient.virtualKeys.list();

// Core SDK - Client or server
import { ConduitCoreClient } from '@knn_labs/conduit-gateway-client';

const coreClient = new ConduitCoreClient({
  baseURL: 'http://localhost:5000',
  apiKey: 'your-virtual-key'
});

const response = await coreClient.chat.completions.create({
  model: 'gpt-4',
  messages: [{ role: 'user', content: 'Hello!' }]
});
```

## 🎯 Documentation Guides

### Integration Guides
- **[Next.js Integration](./nextjs-integration.md)** - Complete guide for integrating Conduit SDKs in Next.js applications with TypeScript, covering App Router, Pages Router, authentication, streaming, and production deployment

### SDK Reference
- **[Quick Reference](./quick-reference.md)** - Common SDK patterns and usage examples
- **[Best Practices](./best-practices.md)** - Security, performance, and code quality best practices
- **[Troubleshooting](./troubleshooting.md)** - Common issues and solutions

### Advanced Features
- **[Connection Management](./connection-management.md)** - Managing SignalR real-time connections
- **[Health Checks](./health-checks.md)** - Lightweight health check functionality

## 🔑 Admin SDK Quick Links

For detailed Admin SDK documentation, see:

- **[Admin SDK TypeScript Guide](../admin-sdk-typescript.md)** - Complete TypeScript SDK guide with setup, authentication, and examples
- **[Admin API Guide](../admin-api-guide.md)** - Admin API overview and architecture
- **[Admin Authentication](../admin-authentication.md)** - Authentication patterns and setup
- **[Admin Examples](../admin-examples.md)** - Practical code examples

## 🚀 Core SDK Quick Links

For detailed Core SDK documentation, see:

- **[Gateway API Guide](../core-api-guide.md)** - Gateway API overview and SDK introduction
- **[Gateway API Detailed](../core-api-detailed.md)** - Complete Gateway API documentation with examples
- **[Function Calling](../function-calling.md)** - How to use function calling with LLMs
- **[Multimodal Vision](../multimodal-vision.md)** - Vision and multimodal capabilities

## 🎓 Usage Patterns by Framework

### Next.js
See **[Next.js Integration Guide](./nextjs-integration.md)** for:
- App Router and Pages Router patterns
- API route setup
- Authentication with NextAuth
- State management with React Query
- Streaming responses
- Error handling and testing

### Node.js Backend
```typescript
// Server-side Node.js application
import { FetchConduitAdminClient } from '@knn_labs/conduit-admin-client';
import { ConduitCoreClient } from '@knn_labs/conduit-gateway-client';

// Initialize clients
const adminClient = new FetchConduitAdminClient({
  baseUrl: process.env.ADMIN_API_URL!,
  masterKey: process.env.MASTER_KEY!
});

const coreClient = new ConduitCoreClient({
  baseURL: process.env.CORE_API_URL!,
  apiKey: process.env.VIRTUAL_KEY!
});

// Use in your application
app.post('/api/chat', async (req, res) => {
  const completion = await coreClient.chat.completions.create({
    model: 'gpt-4',
    messages: req.body.messages
  });
  res.json(completion);
});
```

### Express.js
```typescript
import express from 'express';
import { ConduitCoreClient } from '@knn_labs/conduit-gateway-client';

const app = express();
const coreClient = new ConduitCoreClient({
  baseURL: process.env.CORE_API_URL!,
  apiKey: process.env.VIRTUAL_KEY!
});

app.post('/chat', async (req, res) => {
  try {
    const response = await coreClient.chat.completions.create(req.body);
    res.json(response);
  } catch (error) {
    res.status(500).json({ error: error.message });
  }
});
```

## 🔐 Security Best Practices

### Never Expose Admin SDK Client-Side
```typescript
// ❌ WRONG - Exposes master key
'use client';
import { FetchConduitAdminClient } from '@knn_labs/conduit-admin-client';

// ✅ CORRECT - Admin SDK only in API routes/server components
// app/api/admin/keys/route.ts
import { FetchConduitAdminClient } from '@knn_labs/conduit-admin-client';
```

### Environment Variables
```bash
# Server-side only (never expose)
CONDUIT_MASTER_KEY=your-master-key
CONDUIT_ADMIN_API_URL=http://localhost:5002

# Can be exposed client-side (prefixed with NEXT_PUBLIC_)
NEXT_PUBLIC_CORE_API_URL=http://localhost:5000
```

### Virtual Keys for Core SDK
Always use virtual keys (not master keys) for Core SDK operations:

```typescript
// ❌ WRONG - Using master key for core operations
const coreClient = new ConduitCoreClient({
  apiKey: process.env.CONDUIT_MASTER_KEY
});

// ✅ CORRECT - Using virtual key
const coreClient = new ConduitCoreClient({
  apiKey: process.env.CONDUIT_VIRTUAL_KEY
});
```

## 🧪 Testing

### Mocking SDK Clients

```typescript
// __tests__/utils/mock-clients.ts
export const mockAdminClient = {
  virtualKeys: {
    list: jest.fn(),
    get: jest.fn(),
    create: jest.fn(),
    update: jest.fn(),
    delete: jest.fn(),
  },
  providers: {
    list: jest.fn(),
    testConnection: jest.fn(),
  },
};

// In your tests
import { mockAdminClient } from '@/tests/utils/mock-clients';

jest.mock('@knn_labs/conduit-admin-client', () => ({
  FetchConduitAdminClient: jest.fn(() => mockAdminClient),
}));

test('fetches virtual keys', async () => {
  mockAdminClient.virtualKeys.list.mockResolvedValue([
    { id: 1, keyName: 'Test Key' }
  ]);

  // Your test code
});
```

## 🆘 Getting Help

- **[Troubleshooting Guide](./troubleshooting.md)** - Common issues and solutions
- **[GitHub Issues](https://github.com/nickna/Conduit/issues)** - Report bugs or request features

## 📖 Related Documentation

- **[Gateway API Reference](../gateway/api-reference.md)** - Gateway endpoint documentation
- **[Admin API Reference](../admin/api-reference.md)** - Admin endpoint documentation
- **[Architecture](../../architecture/)** - System design and patterns
- **[Development](../../development/)** - Contributing to Conduit codebase

---

**Note**: All SDK packages follow semantic versioning. Check package versions for compatibility.
