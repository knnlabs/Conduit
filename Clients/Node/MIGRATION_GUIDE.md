# Type Safety Migration Guide

This guide helps you migrate from the old SDK with `any`/`unknown` types to the new fully type-safe SDK.

## Overview

The new SDK provides:
- 🎯 **Full type safety** from OpenAPI specifications
- 🚀 **Better IDE support** with autocomplete
- 🐛 **Fewer runtime errors** through compile-time checking
- 📚 **Self-documenting code** with inline type information

## Quick Start

### 1. Update Your Imports

```typescript
// Old
import { ConduitClient } from '@conduit/core-sdk';

// New
import { ConduitCoreClient } from '@conduit/core-sdk';
import type { components } from '@conduit/core-sdk/generated';
```

### 2. Update Client Initialization

```typescript
// Old - No type checking on config
const client = new ConduitClient({
  apiKey: process.env.API_KEY,
  baseUrl: 'https://api.example.com', // Could have typos
  maxRetries: '3', // Wrong type not caught
});

// New - Full type safety
const client = new ConduitCoreClient({
  apiKey: process.env.API_KEY!,
  baseURL: 'https://api.example.com', // IDE autocompletes property names
  maxRetries: 3, // Type error if not a number
});
```

## Common Migration Patterns

### Pattern 1: Chat Completions

#### Before
```typescript
const response = await client.chat.create({
  model: 'gpt-4',
  messages: [{
    role: 'usr', // Typo not caught
    content: 'Hello'
  }],
  temprature: 0.7, // Typo not caught
} as any);

// No type safety on response
console.log(response.data.choices[0].text);
```

#### After
```typescript
const response = await client.chat.create({
  model: 'gpt-4',
  messages: [{
    role: 'user', // Autocomplete prevents typos
    content: 'Hello'
  }],
  temperature: 0.7, // Autocomplete prevents typos
});

// Full type safety on response
console.log(response.choices[0].message.content);
```

### Pattern 2: Error Handling

#### Before
```typescript
try {
  await client.chat.create(request);
} catch (error: any) {
  // Guessing at error structure
  if (error.response?.status === 429) {
    const retryAfter = error.response.headers['retry-after'];
    // ...
  }
}
```

#### After
```typescript
try {
  await client.chat.create(request);
} catch (error) {
  // Type-safe error handling
  if (client.isRateLimitError(error)) {
    console.log(`Retry after ${error.retryAfter} seconds`);
  } else if (client.isAuthError(error)) {
    console.log(`Auth failed: ${error.code}`);
  }
}
```

### Pattern 3: Streaming Responses

#### Before
```typescript
const stream = await client.chat.create({
  model: 'gpt-4',
  messages: messages,
  stream: true
} as any);

// Manually parsing SSE
for await (const chunk of stream) {
  const data = JSON.parse(chunk); // Could fail
  console.log(data.choices?.[0]?.delta?.content);
}
```

#### After
```typescript
const stream = await client.chat.create({
  model: 'gpt-4',
  messages: messages,
  stream: true // Changes return type automatically
});

// Type-safe streaming
for await (const chunk of stream) {
  // chunk is typed as ChatCompletionChunk
  if (chunk.choices[0]?.delta?.content) {
    process.stdout.write(chunk.choices[0].delta.content);
  }
}
```

### Pattern 4: Function Calling

#### Before
```typescript
const response = await client.chat.create({
  model: 'gpt-4',
  messages: messages,
  functions: [{
    name: 'get_weather',
    parameters: { /* ... */ }
  }],
  function_call: 'auto'
} as any);

// Parsing function calls manually
const functionCall = JSON.parse(
  response.choices[0].message.function_call.arguments
);
```

#### After
```typescript
const response = await client.chat.create({
  model: 'gpt-4',
  messages: messages,
  tools: [{
    type: 'function',
    function: {
      name: 'get_weather',
      description: 'Get weather data',
      parameters: { /* fully typed */ }
    }
  }],
  tool_choice: 'auto'
});

// Type-safe tool calls
const toolCall = response.choices[0].message.tool_calls?.[0];
if (toolCall?.type === 'function') {
  const args = JSON.parse(toolCall.function.arguments);
  // Process with confidence
}
```

