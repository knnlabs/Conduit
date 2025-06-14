using ConduitLLM.Core.Models;

namespace ConduitLLM.WebUI.Models
{
    /// <summary>
    /// Represents a streaming chat response that can contain either content or metrics.
    /// </summary>
    public class StreamingChatResponse
    {
        /// <summary>
        /// The type of response: "content", "metrics", "metrics-final", "done", or "error".
        /// </summary>
        public string EventType { get; set; } = "content";

        /// <summary>
        /// Chat completion chunk for content events.
        /// </summary>
        public ChatCompletionChunk? Chunk { get; set; }

        /// <summary>
        /// Performance metrics for metrics events.
        /// </summary>
        public PerformanceMetrics? Metrics { get; set; }

        /// <summary>
        /// Error message for error events.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Indicates if this is the final event in the stream.
        /// </summary>
        public bool IsFinal => EventType == "done" || EventType == "metrics-final" || EventType == "error";
    }
}