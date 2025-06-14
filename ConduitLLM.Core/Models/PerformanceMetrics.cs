using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Performance metrics for LLM operations.
    /// </summary>
    public class PerformanceMetrics
    {
        /// <summary>
        /// Total response time in milliseconds.
        /// </summary>
        [JsonPropertyName("total_latency_ms")]
        public long TotalLatencyMs { get; set; }

        /// <summary>
        /// Time to first token in milliseconds (for streaming responses).
        /// </summary>
        [JsonPropertyName("time_to_first_token_ms")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? TimeToFirstTokenMs { get; set; }

        /// <summary>
        /// Overall tokens per second (completion tokens / generation time).
        /// </summary>
        [JsonPropertyName("tokens_per_second")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? TokensPerSecond { get; set; }

        /// <summary>
        /// Prompt processing speed in tokens per second.
        /// </summary>
        [JsonPropertyName("prompt_tokens_per_second")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? PromptTokensPerSecond { get; set; }

        /// <summary>
        /// Completion generation speed in tokens per second.
        /// </summary>
        [JsonPropertyName("completion_tokens_per_second")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? CompletionTokensPerSecond { get; set; }

        /// <summary>
        /// Provider name that handled the request.
        /// </summary>
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Model name used for the request.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Whether this was a streaming response.
        /// </summary>
        [JsonPropertyName("streaming")]
        public bool Streaming { get; set; }

        /// <summary>
        /// Number of retry attempts (if any).
        /// </summary>
        [JsonPropertyName("retry_attempts")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int RetryAttempts { get; set; }

        /// <summary>
        /// Average inter-token latency in milliseconds (for streaming).
        /// </summary>
        [JsonPropertyName("avg_inter_token_latency_ms")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? AvgInterTokenLatencyMs { get; set; }
    }
}