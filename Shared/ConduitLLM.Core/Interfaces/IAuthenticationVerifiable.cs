namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for providers that support authentication verification.
    /// </summary>
    /// <remarks>
    /// This interface enables provider-specific authentication testing without requiring
    /// the service layer to have knowledge of each provider's authentication mechanism.
    /// </remarks>
    public interface IAuthenticationVerifiable
    {
        /// <summary>
        /// Verifies that the provided credentials are valid for this provider.
        /// </summary>
        /// <param name="apiKey">Optional API key to test. If null, uses the configured key.</param>
        /// <param name="baseUrl">Optional base URL override. If null, uses the configured URL.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>An authentication result indicating success or failure with details.</returns>
        Task<AuthenticationResult> VerifyAuthenticationAsync(
            string? apiKey = null,
            string? baseUrl = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the health check URL for this provider.
        /// </summary>
        /// <param name="baseUrl">Optional base URL override. If null, uses the configured URL.</param>
        /// <returns>The URL to use for health checks.</returns>
        string GetHealthCheckUrl(string? baseUrl = null);
    }

    /// <summary>
    /// Represents the result of an authentication verification attempt.
    /// </summary>
    public class AuthenticationResult
    {
        /// <summary>
        /// Indicates whether the authentication was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// A human-readable message describing the result.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Detailed error information if the authentication failed.
        /// </summary>
        public string? ErrorDetails { get; set; }

        /// <summary>
        /// The response time in milliseconds, if applicable.
        /// </summary>
        public double? ResponseTimeMs { get; set; }

        /// <summary>
        /// Additional provider-specific details about the authentication attempt.
        /// </summary>
        public string? ProviderDetails { get; set; }

        /// <summary>
        /// Creates a successful authentication result.
        /// </summary>
        /// <param name="message">Success message.</param>
        /// <param name="responseTimeMs">Response time in milliseconds.</param>
        /// <returns>A successful authentication result.</returns>
        public static AuthenticationResult Success(string message, double? responseTimeMs = null)
        {
            return new AuthenticationResult
            {
                IsSuccess = true,
                Message = message,
                ResponseTimeMs = responseTimeMs
            };
        }

        /// <summary>
        /// Creates a failed authentication result.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="errorDetails">Detailed error information.</param>
        /// <returns>A failed authentication result.</returns>
        public static AuthenticationResult Failure(string message, string? errorDetails = null)
        {
            return new AuthenticationResult
            {
                IsSuccess = false,
                Message = message,
                ErrorDetails = errorDetails
            };
        }
    }
}