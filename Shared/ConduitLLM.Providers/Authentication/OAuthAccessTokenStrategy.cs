using System.Net.Http.Headers;

namespace ConduitLLM.Providers.Authentication
{
    /// <summary>
    /// Applies a short-lived OAuth 2.0 access token as a Bearer credential.
    /// </summary>
    /// <remarks>
    /// The provider client, rather than the operator, obtains the token. Consequently this
    /// strategy does not require a value in <c>ProviderKeyCredential.ApiKey</c>, even though the
    /// resulting HTTP authentication header has the usual Bearer shape.
    /// </remarks>
    public sealed class OAuthAccessTokenStrategy : IAuthenticationStrategy
    {
        public static OAuthAccessTokenStrategy Instance { get; } = new();

        private OAuthAccessTokenStrategy()
        {
        }

        public string AuthenticationType => "OAuth2 Bearer Token";

        public bool RequiresApiKey => false;

        public void ApplyAuthentication(HttpClient client, string apiKey)
        {
            client.DefaultRequestHeaders.Authorization = CreateAuthenticationHeader(apiKey);
        }

        public void ApplyAuthentication(HttpRequestMessage request, string apiKey)
        {
            request.Headers.Authorization = CreateAuthenticationHeader(apiKey);
        }

        public AuthenticationHeaderValue CreateAuthenticationHeader(string apiKey) =>
            new("Bearer", apiKey);
    }
}
