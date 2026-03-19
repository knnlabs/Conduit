using ConduitLLM.Configuration;
using ConduitLLM.Providers.Configuration;

namespace ConduitLLM.Providers.Groq
{
    /// <summary>
    /// GroqClient partial class containing error handling methods.
    /// </summary>
    public partial class GroqClient
    {
        /// <summary>
        /// Gets the Groq-specific error messages from the configuration registry.
        /// </summary>
        private static ProviderErrorMessages GroqErrorMessages =>
            ProviderConfigurationRegistry.GetErrorMessages(ProviderType.Groq);

        /// <summary>
        /// Extracts a more helpful error message from exception details for Groq errors.
        /// Adds Groq-specific keyword matching on top of base extraction.
        /// </summary>
        protected override string ExtractEnhancedErrorMessage(Exception ex)
        {
            var baseResult = base.ExtractEnhancedErrorMessage(ex);

            // If the base found something useful beyond the raw message, use it
            if (!string.IsNullOrEmpty(baseResult) &&
                !baseResult.Equals(ex.Message) &&
                !baseResult.Contains("Exception of type"))
            {
                return baseResult;
            }

            // Groq-specific keyword matching
            var msg = ex.Message;

            if (msg.Contains("model not found", StringComparison.OrdinalIgnoreCase) ||
                (msg.Contains("The model", StringComparison.OrdinalIgnoreCase) &&
                msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase)))
            {
                return GroqErrorMessages.ModelNotFound;
            }

            if (msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
            {
                return GroqErrorMessages.RateLimitExceeded;
            }

            // Fallback: use base result with provider prefix
            return $"Groq API error: {baseResult}";
        }
    }
}
