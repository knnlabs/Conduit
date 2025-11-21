namespace ConduitLLM.Core.Events
{

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
    // Model Domain Events
    // ===============================

    /// <summary>
    /// Raised when a model is created, updated, or deleted
    /// Critical for discovery cache invalidation
    /// </summary>
    public record ModelUpdated : DomainEvent
    {
        /// <summary>
        /// Model database ID
        /// </summary>
        public int ModelId { get; init; }
        
        /// <summary>
        /// Model name
        /// </summary>
        public string ModelName { get; init; } = string.Empty;
        
        /// <summary>
        /// Model series ID
        /// </summary>
        public int ModelSeriesId { get; init; }
        
        /// <summary>
        /// Type of change (Created, Updated, Deleted)
        /// </summary>
        public string ChangeType { get; init; } = string.Empty;
        
        /// <summary>
        /// Properties that were changed (for selective invalidation)
        /// </summary>
        public string[] ChangedProperties { get; init; } = Array.Empty<string>();
        
        /// <summary>
        /// Whether parameters were updated
        /// </summary>
        public bool ParametersChanged { get; init; }
        
        /// <summary>
        /// Partition key for ordered processing per model
        /// </summary>
        public string PartitionKey => ModelId.ToString();
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

    // ===============================
    // Discovery Cache Domain Events
    // ===============================

    /// <summary>
    /// Raised when an admin explicitly requests invalidation of the discovery cache
    /// Triggers cache invalidation across all Core API instances
    /// </summary>
    public record DiscoveryCacheInvalidationRequested : DomainEvent
    {
        /// <summary>
        /// Reason for cache invalidation (for logging/auditing)
        /// </summary>
        public string Reason { get; init; } = "Manual invalidation";

        /// <summary>
        /// User or service that requested the invalidation
        /// </summary>
        public string RequestedBy { get; init; } = "System";

        /// <summary>
        /// Partition key - use constant since this is a system-wide operation
        /// </summary>
        public string PartitionKey => "discovery-cache";
    }
}