namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for logging a request
    /// </summary>
    public class LogRequestDto
    {
        /// <summary>
        /// Unique identifier for the log entry
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID of the virtual key used for the request
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Name of the model used for the request
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

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
        /// Timestamp of when the request was made
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
