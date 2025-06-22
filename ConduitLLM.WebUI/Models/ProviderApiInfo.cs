using System;
using System.Collections.Generic;

namespace ConduitLLM.WebUI.Models
{
    /// <summary>
    /// Helper class for provider API information like URLs for documentation and API key management
    /// </summary>
    public static class ProviderApiInfo
    {
        private static readonly Dictionary<string, string> _apiKeyUrls = new(StringComparer.OrdinalIgnoreCase)
        {
            { "openai", "https://platform.openai.com/api-keys" },
            { "azure_openai", "https://portal.azure.com/#blade/Microsoft_Azure_ProjectOxford/CognitiveServicesHub/OpenAI" },
            { "anthropic", "https://console.anthropic.com/account/keys" },
            { "google_gemini", "https://ai.google.dev/tutorials/setup" },
            { "cohere", "https://dashboard.cohere.com/api-keys" },
            { "mistral", "https://console.mistral.ai/api-keys" },
            { "groq", "https://console.groq.com/keys" },
            { "together", "https://api.together.xyz/settings/api-keys" },
            { "huggingface", "https://huggingface.co/settings/tokens" },
            { "replicate", "https://replicate.com/account/api-tokens" },
            { "ollama", "https://github.com/ollama/ollama" },
            { "fireworks", "https://app.fireworks.ai/users/settings/api-keys" },
            { "minimax", "https://www.minimaxi.com/user-center/basic-information" }
        };

        private static readonly Dictionary<string, string> _documentationUrls = new(StringComparer.OrdinalIgnoreCase)
        {
            { "openai", "https://platform.openai.com/docs" },
            { "azure_openai", "https://learn.microsoft.com/en-us/azure/ai-services/openai/" },
            { "anthropic", "https://docs.anthropic.com/claude/reference/getting-started-with-the-api" },
            { "google_gemini", "https://ai.google.dev/docs" },
            { "cohere", "https://docs.cohere.com/reference/about" },
            { "mistral", "https://docs.mistral.ai/" },
            { "groq", "https://console.groq.com/docs" },
            { "together", "https://docs.together.ai/docs" },
            { "huggingface", "https://huggingface.co/docs" },
            { "replicate", "https://replicate.com/docs" },
            { "ollama", "https://github.com/ollama/ollama/blob/main/docs/api.md" },
            { "fireworks", "https://readme.fireworks.ai/" },
            { "minimax", "https://www.minimaxi.com/document" }
        };

        /// <summary>
        /// Gets the URL for obtaining an API key for a provider
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>The URL for obtaining an API key, or null if not available</returns>
        public static string? GetApiKeyUrl(string providerName)
        {
            return _apiKeyUrls.TryGetValue(providerName, out var url) ? url : null;
        }

        /// <summary>
        /// Gets the URL for API documentation for a provider
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>The URL for documentation, or null if not available</returns>
        public static string? GetDocumentationUrl(string providerName)
        {
            return _documentationUrls.TryGetValue(providerName, out var url) ? url : null;
        }
    }
}
