using System;
using System.Collections.Generic;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Events
{
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
        public bool SupportsVideoGeneration { get; init; }
        public bool SupportsAudioTranscription { get; init; }
        public bool SupportsTextToSpeech { get; init; }
        public bool SupportsRealtimeAudio { get; init; }
        public bool SupportsFunctionCalling { get; init; }
        public Dictionary<string, object> AdditionalCapabilities { get; init; } = new();
    }

    // ===============================
    // Model Cost Domain Events
    // ===============================

    /// <summary>
    /// Raised when model costs are created, updated, or deleted
    /// Critical for cache invalidation across all services
    /// </summary>
    public record ModelCostChanged : DomainEvent
    {
        /// <summary>
        /// Model cost database ID
        /// </summary>
        public int ModelCostId { get; init; }
        
        /// <summary>
        /// Cost name that was affected
        /// </summary>
        public string CostName { get; init; } = string.Empty;
        
        /// <summary>
        /// Type of change (Created, Updated, Deleted)
        /// </summary>
        public string ChangeType { get; init; } = string.Empty;
        
        /// <summary>
        /// Properties that were changed (for selective invalidation)
        /// </summary>
        public string[] ChangedProperties { get; init; } = Array.Empty<string>();
        
        /// <summary>
        /// Partition key for ordered processing per model cost
        /// </summary>
        public string PartitionKey => ModelCostId.ToString();
    }

    // ===============================
    // Global Setting Domain Events
    // ===============================

    /// <summary>
    /// Raised when a global setting is created, updated, or deleted
    /// Critical for cache invalidation across all services
    /// </summary>
    public record GlobalSettingChanged : DomainEvent
    {
        /// <summary>
        /// Global setting database ID
        /// </summary>
        public int SettingId { get; init; }
        
        /// <summary>
        /// Global setting key
        /// </summary>
        public string SettingKey { get; init; } = string.Empty;
        
        /// <summary>
        /// Type of change (Created, Updated, Deleted)
        /// </summary>
        public string ChangeType { get; init; } = string.Empty;
        
        /// <summary>
        /// Properties that were changed (for selective invalidation)
        /// </summary>
        public string[] ChangedProperties { get; init; } = Array.Empty<string>();
        
        /// <summary>
        /// Partition key for ordered processing per setting
        /// </summary>
        public string PartitionKey => SettingId.ToString();
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

    // ===============================
    // IP Filter Domain Events
    // ===============================

    /// <summary>
    /// Raised when an IP filter is created, updated, or deleted
    /// Critical for cache invalidation and security policy updates across services
    /// </summary>
    public record IpFilterChanged : DomainEvent
    {
        /// <summary>
        /// IP filter database ID
        /// </summary>
        public int FilterId { get; init; }
        
        /// <summary>
        /// IP address or CIDR range
        /// </summary>
        public string IpAddressOrCidr { get; init; } = string.Empty;
        
        /// <summary>
        /// Filter type (whitelist/blacklist)
        /// </summary>
        public string FilterType { get; init; } = string.Empty;
        
        /// <summary>
        /// Whether the filter is enabled
        /// </summary>
        public bool IsEnabled { get; init; }
        
        /// <summary>
        /// Type of change (Created, Updated, Deleted)
        /// </summary>
        public string ChangeType { get; init; } = string.Empty;
        
        /// <summary>
        /// Properties that were changed (for selective invalidation)
        /// </summary>
        public string[] ChangedProperties { get; init; } = Array.Empty<string>();
        
        /// <summary>
        /// Filter description for logging
        /// </summary>
        public string Description { get; init; } = string.Empty;
        
        /// <summary>
        /// Partition key for ordered processing per filter
        /// </summary>
        public string PartitionKey => FilterId.ToString();
    }
}