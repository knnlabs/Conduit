using System;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Cost data for a specific virtual key
    /// </summary>
    public class VirtualKeyCostDataDto
    {
        /// <summary>
        /// Virtual key ID
        /// </summary>
        public int KeyId { get; set; }
        
        /// <summary>
        /// Virtual key name
        /// </summary>
        public string KeyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of requests
        /// </summary>
        public int Requests { get; set; }
        
        /// <summary>
        /// Total cost
        /// </summary>
        public decimal Cost { get; set; }
    }
}