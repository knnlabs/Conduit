using System.Net;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.OpenAI
{
    /// <summary>
    /// OpenAIClient partial class containing error handling and classification functionality.
    /// </summary>
    public partial class OpenAIClient
    {
        /// <summary>
        /// Refines error classification based on OpenAI-specific error patterns.
        /// </summary>
        /// <param name="baseType">The base error type determined from HTTP status code.</param>
        /// <param name="responseBody">The response body containing error details.</param>
        /// <returns>The refined error type.</returns>
        protected override ProviderErrorType RefineErrorClassification(
            ProviderErrorType baseType, 
            string? responseBody)
        {
            // OpenAI often returns 403 for insufficient quota
            if (baseType == ProviderErrorType.AccessForbidden && !string.IsNullOrEmpty(responseBody))
            {
                var lowerBody = responseBody.ToLowerInvariant();
                
                // Check for quota/billing related messages
                if (lowerBody.Contains("insufficient_quota") ||
                    lowerBody.Contains("exceeded your current quota") ||
                    lowerBody.Contains("billing") ||
                    lowerBody.Contains("payment") ||
                    lowerBody.Contains("credit"))
                {
                    Logger.LogWarning("OpenAI returned 403 for insufficient quota/billing issue");
                    return ProviderErrorType.InsufficientBalance;
                }
            }
            
            // Check for rate limit in error message even if status isn't 429
            if (!string.IsNullOrEmpty(responseBody))
            {
                var lowerBody = responseBody.ToLowerInvariant();
                
                if (lowerBody.Contains("rate limit") || 
                    lowerBody.Contains("too many requests"))
                {
                    return ProviderErrorType.RateLimitExceeded;
                }
                
                // Model not found patterns
                if (lowerBody.Contains("model") && 
                    (lowerBody.Contains("not found") || 
                     lowerBody.Contains("does not exist") ||
                     lowerBody.Contains("invalid model")))
                {
                    return ProviderErrorType.ModelNotFound;
                }
            }
            
            return baseType;
        }
    }
}