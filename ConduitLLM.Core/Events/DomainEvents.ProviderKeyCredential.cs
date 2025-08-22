namespace ConduitLLM.Core.Events
{
    // ===============================
    // Provider Key Credential Domain Events
    // ===============================

    /// <summary>
    /// Raised when a new provider key credential is created
    /// </summary>
    public record ProviderKeyCredentialCreated : DomainEvent
    {
        /// <summary>
        /// Provider key credential database ID
        /// </summary>
        public int KeyId { get; init; }
        
        /// <summary>
        /// Provider credential database ID
        /// </summary>
        public int ProviderId { get; init; }
        
        /// <summary>
        /// Whether this is the primary key
        /// </summary>
        public bool IsPrimary { get; init; }
        
        /// <summary>
        /// Whether the key is enabled
        /// </summary>
        public bool IsEnabled { get; init; }
        
        /// <summary>
        /// Partition key for ordered processing per provider
        /// </summary>
        public string PartitionKey => ProviderId.ToString();
    }

    /// <summary>
    /// Raised when a provider key credential is updated
    /// </summary>
    public record ProviderKeyCredentialUpdated : DomainEvent
    {
        /// <summary>
        /// Provider key credential database ID
        /// </summary>
        public int KeyId { get; init; }
        
        /// <summary>
        /// Provider credential database ID
        /// </summary>
        public int ProviderId { get; init; }
        
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
    /// Raised when a provider key credential is deleted
    /// </summary>
    public record ProviderKeyCredentialDeleted : DomainEvent
    {
        /// <summary>
        /// Provider key credential database ID
        /// </summary>
        public int KeyId { get; init; }
        
        /// <summary>
        /// Provider credential database ID
        /// </summary>
        public int ProviderId { get; init; }
        
        /// <summary>
        /// Partition key for ordered processing per provider
        /// </summary>
        public string PartitionKey => ProviderId.ToString();
    }

    /// <summary>
    /// Raised when the primary key for a provider changes
    /// </summary>
    public record ProviderKeyCredentialPrimaryChanged : DomainEvent
    {
        /// <summary>
        /// Provider credential database ID
        /// </summary>
        public int ProviderId { get; init; }
        
        /// <summary>
        /// Previous primary key ID (may be 0 if none)
        /// </summary>
        public int OldPrimaryKeyId { get; init; }
        
        /// <summary>
        /// New primary key ID
        /// </summary>
        public int NewPrimaryKeyId { get; init; }
        
        /// <summary>
        /// Partition key for ordered processing per provider
        /// </summary>
        public string PartitionKey => ProviderId.ToString();
    }
}