namespace ConduitLLM.Core.Events
{
    // ===============================
    // Virtual Key Domain Events
    // ===============================

    /// <summary>
    /// Raised when a new virtual key is created
    /// Critical for cache initialization and real-time synchronization
    /// </summary>
    public record VirtualKeyCreated : DomainEvent
    {
        /// <summary>
        /// Virtual Key database ID
        /// </summary>
        public int KeyId { get; init; }
        
        /// <summary>
        /// Virtual Key hash for cache operations
        /// </summary>
        public string KeyHash { get; init; } = string.Empty;
        
        /// <summary>
        /// Key name for logging and audit purposes
        /// </summary>
        public string KeyName { get; init; } = string.Empty;
        
        /// <summary>
        /// When the key was created
        /// </summary>
        public DateTime CreatedAt { get; init; }
        
        /// <summary>
        /// Whether the key is enabled at creation
        /// </summary>
        public bool IsEnabled { get; init; } = true;
        
        /// <summary>
        /// Allowed models at creation (if specified)
        /// </summary>
        public string? AllowedModels { get; init; }
        
        /// <summary>
        /// Virtual key group ID
        /// </summary>
        public int VirtualKeyGroupId { get; init; }
        
        /// <summary>
        /// Partition key for ordered processing per virtual key
        /// </summary>
        public string PartitionKey => KeyId.ToString();
    }

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

    /// <summary>
    /// Raised when a spend update cannot be processed immediately
    /// Allows other services to handle the update in appropriate context
    /// </summary>
    public record SpendUpdateDeferred : DomainEvent
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
        /// Optional request identifier for correlation
        /// </summary>
        public string RequestId { get; init; } = string.Empty;
        
        /// <summary>
        /// Reason for deferral
        /// </summary>
        public string Reason { get; init; } = string.Empty;
        
        /// <summary>
        /// Partition key for ordered processing per virtual key
        /// </summary>
        public string PartitionKey => KeyId.ToString();
    }
}