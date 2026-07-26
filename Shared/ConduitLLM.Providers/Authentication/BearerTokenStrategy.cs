using System.Net.Http.Headers;

namespace ConduitLLM.Providers.Authentication
{
    /// <summary>
    /// Authentication strategy using Bearer token in the Authorization header.
    /// </summary>
    /// <remarks>
    /// Used by most OpenAI-compatible providers including:
    /// - OpenAI
    /// - Groq
    /// - Fireworks
    /// - Cerebras
    /// - SambaNova
    /// - DeepInfra
    /// - MiniMax
    ///
    /// Header format: Authorization: Bearer {apiKey}
    /// </remarks>
    public sealed class BearerTokenStrategy : IAuthenticationStrategy
    {
        /// <summary>
        /// Singleton instance for reuse.
        /// </summary>
        public static readonly BearerTokenStrategy Instance = new();

        /// <inheritdoc />
        public string AuthenticationType => "Bearer";

        /// <inheritdoc />
        public bool RequiresApiKey => true;

        /// <inheritdoc />
        public void ApplyAuthentication(HttpClient client, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        /// <inheritdoc />
        public void ApplyAuthentication(HttpRequestMessage request, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        /// <inheritdoc />
        public AuthenticationHeaderValue? CreateAuthenticationHeader(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
            }

            return new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }
}
