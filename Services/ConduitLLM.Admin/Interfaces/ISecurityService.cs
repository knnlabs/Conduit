namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Unified security service for Admin API
    /// </summary>
    public interface ISecurityService
    {
        /// <summary>
        /// Checks if a request is allowed based on all security rules
        /// </summary>
        Task<SecurityCheckResult> IsRequestAllowedAsync(HttpContext context);

        /// <summary>
        /// Records a failed authentication attempt
        /// </summary>
        Task RecordFailedAuthAsync(string ipAddress);

        /// <summary>
        /// Clears failed authentication attempts for an IP
        /// </summary>
        Task ClearFailedAuthAttemptsAsync(string ipAddress);

        /// <summary>
        /// Checks if an IP is banned due to failed authentication
        /// </summary>
        Task<bool> IsIpBannedAsync(string ipAddress);

        /// <summary>
        /// Validates the API key
        /// </summary>
        bool ValidateApiKey(string providedKey);
    }

    /// <summary>
    /// Result of a security check
    /// </summary>
    public class SecurityCheckResult
    {
        /// <summary>
        /// Whether the request is allowed
        /// </summary>
        public bool IsAllowed { get; set; }
        
        /// <summary>
        /// Reason for denial if not allowed
        /// </summary>
        public string Reason { get; set; } = "";
        
        /// <summary>
        /// HTTP status code to return
        /// </summary>
        public int? StatusCode { get; set; }
    }
}