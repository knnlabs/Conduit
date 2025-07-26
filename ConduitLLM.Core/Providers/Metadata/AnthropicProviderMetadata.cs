using System.Collections.Generic;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Providers.Metadata
{
    /// <summary>
    /// Provider metadata for Anthropic (Claude).
    /// </summary>
    public class AnthropicProviderMetadata : BaseProviderMetadata
    {
        /// <inheritdoc />
        public override ProviderType ProviderType => ProviderType.Anthropic;

        /// <inheritdoc />
        public override string DisplayName => "Anthropic";

        /// <inheritdoc />
        public override string DefaultBaseUrl => "https://api.anthropic.com/v1";

        /// <summary>
        /// Initializes a new instance of the AnthropicProviderMetadata class.
        /// </summary>
        public AnthropicProviderMetadata()
        {
            // Configure Anthropic-specific capabilities
            Capabilities = new ProviderCapabilities
            {
                Provider = ProviderType.ToString(),
                ChatParameters = new ChatParameterSupport
                {
                    Temperature = true,
                    MaxTokens = true,
                    TopP = true,
                    TopK = true,
                    Stop = true,
                    PresencePenalty = false, // Anthropic doesn't support these OpenAI-specific params
                    FrequencyPenalty = false,
                    LogitBias = false,
                    N = false,
                    User = false,
                    Seed = false,
                    ResponseFormat = false,
                    Tools = true, // Anthropic supports tool use
                    Constraints = new ParameterConstraints
                    {
                        TemperatureRange = new Range<double>(0.0, 1.0),
                        TopPRange = new Range<double>(0.0, 1.0),
                        TopKRange = new Range<int>(1, 100),
                        MaxTokenLimit = 200000 // Claude 3 max context
                    }
                },
                Features = new FeatureSupport
                {
                    Streaming = true,
                    Embeddings = false,
                    ImageGeneration = false,
                    VisionInput = true, // Claude 3 supports vision
                    FunctionCalling = true, // Via tool use
                    AudioTranscription = false,
                    TextToSpeech = false
                }
            };

            // Configure authentication
            AuthRequirements = new AuthenticationRequirements
            {
                RequiresApiKey = true,
                SupportsOAuth = false,
                ApiKeyHeaderName = "x-api-key",
                CustomFields = new List<AuthField>
                {
                    new AuthField
                    {
                        Name = "apiKey",
                        DisplayName = "Anthropic API Key",
                        Required = true,
                        Type = AuthFieldType.Password,
                        HelpText = "Get your API key from https://console.anthropic.com/settings/keys"
                    },
                    new AuthField
                    {
                        Name = "anthropicVersion",
                        DisplayName = "API Version",
                        Required = false,
                        Type = AuthFieldType.Text,
                        ValidationPattern = @"^\d{4}-\d{2}-\d{2}$",
                        HelpText = "API version in YYYY-MM-DD format (e.g., 2023-06-01)"
                    }
                }
            };

            // Configure hints
            ConfigurationHints = new ProviderConfigurationHints
            {
                DocumentationUrl = "https://docs.anthropic.com/claude/reference",
                ExampleValues = new Dictionary<string, string>
                {
                    ["apiKey"] = "sk-ant-...",
                    ["model"] = "claude-3-opus-20240229",
                    ["anthropicVersion"] = "2023-06-01"
                },
                Tips = new List<ConfigurationTip>
                {
                    new ConfigurationTip
                    {
                        Title = "API Version Header",
                        Description = "Anthropic requires an 'anthropic-version' header. If not specified, the latest version will be used.",
                        Severity = TipSeverity.Info
                    },
                    new ConfigurationTip
                    {
                        Title = "Model Naming",
                        Description = "Claude models include version dates (e.g., claude-3-opus-20240229)",
                        Severity = TipSeverity.Info
                    },
                    new ConfigurationTip
                    {
                        Title = "Message Format",
                        Description = "Anthropic uses a different message format than OpenAI. The system ensures proper conversion.",
                        Severity = TipSeverity.Info
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

            // Additional Anthropic-specific validation
            if (configuration.TryGetValue("apiKey", out var apiKey))
            {
                var key = apiKey?.ToString() ?? "";
                if (!key.StartsWith("sk-ant-"))
                {
                    return ValidationResult.Failure("apiKey", 
                        "Anthropic API key must start with 'sk-ant-'");
                }
            }

            return ValidationResult.Success();
        }
    }
}