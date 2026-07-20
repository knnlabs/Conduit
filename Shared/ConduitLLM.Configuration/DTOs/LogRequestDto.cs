namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for logging a request
    /// </summary>
    public class LogRequestDto
    {
        /// <summary>
        /// Unique identifier for the log entry
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID of the virtual key used for the request
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Name of the model used for the request
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// ID of the provider that processed the request.
        /// </summary>
        public int? ProviderId { get; set; }

        /// <summary>
        /// Type of the provider that processed the request (e.g., "OpenAI", "Anthropic").
        /// </summary>
        public string? ProviderType { get; set; }
        public int? ModelProviderMappingId { get; set; }
        public bool PromptCachingEligible { get; set; }
        public bool PromptCachingPolicyApplied { get; set; }
        public decimal CachedReadSavings { get; set; }
        public decimal CacheWritePremium { get; set; }
        public bool RoutingAffinityUsed { get; set; }
        public string? RoutingDecisionReason { get; set; }
        public int RoutingFailoverCount { get; set; }

        /// <summary>
        /// Type of the request (chat, completion, embedding, etc.)
        /// </summary>
        public string RequestType { get; set; } = string.Empty;

        /// <summary>
        /// Number of input tokens in the request
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Number of output tokens in the response
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Number of input tokens read from cache. Null if caching was not used.
        /// </summary>
        public int? CachedInputTokens { get; set; }

        /// <summary>
        /// Number of tokens written to cache. Null if caching was not used.
        /// </summary>
        public int? CachedWriteTokens { get; set; }

        /// <summary>
        /// Cost of the request
        /// </summary>
        public decimal Cost { get; set; }

        /// <summary>
        /// How the cost was determined (ModelCost vs. provider-reported cost). Null for
        /// ModelCost-billed requests.
        /// </summary>
        public Enums.RequestBillingMethod? BillingMethod { get; set; }

        /// <summary>
        /// The raw provider-reported cost (USD, pre-markup) when billed from provider cost; else null.
        /// </summary>
        public decimal? ProviderReportedCostUsd { get; set; }

        /// <summary>
        /// Provider-cost markup multiplier applied to the reported cost.
        /// </summary>
        public decimal? ProviderCostMarkupMultiplier { get; set; }

        /// <summary>
        /// Timestamp of the billable event. Null for zero-cost/unbilled rows.
        /// </summary>
        public DateTime? BilledAtUtc { get; set; }

        /// <summary>
        /// Response time in milliseconds
        /// </summary>
        public double ResponseTimeMs { get; set; }

        /// <summary>
        /// Optional identifier of the user making the request
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Optional IP address of the client making the request
        /// </summary>
        public string? ClientIp { get; set; }

        /// <summary>
        /// Optional request path
        /// </summary>
        public string? RequestPath { get; set; }

        /// <summary>
        /// Optional status code of the response
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Timestamp of when the request was made
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional metadata as JSON for request-type-specific details.
        /// Used for functions, images, video, audio, and other execution types.
        /// </summary>
        public string? Metadata { get; set; }
    }
}
