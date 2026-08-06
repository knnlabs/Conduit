using System.Net.Http.Headers;

namespace ConduitLLM.Providers.Authentication
{
    /// <summary>
    /// Authentication strategy using Token scheme in the Authorization header.
    /// </summary>
    /// <remarks>
    /// Used by Replicate API.
    ///
    /// Header format: Authorization: Token {apiKey}
    /// </remarks>
    public sealed class TokenStrategy : IAuthenticationStrategy
    {
        /// <summary>
        /// Singleton instance for reuse.
        /// </summary>
        public static readonly TokenStrategy Instance = new();

        /// <inheritdoc />
        public string AuthenticationType => "Token";

        /// <inheritdoc />
        public bool RequiresApiKey => true;

        /// <inheritdoc />
        public void ApplyAuthentication(HttpClient client, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiKey);
        }

        /// <inheritdoc />
        public void ApplyAuthentication(HttpRequestMessage request, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Token", apiKey);
        }

        /// <inheritdoc />
        public AuthenticationHeaderValue? CreateAuthenticationHeader(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
            }

            return new AuthenticationHeaderValue("Token", apiKey);
        }
    }
}