## Admin SDK Migration

### Pattern 5: Virtual Key Management

#### Before
```typescript
const key = await adminClient.virtualKeys.create({
  name: 'test-key', // Wrong property name
  maxBudget: '100', // Wrong type
  budgetDuration: 'daily', // Wrong case
} as any);
```

#### After
```typescript
import type { components } from '@conduit/admin-sdk/generated';

type CreateKeyRequest = components['schemas']['CreateVirtualKeyRequest'];

const key = await adminClient.virtualKeys.create({
  keyName: 'test-key', // Correct property name
  maxBudget: 100, // Correct type
  budgetDuration: 'Daily', // Correct case from enum
});
```

### Pattern 6: Analytics Queries

#### Before
```typescript
const analytics = await adminClient.analytics.query({
  start: '2024-01-01', // String dates
  end: '2024-01-31',
  metrics: 'requests,tokens,cost', // String list
  groupBy: 'day'
} as any);
```

#### After
```typescript
const analytics = await adminClient.analytics.query({
  startDate: new Date('2024-01-01'), // Proper date types
  endDate: new Date('2024-01-31'),
  metrics: ['requests', 'tokens', 'cost'], // Proper array
  groupBy: 'day' // Enum autocomplete
});
```

## Advanced Type Usage

### Using Generated Types Directly

```typescript
import type { components, operations } from '@conduit/core-sdk/generated';

// Use component schemas
type Message = components['schemas']['Message'];
type Model = components['schemas']['Model'];

// Use operation types
type CreateChatParams = operations['createChatCompletion']['requestBody']['content']['application/json'];
type CreateChatResponse = operations['createChatCompletion']['responses']['200']['content']['application/json'];

// Create strongly-typed functions
function buildMessage(role: Message['role'], content: string): Message {
  return { role, content };
}
```

### Creating Type-Safe Wrappers

```typescript
import type { components } from '@conduit/core-sdk/generated';

class TypedChatBuilder {
  private messages: components['schemas']['Message'][] = [];
  
  addMessage(
    role: components['schemas']['Message']['role'],
    content: string
  ): this {
    this.messages.push({ role, content });
    return this;
  }
  
  build(): components['schemas']['ChatCompletionRequest'] {
    return {
      model: 'gpt-4',
      messages: this.messages,
      temperature: 0.7,
      max_tokens: 1000,
    };
  }
}
```

## Troubleshooting

### Type Errors After Migration

1. **Property name changes**: The OpenAPI spec might use different names than the old SDK
   ```typescript
   // Look for autocomplete suggestions
   const request = {
     // IDE will show available properties
   };
   ```

2. **Enum value changes**: Check the exact casing and values
   ```typescript
   // Hover over the property to see valid values
   budgetDuration: 'Daily' // not 'daily'
   ```

3. **Required vs optional**: Some fields may have changed
   ```typescript
   // Check generated types for required fields
   type Request = components['schemas']['RequestType'];
   // Required fields won't have '?' after the name
   ```

### Runtime Errors

1. **Update error handling**: Use type guards
   ```typescript
   if (client.isConduitError(error)) {
     // Handle Conduit-specific errors
   }
   ```

2. **Check response structure**: Generated types match actual API
   ```typescript
   // Trust the types - they're generated from the API
   ```

## Best Practices

1. **Don't use `any`**: Let TypeScript help you
2. **Use type imports**: Keep bundle size small
   ```typescript
   import type { components } from '../generated/api';
   ```
3. **Create type aliases**: Make code more readable
   ```typescript
   type ChatRequest = components['schemas']['ChatCompletionRequest'];
   ```
4. **Leverage IDE features**: Use autocomplete and hover for documentation
5. **Run type checking**: Add to your build process
   ```json
   {
     "scripts": {
       "typecheck": "tsc --noEmit"
     }
   }
   ```

## Getting Help

- Check generated types in `src/generated/`
- Use IDE hover for inline documentation
- Run `npm run typecheck` to find issues
- See `examples/type-safety-comparison.ts` for more patterns