# Streaming with Tool Calls

This guide documents Conduit's enhanced Server-Sent Events (SSE) streaming architecture that combines OpenAI API compatibility with Conduit-specific extensions for richer streaming experiences.

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [SSE Event Types](#sse-event-types)
3. [Tool Execution Lifecycle](#tool-execution-lifecycle)
4. [Backend Implementation](#backend-implementation)
5. [SDK Usage](#sdk-usage)
6. [WebAdmin Integration](#webadmin-integration)
7. [OpenAI Compatibility](#openai-compatibility)

---

## Architecture Overview

Conduit implements a **hybrid streaming architecture** that:

- ✅ **Maintains full OpenAI API compatibility** for standard `ChatCompletionChunk` events
- ✅ **Extends with custom SSE event types** for reasoning, tool execution, and metrics
- ✅ **Preserves finish_reason semantics** for mid-stream tool calls vs. completion
- ✅ **Provides type-safe event parsing** via SDK type guards

### Key Design Principles

1. **OpenAI Compatibility**: Standard clients can consume the stream using only `content` events
2. **Progressive Enhancement**: Enhanced clients can handle additional event types for richer UX
3. **Type Safety**: TypeScript type guards enable compile-time verification
4. **Event Ordering**: Events are emitted in a predictable lifecycle

---

## SSE Event Types

Conduit supports the following SSE event types, sent via the `event:` SSE field:

### Standard OpenAI Events

| Event Type | Description | OpenAI Compatible |
|------------|-------------|-------------------|
| `content` | Standard chat completion chunks | ✅ Yes |
| `done` | Stream completion marker | ✅ Yes |

### Conduit Extensions

| Event Type | Description | Use Case |
|------------|-------------|----------|
| `reasoning` | Model thinking/reasoning content | Display AI reasoning process |
| `tool-executing` | Tool execution status updates | Real-time function call feedback |
| `tool-result` | Individual tool execution results | Detailed logging/debugging |
| `metrics` | Live performance metrics during streaming | Performance monitoring |
| `metrics-final` | Final comprehensive metrics at completion | Analytics, billing |
| `error` | Error events during streaming | Error handling |

### TypeScript Type Definitions

```typescript
export enum EnhancedSSEEventType {
  Content = 'content',           // OpenAI compatible
  Reasoning = 'reasoning',       // Conduit extension
  ToolExecuting = 'tool-executing', // Conduit extension
  ToolResult = 'tool-result',    // Conduit extension
  Metrics = 'metrics',           // Conduit extension
  MetricsFinal = 'metrics-final', // Conduit extension
  Error = 'error',               // Conduit extension
  Done = 'done',                 // OpenAI compatible
}
```

---

## Tool Execution Lifecycle

When a model invokes tools during streaming, the following event sequence occurs:

### 1. Tool Call Announcement (via `content` event)

```json
{
  "event": "content",
  "data": {
    "id": "chatcmpl-123",
    "object": "chat.completion.chunk",
    "choices": [{
      "index": 0,
      "delta": {
        "tool_calls": [{
          "index": 0,
          "id": "call_abc123",
          "type": "function",
          "function": {
            "name": "get_weather",
            "arguments": "{\"location\":\"San Francisco\"}"
          }
        }]
      },
      "finish_reason": "tool_calls"
    }]
  }
}
```

**Key Point**: `finish_reason: "tool_calls"` indicates the stream will **continue** after tool execution.

### 2. Tool Execution Started (via `tool-executing` event)

```json
{
  "event": "tool-executing",
  "data": {
    "tool_call_id": "call_abc123",
    "function_name": "get_weather",
    "status": "started"
  }
}
```

### 3. Tool Execution Completed (via `tool-executing` event)

```json
{
  "event": "tool-executing",
  "data": {
    "tool_call_id": "call_abc123",
    "function_name": "get_weather",
    "status": "completed",
    "result": {
      "temperature": 72,
      "condition": "sunny"
    },
    "cost": 0.001,
    "function_execution_id": "exec-xyz789"
  }
}
```

### 4. Tool Result (via `tool-result` event) - Optional

```json
{
  "event": "tool-result",
  "data": {
    "tool_call_id": "call_abc123",
    "result": {
      "temperature": 72,
      "condition": "sunny"
    }
  }
}
```

**Note**: `tool-result` events are optional and provide detailed logging. Most clients only need `tool-executing` events.

### 5. Model Response with Tool Results (via `content` event)

```json
{
  "event": "content",
  "data": {
    "id": "chatcmpl-123",
    "object": "chat.completion.chunk",
    "choices": [{
      "index": 0,
      "delta": {
        "content": "The weather in San Francisco is currently 72°F and sunny."
      },
      "finish_reason": null
    }]
  }
}
```

### 6. Stream Completion (via `content` event)

```json
{
  "event": "content",
  "data": {
    "id": "chatcmpl-123",
    "object": "chat.completion.chunk",
    "choices": [{
      "index": 0,
      "delta": {},
      "finish_reason": "stop"
    }]
  }
}
```

### 7. Final Metrics (via `metrics-final` event)

```json
{
  "event": "metrics-final",
  "data": {
    "total_latency_ms": 2500,
    "time_to_first_token_ms": 150,
    "tokens_per_second": 42.0,
    "provider": "openai",
    "model": "gpt-4",
    "total_tokens": 155,
    "completion_tokens": 105,
    "prompt_tokens": 50
  }
}
```

---

## Backend Implementation

### Routing SSE Events

The backend routes different event types based on the SSE `event:` field:

```csharp
// In ChatController.cs - StreamChatCompletion method

// Standard content chunk (OpenAI compatible)
await context.Response.WriteAsync($"data: {chunkJson}\n\n");

// Reasoning content
await context.Response.WriteAsync($"event: reasoning\n");
await context.Response.WriteAsync($"data: {{\"content\":\"{reasoning}\"}}\n\n");

// Tool execution status
await context.Response.WriteAsync($"event: tool-executing\n");
await context.Response.WriteAsync($"data: {toolStatusJson}\n\n");

// Tool result
await context.Response.WriteAsync($"event: tool-result\n");
await context.Response.WriteAsync($"data: {toolResultJson}\n\n");

// Streaming metrics
await context.Response.WriteAsync($"event: metrics\n");
await context.Response.WriteAsync($"data: {metricsJson}\n\n");

// Final metrics
await context.Response.WriteAsync($"event: metrics-final\n");
await context.Response.WriteAsync($"data: {finalMetricsJson}\n\n");
```

### Important Backend Behaviors

#### finish_reason Handling

```csharp
// finish_reason: "tool_calls" = mid-stream continuation
if (finishReason == "tool_calls")
{
    // Backend executes tools and continues streaming
    // DO NOT end the stream!
    continue;
}

// finish_reason: "stop" or "length" = actual completion
if (finishReason == "stop" || finishReason == "length")
{
    // Send final metrics
    await SendFinalMetrics();
    break;
}
```

#### Tool Execution Flow

1. Model emits `finish_reason: "tool_calls"` with tool call details
2. Backend sends `tool-executing` event with `status: "started"`
3. Backend executes function via FunctionsService
4. Backend sends `tool-executing` event with `status: "completed"` and result
5. Backend sends tool result back to model
6. Model continues generating response with tool results
7. Model emits `finish_reason: "stop"` when complete

---

## SDK Usage

### Type Guards

The SDK provides type guards for discriminating event types:

```typescript
import {
  isChatCompletionChunk,
  isStreamingMetrics,
  isFinalMetrics,
  isReasoningEvent,
  isToolExecutingEvent,
  isToolResultEvent
} from '@knn_labs/conduit-gateway-client';

// Process stream events
for await (const event of stream) {
  if (isChatCompletionChunk(event)) {
    // Standard OpenAI chunk
    const content = event.choices?.[0]?.delta?.content;
    const toolCalls = event.choices?.[0]?.delta?.tool_calls;
    const finishReason = event.choices?.[0]?.finish_reason;

    // Handle content, tool calls, finish_reason
  }
  else if (isReasoningEvent(event)) {
    // Reasoning content
    const reasoning = event.content;
  }
  else if (isToolExecutingEvent(event)) {
    // Tool execution status
    const { tool_call_id, function_name, status, result, cost } = event;
  }
  else if (isToolResultEvent(event)) {
    // Tool result (optional detailed logging)
    const { tool_call_id, result, error } = event;
  }
  else if (isStreamingMetrics(event)) {
    // Live metrics
    const tokensPerSecond = event.current_tokens_per_second;
  }
  else if (isFinalMetrics(event)) {
    // Final metrics
    const { total_latency_ms, tokens_per_second, total_tokens } = event;
  }
}
```

### Type Definitions

```typescript
// Reasoning event
export interface ReasoningEvent {
  content: string;
}

// Tool execution event
export interface ToolExecutingEvent {
  tool_call_id?: string;
  function_name?: string;
  status: string; // "started" | "completed" | "failed"
  result?: unknown;
  cost?: number;
  error_message?: string;
  function_execution_id?: string;
}

// Tool result event
export interface ToolResultEvent {
  tool_call_id: string;
  result: unknown;
  error?: string;
}

// Streaming metrics
export interface StreamingMetrics {
  request_id?: string;
  elapsed_ms?: number;
  tokens_generated?: number;
  current_tokens_per_second?: number;
  time_to_first_token_ms?: number;
  avg_inter_token_latency_ms?: number;
}

// Final metrics
export interface FinalMetrics {
  total_latency_ms?: number;
  time_to_first_token_ms?: number;
  tokens_per_second?: number;
  prompt_tokens_per_second?: number;
  completion_tokens_per_second?: number;
  provider?: string;
  model?: string;
  streaming?: boolean;
  avg_inter_token_latency_ms?: number;
  prompt_tokens?: number;
  completion_tokens?: number;
  total_tokens?: number;
}
```

### Complete SDK Example

```typescript
import { getBrowserCoreClient } from './browserCoreClient';
import {
  isChatCompletionChunk,
  isFinalMetrics,
  isToolExecutingEvent,
  buildMessageContent
} from '@knn_labs/conduit-gateway-client';

async function streamChatWithTools() {
  const client = await getBrowserCoreClient();

  const chatRequest = {
    messages: [
      {
        role: 'user' as const,
        content: 'What is the weather in San Francisco?'
      }
    ],
    model: 'gpt-4',
    stream: true as const,
    function_configuration_ids: ['weather-functions']
  };

  const stream = await client.chat.create(chatRequest);

  let totalContent = '';
  const toolCalls = [];

  for await (const event of stream) {
    if (isChatCompletionChunk(event)) {
      // Handle content
      const content = event.choices?.[0]?.delta?.content;
      if (content) {
        totalContent += content;
        console.log('Content:', content);
      }

      // Handle tool calls
      const deltaToolCalls = event.choices?.[0]?.delta?.tool_calls;
      if (deltaToolCalls) {
        // Accumulate tool calls
        for (const toolCall of deltaToolCalls) {
          const index = toolCall.index ?? 0;
          if (!toolCalls[index]) {
            toolCalls[index] = {
              id: toolCall.id ?? '',
              type: 'function',
              function: {
                name: toolCall.function?.name ?? '',
                arguments: toolCall.function?.arguments ?? ''
              }
            };
          } else {
            // Append to existing tool call
            if (toolCall.function?.arguments) {
              toolCalls[index].function.arguments += toolCall.function.arguments;
            }
          }
        }
      }

      // Check for finish_reason
      const finishReason = event.choices?.[0]?.finish_reason;
      if (finishReason === 'tool_calls') {
        console.log('Tool calls invoked, waiting for execution...');
        // DO NOT end the stream - backend will continue
      } else if (finishReason === 'stop') {
        console.log('Stream complete');
      }
    }
    else if (isToolExecutingEvent(event)) {
      console.log(`Tool ${event.function_name}: ${event.status}`);
      if (event.status === 'completed') {
        console.log('Result:', event.result);
        console.log('Cost:', event.cost);
      }
    }
    else if (isFinalMetrics(event)) {
      console.log('Final metrics:', event);
      console.log(`Total tokens: ${event.total_tokens}`);
      console.log(`Speed: ${event.tokens_per_second} tokens/sec`);
    }
  }

  console.log('Final content:', totalContent);
  console.log('Tool calls:', toolCalls);
}
```

---

## WebAdmin Integration

The WebAdmin uses the SDK through `SDKChatStreamingAdapter` with callbacks:

### Streaming Callbacks

```typescript
export interface StreamingCallbacks {
  onStart?: () => void;
  onChunk?: (chunk: ChatCompletionChunk) => void;
  onContent?: (delta: string, fullContent: string) => void;
  onReasoning?: (delta: string, fullReasoning: string) => void;
  onToolExecuting?: (event: {
    tool_call_id?: string;
    function_name?: string;
    status: string;
    result?: unknown;
    cost?: number;
    error_message?: string;
    function_execution_id?: string;
  }) => void;
  onToolResult?: (event: {
    tool_call_id: string;
    result: unknown;
    error?: string;
  }) => void;
  onMetrics?: (metrics: StreamingMetrics) => void;
  onTokensPerSecond?: (tps: number) => void;
  onComplete?: (response: {
    content: string;
    metadata: {
      model?: string;
      finishReason?: string;
      tokensUsed?: number;
      completionTokens?: number;
      promptTokens?: number;
      latency?: number;
      timeToFirstToken?: number;
      tokensPerSecond?: number;
      streaming?: boolean;
      provider?: string;
      toolCalls?: Array<{
        id: string;
        type: 'function';
        function: {
          name: string;
          arguments: string;
        };
      }>;
    };
  }) => void;
  onError?: (error: StreamingError) => void;
  onAbort?: () => void;
}
```

### Usage Example

```typescript
import { SDKChatStreamingAdapter } from './sdkChatStreamingAdapter';

const adapter = new SDKChatStreamingAdapter({
  showTokensPerSecond: true,
  enableLogging: true
});

await adapter.streamMessage(
  'What is the weather?',
  {
    model: 'gpt-4',
    messages: [],
    functionConfigurationIds: ['weather-functions']
  },
  {
    onStart: () => {
      console.log('Stream started');
    },
    onContent: (delta, fullContent) => {
      console.log('Content delta:', delta);
    },
    onToolExecuting: (event) => {
      if (event.status === 'started') {
        console.log(`Executing ${event.function_name}...`);
      } else if (event.status === 'completed') {
        console.log(`Completed ${event.function_name}:`, event.result);
      }
    },
    onComplete: ({ content, metadata }) => {
      console.log('Final content:', content);
      console.log('Tool calls:', metadata.toolCalls);
      console.log('Metrics:', {
        tokens: metadata.tokensUsed,
        latency: metadata.latency,
        speed: metadata.tokensPerSecond
      });
    },
    onError: (error) => {
      console.error('Stream error:', error);
    }
  }
);
```

### Reasoning Content Handling

The adapter automatically handles models that output to reasoning instead of content:

```typescript
// In SDKChatStreamingAdapter.ts
let totalContent = '';
let totalReasoning = '';

// During streaming
if (content) {
  totalContent += content;
}

if (reasoning) {
  totalReasoning += reasoning;
}

// On completion - use reasoning as fallback
const finalContent = totalContent.length > 0 ? totalContent : totalReasoning;

callbacks.onComplete({
  content: finalContent,
  metadata: { ... }
});
```

This ensures models like `gpt-oss-20b` that non-deterministically output to the `reasoning` field still work correctly.

---

## OpenAI Compatibility

### Guaranteed Compatibility

✅ **Standard OpenAI clients can consume Conduit streams** by:
- Ignoring SSE `event:` field and processing all events as `data:`
- Only handling `ChatCompletionChunk` objects
- Ignoring Conduit-specific event types

### Example: OpenAI SDK

```typescript
import OpenAI from 'openai';

const openai = new OpenAI({
  baseURL: 'https://your-conduit-instance.com/v1',
  apiKey: 'your-api-key'
});

const stream = await openai.chat.completions.create({
  model: 'gpt-4',
  messages: [{ role: 'user', content: 'Hello!' }],
  stream: true,
  // Tool calls work with standard OpenAI format
  tools: [{
    type: 'function',
    function: {
      name: 'get_weather',
      parameters: { ... }
    }
  }]
});

for await (const chunk of stream) {
  // Standard OpenAI chunk processing
  const content = chunk.choices[0]?.delta?.content;
  const toolCalls = chunk.choices[0]?.delta?.tool_calls;
  const finishReason = chunk.choices[0]?.finish_reason;

  // Works exactly like OpenAI API
  // Ignores Conduit extensions automatically
}
```

### What Standard Clients See

When using standard OpenAI clients with Conduit:
- ✅ All `content` events are processed normally
- ✅ Tool calls work via `tool_calls` delta
- ✅ `finish_reason: "tool_calls"` indicates tool execution
- ✅ `finish_reason: "stop"` indicates completion
- ⚠️ Conduit extension events (`reasoning`, `tool-executing`, etc.) are **ignored**
- ⚠️ Enhanced metrics are **not available**

### Conduit-Specific Extensions

To access Conduit extensions, use:
- `@knn_labs/conduit-gateway-client` SDK (TypeScript/Node.js)
- Custom SSE parsers that handle `event:` field
- Type guards for discriminating event types

---

## Best Practices

### 1. Handle finish_reason Correctly

```typescript
if (finishReason === 'tool_calls') {
  // DO NOT end the stream!
  // Backend will execute tools and continue streaming
  console.log('Tool execution in progress...');
  continue;
}

if (finishReason === 'stop' || finishReason === 'length') {
  // Actual completion
  console.log('Stream complete');
  break;
}
```

### 2. Use Type Guards

```typescript
// ✅ Good - type-safe
if (isToolExecutingEvent(event)) {
  const { function_name, status } = event; // TypeScript knows the shape
}

// ❌ Bad - unsafe casting
const toolEvent = event as ToolExecutingEvent;
```

### 3. Handle Reasoning Fallback

Some models output to `reasoning` instead of `content`. Always provide a fallback:

```typescript
let totalContent = '';
let totalReasoning = '';

// Accumulate both
if (content) totalContent += content;
if (reasoning) totalReasoning += reasoning;

// Use reasoning as fallback
const finalContent = totalContent || totalReasoning;
```

### 4. Track Tool Calls Incrementally

Tool calls stream incrementally. Accumulate them:

```typescript
const toolCalls: ToolCall[] = [];

for (const deltaToolCall of deltaToolCalls) {
  const index = deltaToolCall.index ?? 0;

  if (!toolCalls[index]) {
    // Initialize new tool call
    toolCalls[index] = {
      id: deltaToolCall.id ?? '',
      type: 'function',
      function: {
        name: deltaToolCall.function?.name ?? '',
        arguments: deltaToolCall.function?.arguments ?? ''
      }
    };
  } else {
    // Append to existing
    if (deltaToolCall.function?.arguments) {
      toolCalls[index].function.arguments += deltaToolCall.function.arguments;
    }
  }
}
```

### 5. Use Final Metrics for Accuracy

Streaming metrics are estimates. Use `metrics-final` for accurate totals:

```typescript
if (isFinalMetrics(event)) {
  // Accurate counts from backend Stopwatch
  const actualLatency = event.total_latency_ms;
  const actualTokens = event.total_tokens;
  const actualSpeed = event.tokens_per_second;
}
```

---

## Troubleshooting

### Stream Ends Prematurely After Tool Calls

**Problem**: Stream ends immediately after `finish_reason: "tool_calls"`

**Solution**: Do not end the stream on `finish_reason: "tool_calls"`. The backend will execute tools and continue streaming.

```typescript
// ✅ Correct
if (finishReason === 'tool_calls') {
  continue; // Keep processing stream
}

// ❌ Wrong
if (finishReason) {
  break; // Ends too early!
}
```

### No Content Received

**Problem**: `totalContent` is empty but stream completes

**Solution**: Check if model outputted to `reasoning` field instead:

```typescript
const finalContent = totalContent || totalReasoning;
```

### Tool Execution Events Not Firing

**Problem**: Not receiving `tool-executing` events

**Checklist**:
1. Are you using Conduit SDK or parsing `event:` field?
2. Is `function_configuration_ids` provided in request?
3. Are you handling `isToolExecutingEvent()` type guard?

```typescript
// Standard OpenAI SDK won't see these events!
// Use @knn_labs/conduit-gateway-client instead
```

### Type Errors with Tool Calls

**Problem**: TypeScript errors when accessing `tool_calls`

**Solution**: Tool calls come as deltas. Check for undefined:

```typescript
const deltaToolCalls = event.choices?.[0]?.delta?.tool_calls;
if (deltaToolCalls && Array.isArray(deltaToolCalls)) {
  for (const toolCall of deltaToolCalls) {
    // Safe access
  }
}
```

---

## Related Documentation

- [Function Calling Guide](./features/function-calling.md)
- [Gateway API Getting Started](./gateway/getting-started.md)
- [SDK Best Practices](./sdk/best-practices.md)
- [Real-time Streaming Architecture](../architecture/real-time/streaming-and-websockets.md)
