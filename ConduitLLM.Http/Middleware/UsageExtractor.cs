using System.Text.Json;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Http.Middleware
{
    /// <summary>
    /// Static helper methods for extracting usage data from LLM API responses.
    /// Supports multiple provider formats including OpenAI and Anthropic.
    /// </summary>
    public static class UsageExtractor
    {
        /// <summary>
        /// Extracts usage data from a JSON response element.
        /// </summary>
        /// <param name="usageElement">The usage JSON element from the response</param>
        /// <param name="logger">Logger for error reporting</param>
        /// <returns>Extracted usage data or null if extraction fails</returns>
        public static Usage? ExtractUsage(JsonElement usageElement, ILogger logger)
        {
            try
            {
                var usage = new Usage();

                // Standard OpenAI fields
                if (usageElement.TryGetProperty("prompt_tokens", out var promptTokens))
                    usage.PromptTokens = promptTokens.GetInt32();

                if (usageElement.TryGetProperty("completion_tokens", out var completionTokens))
                    usage.CompletionTokens = completionTokens.GetInt32();

                if (usageElement.TryGetProperty("total_tokens", out var totalTokens))
                    usage.TotalTokens = totalTokens.GetInt32();

                // Anthropic format (uses input_tokens/output_tokens)
                // Note: These will override OpenAI fields if both exist
                if (usageElement.TryGetProperty("input_tokens", out var inputTokens))
                    usage.PromptTokens = inputTokens.GetInt32();

                if (usageElement.TryGetProperty("output_tokens", out var outputTokens))
                    usage.CompletionTokens = outputTokens.GetInt32();

                // Anthropic cached tokens
                if (usageElement.TryGetProperty("cache_creation_input_tokens", out var cacheWriteTokens))
                    usage.CachedWriteTokens = cacheWriteTokens.GetInt32();

                if (usageElement.TryGetProperty("cache_read_input_tokens", out var cacheReadTokens))
                    usage.CachedInputTokens = cacheReadTokens.GetInt32();

                // Image generation
                if (usageElement.TryGetProperty("images", out var imageCount))
                    usage.ImageCount = imageCount.GetInt32();

                // Validate we have at least some usage data
                if (usage.PromptTokens == null && 
                    usage.CompletionTokens == null && 
                    usage.ImageCount == null)
                {
                    return null;
                }

                return usage;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to extract usage data from response");
                return null;
            }
        }

        /// <summary>
        /// Determines the request type from the API path.
        /// </summary>
        /// <param name="path">The request path</param>
        /// <returns>The type of request (chat, completion, embedding, etc.)</returns>
        public static string DetermineRequestType(PathString path)
        {
            var pathValue = path.Value?.ToLowerInvariant() ?? "";
            
            if (pathValue.Contains("/chat/completions"))
                return "chat";
            if (pathValue.Contains("/completions"))
                return "completion";
            if (pathValue.Contains("/embeddings"))
                return "embedding";
            if (pathValue.Contains("/images/generations"))
                return "image";
            if (pathValue.Contains("/audio/transcriptions"))
                return "transcription";
            if (pathValue.Contains("/audio/speech"))
                return "tts";
            if (pathValue.Contains("/videos/generations"))
                return "video";
            
            return "other";
        }

        /// <summary>
        /// Calculates the response time from the request start time stored in HttpContext.
        /// </summary>
        /// <param name="context">The HTTP context</param>
        /// <returns>Response time in milliseconds</returns>
        public static double GetResponseTime(HttpContext context)
        {
            if (context.Items.TryGetValue("RequestStartTime", out var startTimeObj) && 
                startTimeObj is DateTime startTime)
            {
                return (DateTime.UtcNow - startTime).TotalMilliseconds;
            }
            
            return 0;
        }
    }
}