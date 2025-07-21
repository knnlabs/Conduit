# SDK Type Safety Migration Guide

This comprehensive guide helps you migrate from the previous versions of the Conduit Node.js SDKs to the new type-safe, fetch-based implementations.

## Quick Reference

| Change Category | Impact Level | Migration Required |
|----------------|--------------|-------------------|
| React Query Removal | **High** | Yes - Replace all hooks |
| Axios → Fetch Migration | **High** | Yes - Update configurations |
| Error Handling | **Medium** | Recommended - Better types |
| Client Configuration | **Medium** | Optional - Enhanced options |
| Type Safety | **Medium** | Optional - Stricter checking |
| Core API Methods | **Low** | Minimal - Mostly compatible |

---

## Migration Steps

### Step 1: Update Dependencies

```bash
# Update to latest versions
npm install @knn_labs/conduit-admin-client@latest
npm install @knn_labs/conduit-core-client@latest

# Remove old dependencies if no longer needed
npm uninstall axios @tanstack/react-query
```

### Step 2: Update Client Initialization

#### Admin SDK Client

```typescript
// ❌ BEFORE
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

const client = new ConduitAdminClient({
  masterKey: process.env.MASTER_KEY,
  adminApiUrl: 'https://admin.example.com',
  // axios-specific options
  axios: {
    timeout: 5000,
    interceptors: {
      request: [(config) => config],
      response: [(response) => response]
    }
  }
});

// ✅ AFTER
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

const client = new ConduitAdminClient({
  masterKey: process.env.MASTER_KEY!,
  adminApiUrl: 'https://admin.example.com',
  options: {
    timeout: 5000,
    onRequest: async (config) => {
      console.log('Request:', config);
    },
    onResponse: async (response) => {
      console.log('Response:', response);
    }
  }
});
```

#### Core SDK Client

```typescript
// ❌ BEFORE  
import { ConduitCoreClient } from '@knn_labs/conduit-core-client';

const client = new ConduitCoreClient({
  apiKey: process.env.API_KEY,
  baseUrl: 'https://api.example.com', // Note: 'baseUrl'
  maxRetries: '3', // String value
  timeout: 30000
});

// ✅ AFTER
import { ConduitCoreClient } from '@knn_labs/conduit-core-client';

const client = new ConduitCoreClient({
  apiKey: process.env.API_KEY!,
  baseURL: 'https://api.example.com', // Note: 'baseURL' 
  maxRetries: 3, // Number value
  timeout: 30000,
  debug: process.env.NODE_ENV === 'development'
});
```

### Step 3: Migrate from React Query to Direct Calls

#### Virtual Key Management

```typescript
// ❌ BEFORE - React Query hooks
import { 
  useVirtualKeys, 
  useCreateVirtualKey,
  useDeleteVirtualKey 
} from '@knn_labs/conduit-admin-client';

function VirtualKeysComponent() {
  const { data: keys, isLoading } = useVirtualKeys();
  const createMutation = useCreateVirtualKey();
  const deleteMutation = useDeleteVirtualKey();

  const handleCreate = () => {
    createMutation.mutate({
      keyName: 'test-key',
      maxBudget: 100
    });
  };

  if (isLoading) return <div>Loading...</div>;
  return <div>{/* render keys */}</div>;
}

// ✅ AFTER - Direct service calls
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
import { useState, useEffect } from 'react';

function VirtualKeysComponent() {
  const [keys, setKeys] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const client = new ConduitAdminClient(config);

  useEffect(() => {
    async function loadKeys() {
      try {
        setIsLoading(true);
        const data = await client.virtualKeys.list();
        setKeys(data);
      } catch (error) {
        console.error('Failed to load keys:', error);
      } finally {
        setIsLoading(false);
      }
    }
    loadKeys();
  }, []);

  const handleCreate = async () => {
    try {
      await client.virtualKeys.create({
        keyName: 'test-key',
        maxBudget: 100,
        budgetDuration: 'Daily' // Enum value required
      });
      // Refresh the list
      const data = await client.virtualKeys.list();
      setKeys(data);
    } catch (error) {
      console.error('Failed to create key:', error);
    }
  };

  if (isLoading) return <div>Loading...</div>;
  return <div>{/* render keys */}</div>;
}
```

#### Chat Completions

