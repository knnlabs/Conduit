# Performance Metrics Architecture

## Overview

ConduitLLM provides comprehensive performance metrics for LLM interactions, including tokens per second, latency measurements, and provider information. This document describes the architecture and implementation of the performance tracking system.

## Architecture

### Core Components

1. **PerformanceMetrics Model** (`ConduitLLM.Core/Models/PerformanceMetrics.cs`)
   - Central DTO for all performance-related data
   - Includes: latency, throughput, token counts, provider info
   - JSON serialization with snake_case for OpenAI compatibility

2. **Decorator Pattern Implementation**
   - `PerformanceTrackingLLMClient`: Wraps any LLM client to add metrics
   - `IPerformanceMetricsService`: Service for metrics calculation
   - Clean separation of concerns - no changes to provider implementations

3. **Configuration** (`PerformanceTrackingSettings`)
   - Enable/disable tracking globally
   - Include metrics in API responses
   - Support for excluding specific providers/models
   - Streaming metrics configuration

### Non-Streaming Metrics

For standard chat completions, metrics are included directly in the response:

```json
{
  "id": "chatcmpl-123",
  "choices": [...],
  "usage": {...},
  "performance_metrics": {
    "total_latency_ms": 1508,
    "tokens_per_second": 9.94,
    "prompt_tokens_per_second": 26.52,
    "provider": "openai",
    "model": "gpt-4",
    "streaming": false
  }
}
```

### Streaming Metrics

Streaming responses use enhanced Server-Sent Events (SSE) with multiple event types:

```
event: content
data: {"choices":[{"delta":{"content":"Hello"}}]}

event: metrics
data: {"elapsed_ms":500,"tokens_generated":5,"current_tokens_per_second":10.0}

event: metrics-final
data: {"total_latency_ms":2000,"time_to_first_token_ms":100,"tokens_per_second":15.5}

event: done
data: [DONE]
```

#### Event Types
- `content`: Standard OpenAI-compatible content chunks
- `metrics`: Periodic metrics updates during streaming
- `metrics-final`: Comprehensive metrics at stream completion
- `done`: Stream termination signal

### Implementation Details

#### 1. Metrics Collection
```csharp
public class StreamingMetricsCollector
{
    public void RecordFirstToken();
    public void RecordToken();
    public StreamingMetrics GetMetrics();
    public StreamingMetrics GetFinalMetrics();
}
```

#### 2. Enhanced SSE Writer
```csharp
public class EnhancedSSEResponseWriter
{
    public async Task WriteContentEventAsync<T>(T data);
    public async Task WriteMetricsEventAsync<T>(T metrics);
    public async Task WriteFinalMetricsEventAsync<T>(T metrics);
}
```

#### 3. Client Integration
The `ConduitApiClient` in WebUI handles multiple event types:
- Parses content events for display
- Captures metrics events for real-time updates
- Stores final metrics with completed messages

## Configuration

### Environment Variables
```bash
# Enable performance tracking
Conduit__PerformanceTracking__Enabled=true

# Include metrics in API responses
Conduit__PerformanceTracking__IncludeInResponse=true

# Enable streaming metrics
Conduit__PerformanceTracking__TrackStreamingMetrics=true

# Store metrics for analytics (future)
Conduit__PerformanceTracking__StoreMetrics=false
```

### Exclusions
```json
{
  "PerformanceTracking": {
    "ExcludedProviders": ["ollama"],
    "ExcludedModels": ["whisper-1", "dall-e-3"]
  }
}
```

## UI Components

### PerformanceStats Component
Displays metrics in a compact, readable format:
- Primary metric: Tokens/second (highlighted)
- Secondary metrics: Latency, TTFT, provider
- Responsive design with dark mode support

### Integration Points
- `ChatHistory.razor`: Shows metrics next to assistant messages
- `Chat.razor`: Captures metrics from API responses
- `StreamingMetricsService`: Manages real-time updates (future)

## Best Practices

1. **Zero Overhead**: Tracking only applies when enabled
2. **Provider Agnostic**: Works with any LLM provider
3. **Backward Compatible**: Doesn't break existing integrations
4. **Standards Compliant**: Uses standard SSE format

## Future Enhancements

1. **Analytics Dashboard**: Historical metrics visualization
2. **Alerting**: Performance degradation notifications
3. **A/B Testing**: Compare provider performance
4. **Cost Tracking**: Integrate with usage costs
5. **Export Capabilities**: Metrics data export

## Troubleshooting

### Metrics Not Showing
1. Verify configuration is enabled
2. Check browser console for errors
3. Ensure provider isn't excluded
4. Confirm API response includes metrics

### Streaming Metrics Issues
1. Check SSE event parsing
2. Verify WebSocket/SSE support
3. Review browser network tab
4. Check for proxy/firewall interference