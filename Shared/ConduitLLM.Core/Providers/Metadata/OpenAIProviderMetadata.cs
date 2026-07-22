using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Providers;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Providers.Metadata
{
    /// <summary>
    /// Provider metadata for OpenAI.
    /// </summary>
    public class OpenAIProviderMetadata : BaseProviderMetadata
    {
        /// <inheritdoc />
        public override ProviderType ProviderType => ProviderType.OpenAI;

        /// <inheritdoc />
        public override string DisplayName => "OpenAI";

        /// <inheritdoc />
        public override string DefaultBaseUrl => ProviderAdapterDefaultsRegistry.GetRequired(ProviderType).DefaultBaseUrl;

        /// <summary>
        /// Initializes a new instance of the OpenAIProviderMetadata class.
        /// </summary>
        public OpenAIProviderMetadata()
        {
            // Configure authentication
            AuthRequirements = new AuthenticationRequirements
            {
                RequiresApiKey = true,
                SupportsOAuth = false,
                ApiKeyHeaderName = "Authorization",
                CustomFields = new List<AuthField>
                {
                    CreateApiKeyField("OpenAI API Key", 
                        "Get your API key from https://platform.openai.com/api-keys")
                }
            };

            // Configure hints
            ConfigurationHints = new ProviderConfigurationHints
            {
                DocumentationUrl = "https://platform.openai.com/docs/api-reference",
                ExampleValues = new Dictionary<string, string>
                {
                    ["apiKey"] = "sk-...",
                    ["model"] = "gpt-4-turbo-preview"
                },
                Tips = new List<ConfigurationTip>
                {
                    new ConfigurationTip
                    {
                        Title = "API Key Format",
                        Description = "OpenAI API keys start with 'sk-' followed by alphanumeric characters",
                        Severity = TipSeverity.Info
                    },
                    new ConfigurationTip
                    {
                        Title = "Rate Limits",
                        Description = "Be aware of rate limits based on your API tier. Consider implementing retry logic.",
                        Severity = TipSeverity.Warning
                    }
                }
            };
        }

        /// <inheritdoc />
        public override ValidationResult ValidateConfiguration(Dictionary<string, object> configuration)
        {
            var baseResult = base.ValidateConfiguration(configuration);
            if (!baseResult.IsValid)
                return baseResult;

            // Additional OpenAI-specific validation
            if (configuration.TryGetValue("apiKey", out var apiKey))
            {
                var key = apiKey?.ToString() ?? "";
                if (!key.StartsWith("sk-"))
                {
                    return ValidationResult.Failure("apiKey", 
                        "OpenAI API key must start with 'sk-'");
                }
            }

            return ValidationResult.Success();
        }
    }
}
