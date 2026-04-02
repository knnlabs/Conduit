using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.Gateway.EventHandlers
{
    /// <summary>
    /// Shared utility methods for image and video generation event handlers.
    /// Consolidates duplicated cache management, failure tracking, and error analysis logic.
    /// </summary>
    internal static class MediaGenerationHandlerHelper
    {
        #region Completed Handler Helpers

        /// <summary>
        /// Maintains a rolling list of recently completed media generation tasks in memory cache.
        /// Keeps the last 100 entries with a 24-hour TTL.
        /// </summary>
        public static void UpdateCompletedTasksCache(IMemoryCache cache, string cacheKey, object completionData)
        {
            var completedTasks = cache.Get<List<object>>(cacheKey) ?? new List<object>();

            completedTasks.Add(completionData);

            if (completedTasks.Count > 100)
            {
                completedTasks = completedTasks.Skip(completedTasks.Count - 100).ToList();
            }

            cache.Set(cacheKey, completedTasks, TimeSpan.FromHours(24));
        }

        #endregion

        #region Failed Handler Helpers

        /// <summary>
        /// Tracks per-provider failure count in a 1-hour sliding window.
        /// Returns the updated failure count.
        /// </summary>
        public static int TrackFailureMetrics(
            IMemoryCache cache, string cacheKeyPrefix, string provider, string mediaType, ILogger logger)
        {
            var failureCacheKey = $"{cacheKeyPrefix}{provider}";
            var failureCount = 0;

            if (cache.TryGetValue<int>(failureCacheKey, out var existingCount))
            {
                failureCount = existingCount;
            }

            failureCount++;
            cache.Set(failureCacheKey, failureCount, TimeSpan.FromHours(1));

            logger.LogWarning("Provider {Provider} {MediaType} failure count in last hour: {FailureCount}",
                provider, mediaType, failureCount);

            return failureCount;
        }

        /// <summary>
        /// Common error patterns shared across all media generation types.
        /// </summary>
        private static readonly Dictionary<string, string> CommonErrorPatterns = new()
        {
            ["rate limit"] = "Provider rate limit exceeded - consider implementing backoff",
            ["timeout"] = "Request timeout - provider may be experiencing high load",
            ["invalid api key"] = "Authentication failure - check provider credentials",
            ["insufficient credits"] = "Provider account has insufficient credits",
            ["content policy"] = "Content violates provider's usage policy",
            ["model not found"] = "Requested model is not available"
        };

        /// <summary>
        /// Analyzes the error message against known patterns and logs actionable diagnostics.
        /// Checks common patterns first, then any additional media-specific patterns.
        /// </summary>
        public static void AnalyzeErrorPattern(
            string error, string taskId, ILogger logger,
            Dictionary<string, string>? additionalPatterns = null)
        {
            var lowerError = error.ToLowerInvariant();

            // Check common patterns first, then additional ones
            var allPatterns = additionalPatterns != null
                ? CommonErrorPatterns.Concat(additionalPatterns)
                : CommonErrorPatterns;

            foreach (var (pattern, analysis) in allPatterns)
            {
                if (lowerError.Contains(pattern))
                {
                    logger.LogWarning("Error pattern detected for task {TaskId}: {Analysis}",
                        taskId, analysis);
                    return;
                }
            }
        }

        /// <summary>
        /// Critical error patterns that indicate provider-level issues requiring immediate attention
        /// (authentication failures, account problems, insufficient funds).
        /// </summary>
        private static readonly string[] CriticalErrorPatterns =
        {
            "invalid api key",
            "authentication failed",
            "unauthorized",
            "forbidden",
            "account suspended",
            "insufficient credits"
        };

        /// <summary>
        /// Checks if the error represents a critical failure that needs immediate attention.
        /// </summary>
        public static bool IsCriticalFailure(string error)
        {
            var lowerError = error.ToLowerInvariant();
            return CriticalErrorPatterns.Any(pattern => lowerError.Contains(pattern));
        }

        /// <summary>
        /// Categorizes an error into a type string for structured metrics/alerting.
        /// </summary>
        public static string DetermineErrorType(string error, string? errorCode)
        {
            if (string.IsNullOrEmpty(error))
                return "unknown";

            var lowerError = error.ToLowerInvariant();

            if (lowerError.Contains("rate limit") || lowerError.Contains("quota"))
                return "rate_limit";
            if (lowerError.Contains("auth") || lowerError.Contains("unauthorized"))
                return "authentication";
            if (lowerError.Contains("timeout"))
                return "timeout";
            if (lowerError.Contains("invalid") || lowerError.Contains("bad request"))
                return "validation";
            if (lowerError.Contains("not found"))
                return "not_found";
            if (lowerError.Contains("server error") || lowerError.Contains("internal"))
                return "server_error";

            return "other";
        }

        #endregion
    }
}
