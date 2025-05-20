using System;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Detailed cost data for export
    /// </summary>
    public class DetailedCostDataDto
    {
        /// <summary>
        /// Timestamp of the request
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Virtual key ID
        /// </summary>
        public int VirtualKeyId { get; set; }
        
        /// <summary>
        /// Virtual key name
        /// </summary>
        public string VirtualKeyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Model name
        /// </summary>
        public string ModelName { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of input tokens
        /// </summary>
        public int InputTokens { get; set; }
        
        /// <summary>
        /// Number of output tokens
        /// </summary>
        public int OutputTokens { get; set; }
        
        /// <summary>
        /// Total tokens (input + output)
        /// </summary>
        public int TotalTokens { get; set; }
        
        /// <summary>
        /// Total cost
        /// </summary>
        public decimal Cost { get; set; }
        
        /// <summary>
        /// Latency in milliseconds
        /// </summary>
        public long LatencyMs { get; set; }
        
        /// <summary>
        /// Whether the request was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Error message if the request failed
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Date of the cost data (alias for backwards compatibility)
        /// </summary>
        public DateTime Date
        {
            get => Timestamp;
            set => Timestamp = value;
        }
        
        /// <summary>
        /// Model name (alias for backwards compatibility)
        /// </summary>
        public string Model
        {
            get => ModelName;
            set => ModelName = value;
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
            get => 1; // Each record represents a single request
            set { /* No-op for backward compatibility */ }
        }
    }
}