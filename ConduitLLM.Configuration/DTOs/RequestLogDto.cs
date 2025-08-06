using System;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for request logs
    /// </summary>
    public class RequestLogDto
    {
        /// <summary>
        /// Unique identifier for the request log
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID of the virtual key used for the request
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Name of the virtual key used for the request
        /// </summary>
        public string VirtualKeyName { get; set; } = string.Empty;

        /// <summary>
        /// ID of the model used for the request
        /// </summary>
        public string ModelId { get; set; } = string.Empty;


        /// <summary>
        /// Type of the request (chat, completion, embedding, etc.)
        /// </summary>
        public string RequestType { get; set; } = string.Empty;

        /// <summary>
        /// Number of input tokens in the request
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Number of output tokens in the response
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
        /// Timestamp of the request
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional identifier of the user making the request
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Optional IP address of the client making the request
        /// </summary>
        public string? ClientIp { get; set; }

        /// <summary>
        /// Optional request path
        /// </summary>
        public string? RequestPath { get; set; }

        /// <summary>
        /// Optional status code of the response
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Total tokens (input + output)
        /// </summary>
        public int TotalTokens => InputTokens + OutputTokens;
    }
}
