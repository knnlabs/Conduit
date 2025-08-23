namespace ConduitLLM.Providers.Cerebras
{
    /// <summary>
    /// CerebrasClient partial class containing validation methods.
    /// </summary>
    public partial class CerebrasClient
    {
        /// <summary>
        /// Validates the model ID for Cerebras-specific requirements.
        /// </summary>
        /// <param name="modelId">The model ID to validate.</param>
        /// <returns>True if the model ID is valid, false otherwise.</returns>
        private bool IsValidModelId(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
                return false;

            // Cerebras model IDs follow specific patterns
            var validPrefixes = new[]
            {
                "llama3.1-",
                "llama-3.3-",
                "llama-4-scout-",
                "qwen-3-",
                "deepseek-r1-"
            };

            foreach (var prefix in validPrefixes)
            {
                if (modelId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
