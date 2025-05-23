using System;

namespace ConduitLLM.Configuration.DTOs.VirtualKey
{
    /// <summary>
    /// Extended information about a virtual key for validation purposes
    /// </summary>
    public class VirtualKeyValidationInfoDto
    {
        /// <summary>
        /// The ID of the virtual key
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// The name of the virtual key
        /// </summary>
        public string KeyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Comma-separated list of allowed models or model patterns
        /// </summary>
        public string? AllowedModels { get; set; }
        
        /// <summary>
        /// Maximum budget for this key
        /// </summary>
        public decimal? MaxBudget { get; set; }
        
        /// <summary>
        /// Current spend amount
        /// </summary>
        public decimal CurrentSpend { get; set; }
        
        /// <summary>
        /// Budget duration ("Total", "Monthly", "Daily")
        /// </summary>
        public string? BudgetDuration { get; set; }
        
        /// <summary>
        /// Start date of the current budget period
        /// </summary>
        public DateTime? BudgetStartDate { get; set; }
        
        /// <summary>
        /// Whether the key is enabled
        /// </summary>
        public bool IsEnabled { get; set; }
        
        /// <summary>
        /// Expiration date of the key
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
        
        /// <summary>
        /// Rate limit in requests per minute
        /// </summary>
        public int? RateLimitRpm { get; set; }
        
        /// <summary>
        /// Rate limit in requests per day
        /// </summary>
        public int? RateLimitRpd { get; set; }
    }
}