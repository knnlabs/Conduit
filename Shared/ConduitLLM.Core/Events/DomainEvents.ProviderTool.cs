namespace ConduitLLM.Core.Events
{
    // ===============================
    // Provider Tool Domain Events
    // ===============================

    /// <summary>
    /// Raised when provider tools are created, updated, or deleted.
    /// Critical for cache invalidation of tool cost lookups in the billing pipeline.
    /// </summary>
    public record ProviderToolChanged : DomainEvent
    {
        /// <summary>
        /// Provider tool database ID
        /// </summary>
        public int ProviderToolId { get; init; }

        /// <summary>
        /// Tool name that was affected
        /// </summary>
        public string ToolName { get; init; } = string.Empty;

        /// <summary>
        /// Provider type string (e.g., "Groq", "OpenAI")
        /// </summary>
        public string ProviderType { get; init; } = string.Empty;

        /// <summary>
        /// Type of change (Created, Updated, Deleted)
        /// </summary>
        public string ChangeType { get; init; } = string.Empty;

        /// <summary>
        /// Partition key for ordered processing per provider type
        /// </summary>
        public string PartitionKey => ProviderType;
    }
}
