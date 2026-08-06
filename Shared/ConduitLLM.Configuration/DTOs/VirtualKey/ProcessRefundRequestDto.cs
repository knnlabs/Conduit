using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.VirtualKey
{
    /// <summary>
    /// Data transfer object for refund processing requests
    /// </summary>
    public class ProcessRefundRequestDto
    {
        /// <summary>
        /// The model ID that was used in the original request
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// The original usage data that was charged
        /// </summary>
        public UsageDto OriginalUsage { get; set; } = new UsageDto();

        /// <summary>
        /// The usage data to be refunded (must not exceed original usage)
        /// </summary>
        public UsageDto RefundUsage { get; set; } = new UsageDto();

        /// <summary>
        /// The reason for the refund (required)
        /// </summary>
        public string RefundReason { get; set; } = string.Empty;

        /// <summary>
        /// ID of the original debit transaction being refunded
        /// </summary>
        [Required]
        public string OriginalTransactionId { get; set; } = string.Empty;

        /// <summary>
        /// Optional ID of the original request log being refunded. When supplied and that request was
        /// billed from a trusted provider-reported cost, the refund is prorated from the amount actually
        /// charged rather than recomputed from ModelCost rates.
        /// </summary>
        public int? RequestLogId { get; set; }
    }

    /// <summary>
    /// Simplified usage data transfer object for refund requests
    /// </summary>
    public class UsageDto
    {
        /// <summary>
        /// Number of prompt tokens
        /// </summary>
        public int? PromptTokens { get; set; }

        /// <summary>
        /// Number of completion tokens
        /// </summary>
        public int? CompletionTokens { get; set; }

        /// <summary>
        /// Total number of tokens
        /// </summary>
        public int? TotalTokens { get; set; }

        /// <summary>
        /// Number of cached input tokens (read from cache)
        /// </summary>
        public int? CachedInputTokens { get; set; }

        /// <summary>
        /// Whether cached input tokens are included in <see cref="PromptTokens"/>.
        /// Anthropic usage should set this to false.
        /// </summary>
        public bool CachedInputTokensIncludedInPrompt { get; set; } = true;

        /// <summary>
        /// Number of tokens written to cache
        /// </summary>
        public int? CachedWriteTokens { get; set; }

        /// <summary>
        /// Number of reasoning tokens (e.g., OpenAI o1 models)
        /// </summary>
        public int? ReasoningTokens { get; set; }

        /// <summary>
        /// Number of images generated
        /// </summary>
        public int? ImageCount { get; set; }

        /// <summary>
        /// Image quality tier (e.g., "standard", "hd")
        /// </summary>
        public string? ImageQuality { get; set; }

        /// <summary>
        /// Image resolution (e.g., "1024x1024")
        /// </summary>
        public string? ImageResolution { get; set; }

        /// <summary>
        /// Video duration in seconds
        /// </summary>
        public double? VideoDurationSeconds { get; set; }

        /// <summary>
        /// Video resolution (e.g., "1920x1080")
        /// </summary>
        public string? VideoResolution { get; set; }

        /// <summary>
        /// Number of search units (for reranking operations)
        /// </summary>
        public int? SearchUnits { get; set; }

        /// <summary>
        /// Number of inference steps (for image generation)
        /// </summary>
        public int? InferenceSteps { get; set; }

        /// <summary>
        /// Whether this is a batch processing request
        /// </summary>
        public bool? IsBatch { get; set; }
    }
}
