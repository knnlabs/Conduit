# Conduit Core API Client for Node.js

Official Node.js client library for the Conduit Core API - an OpenAI-compatible multi-provider LLM gateway.

## Features

- 🚀 **Full TypeScript Support** - Complete type definitions for all API operations
- 🔄 **OpenAI Compatibility** - Drop-in replacement for OpenAI client library
- 📡 **Streaming Support** - Real-time streaming with async iterators
- 🔧 **Function Calling** - Full support for tool use and function calling
- 🛡️ **Robust Error Handling** - Typed errors with automatic retries
- 📊 **Performance Metrics** - Built-in performance tracking
- 🔑 **Virtual Key Authentication** - Secure API access with virtual keys

## Installation

```bash
npm install @conduit/core
```

or

```bash
yarn add @conduit/core
```

## Quick Start

```typescript
import { ConduitCoreClient } from '@conduit/core';

const client = new ConduitCoreClient({
  apiKey: 'your-virtual-key',
  baseURL: 'https://api.conduit.ai', // Optional
});

// Basic chat completion
const response = await client.chat.completions.create({
  model: 'gpt-4',
  messages: [
    { role: 'user', content: 'Hello, how are you?' }
  ],
});

console.log(response.choices[0].message.content);
```

## Configuration

```typescript
const client = new ConduitCoreClient({
  apiKey: 'your-virtual-key',        // Required
  baseURL: 'https://api.conduit.ai',  // Optional, defaults to https://api.conduit.ai
  timeout: 60000,                     // Optional, request timeout in ms
  maxRetries: 3,                      // Optional, number of retries
  headers: {                          // Optional, custom headers
    'X-Custom-Header': 'value'
  },
  debug: true,                        // Optional, enables debug logging
});
```

## Chat Completions

### Basic Usage

```typescript
const response = await client.chat.completions.create({
  model: 'gpt-4',
  messages: [
    { role: 'system', content: 'You are a helpful assistant.' },
    { role: 'user', content: 'What is the capital of France?' }
  ],
  temperature: 0.7,
  max_tokens: 150,
});
```

### Streaming Responses

```typescript
const stream = await client.chat.completions.create({
  model: 'gpt-4',
  messages: [{ role: 'user', content: 'Tell me a story.' }],
  stream: true,
});

for await (const chunk of stream) {
  const content = chunk.choices[0]?.delta?.content;
  if (content) {
    process.stdout.write(content);
  }
}
```

### React Query Hooks - Streaming

The React Query integration now supports proper streaming with callbacks:

```typescript
import { useChatCompletionStream } from '@conduit/core/react-query';

function ChatComponent() {
  const streamingCompletion = useChatCompletionStream();
  const [streamedContent, setStreamedContent] = useState('');

  const handleStream = async () => {
    await streamingCompletion.mutateAsync({
      model: 'gpt-4',
      messages: [{ role: 'user', content: 'Tell me a story' }],
      stream: true,
      streamingOptions: {
        onChunk: (chunk) => {
          // Handle each streaming chunk
          if (chunk.choices?.[0]?.delta?.content) {
            setStreamedContent(prev => prev + chunk.choices[0].delta.content);
          }
        },
        onComplete: () => {
          console.log('Streaming completed');
        },
        onError: (error) => {
          console.error('Streaming error:', error);
        }
      }
    });
  };

  return (
    <div>
      <button onClick={handleStream}>Start Streaming</button>
      <div>{streamedContent}</div>
    </div>
  );
}
```

### Function Calling

```typescript
const tools = [{
  type: 'function',
  function: {
    name: 'get_weather',
    description: 'Get the current weather',
    parameters: {
      type: 'object',
      properties: {
        location: { type: 'string' }
      },
      required: ['location']
    }
  }
}];

const response = await client.chat.completions.create({
  model: 'gpt-4',
  messages: [{ role: 'user', content: 'What\'s the weather in Paris?' }],
  tools,
  tool_choice: 'auto',
});

if (response.choices[0].message.tool_calls) {
  // Handle function calls
}
```

### JSON Response Format

```typescript
const response = await client.chat.completions.create({
  model: 'gpt-4',
  messages: [{ role: 'user', content: 'List 3 colors as JSON.' }],
  response_format: { type: 'json_object' },
});
```

## Models

### List Available Models

```typescript
const models = await client.models.list();
models.forEach(model => {
  console.log(`${model.id} - ${model.owned_by}`);
});
```

### Check Model Availability

```typescript
const exists = await client.models.exists('gpt-4');
if (exists) {
  console.log('GPT-4 is available');
}
```

## Error Handling

The client provides typed errors for different scenarios:

```typescript
import { 
  ConduitError,
  AuthenticationError,
  RateLimitError,
  ValidationError 
} from '@conduit/core';

try {
  const response = await client.chat.completions.create({
    model: 'gpt-4',
    messages: [{ role: 'user', content: 'Hello' }],
  });
} catch (error) {
  if (error instanceof AuthenticationError) {
    console.error('Invalid API key');
  } else if (error instanceof RateLimitError) {
    console.error(`Rate limited. Retry after ${error.retryAfter} seconds`);
  } else if (error instanceof ValidationError) {
    console.error(`Invalid request: ${error.message}`);
  } else if (error instanceof ConduitError) {
    console.error(`API error: ${error.message} (${error.code})`);
  }
}
```

## Video Generation

### Basic Video Generation

```typescript
// Start an async video generation task
const response = await client.videos.generateAsync({
  prompt: 'A serene lake at sunset',
  model: 'minimax-video-01',
  duration: 6,
  size: '1280x720',
});

// Poll for completion
const result = await client.videos.pollTaskUntilCompletion(response.task_id);
console.log('Video URL:', result.data[0].url);
```

