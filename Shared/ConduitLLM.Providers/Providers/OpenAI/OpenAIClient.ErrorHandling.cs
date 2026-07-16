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
        /// Refines error classification with OpenAI-specific logging.
        /// Common patterns (quota, rate limit, model not found) are handled by BaseLLMClient.
        /// </summary>
        protected override ProviderErrorType RefineErrorClassification(
            ProviderErrorType baseType,
            string? responseBody)
        {
            var refined = base.RefineErrorClassification(baseType, responseBody);

            // Add OpenAI-specific logging for quota issues
            if (refined == ProviderErrorType.InsufficientBalance &&
                baseType == ProviderErrorType.AccessForbidden)
            {
                Logger.LogWarning("OpenAI returned 403 for insufficient quota/billing issue");
            }

            return refined;
        }
    }
}
