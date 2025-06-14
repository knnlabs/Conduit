# Streaming Performance Metrics Architecture

## Overview

This document describes the implemented architecture for delivering performance metrics alongside streaming chat completions without disrupting the content stream.

## Implementation Status ✅

- Streaming responses use Server-Sent Events (SSE) to deliver content chunks
- Performance metrics are calculated server-side and transmitted during streaming
- WebUI displays metrics for both streaming and non-streaming responses
- Implementation uses enhanced SSE with multiple event types

## Implemented Solution: Enhanced SSE Architecture

### Core Concept

Establish two communication channels:
1. **Primary Channel**: Existing SSE stream for content chunks
2. **Metrics Channel**: Secondary channel for performance telemetry

### Architecture Components

```
┌─────────────┐     ┌─────────────────┐     ┌──────────────┐
│   WebUI     │────▶│  API Gateway    │────▶│  LLM Client  │
│             │     │                 │     │              │
│ ┌─────────┐ │     │ ┌─────────────┐ │     │ ┌──────────┐ │
│ │Content  │◀──SSE──│ │Content      │◀──────│ │Provider  │ │
│ │Display  │ │     │ │Streaming    │ │     │ │Response  │ │
│ └─────────┘ │     │ └─────────────┘ │     │ └──────────┘ │
│             │     │                 │     │              │
│ ┌─────────┐ │     │ ┌─────────────┐ │     │ ┌──────────┐ │
│ │Metrics  │◀──WS───│ │Metrics      │◀──────│ │Perf      │ │
│ │Display  │ │     │ │Channel      │ │     │ │Tracking  │ │
│ └─────────┘ │     │ └─────────────┘ │     │ └──────────┘ │
└─────────────┘     └─────────────────┘     └──────────────┘
```

### Implementation Options

#### Option 1: Enhanced SSE with Metadata Events (Recommended)

**Approach**: Use SSE's event type feature to send both content and metrics through the same connection.

**Benefits**:
- Single connection (efficient)
- Built on existing infrastructure
- Clean separation via event types
- Progressive metrics updates

**Implementation**:
```typescript
// Content event
event: content
data: {"choices":[{"delta":{"content":"Hello"}}]}

// Metrics event
event: metrics
data: {"ttft":120,"tokens_per_second":45.2,"tokens_generated":15}

// Final metrics event
event: metrics-final
data: {"total_latency":2500,"total_tokens":127,"avg_tps":50.8}
```

#### Option 2: WebSocket Sidecar

**Approach**: Establish a WebSocket connection for real-time metrics alongside SSE content stream.

**Benefits**:
- True bidirectional communication
- Can send client metrics back
- More flexible for future features

**Drawbacks**:
- Additional connection overhead
- More complex client implementation

#### Option 3: HTTP Polling for Metrics

**Approach**: Client polls a metrics endpoint during streaming.

**Benefits**:
- Simple implementation
- Works with any client

**Drawbacks**:
- Inefficient (many requests)
- Not real-time

### Implementation Details

#### Implemented Components ✅

1. **StreamingMetricsCollector** (`ConduitLLM.Http/Services/StreamingMetricsCollector.cs`)
   - Tracks per-request metrics during streaming
   - Calculates running statistics (tokens/sec, latencies)
   - Thread-safe implementation

2. **EnhancedSSEResponseWriter** (`ConduitLLM.Http/Services/EnhancedSSEResponseWriter.cs`)
   ```csharp
   public class EnhancedSSEResponseWriter
   {
       public async Task WriteContentEventAsync<T>(T data);
       public async Task WriteMetricsEventAsync<T>(T metrics);
       public async Task WriteFinalMetricsEventAsync<T>(T metrics);
       public async Task WriteDoneEventAsync();
   }
   ```

3. **Request Context Enhancement**
   - Request ID added via X-Request-ID header
   - Metrics tracked per request during streaming

#### Phase 2: API Enhancement

