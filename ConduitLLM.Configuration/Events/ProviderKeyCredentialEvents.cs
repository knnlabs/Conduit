namespace ConduitLLM.Configuration.Events
{
    /// <summary>
    /// Base class for domain events
    /// </summary>
    public abstract record DomainEvent
    {
        /// <summary>
        /// When the event occurred
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Correlation ID for tracking related events
        /// </summary>
        public Guid CorrelationId { get; init; } = Guid.NewGuid();
    }

    /// <summary>
    /// Event raised when a new provider key credential is created
    /// </summary>
    public record ProviderKeyCredentialCreated : DomainEvent
    {
        /// <summary>
        /// The ID of the created key credential
        /// </summary>
        public int KeyId { get; init; }

        /// <summary>
        /// The ID of the provider credential this key belongs to
        /// </summary>
        public int ProviderId { get; init; }

        /// <summary>
        /// Whether this key is set as the primary key
        /// </summary>
        public bool IsPrimary { get; init; }

        /// <summary>
        /// Whether this key is enabled
        /// </summary>
        public bool IsEnabled { get; init; }
    }

    /// <summary>
    /// Event raised when a provider key credential is updated
    /// </summary>
    public record ProviderKeyCredentialUpdated : DomainEvent
    {
        /// <summary>
        /// The ID of the updated key credential
        /// </summary>
        public int KeyId { get; init; }

        /// <summary>
        /// The ID of the provider credential this key belongs to
        /// </summary>
        public int ProviderId { get; init; }

        /// <summary>
        /// List of properties that were changed
        /// </summary>
        public string[] ChangedProperties { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Event raised when a provider key credential is deleted
    /// </summary>
    public record ProviderKeyCredentialDeleted : DomainEvent
    {
        /// <summary>
        /// The ID of the deleted key credential
        /// </summary>
        public int KeyId { get; init; }

        /// <summary>
        /// The ID of the provider credential this key belonged to
        /// </summary>
        public int ProviderId { get; init; }
    }

    /// <summary>
    /// Event raised when the primary key for a provider is changed
    /// </summary>
    public record ProviderKeyCredentialPrimaryChanged : DomainEvent
    {
        /// <summary>
        /// The ID of the provider credential
        /// </summary>
        public int ProviderId { get; init; }

        /// <summary>
        /// The ID of the old primary key (0 if none)
        /// </summary>
        public int OldPrimaryKeyId { get; init; }

        /// <summary>
        /// The ID of the new primary key
        /// </summary>
        public int NewPrimaryKeyId { get; init; }
    }
}