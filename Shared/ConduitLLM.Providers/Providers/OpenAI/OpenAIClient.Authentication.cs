using ConduitLLM.Configuration;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Configuration;

namespace ConduitLLM.Providers.OpenAI
{
    public partial class OpenAIClient
    {
        /// <summary>
        /// Gets the health check URL for OpenAI or Azure OpenAI.
        /// </summary>
        public override string GetHealthCheckUrl(string? baseUrl = null)
        {
            // Resolve through the registry so structured settings (Azure's {resource_name}) are
            // substituted; a raw Provider.BaseUrl read would leave the placeholder in the URL.
            var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl)
                ? baseUrl.TrimEnd('/')
                : ProviderConfigurationRegistry.ResolveBaseUrl(Provider);

            if (_isAzure)
            {
                var url = UrlBuilder.Combine(effectiveBaseUrl, "openai", "deployments");
                return UrlBuilder.AppendQueryString(url, ("api-version", AzureApiVersion));
            }

            return UrlBuilder.Combine(effectiveBaseUrl, Constants.Endpoints.Models);
        }

        /// <summary>
        /// Gets the default base URL for the configured provider type from the registry.
        /// </summary>
        protected override string GetDefaultBaseUrl()
        {
            return ProviderConfigurationRegistry.GetDefaultBaseUrl(Provider.ProviderType)
                ?? ProviderConfigurationRegistry.GetDefaultBaseUrl(ProviderType.OpenAI)!;
        }
    }
}
