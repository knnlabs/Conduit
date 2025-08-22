namespace ConduitLLM.Providers.Groq
{
    /// <summary>
    /// GroqClient partial class containing error handling methods.
    /// </summary>
    public partial class GroqClient
    {
        /// <summary>
        /// Extracts a more helpful error message from exception details for Groq errors.
        /// </summary>
        /// <param name="ex">The exception to extract information from.</param>
        /// <returns>An enhanced error message specific to Groq errors.</returns>
        /// <remarks>
        /// This overrides the base implementation to provide more specific error extraction for Groq.
        /// </remarks>
        protected override string ExtractEnhancedErrorMessage(Exception ex)
        {
            // Use the base implementation first
            var baseErrorMessage = base.ExtractEnhancedErrorMessage(ex);

            // If the base implementation found a useful message, return it
            if (!string.IsNullOrEmpty(baseErrorMessage) &&
                !baseErrorMessage.Equals(ex.Message) &&
                !baseErrorMessage.Contains("Exception of type"))
            {
                return baseErrorMessage;
            }

            // Groq-specific error extraction
            var msg = ex.Message;

            // If we find "model not found" in the message, provide a more helpful message
            if (msg.Contains("model not found", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("The model", StringComparison.OrdinalIgnoreCase) &&
                msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
            {
                return Constants.ErrorMessages.ModelNotFound;
            }

            // For rate limit errors, provide a clearer message
            if (msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
            {
                return Constants.ErrorMessages.RateLimitExceeded;
            }

            // Look for Body data
            if (ex.Data.Contains("Body") && ex.Data["Body"] is string body && !string.IsNullOrEmpty(body))
            {
                return $"Groq API error: {body}";
            }

            // Try inner exception
            if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
            {
                return $"Groq API error: {ex.InnerException.Message}";
            }

            // Fallback to original message
            return $"Groq API error: {msg}";
        }
    }
}
