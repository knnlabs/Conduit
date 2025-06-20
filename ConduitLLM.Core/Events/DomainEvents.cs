using System;
using System.Collections.Generic;

namespace ConduitLLM.Core.Events
{
    /// <summary>
    /// Base interface for all domain events in the Conduit system
    /// </summary>
    public interface IDomainEvent
    {
        /// <summary>
        /// Unique identifier for the event
        /// </summary>
        string EventId { get; }
        
        /// <summary>
        /// Timestamp when the event occurred
        /// </summary>
        DateTime Timestamp { get; }
        
        /// <summary>
        /// Correlation ID for tracking related events
        /// </summary>
        string CorrelationId { get; }
    }

    /// <summary>
    /// Base record for domain events with common properties
    /// </summary>
    public abstract record DomainEvent : IDomainEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string CorrelationId { get; init; } = string.Empty;
    }

    // ===============================
    // Virtual Key Domain Events
    // ===============================

    /// <summary>
    /// Raised when a virtual key is updated (properties changed)
    /// Critical for cache invalidation across all services
    /// </summary>
    public record VirtualKeyUpdated : DomainEvent
    {
        /// <summary>
        /// Virtual Key database ID
        /// </summary>
        public int KeyId { get; init; }
        
        /// <summary>
        /// Virtual Key hash for cache invalidation
        /// </summary>
        public string KeyHash { get; init; } = string.Empty;
        
        /// <summary>
        /// Properties that were changed (for selective invalidation)
        /// </summary>
        public string[] ChangedProperties { get; init; } = Array.Empty<string>();
        
        /// <summary>
        /// Partition key for ordered processing per virtual key
        /// </summary>
        public string PartitionKey => KeyId.ToString();
    }

    /// <summary>
    /// Raised when a virtual key is deleted
    /// Critical for cache invalidation and cleanup
    /// </summary>
    public record VirtualKeyDeleted : DomainEvent
    {
        /// <summary>
        /// Virtual Key database ID
        /// </summary>
        public int KeyId { get; init; }
        
        /// <summary>
        /// Virtual Key hash for cache invalidation
        /// </summary>
        public string KeyHash { get; init; } = string.Empty;
        
        /// <summary>
        /// Key name for logging/audit purposes
        /// </summary>
        public string KeyName { get; init; } = string.Empty;
        
        /// <summary>
        /// Partition key for ordered processing per virtual key
        /// </summary>
        public string PartitionKey => KeyId.ToString();
    }

    /// <summary>
    /// Request to update virtual key spend (replaces direct UpdateSpendAsync calls)
    /// Enables ordered processing and eliminates race conditions
    /// </summary>
    public record SpendUpdateRequested : DomainEvent
    {
        /// <summary>
        /// Virtual Key database ID
        /// </summary>
        public int KeyId { get; init; }
        
        /// <summary>
        /// Amount to add to current spend
        /// </summary>
        public decimal Amount { get; init; }
        
        /// <summary>
        /// Optional request identifier for tracking
        /// </summary>
        public string RequestId { get; init; } = string.Empty;
        
        /// <summary>
        /// Partition key for ordered processing per virtual key
        /// </summary>
        public string PartitionKey => KeyId.ToString();
    }

    /// <summary>
    /// Confirmation that virtual key spend was updated
    /// Used for cache invalidation and audit logging
    /// </summary>
    public record SpendUpdated : DomainEvent
    {
        /// <summary>
        /// Virtual Key database ID
        /// </summary>
        public int KeyId { get; init; }
        
        /// <summary>
        /// Virtual Key hash for cache invalidation
        /// </summary>
        public string KeyHash { get; init; } = string.Empty;
        
        /// <summary>
        /// Amount that was added
        /// </summary>
        public decimal Amount { get; init; }
        
        /// <summary>
        /// New total spend after update
        /// </summary>
        public decimal NewTotalSpend { get; init; }
        
        /// <summary>
        /// Optional request identifier for correlation
        /// </summary>
        public string RequestId { get; init; } = string.Empty;
        
        /// <summary>
        /// Partition key for ordered processing per virtual key
        /// </summary>
        public string PartitionKey => KeyId.ToString();
    }

    // ===============================
    // Provider Credential Domain Events
    // ===============================

    /// <summary>
    /// Raised when provider credentials are updated
    /// Critical for invalidating cached credentials across services
    /// </summary>
    public record ProviderCredentialUpdated : DomainEvent
    {
        /// <summary>
        /// Provider credential database ID
        /// </summary>
        public int ProviderId { get; init; }
        
        /// <summary>
        /// Provider name (OpenAI, Anthropic, etc.)
        /// </summary>
        public string ProviderName { get; init; } = string.Empty;
        
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
    public record ProviderCredentialDeleted : DomainEvent
    {
        /// <summary>
        /// Provider credential database ID
        /// </summary>
        public int ProviderId { get; init; }
        
        /// <summary>
        /// Provider name for logging
        /// </summary>
        public string ProviderName { get; init; } = string.Empty;
        
        /// <summary>
        /// Partition key for ordered processing per provider
        /// </summary>
        public string PartitionKey => ProviderId.ToString();
    }

    // ===============================
    // Model Capability Domain Events
    // ===============================

    /// <summary>
    /// Raised when model capabilities are discovered for a provider
    /// Eliminates redundant external API calls across services
    /// </summary>
    public record ModelCapabilitiesDiscovered : DomainEvent
    {
        /// <summary>
        /// Provider credential database ID
        /// </summary>
        public int ProviderId { get; init; }
        
        /// <summary>
        /// Provider name
        /// </summary>
        public string ProviderName { get; init; } = string.Empty;
        
        /// <summary>
        /// Discovered models and their capabilities
        /// Key: ModelId, Value: Capability flags
        /// </summary>
        public Dictionary<string, ModelCapabilities> ModelCapabilities { get; init; } = new();
        
        /// <summary>
        /// When the discovery was performed
        /// </summary>
        public DateTime DiscoveredAt { get; init; } = DateTime.UtcNow;
        
        /// <summary>
        /// Partition key for ordered processing per provider
        /// </summary>
        public string PartitionKey => ProviderId.ToString();
    }

    /// <summary>
    /// Model capability flags
    /// </summary>
    public record ModelCapabilities
    {
        public bool SupportsImageGeneration { get; init; }
        public bool SupportsVision { get; init; }
        public bool SupportsEmbeddings { get; init; }
        public bool SupportsAudioTranscription { get; init; }
        public bool SupportsTextToSpeech { get; init; }
        public bool SupportsRealtimeAudio { get; init; }
        public bool SupportsFunctionCalling { get; init; }
        public Dictionary<string, object> AdditionalCapabilities { get; init; } = new();
    }

    // ===============================
    // Health Monitoring Domain Events
    // ===============================

    /// <summary>
    /// Raised when provider health status changes
    /// Enables real-time health propagation across services
    /// </summary>
    public record ProviderHealthChanged : DomainEvent
    {
        /// <summary>
        /// Provider credential database ID
        /// </summary>
        public int ProviderId { get; init; }
        
        /// <summary>
        /// Provider name
        /// </summary>
        public string ProviderName { get; init; } = string.Empty;
        
        /// <summary>
        /// Whether the provider is currently healthy
        /// </summary>
        public bool IsHealthy { get; init; }
        
        /// <summary>
        /// Health status details
        /// </summary>
        public string Status { get; init; } = string.Empty;
        
        /// <summary>
        /// Additional health check data
        /// </summary>
        public Dictionary<string, object> HealthData { get; init; } = new();
        
        /// <summary>
        /// Partition key for ordered processing per provider
        /// </summary>
        public string PartitionKey => ProviderId.ToString();
    }

    // ===============================
    // Model Mapping Domain Events
    // ===============================

    /// <summary>
    /// Raised when model mappings are added or updated
    /// Critical for navigation state updates
    /// </summary>
    public record ModelMappingChanged : DomainEvent
    {
        /// <summary>
        /// Model mapping database ID
        /// </summary>
        public int MappingId { get; init; }
        
        /// <summary>
        /// Model alias
        /// </summary>
        public string ModelAlias { get; init; } = string.Empty;
        
        /// <summary>
        /// Provider credential ID
        /// </summary>
        public int ProviderId { get; init; }
        
        /// <summary>
        /// Whether the mapping is enabled
        /// </summary>
        public bool IsEnabled { get; init; }
        
        /// <summary>
        /// Type of change (Created, Updated, Deleted)
        /// </summary>
        public string ChangeType { get; init; } = string.Empty;
        
        /// <summary>
        /// Partition key for ordered processing per mapping
        /// </summary>
        public string PartitionKey => MappingId.ToString();
    }
}