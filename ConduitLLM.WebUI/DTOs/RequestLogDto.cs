using System;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Data transfer object for request logs used in the WebUI.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: There are two RequestLogDto classes in the project:
    /// 1. ConduitLLM.WebUI.DTOs.RequestLogDto (this one)
    /// 2. ConduitLLM.Configuration.DTOs.RequestLogDto
    ///
    /// When referencing either class, use the fully qualified name to avoid ambiguity.
    /// This class is primarily for WebUI consumption, while the Configuration.DTOs version
    /// is for API/client consumption.
    /// </remarks>
    public class RequestLogDto
    {
        /// <summary>
        /// ID of the request log
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID of the virtual key used
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Name of the virtual key
        /// </summary>
        public string VirtualKeyName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the model used (alias for ModelId in old system)
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Model ID (for backwards compatibility)
        /// </summary>
        public string ModelId
        {
            get => ModelName;
            set => ModelName = value;
        }

        /// <summary>
        /// Type of request (chat, completions, etc.)
        /// </summary>
        public string RequestType { get; set; } = string.Empty;

        /// <summary>
        /// Number of input tokens
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Number of output tokens
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Cost of the request
        /// </summary>
        public decimal Cost { get; set; }

        /// <summary>
        /// Response time in milliseconds
        /// </summary>
        public double ResponseTimeMs { get; set; }

        /// <summary>
        /// User ID associated with the request (if any)
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Client IP address
        /// </summary>
        public string? ClientIp { get; set; }

        /// <summary>
        /// Request path
        /// </summary>
        public string? RequestPath { get; set; }

        /// <summary>
        /// HTTP status code
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Timestamp when the request was made
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}
