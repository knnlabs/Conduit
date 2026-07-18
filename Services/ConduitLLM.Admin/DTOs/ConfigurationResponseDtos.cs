using System;
using System.Collections.Generic;

using ConduitLLM.Configuration.DTOs.Cache;

namespace ConduitLLM.Admin.DTOs
{
    /// <summary>
    /// Generic response DTO carrying a confirmation message.
    /// </summary>
    public class MessageResponseDto
    {
        /// <summary>
        /// The confirmation message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response DTO for the cache regions endpoint.
    /// </summary>
    public class CacheRegionsResponseDto
    {
        /// <summary>
        /// The configured cache regions.
        /// </summary>
        public List<CacheRegionDto> Regions { get; set; } = new();

        /// <summary>
        /// Timestamp when the response was generated (UTC).
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}
