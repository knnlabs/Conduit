namespace ConduitLLM.Security.Models
{
    /// <summary>
    /// Result of a security check from the security middleware
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

        /// <summary>
        /// Additional headers to include in response (e.g., rate limit headers)
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new();

        /// <summary>
        /// Creates an allowed result
        /// </summary>
        public static SecurityCheckResult Allowed() => new() { IsAllowed = true };

        /// <summary>
        /// Creates a denied result
        /// </summary>
        public static SecurityCheckResult Denied(string reason, int statusCode = 403)
            => new() { IsAllowed = false, Reason = reason, StatusCode = statusCode };

        /// <summary>
        /// Creates a rate limited result
        /// </summary>
        public static SecurityCheckResult RateLimited(
            string reason,
            int retryAfterSeconds,
            int? limit = null,
            int? remaining = null)
        {
            var result = new SecurityCheckResult
            {
                IsAllowed = false,
                Reason = reason,
                StatusCode = 429,
                Headers = new Dictionary<string, string>
                {
                    ["Retry-After"] = retryAfterSeconds.ToString()
                }
            };

            if (limit.HasValue)
                result.Headers["X-RateLimit-Limit"] = limit.Value.ToString();
            if (remaining.HasValue)
                result.Headers["X-RateLimit-Remaining"] = remaining.Value.ToString();
            return result;
        }
    }
}
