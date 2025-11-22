namespace ConduitLLM.Core.Events
{
    // ===============================
    // Function Configuration Domain Events
    // ===============================

    /// <summary>
    /// Raised when a function configuration is created, updated, or deleted
    /// Critical for function discovery cache invalidation across all services
    /// </summary>
    public record FunctionConfigurationChanged : DomainEvent
    {
        /// <summary>
        /// Function configuration database ID
        /// </summary>
        public int FunctionConfigurationId { get; init; }

        /// <summary>
        /// Function configuration name
        /// </summary>
        public string ConfigurationName { get; init; } = string.Empty;

        /// <summary>
        /// Provider type (Exa, Tavily, etc.)
        /// </summary>
        public string ProviderType { get; init; } = string.Empty;

        /// <summary>
        /// Function purpose (Search, Answer, etc.)
        /// </summary>
        public string Purpose { get; init; } = string.Empty;

        /// <summary>
        /// Type of change (Created, Updated, Deleted)
        /// </summary>
        public string ChangeType { get; init; } = string.Empty;

        /// <summary>
        /// Properties that were changed (for selective invalidation)
        /// </summary>
        public string[] ChangedProperties { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Whether the IsEnabled property was changed
        /// </summary>
        public bool IsEnabledChanged { get; init; }

        /// <summary>
        /// Whether the CacheTtlMinutes property was changed
        /// </summary>
        public bool CacheTtlChanged { get; init; }

        /// <summary>
        /// Partition key for ordered processing per function configuration
        /// </summary>
        public string PartitionKey => FunctionConfigurationId.ToString();
    }

    // ===============================
    // Function Discovery Cache Domain Events
    // ===============================

    /// <summary>
    /// Raised when an admin explicitly requests invalidation of the function discovery cache
    /// Triggers cache invalidation across all Core API and Admin API instances
    /// </summary>
    public record FunctionDiscoveryCacheInvalidationRequested : DomainEvent
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
        public string PartitionKey => "function-discovery-cache";
    }
}
