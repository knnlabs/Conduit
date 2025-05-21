using System;
using ConduitLLM.Configuration.DTOs.Costs;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Extended wrapper for VirtualKeyCostDataDto that adds backward compatibility properties.
    /// This class should be used in the WebUI project to ensure compatibility with older code.
    /// </summary>
    public class VirtualKeyCostDataDto
    {
        /// <summary>
        /// Virtual key ID
        /// </summary>
        public int VirtualKeyId { get; set; }
        
        /// <summary>
        /// Virtual key name
        /// </summary>
        public string KeyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Virtual key name alias (for compatibility)
        /// </summary>
        public string VirtualKeyName
        {
            get => KeyName;
            set => KeyName = value;
        }
        
        /// <summary>
        /// Total cost
        /// </summary>
        public decimal Cost { get; set; }
        
        /// <summary>
        /// Total cost alias (for compatibility)
        /// </summary>
        public decimal TotalCost
        {
            get => Cost;
            set => Cost = value;
        }
        
        /// <summary>
        /// Number of requests
        /// </summary>
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Total requests alias (for compatibility)
        /// </summary>
        public int TotalRequests
        {
            get => RequestCount;
            set => RequestCount = value;
        }
        
        /// <summary>
        /// Average cost per request
        /// </summary>
        public decimal AverageCostPerRequest { get; set; }
        
        /// <summary>
        /// Number of input tokens
        /// </summary>
        public int InputTokens { get; set; }
        
        /// <summary>
        /// Total input tokens alias (for compatibility)
        /// </summary>
        public int TotalInputTokens
        {
            get => InputTokens;
            set => InputTokens = value;
        }

        /// <summary>
        /// Number of output tokens
        /// </summary>
        public int OutputTokens { get; set; }
        
        /// <summary>
        /// Total output tokens alias (for compatibility)
        /// </summary>
        public int TotalOutputTokens
        {
            get => OutputTokens;
            set => OutputTokens = value;
        }
        
        /// <summary>
        /// Average response time in milliseconds
        /// </summary>
        public double AverageResponseTimeMs { get; set; }
        
        /// <summary>
        /// Last time the key was used
        /// </summary>
        public DateTime? LastUsedAt { get; set; }
        
        /// <summary>
        /// Last request time (alias for LastUsedAt)
        /// </summary>
        public DateTime LastRequestTime
        {
            get => LastUsedAt ?? DateTime.MinValue;
            set => LastUsedAt = value;
        }
        
        /// <summary>
        /// Last request date (alias for LastUsedAt)
        /// </summary>
        public DateTime LastRequestDate
        {
            get => LastUsedAt ?? DateTime.MinValue;
            set => LastUsedAt = value;
        }
        
        /// <summary>
        /// Time when the key was created
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// First request time (alias for CreatedAt)
        /// </summary>
        public DateTime FirstRequestTime
        {
            get => CreatedAt;
            set => CreatedAt = value;
        }
        
        /// <summary>
        /// Number of requests in the last day
        /// </summary>
        public int LastDayRequests { get; set; }
        
        /// <summary>
        /// Creates a new instance from the Configuration VirtualKeyCostDataDto
        /// </summary>
        /// <param name="dto">The Configuration DTO to convert from</param>
        /// <returns>A new WebUI VirtualKeyCostDataDto with properties populated from the input</returns>
        public static VirtualKeyCostDataDto FromConfigurationDto(ConduitLLM.Configuration.DTOs.Costs.VirtualKeyCostDataDto dto)
        {
            return new VirtualKeyCostDataDto
            {
                VirtualKeyId = dto.VirtualKeyId,
                KeyName = dto.KeyName,
                Cost = dto.Cost,
                RequestCount = dto.RequestCount,
                AverageCostPerRequest = dto.AverageCostPerRequest,
                // The remaining properties will have default values
                InputTokens = 0,
                OutputTokens = 0,
                AverageResponseTimeMs = 0,
                LastUsedAt = null,
                CreatedAt = DateTime.MinValue,
                LastDayRequests = 0
            };
        }
    }
}
