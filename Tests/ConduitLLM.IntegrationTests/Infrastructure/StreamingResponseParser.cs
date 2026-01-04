using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ConduitLLM.IntegrationTests.Infrastructure;

/// <summary>
/// Utility class for parsing Server-Sent Events (SSE) streaming responses.
/// Used for testing streaming chat completions.
/// </summary>
public static class StreamingResponseParser
{
    /// <summary>
    /// Parses an SSE stream and yields events as they arrive.
    /// </summary>
    /// <param name="stream">The response stream from a streaming chat completion request.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An async enumerable of SSE events.</returns>
    public static async IAsyncEnumerable<SSEEvent> ParseAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);
        string? currentEventType = null;

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line))
            {
                // Empty line - end of event
                currentEventType = null;
                continue;
            }

            if (line.StartsWith("event:"))
            {
                currentEventType = line[6..].Trim();
            }
            else if (line.StartsWith("data:"))
            {
                var data = line[5..].Trim();
                if (data == "[DONE]")
                {
                    yield return new SSEEvent
                    {
                        EventType = "done",
                        Data = data,
                        IsDone = true
                    };
                    yield break;
                }

                yield return new SSEEvent
                {
                    EventType = currentEventType,
                    Data = data,
                    IsDone = false
                };

                // Reset event type after yielding
                currentEventType = null;
            }
        }
    }

    /// <summary>
    /// Collects all events from a stream into a list.
    /// </summary>
    public static async Task<List<SSEEvent>> CollectAllAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var events = new List<SSEEvent>();
        await foreach (var evt in ParseAsync(stream, cancellationToken))
        {
            events.Add(evt);
        }
        return events;
    }

    /// <summary>
    /// Extracts the aggregated content from all streaming chunks.
    /// </summary>
    public static string ExtractContent(IEnumerable<SSEEvent> events)
    {
        var content = new System.Text.StringBuilder();

        foreach (var evt in events.Where(e => !e.IsDone && e.EventType != "metrics-final"))
        {
            try
            {
                using var doc = JsonDocument.Parse(evt.Data);
                var root = doc.RootElement;

                // Handle standard chat completion chunks
                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("content", out var contentElement))
                    {
                        var chunk = contentElement.GetString();
                        if (!string.IsNullOrEmpty(chunk))
                        {
                            content.Append(chunk);
                        }
                    }
                }
                // Handle reasoning events
                else if (evt.EventType == "reasoning" &&
                         root.TryGetProperty("content", out var reasoningContent))
                {
                    var chunk = reasoningContent.GetString();
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        content.Append(chunk);
                    }
                }
            }
            catch (JsonException)
            {
                // Skip malformed JSON chunks
            }
        }

        return content.ToString();
    }

    /// <summary>
    /// Extracts usage information from the final metrics event or the last chunk.
    /// </summary>
    public static StreamingUsage? ExtractFinalUsage(IEnumerable<SSEEvent> events)
    {
        // First, try to find metrics-final event (Conduit-specific)
        var metricsFinalEvent = events.FirstOrDefault(e => e.EventType == "metrics-final");
        if (metricsFinalEvent != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(metricsFinalEvent.Data);
                var root = doc.RootElement;

                return new StreamingUsage
                {
                    PromptTokens = GetIntOrNull(root, "prompt_tokens") ?? 0,
                    CompletionTokens = GetIntOrNull(root, "completion_tokens") ?? 0,
                    TotalTokens = GetIntOrNull(root, "total_tokens") ?? 0,
                    TokensPerSecond = GetDoubleOrNull(root, "tokens_per_second")
                };
            }
            catch (JsonException)
            {
                // Fall through to try other methods
            }
        }

        // Try to find usage in the last non-done chunk (OpenAI style)
        var lastChunk = events
            .Where(e => !e.IsDone && e.EventType != "metrics-final")
            .LastOrDefault();

        if (lastChunk != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(lastChunk.Data);
                var root = doc.RootElement;

                if (root.TryGetProperty("usage", out var usage))
                {
                    return new StreamingUsage
                    {
                        PromptTokens = GetIntOrNull(usage, "prompt_tokens") ?? 0,
                        CompletionTokens = GetIntOrNull(usage, "completion_tokens") ?? 0,
                        TotalTokens = GetIntOrNull(usage, "total_tokens") ?? 0
                    };
                }
            }
            catch (JsonException)
            {
                // No usage found
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts tool call events from the stream.
    /// </summary>
    public static List<ToolCallEvent> ExtractToolCalls(IEnumerable<SSEEvent> events)
    {
        var toolCalls = new List<ToolCallEvent>();

        foreach (var evt in events.Where(e => e.EventType == "tool-executing"))
        {
            try
            {
                using var doc = JsonDocument.Parse(evt.Data);
                var root = doc.RootElement;

                toolCalls.Add(new ToolCallEvent
                {
                    ToolName = root.TryGetProperty("tool_name", out var name) ? name.GetString() : null,
                    State = root.TryGetProperty("state", out var state) ? state.GetString() : null,
                    Arguments = root.TryGetProperty("arguments", out var args) ? args.ToString() : null
                });
            }
            catch (JsonException)
            {
                // Skip malformed tool call events
            }
        }

        return toolCalls;
    }

    private static int? GetIntOrNull(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetInt32();
        }
        return null;
    }

    private static double? GetDoubleOrNull(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetDouble();
        }
        return null;
    }
}

/// <summary>
/// Represents a Server-Sent Event from a streaming response.
/// </summary>
public class SSEEvent
{
    /// <summary>
    /// The event type (e.g., "content", "reasoning", "tool-executing", "metrics-final").
    /// Null for standard data-only events.
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// The JSON data payload of the event.
    /// </summary>
    public string Data { get; set; } = "";

    /// <summary>
    /// True if this is the [DONE] marker indicating end of stream.
    /// </summary>
    public bool IsDone { get; set; }
}

/// <summary>
/// Usage information extracted from streaming responses.
/// </summary>
public class StreamingUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public double? TokensPerSecond { get; set; }
}

/// <summary>
/// Tool call information from streaming responses.
/// </summary>
public class ToolCallEvent
{
    public string? ToolName { get; set; }
    public string? State { get; set; }  // "started", "completed"
    public string? Arguments { get; set; }
}