1. **Modify Streaming Endpoint**
   ```csharp
   [HttpPost("v1/chat/completions")]
   public async Task StreamCompletionWithMetrics(
       ChatCompletionRequest request,
       CancellationToken cancellationToken)
   {
       var requestId = Guid.NewGuid().ToString();
       Response.Headers.Add("X-Request-ID", requestId);
       
       // Start metrics collection
       var metricsCollector = new StreamingMetricsCollector(requestId);
       
       // Stream content chunks
       await foreach (var chunk in GetCompletionStream(request))
       {
           // Send content event
           await WriteSSEAsync("content", chunk);
           
           // Periodically send metrics events
           if (metricsCollector.ShouldEmitMetrics())
           {
               await WriteSSEAsync("metrics", metricsCollector.GetMetrics());
           }
       }
       
       // Send final metrics
       await WriteSSEAsync("metrics-final", metricsCollector.GetFinalMetrics());
   }
   ```

#### Phase 3: Client-Side Implementation

1. **Enhanced SSE Client**
   ```typescript
   interface StreamingClient {
       onContent(handler: (chunk: ChatCompletionChunk) => void): void;
       onMetrics(handler: (metrics: StreamingMetrics) => void): void;
       onFinalMetrics(handler: (metrics: PerformanceMetrics) => void): void;
   }
   ```

2. **WebUI Integration**
   - Update `ConduitApiClient` to handle multiple event types
   - Create `StreamingMetricsStore` to manage metrics state
   - Update `ChatMessage` component to display live metrics

### Data Models

```csharp
public class StreamingMetrics
{
    public string RequestId { get; set; }
    public long ElapsedMs { get; set; }
    public int TokensGenerated { get; set; }
    public double CurrentTokensPerSecond { get; set; }
    public long? TimeToFirstTokenMs { get; set; }
    public double? AvgInterTokenLatencyMs { get; set; }
}

public class StreamingMetricsUpdate
{
    public string EventType { get; set; } // "progress" | "final"
    public StreamingMetrics Metrics { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
```

### Benefits of This Approach

1. **Progressive Enhancement**: Works with existing clients, enhanced clients get metrics
2. **Efficient**: Single connection, minimal overhead
3. **Real-time**: Metrics update during streaming
4. **Extensible**: Can add more telemetry types later
5. **Standard Compliant**: Uses standard SSE event types

### Migration Strategy

1. **Phase 1**: Deploy server with metrics events (backward compatible)
2. **Phase 2**: Update WebUI to consume metrics events
3. **Phase 3**: Add configuration to control metrics frequency
4. **Phase 4**: Extend to other endpoints (embeddings, etc.)

### Alternative: Client-Side Estimation

As a fallback or complement, implement client-side metrics estimation:

```typescript
class ClientSideMetricsEstimator {
    private startTime: number;
    private firstTokenTime?: number;
    private tokenCount: number = 0;
    private lastTokenTime: number;
    
    onChunkReceived(chunk: ChatCompletionChunk) {
        if (!this.firstTokenTime && chunk.choices[0]?.delta?.content) {
            this.firstTokenTime = Date.now();
        }
        
        // Count tokens (approximate)
        const content = chunk.choices[0]?.delta?.content || '';
        this.tokenCount += this.estimateTokens(content);
        this.lastTokenTime = Date.now();
    }
    
    getMetrics(): ClientSideMetrics {
        const elapsed = Date.now() - this.startTime;
        return {
            timeToFirstToken: this.firstTokenTime ? this.firstTokenTime - this.startTime : null,
            estimatedTokensPerSecond: this.tokenCount / (elapsed / 1000),
            tokensGenerated: this.tokenCount
        };
    }
}
```

### Security Considerations

1. **Request ID Generation**: Use cryptographically secure random IDs
2. **Metrics Access Control**: Ensure users can only access their own metrics
3. **Rate Limiting**: Implement limits on metrics emission frequency
4. **Data Retention**: Clear metrics cache after reasonable timeout

### Performance Considerations

1. **Memory Usage**: Implement sliding window for metrics storage
2. **Emission Frequency**: Balance between real-time updates and overhead
3. **Compression**: Consider compressing metrics events for large responses

## Conclusion

The enhanced SSE approach provides the best balance of simplicity, efficiency, and functionality. It builds on existing infrastructure while providing real-time performance visibility for streaming responses.