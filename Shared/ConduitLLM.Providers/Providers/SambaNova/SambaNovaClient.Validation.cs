namespace ConduitLLM.Providers.SambaNova
{
    /// <summary>
    /// SambaNovaClient partial class containing validation methods.
    /// </summary>
    public partial class SambaNovaClient
    {
        /// <summary>
        /// Validates the model ID for SambaNova-specific requirements.
        /// </summary>
        /// <param name="modelId">The model ID to validate.</param>
        /// <returns>True if the model ID is valid, false otherwise.</returns>
        private bool IsValidModelId(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
                return false;

            // SambaNova model IDs follow specific patterns
            var validPrefixes = new[]
            {
                "DeepSeek-",
                "Meta-Llama-",
                "Llama-",
                "Qwen",
                "E5-",
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