```typescript
// ❌ BEFORE - React Query hooks
import { useChatCompletion } from '@knn_labs/conduit-core-client';

function ChatComponent() {
  const { mutate: sendMessage, data, isLoading } = useChatCompletion();

  const handleSend = (message: string) => {
    sendMessage({
      model: 'gpt-4',
      messages: [{ role: 'user', content: message }]
    });
  };

  return <div>{/* chat UI */}</div>;
}

// ✅ AFTER - Direct service calls with proper state management
import { ConduitCoreClient } from '@knn_labs/conduit-core-client';
import { useState } from 'react';

function ChatComponent() {
  const [isLoading, setIsLoading] = useState(false);
  const [response, setResponse] = useState(null);
  const client = new ConduitCoreClient(config);

  const handleSend = async (message: string) => {
    try {
      setIsLoading(true);
      const result = await client.chat.create({
        model: 'gpt-4',
        messages: [{ role: 'user', content: message }],
        temperature: 0.7,
        max_tokens: 1000
      });
      setResponse(result);
    } catch (error) {
      console.error('Chat error:', error);
    } finally {
      setIsLoading(false);
    }
  };

  return <div>{/* chat UI */}</div>;
}
```

### Step 4: Update Error Handling

#### Enhanced Error Handling with Type Guards

```typescript
// ❌ BEFORE - Generic error handling
try {
  const response = await client.chat.create(request);
} catch (error: any) {
  if (error.response?.status === 429) {
    console.log('Rate limited');
  } else if (error.response?.status === 401) {
    console.log('Authentication failed');
  } else {
    console.log('Unknown error:', error.message);
  }
}

// ✅ AFTER - Type-safe error handling
import { 
  ConduitError,
  ValidationError,
  NotFoundError 
} from '@knn_labs/conduit-core-client';

try {
  const response = await client.chat.create(request);
} catch (error) {
  if (client.isRateLimitError(error)) {
    console.log(`Rate limited. Retry after ${error.retryAfter} seconds`);
  } else if (client.isAuthError(error)) {
    console.log(`Authentication failed: ${error.code}`);
  } else if (error instanceof ValidationError) {
    console.log(`Validation error: ${error.details}`);
  } else if (error instanceof ConduitError) {
    console.log(`API error: ${error.statusCode} - ${error.message}`);
  } else {
    console.log('Unexpected error:', error);
  }
}
```

#### Error Context and Debugging

```typescript
// ✅ NEW - Enhanced error information
try {
  await client.virtualKeys.create(invalidData);
} catch (error) {
  if (error instanceof ConduitError) {
    console.log('Error details:', {
      statusCode: error.statusCode,
      code: error.code,
      endpoint: error.endpoint,
      method: error.method,
      context: error.context
    });
  }
}
```

### Step 5: Migrate Configuration Options

#### Advanced Retry Configuration

```typescript
// ❌ BEFORE - Simple retry count
const client = new ConduitCoreClient({
  apiKey: 'key',
  maxRetries: 3
});

// ✅ AFTER - Enhanced retry configuration
const client = new ConduitCoreClient({
  apiKey: 'key',
  maxRetries: {
    maxRetries: 3,
    retryDelay: 1000,
    retryCondition: (error) => {
      // Custom retry logic
      return error.statusCode >= 500 || error.statusCode === 429;
    }
  },
  retryDelay: [1000, 2000, 4000] // Progressive backoff
});
```

#### SignalR Configuration

```typescript
// ✅ NEW - Enhanced SignalR configuration
const client = new ConduitAdminClient({
  masterKey: 'key',
  adminApiUrl: 'https://admin.example.com',
  options: {
    signalR: {
      enabled: true,
      autoConnect: true,
      reconnectDelay: [1000, 2000, 5000],
      logLevel: 'Information',
      connectionTimeout: 30000
    }
  }
});
```

### Step 6: Update Type Annotations

#### Use Generated Types

```typescript
// ✅ NEW - Import and use generated types
import type { 
  components as AdminComponents 
} from '@knn_labs/conduit-admin-client/generated';

import type { 
  components as CoreComponents 
} from '@knn_labs/conduit-core-client/generated';

// Type your variables
type VirtualKey = AdminComponents['schemas']['VirtualKeyDto'];
type ChatRequest = CoreComponents['schemas']['ChatCompletionRequest'];
type ChatResponse = CoreComponents['schemas']['ChatCompletionResponse'];

// Use in functions
async function createTypedChat(
  request: ChatRequest
): Promise<ChatResponse> {
  return await client.chat.create(request);
}

// Create type-safe builders
class ChatRequestBuilder {
  private request: Partial<ChatRequest> = {};

  setModel(model: string): this {
    this.request.model = model;
    return this;
  }

  addMessage(
    role: CoreComponents['schemas']['Message']['role'], 
    content: string
  ): this {
    this.request.messages = this.request.messages || [];
    this.request.messages.push({ role, content });
    return this;
  }

  setTemperature(temperature: number): this {
    this.request.temperature = temperature;
    return this;
  }

  build(): ChatRequest {
    if (!this.request.model || !this.request.messages) {
      throw new Error('Model and messages are required');
    }
    return this.request as ChatRequest;
  }
}

// Usage
const request = new ChatRequestBuilder()
  .setModel('gpt-4')
  .addMessage('user', 'Hello')
  .setTemperature(0.7)
  .build();
```

