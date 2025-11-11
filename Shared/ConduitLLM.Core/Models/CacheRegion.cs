namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Defines the cache regions used throughout the Conduit application.
    /// Each region represents a logical grouping of cached data with specific characteristics.
    /// </summary>
    public enum CacheRegion
    {
        /// <summary>
        /// Virtual keys used for API authentication and authorization.
        /// High-security region with immediate invalidation requirements.
        /// </summary>
        VirtualKeys,

        /// <summary>
        /// Rate limiting data for both IP-based and Virtual Key-based limits.
        /// Requires fast access and distributed consistency.
        /// </summary>
        RateLimits,

        /// <summary>
        /// Provider health status and availability information.
        /// Updated frequently based on health checks.
        /// </summary>
        ProviderHealth,

        /// <summary>
        /// Model capabilities and metadata from providers.
        /// Relatively static data with periodic updates.
        /// </summary>
        ModelMetadata,

        /// <summary>
        /// Authentication tokens for admin and service accounts.
        /// Security-critical with strict expiration policies.
        /// </summary>
        AuthTokens,

        /// <summary>
        /// IP filtering rules for security.
        /// Requires immediate propagation of changes.
        /// </summary>
        IpFilters,

        /// <summary>
        /// Asynchronous task status and results.
        /// Short-lived data with automatic cleanup.
        /// </summary>
        AsyncTasks,

        /// <summary>
        /// Provider response caching for identical requests.
        /// Cost optimization with configurable TTL.
        /// </summary>
        ProviderResponses,

        /// <summary>
        /// Embedding vectors for semantic search and similarity.
        /// Large data size with model-specific invalidation.
        /// </summary>
        Embeddings,

        /// <summary>
        /// Global application settings.
        /// Infrequently changed with immediate propagation needs.
        /// </summary>
        GlobalSettings,

        /// <summary>
        /// Provider credentials for external service authentication.
        /// Security-sensitive with encryption requirements.
        /// </summary>
        Providers,

        /// <summary>
        /// Model cost information for billing calculations.
        /// Updated periodically from provider pricing.
        /// </summary>
        ModelCosts,

        /// <summary>
        /// Audio stream data for real-time processing.
        /// Temporary storage with streaming requirements.
        /// </summary>
        AudioStreams,

        /// <summary>
        /// Alert and monitoring data.
        /// Time-series data with retention policies.
        /// </summary>
        Monitoring,

        /// <summary>
        /// Default region for unspecified cache operations.
        /// Should be avoided in favor of specific regions.
        /// </summary>
        Default
    }
}