namespace ConduitLLM.Core.Events
{
    // ===============================
    // Rate Limiting Domain Events
    // ===============================

    /// <summary>
    /// Raised when a rate limit is exceeded for a virtual key
    /// Enables defensive actions and alerting
    /// </summary>
    public record RateLimitExceeded : DomainEvent
    {
        /// <summary>
        /// Virtual Key ID that exceeded the limit
        /// </summary>
        public int VirtualKeyId { get; init; }
        
        /// <summary>
        /// Virtual Key hash for identification
        /// </summary>
        public string VirtualKeyHash { get; init; } = string.Empty;
        
        /// <summary>
        /// Type of rate limit exceeded (RPM, RPD, etc.)
        /// </summary>
        public string LimitType { get; init; } = string.Empty;
        
        /// <summary>
        /// The limit that was exceeded
        /// </summary>
        public int LimitValue { get; init; }
        
        /// <summary>
        /// Current usage that triggered the limit
        /// </summary>
        public int CurrentUsage { get; init; }
        
        /// <summary>
        /// Time window for the rate limit (e.g., "minute", "day")
        /// </summary>
        public string TimeWindow { get; init; } = string.Empty;
        
        /// <summary>
        /// When the rate limit will reset
        /// </summary>
        public DateTime ResetsAt { get; init; }
        
        /// <summary>
        /// IP address of the request (if available)
        /// </summary>
        public string? IpAddress { get; init; }
        
        /// <summary>
        /// Model that was requested
        /// </summary>
        public string? RequestedModel { get; init; }
        
        /// <summary>
        /// Partition key for ordered processing per virtual key
        /// </summary>
        public string PartitionKey => VirtualKeyId.ToString();
    }

    // ===============================
    // Spend Threshold Domain Events
    // ===============================

    /// <summary>
    /// Raised when a virtual key exceeds its spend threshold
    /// Critical for enforcing budget limits and notifications
    /// </summary>
    public record SpendThresholdExceeded : DomainEvent
    {
        /// <summary>
        /// Virtual Key ID that exceeded the threshold
        /// </summary>
        public int VirtualKeyId { get; init; }
        
        /// <summary>
        /// Virtual Key hash for identification
        /// </summary>
        public string VirtualKeyHash { get; init; } = string.Empty;
        
        /// <summary>
        /// Key name for notifications
        /// </summary>
        public string KeyName { get; init; } = string.Empty;
        
        /// <summary>
        /// Current spend amount that exceeded the budget
        /// </summary>
        public decimal CurrentSpend { get; init; }
        
        /// <summary>
        /// Maximum budget that was exceeded
        /// </summary>
        public decimal MaxBudget { get; init; }
        
        /// <summary>
        /// Amount over budget
        /// </summary>
        public decimal AmountOver { get; init; }
        
        /// <summary>
        /// Budget duration type (daily, weekly, monthly, etc.)
        /// </summary>
        public string? BudgetDuration { get; init; }
        
        /// <summary>
        /// When the excess occurred
        /// </summary>
        public DateTime ExceededAt { get; init; }
        
        /// <summary>
        /// Whether the key was automatically disabled
        /// </summary>
        public bool KeyDisabled { get; init; }
        
        /// <summary>
        /// Partition key for ordered processing per virtual key
        /// </summary>
        public string PartitionKey => VirtualKeyId.ToString();
    }
}