### Step 7: Migrate Streaming Responses

#### Type-Safe Streaming

```typescript
// ❌ BEFORE - Manual SSE parsing
const response = await fetch('/api/chat/stream', {
  method: 'POST',
  body: JSON.stringify(request)
});

const reader = response.body?.getReader();
const decoder = new TextDecoder();

while (true) {
  const { done, value } = await reader!.read();
  if (done) break;
  
  const chunk = decoder.decode(value);
  const lines = chunk.split('\n');
  
  for (const line of lines) {
    if (line.startsWith('data: ')) {
      const data = JSON.parse(line.slice(6));
      console.log(data.choices?.[0]?.delta?.content);
    }
  }
}

// ✅ AFTER - Type-safe streaming
const stream = await client.chat.create({
  model: 'gpt-4',
  messages: [{ role: 'user', content: 'Hello' }],
  stream: true // Changes return type to AsyncIterable
});

// Type-safe iteration
for await (const chunk of stream) {
  // chunk is typed as ChatCompletionChunk
  const content = chunk.choices[0]?.delta?.content;
  if (content) {
    process.stdout.write(content);
  }
}
```

### Step 8: Replace Removed Services

#### Database Backup Service

```typescript
// ❌ REMOVED
import { DatabaseBackupService } from '@knn_labs/conduit-admin-client';
const backupService = new DatabaseBackupService(client);
await backupService.createBackup();

// ✅ MIGRATION - Use fetch directly or Admin API
const response = await fetch(`${adminApiUrl}/api/database/backup`, {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${masterKey}`,
    'Content-Type': 'application/json'
  }
});
```

#### Discovery Service

```typescript
// ❌ REMOVED
import { DiscoveryService } from '@knn_labs/conduit-admin-client';
const discovery = new DiscoveryService(client);
const models = await discovery.discoverProviderModels('openai');

// ✅ MIGRATION - Use ModelMapping service
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
const client = new ConduitAdminClient(config);
const models = await client.modelMappings.discoverProviderModels('openai');
```

---

## Advanced Migration Patterns

### Custom HTTP Interceptors

```typescript
// ❌ BEFORE - Axios interceptors
client.axios.interceptors.request.use((config) => {
  config.headers['X-Custom-Header'] = 'value';
  return config;
});

client.axios.interceptors.response.use(
  (response) => response,
  (error) => {
    console.log('Request failed:', error);
    return Promise.reject(error);
  }
);

// ✅ AFTER - Callback-based interceptors
const client = new ConduitCoreClient({
  apiKey: 'key',
  headers: {
    'X-Custom-Header': 'value' // Static headers
  },
  onRequest: async (config) => {
    // Dynamic request modification
    config.headers['X-Timestamp'] = Date.now().toString();
  },
  onResponse: async (response) => {
    console.log(`Response: ${response.status} ${response.statusText}`);
  },
  onError: (error) => {
    console.log('Request failed:', error);
  }
});
```

### Request Cancellation

```typescript
// ✅ NEW - AbortController support
const controller = new AbortController();

// Cancel after 10 seconds
setTimeout(() => controller.abort(), 10000);

try {
  const response = await client.chat.create(request, {
    signal: controller.signal
  });
} catch (error) {
  if (error.name === 'AbortError') {
    console.log('Request was cancelled');
  }
}
```

### Type-Safe Configuration

```typescript
// ✅ NEW - Type-safe environment configuration
interface AppConfig {
  conduit: {
    apiKey: string;
    baseURL: string;
    timeout: number;
  };
}

function createTypedClient(config: AppConfig) {
  return new ConduitCoreClient({
    apiKey: config.conduit.apiKey,
    baseURL: config.conduit.baseURL,
    timeout: config.conduit.timeout,
    headers: {
      'User-Agent': 'MyApp/1.0.0'
    }
  });
}
```

---

## Testing Migration

### Update Test Mocks

```typescript
// ❌ BEFORE - Axios mocks
import axios from 'axios';
jest.mock('axios');
const mockedAxios = axios as jest.Mocked<typeof axios>;

mockedAxios.post.mockResolvedValue({
  data: { success: true }
});

