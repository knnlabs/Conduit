using System.Collections.Generic;

namespace ConduitLLM.WebUI.Data;

/// <summary>
/// Contains provider-specific information such as API key generation URLs.
/// </summary>
public static class ProviderApiInfo
{
    /// <summary>
    /// Dictionary mapping provider names to their API key generation URLs.
    /// </summary>
    public static readonly Dictionary<string, string> ApiKeyUrls = new Dictionary<string, string>
    {
        { "OpenAI", "https://platform.openai.com/api-keys" },
        { "Anthropic", "https://console.anthropic.com/keys" },
        { "Cohere", "https://dashboard.cohere.com/api-keys" },
        { "Gemini", "https://makersuite.google.com/app/apikey" },
        { "Fireworks", "https://app.fireworks.ai/users/settings/api-keys" },
        { "OpenRouter", "https://openrouter.ai/keys" },
        { "Cerebras", "https://cloud.cerebras.ai" },
        // Add more providers as needed
    };

    /// <summary>
    /// Dictionary mapping provider names to their documentation URLs.
    /// </summary>
    public static readonly Dictionary<string, string> DocumentationUrls = new Dictionary<string, string>
    {
        { "OpenAI", "https://platform.openai.com/docs/introduction" },
        { "Anthropic", "https://docs.anthropic.com/claude/reference/getting-started-with-the-api" },
        { "Cohere", "https://docs.cohere.com/docs" },
        { "Gemini", "https://ai.google.dev/docs" },
        { "Fireworks", "https://docs.fireworks.ai/api" },
        { "OpenRouter", "https://openrouter.ai/docs" },
        { "Cerebras", "https://cloud.cerebras.ai" },
        // Add more providers as needed
    };

    /// <summary>
    /// Get the API key generation URL for a specified provider.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <returns>The URL for generating API keys, or null if not found.</returns>
    public static string? GetApiKeyUrl(string providerName)
    {
        if (string.IsNullOrEmpty(providerName) || !ApiKeyUrls.ContainsKey(providerName))
        {
            return null;
        }
        
        return ApiKeyUrls[providerName];
    }

    /// <summary>
    /// Get the documentation URL for a specified provider.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <returns>The URL for the provider documentation, or null if not found.</returns>
    public static string? GetDocumentationUrl(string providerName)
    {
        if (string.IsNullOrEmpty(providerName) || !DocumentationUrls.ContainsKey(providerName))
        {
            return null;
        }
        
        return DocumentationUrls[providerName];
    }
}
