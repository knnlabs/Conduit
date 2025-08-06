using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Data transfer object for cache health status
    /// </summary>
    public class CacheHealthDto
    {
        /// <summary>
        /// Overall health status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Health check timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Individual region health statuses
        /// </summary>
        public Dictionary<string, RegionHealthDto> Regions { get; set; } = new();

        /// <summary>
        /// Any health issues detected
        /// </summary>
        public List<string> Issues { get; set; } = new();
    }
}