// ✅ AFTER - Fetch mocks
global.fetch = jest.fn(() =>
  Promise.resolve({
    ok: true,
    status: 200,
    json: () => Promise.resolve({ success: true })
  })
) as jest.Mock;

// Or use MSW for more realistic testing
import { rest } from 'msw';
import { setupServer } from 'msw/node';

const server = setupServer(
  rest.post('/api/chat', (req, res, ctx) => {
    return res(ctx.json({ choices: [{ message: { content: 'Hello!' } }] }));
  })
);
```

### Type-Safe Test Helpers

```typescript
// ✅ NEW - Type-safe test helpers
import type { components } from '@knn_labs/conduit-core-client/generated';

type ChatRequest = components['schemas']['ChatCompletionRequest'];
type ChatResponse = components['schemas']['ChatCompletionResponse'];

function createMockChatRequest(): ChatRequest {
  return {
    model: 'gpt-4',
    messages: [{ role: 'user', content: 'test' }],
    temperature: 0.7,
    max_tokens: 100
  };
}

function createMockChatResponse(): ChatResponse {
  return {
    id: 'test-123',
    object: 'chat.completion',
    created: Date.now(),
    model: 'gpt-4',
    choices: [{
      index: 0,
      message: { role: 'assistant', content: 'Hello!' },
      finish_reason: 'stop'
    }],
    usage: {
      prompt_tokens: 10,
      completion_tokens: 5,
      total_tokens: 15
    }
  };
}
```

---

## Migration Checklist

### Pre-Migration
- [ ] Read this migration guide completely
- [ ] Review breaking changes documentation
- [ ] Update dependencies to latest versions
- [ ] Create a backup of your current implementation

### Code Migration
- [ ] Update client initialization code
- [ ] Replace React Query hooks with direct service calls
- [ ] Update error handling to use type guards
- [ ] Update type annotations to use generated types
- [ ] Replace removed services with equivalent functionality
- [ ] Update test mocks and helpers

### Testing & Validation
- [ ] Run TypeScript compiler to find type errors
- [ ] Test all API interactions manually
- [ ] Verify error handling works correctly
- [ ] Test streaming functionality if used
- [ ] Validate SignalR connections if used
- [ ] Performance test to ensure no regressions

### Post-Migration
- [ ] Update documentation
- [ ] Train team on new patterns
- [ ] Monitor for any runtime issues
- [ ] Consider leveraging new type safety features

---

## Common Issues and Solutions

### Issue: TypeScript Compilation Errors

```typescript
// Problem: Property 'someField' does not exist on type 'ResponseType'
const value = response.someField; // ❌

// Solution: Check the generated types for correct property names
const value = response.some_field; // ✅ (if that's the correct name)

// Or use type assertion if you're certain
const value = (response as any).someField; // ⚠️ Use sparingly
```

### Issue: Request Timeout Errors

```typescript
// Problem: Requests timing out
const client = new ConduitCoreClient({ apiKey: 'key' });

// Solution: Increase timeout for slow operations
const client = new ConduitCoreClient({
  apiKey: 'key',
  timeout: 60000 // 60 seconds for slow operations
});
```

### Issue: SignalR Connection Issues

```typescript
// Problem: SignalR not connecting
const client = new ConduitAdminClient({
  masterKey: 'key',
  adminApiUrl: 'https://admin.example.com'
});

// Solution: Enable and configure SignalR explicitly
const client = new ConduitAdminClient({
  masterKey: 'key',
  adminApiUrl: 'https://admin.example.com',
  options: {
    signalR: {
      enabled: true,
      autoConnect: true,
      connectionTimeout: 30000
    }
  }
});
```

---

## Support and Resources

- **Documentation**: [API Reference Documentation](./API-Reference.md)
- **Examples**: Check the `examples/` directory in each SDK
- **Issues**: [GitHub Issues](https://github.com/knnlabs/Conduit/issues)
- **Breaking Changes**: [Breaking Changes Documentation](./SDK-TYPE-SAFETY-BREAKING-CHANGES.md)

## Summary

The migration to type-safe SDKs represents a significant improvement in developer experience, reliability, and maintainability. While the migration requires effort, particularly for applications using React Query hooks, the benefits of full type safety, better error handling, and reduced bundle sizes make it worthwhile.

Key takeaways:
- ✅ **Type Safety**: Catch errors at compile time instead of runtime
- ✅ **Better DX**: Enhanced IDE support with autocomplete and inline docs
- ✅ **Modern Architecture**: Native fetch instead of external dependencies
- ✅ **Performance**: Smaller bundle sizes and better tree-shaking
- ✅ **Reliability**: Comprehensive error handling with specific error types