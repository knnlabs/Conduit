namespace ConduitLLM.Core.Events
{
    // ===============================
    // Provider Credential Domain Events
    // ===============================

    /// <summary>
    /// Raised when a new provider is created
    /// Critical for initializing caches and notifying dependent services
    /// </summary>
    public record ProviderCreated : DomainEvent
    {
        /// <summary>
        /// Provider credential database ID
        /// </summary>
        public int ProviderId { get; init; }
        
        /// <summary>
        /// Provider type (OpenAI, Anthropic, etc.)
        /// </summary>
        public string ProviderType { get; init; } = string.Empty;
        
        /// <summary>
        /// Provider display name
        /// </summary>
        public string ProviderName { get; init; } = string.Empty;
        
        /// <summary>
        /// Base URL for the provider (if custom)
        /// </summary>
        public string? BaseUrl { get; init; }
        
        /// <summary>
        /// Whether the provider is enabled
        /// </summary>
        public bool IsEnabled { get; init; }
        
        /// <summary>
        /// When the provider was created
        /// </summary>
        public DateTime CreatedAt { get; init; }
        
        /// <summary>
        /// Partition key for ordered processing per provider
        /// </summary>
        public string PartitionKey => ProviderId.ToString();
    }

    /// <summary>
    /// Raised when provider credentials are updated
    /// Critical for invalidating cached credentials across services
    /// </summary>
    public record ProviderUpdated : DomainEvent
    {
        /// <summary>
        /// Provider credential database ID
        /// </summary>
        public int ProviderId { get; init; }
        
        /// <summary>
        /// Whether the provider is currently enabled
        /// </summary>
        public bool IsEnabled { get; init; }
        
        /// <summary>
        /// Properties that were changed
        /// </summary>
        public string[] ChangedProperties { get; init; } = Array.Empty<string>();
        
        /// <summary>
        /// Partition key for ordered processing per provider
        /// </summary>
        public string PartitionKey => ProviderId.ToString();
    }

    /// <summary>
    /// Raised when provider credentials are deleted
    /// Critical for cleanup and cache invalidation
    /// </summary>
    public record ProviderDeleted : DomainEvent
    {
        /// <summary>
        /// Provider credential database ID
        /// </summary>
        public int ProviderId { get; init; }
        
        /// <summary>
        /// Partition key for ordered processing per provider
        /// </summary>
        public string PartitionKey => ProviderId.ToString();
    }
}