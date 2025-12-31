namespace ConduitLLM.Core.Models;

/// <summary>
/// Server-Sent Events (SSE) event types supported by Conduit's streaming API.
/// Combines OpenAI-compatible standard events with Conduit-specific extensions.
/// </summary>
public static class SSEEventType
{
    /// <summary>
    /// Standard OpenAI content chunks containing delta updates.
    /// Sent as "data:" without explicit event type for OpenAI compatibility.
    /// Contains: ChatCompletionChunk with content, tool_calls, finish_reason, etc.
    /// </summary>
    public const string Content = "content";

    /// <summary>
    /// Conduit extension: Model reasoning/thinking content separate from main response.
    /// Sent as "event: reasoning" to allow UI to show/hide reasoning independently.
    /// Contains: String chunks of reasoning text.
    /// Use case: Models like o1 that output reasoning process.
    /// </summary>
    public const string Reasoning = "reasoning";

    /// <summary>
    /// Conduit extension: Tool/function execution status and progress updates.
    /// Sent as "event: tool-executing" during tool invocation.
    /// Contains: { status: "started|in_progress|completed|failed", tool_calls: [...], results?: [...] }
    /// Use case: Real-time feedback during slow tool execution (API calls, DB queries).
    /// </summary>
    public const string ToolExecuting = "tool-executing";

    /// <summary>
    /// Conduit extension: Individual tool execution results (optional, for detailed logging).
    /// Sent as "event: tool-result" when a specific tool completes.
    /// Contains: { tool_call_id: string, result: any, error?: string }
    /// Use case: Granular tool execution tracking for debugging or UI display.
    /// </summary>
    public const string ToolResult = "tool-result";

    /// <summary>
    /// Conduit extension: Real-time performance metrics during streaming.
    /// Sent as "event: metrics" periodically throughout the stream.
    /// Contains: { elapsed_ms, tokens_generated, current_tokens_per_second, ... }
    /// Use case: Live performance monitoring and UI displays.
    /// </summary>
    public const string Metrics = "metrics";

    /// <summary>
    /// Conduit extension: Final performance metrics at stream completion.
    /// Sent as "event: metrics-final" after the final content chunk.
    /// Contains: { total_latency_ms, tokens_per_second, total_tokens, ... }
    /// Use case: Accurate final metrics with server-side timing.
    /// </summary>
    public const string MetricsFinal = "metrics-final";

    /// <summary>
    /// Conduit extension: Error events during streaming.
    /// Sent as "event: error" when an error occurs mid-stream.
    /// Contains: { message: string, code?: string, retryable?: boolean }
    /// Use case: Graceful error handling without breaking stream connection.
    /// </summary>
    public const string Error = "error";

    /// <summary>
    /// Standard OpenAI stream termination marker.
    /// Sent as "data: [DONE]" to signal stream completion.
    /// Required for OpenAI SDK compatibility.
    /// </summary>
    public const string Done = "done";
}
