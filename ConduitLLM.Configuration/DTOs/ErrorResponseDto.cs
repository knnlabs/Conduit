namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Standardized error response DTO for API endpoints
    /// </summary>
    public class ErrorResponseDto
    {
        /// <summary>
        /// Gets or sets the error message or error details object
        /// </summary>
        public object error { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional error details (optional)
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Gets or sets the error code (optional)
        /// </summary>
        public string? Code { get; set; }

        /// <summary>
        /// Creates a new ErrorResponseDto with the specified error message
        /// </summary>
        /// <param name="errorMessage">The error message</param>
        public ErrorResponseDto(string errorMessage)
        {
            error = errorMessage;
        }

        /// <summary>
        /// Creates a new ErrorResponseDto with an error details object
        /// </summary>
        /// <param name="errorDetails">The error details object</param>
        public ErrorResponseDto(ErrorDetailsDto errorDetails)
        {
            error = errorDetails;
        }

        /// <summary>
        /// Default constructor for deserialization
        /// </summary>
        public ErrorResponseDto()
        {
        }
    }

    /// <summary>
    /// Detailed error information for API responses
    /// </summary>
    public class ErrorDetailsDto
    {
        /// <summary>
        /// Gets or sets the error message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error type
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Creates a new ErrorDetailsDto
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="type">The error type</param>
        public ErrorDetailsDto(string message, string type)
        {
            Message = message;
            Type = type;
        }

        /// <summary>
        /// Default constructor for deserialization
        /// </summary>
        public ErrorDetailsDto()
        {
        }
    }
}