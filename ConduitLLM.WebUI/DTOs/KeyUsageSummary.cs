using System;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Summarized usage statistics for a specific virtual key
    /// </summary>
    public class KeyUsageSummary
    {
        /// <summary>
        /// ID of the virtual key
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Name of the virtual key
        /// </summary>
        public string KeyName { get; set; } = string.Empty;

        /// <summary>
        /// Alias for KeyName for backward compatibility
        /// </summary>
        public string VirtualKeyName
        {
            get => KeyName;
            set => KeyName = value;
        }

        /// <summary>
        /// Total number of requests made with this key
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// Total cost of requests made with this key
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Total input tokens processed with this key
        /// </summary>
        public int TotalInputTokens { get; set; }

        /// <summary>
        /// Total output tokens generated with this key
        /// </summary>
        public int TotalOutputTokens { get; set; }

        /// <summary>
        /// Average response time in milliseconds
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// Date of the most recent request
        /// </summary>
        public DateTime? LastRequestDate { get; set; }

        /// <summary>
        /// Date of the most recent request (alias for LastRequestDate for backward compatibility)
        /// </summary>
        public DateTime? LastUsed
        {
            get => LastRequestDate;
            set => LastRequestDate = value;
        }

        /// <summary>
        /// Success rate (as a percentage)
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Average response time (alias for AverageResponseTimeMs for backward compatibility)
        /// </summary>
        public double AverageResponseTime { get => AverageResponseTimeMs; set => AverageResponseTimeMs = value; }

        /// <summary>
        /// Time of the first request
        /// </summary>
        public DateTime? FirstRequestTime { get; set; }

        /// <summary>
        /// Time of the last request (alias for LastRequestDate for backward compatibility)
        /// </summary>
        public DateTime? LastRequestTime { get => LastRequestDate; set => LastRequestDate = value; }

        /// <summary>
        /// Number of requests in the last 24 hours
        /// </summary>
        public int RequestsLast24Hours { get; set; }

        /// <summary>
        /// Number of requests in the last day (alias for RequestsLast24Hours)
        /// </summary>
        public int LastDayRequests
        {
            get => RequestsLast24Hours;
            set => RequestsLast24Hours = value;
        }

        /// <summary>
        /// Number of requests in the last 7 days
        /// </summary>
        public int RequestsLast7Days { get; set; }

        /// <summary>
        /// Number of requests in the last 30 days
        /// </summary>
        public int RequestsLast30Days { get; set; }

        /// <summary>
        /// Date when the key was created
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