### Video Generation with Progress Tracking

The SDK provides real-time progress tracking for video generation through SignalR with automatic fallback to polling.

```typescript
const { taskId, result } = await client.videos.generateWithProgress({
  prompt: 'A serene lake at sunset',
  model: 'minimax-video-01', 
  duration: 6,
}, {
  onProgress: ({ percentage, status, message }) => {
    console.log(`Progress: ${percentage}% - ${status}`);
    if (message) console.log(`Status: ${message}`);
  },
  onStarted: (taskId, estimatedSeconds) => {
    console.log(`Started task ${taskId}, ETA: ${estimatedSeconds}s`);
  },
  onCompleted: (videoResult) => {
    console.log('Video generated:', videoResult.data[0].url);
  },
  onFailed: (error, isRetryable) => {
    console.error(`Generation failed: ${error}`);
    if (isRetryable) console.log('Task can be retried');
  }
});

// Wait for the result
const video = await result;
```

### Migrating from Polling to Progress Tracking

#### Before (Manual Polling):
```typescript
const response = await client.videos.generateAsync(request);
const taskId = response.task_id;

// Poll manually
while (true) {
  const status = await client.videos.getTaskStatus(taskId);
  console.log(`Progress: ${status.progress}%`);
  
  if (status.status === 'Completed') {
    console.log('Video ready:', status.result.data[0].url);
    break;
  }
  
  if (status.status === 'Failed') {
    throw new Error(status.error);
  }
  
  await new Promise(resolve => setTimeout(resolve, 2000));
}
```

#### After (Automatic Progress):
```typescript
const { result } = await client.videos.generateWithProgress(request, {
  onProgress: ({ percentage }) => console.log(`Progress: ${percentage}%`),
  onCompleted: (video) => console.log('Video ready:', video.data[0].url),
  onFailed: (error) => console.error('Failed:', error)
});

const video = await result;
```

### Video Generation with SignalR Configuration

```typescript
// Create client with SignalR enabled
const client = new ConduitCoreClient({
  apiKey: 'your-virtual-key',
  signalR: {
    enabled: true,
    autoConnect: true,
    reconnectAttempts: 3,
    reconnectInterval: 5000,
  }
});

// Generate video with real-time updates
const { taskId, result } = await client.videos.generateWithProgress({
  prompt: 'Modern city skyline',
  model: 'minimax-video-01',
});
```

### Cancel Video Generation

```typescript
const { taskId } = await client.videos.generateWithProgress({
  prompt: 'Complex animation',
});

// Cancel if needed
await client.videos.cancelTask(taskId);
```

### Model Capabilities

Check what a video model supports:

```typescript
const capabilities = client.videos.getModelCapabilities('minimax-video-01');
console.log(capabilities);
// {
//   maxDuration: 6,
//   supportedResolutions: ['1280x720', '1920x1080'],
//   supportedFps: [24, 30],
//   supportsCustomStyles: true,
//   supportsSeed: true,
//   maxVideos: 1
// }
```

## Advanced Features

### Correlation IDs

Track requests across your system:

```typescript
const response = await client.chat.completions.create(
  {
    model: 'gpt-4',
    messages: [{ role: 'user', content: 'Hello' }],
  },
  {
    correlationId: 'user-123-session-456',
  }
);
```

### Abort Requests

Cancel long-running requests:

```typescript
const controller = new AbortController();

// Cancel after 5 seconds
setTimeout(() => controller.abort(), 5000);

try {
  const response = await client.chat.completions.create(
    {
      model: 'gpt-4',
      messages: [{ role: 'user', content: 'Write a long essay.' }],
    },
    {
      signal: controller.signal,
    }
  );
} catch (error) {
  if (error.name === 'AbortError') {
    console.log('Request was cancelled');
  }
}
```

### Performance Metrics

Access detailed performance information:

```typescript
const response = await client.chat.completions.create({
  model: 'gpt-4',
  messages: [{ role: 'user', content: 'Hello' }],
});

if (response.performance) {
  console.log(`Provider: ${response.performance.provider_name}`);
  console.log(`Response time: ${response.performance.provider_response_time_ms}ms`);
  console.log(`Tokens/second: ${response.performance.tokens_per_second}`);
}
```

## Migration from OpenAI

Migrating from the OpenAI client is straightforward:

```typescript
// OpenAI client
import OpenAI from 'openai';
const openai = new OpenAI({ apiKey: 'sk-...' });

// Conduit client (drop-in replacement)
import { ConduitCoreClient } from '@conduit/core';
const openai = new ConduitCoreClient({ apiKey: 'your-virtual-key' });

// The API calls remain the same
const response = await openai.chat.completions.create({
  model: 'gpt-4',
  messages: [{ role: 'user', content: 'Hello!' }],
});
```

## TypeScript Support

The library is written in TypeScript and provides complete type definitions:

```typescript
import type { 
  ChatCompletionRequest,
  ChatCompletionResponse,
  ChatCompletionMessage,
  Tool,
  Model 
} from '@conduit/core';
```

## Contributing

Contributions are welcome! Please see our [Contributing Guide](../../CONTRIBUTING.md) for details.

## License

This project is licensed under the MIT License - see the [LICENSE](../../LICENSE) file for details.

## Support

- 📚 [Documentation](https://docs.conduit.ai)
- 💬 [Discord Community](https://discord.gg/conduit)
- 🐛 [Issue Tracker](https://github.com/conduit-ai/conduit/issues)
- 📧 [Email Support](mailto:support@conduit.ai)