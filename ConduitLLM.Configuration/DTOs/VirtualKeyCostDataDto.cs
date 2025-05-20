using System;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Cost data for a specific virtual key
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
        public string VirtualKeyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of requests
        /// </summary>
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Total cost
        /// </summary>
        public decimal TotalCost { get; set; }
        
        /// <summary>
        /// Whether the virtual key is active
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// The date of the last request using this key
        /// </summary>
        public DateTime? LastUsed { get; set; }
        
        // Backwards compatibility properties
        
        /// <summary>
        /// Virtual key ID (alias for backwards compatibility)
        /// </summary>
        public int KeyId
        {
            get => VirtualKeyId;
            set => VirtualKeyId = value;
        }
        
        /// <summary>
        /// Virtual key name (alias for backwards compatibility)
        /// </summary>
        public string KeyName
        {
            get => VirtualKeyName;
            set => VirtualKeyName = value;
        }
        
        /// <summary>
        /// Number of requests (alias for backwards compatibility)
        /// </summary>
        public int Requests
        {
            get => RequestCount;
            set => RequestCount = value;
        }
        
        /// <summary>
        /// Total cost (alias for backwards compatibility)
        /// </summary>
        public decimal Cost
        {
            get => TotalCost;
            set => TotalCost = value;
        }
    }
}