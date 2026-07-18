using System.Net.Http.Headers;

namespace ConduitLLM.Providers.Authentication
{
    /// <summary>
    /// Authentication strategy using a custom API key header.
    /// </summary>
    /// <remarks>
    /// Used by Azure OpenAI and other providers that use custom header-based authentication.
    ///
    /// Default header format: api-key: {apiKey}
    /// </remarks>
    public sealed class ApiKeyHeaderStrategy : IAuthenticationStrategy
    {
        /// <summary>
        /// Singleton instance using the default "api-key" header name (for Azure OpenAI).
        /// </summary>
        public static readonly ApiKeyHeaderStrategy AzureInstance = new("api-key");

        private readonly string _headerName;

        /// <summary>
        /// Creates a new ApiKeyHeaderStrategy with the specified header name.
        /// </summary>
        /// <param name="headerName">The header name to use for the API key.</param>
        public ApiKeyHeaderStrategy(string headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName))
            {
                throw new ArgumentException("Header name cannot be null or empty", nameof(headerName));
            }

            _headerName = headerName;
        }

        /// <inheritdoc />
        public string AuthenticationType => $"Header:{_headerName}";

        /// <inheritdoc />
        public void ApplyAuthentication(HttpClient client, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
            }

            // Remove existing header if present
            client.DefaultRequestHeaders.Remove(_headerName);
            client.DefaultRequestHeaders.Add(_headerName, apiKey);
        }

        /// <inheritdoc />
        public void ApplyAuthentication(HttpRequestMessage request, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
            }

            // Remove existing header if present
            request.Headers.Remove(_headerName);
            request.Headers.Add(_headerName, apiKey);
        }

        /// <inheritdoc />
        public AuthenticationHeaderValue? CreateAuthenticationHeader(string apiKey)
        {
            // This strategy doesn't use the Authorization header
            return null;
        }

        /// <summary>
        /// Gets the header name used by this strategy.
        /// </summary>
        public string HeaderName => _headerName;
    }
}
