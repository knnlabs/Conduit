using System.Net.Http.Headers;

namespace ConduitLLM.Providers.Authentication
{
    /// <summary>
    /// Defines the contract for provider authentication strategies.
    /// </summary>
    /// <remarks>
    /// Different LLM providers use different authentication methods:
    /// - Bearer token (OpenAI, Groq, Fireworks, etc.)
    /// - Token scheme (Replicate)
    /// - API key header (Azure OpenAI)
    ///
    /// This interface allows providers to specify their authentication method
    /// without duplicating authentication logic across provider implementations.
    /// </remarks>
    public interface IAuthenticationStrategy
    {
        /// <summary>
        /// Gets the authentication type name for logging and diagnostics.
        /// </summary>
        string AuthenticationType { get; }

        /// <summary>
        /// Applies authentication to an HttpClient's default request headers.
        /// </summary>
        /// <param name="client">The HttpClient to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        void ApplyAuthentication(HttpClient client, string apiKey);

        /// <summary>
        /// Applies authentication to an individual HttpRequestMessage.
        /// </summary>
        /// <param name="request">The request to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        void ApplyAuthentication(HttpRequestMessage request, string apiKey);

        /// <summary>
        /// Creates an AuthenticationHeaderValue for the given API key.
        /// Returns null if this strategy uses a different header mechanism.
        /// </summary>
        /// <param name="apiKey">The API key to use.</param>
        /// <returns>An AuthenticationHeaderValue or null if not applicable.</returns>
        AuthenticationHeaderValue? CreateAuthenticationHeader(string apiKey);
    }
}
