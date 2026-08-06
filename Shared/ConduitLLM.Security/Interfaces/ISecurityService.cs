using Microsoft.AspNetCore.Http;
using ConduitLLM.Security.Models;

namespace ConduitLLM.Security.Interfaces
{
    /// <summary>
    /// Shared security service interface for both Admin and Gateway APIs.
    /// Provides authentication checking, IP banning, and rate limiting.
    /// </summary>
    public interface ISecurityService
    {
        /// <summary>
        /// Checks if a request is allowed based on all security rules
        /// </summary>
        Task<SecurityCheckResult> IsRequestAllowedAsync(HttpContext context);

        /// <summary>
        /// Records a failed authentication attempt for an IP address
        /// </summary>
        /// <param name="ipAddress">The client IP address</param>
        /// <param name="attemptedKey">The key that was attempted (will be masked in logs)</param>
        Task RecordFailedAuthAsync(string ipAddress, string attemptedKey = "");

        /// <summary>
        /// Clears failed authentication attempts for an IP address
        /// </summary>
        Task ClearFailedAuthAttemptsAsync(string ipAddress);

        /// <summary>
        /// Checks if an IP is banned due to failed authentication
        /// </summary>
        Task<bool> IsIpBannedAsync(string ipAddress);
    }
}
