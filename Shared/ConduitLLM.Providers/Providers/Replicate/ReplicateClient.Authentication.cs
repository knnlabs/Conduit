using System.Net;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Providers.Configuration;

namespace ConduitLLM.Providers.Replicate
{
    public partial class ReplicateClient
    {
        protected override AuthenticationResult? TranslateAuthenticationFailure(
            HttpStatusCode statusCode,
            string responseContent) =>
            statusCode == HttpStatusCode.Unauthorized
                ? AuthenticationResult.Failure(
                    "Authentication failed",
                    ProviderConfigurationRegistry.GetErrorMessages(ProviderType.Replicate).InvalidApiKey)
                : null;

        /// <summary>
        /// Gets the health check URL for Replicate.
        /// Replicate uses the /account endpoint for authentication verification.
        /// </summary>
        public override string GetHealthCheckUrl(string? baseUrl = null)
        {
            var defaultBaseUrl = ProviderConfigurationRegistry.GetDefaultBaseUrl(ProviderType.Replicate)!;

            var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl)
                ? baseUrl.TrimEnd('/')
                : (Provider.BaseUrl ?? defaultBaseUrl).TrimEnd('/');

            if (!effectiveBaseUrl.EndsWith("/v1"))
            {
                effectiveBaseUrl = $"{effectiveBaseUrl}/v1";
            }

            var healthCheckEndpoint =
                ProviderConfigurationRegistry.GetHealthCheckEndpoint(ProviderType.Replicate);
            return $"{effectiveBaseUrl}{healthCheckEndpoint}";
        }
    }
}
