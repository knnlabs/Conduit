using System.Collections.Generic;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Providers.Metadata
{
    /// <summary>
    /// Provider metadata for Google Gemini.
    /// </summary>
    public class GeminiProviderMetadata : BaseProviderMetadata
    {
        /// <inheritdoc />
        public override ProviderType ProviderType => ProviderType.Gemini;

        /// <inheritdoc />
        public override string DisplayName => "Google Gemini";

        /// <inheritdoc />
        public override string DefaultBaseUrl => "https://generativelanguage.googleapis.com/v1beta";

        /// <summary>
        /// Initializes a new instance of the GeminiProviderMetadata class.
        /// </summary>
        public GeminiProviderMetadata()
        {
            // Configure Gemini-specific capabilities
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
                    PresencePenalty = false,
                    FrequencyPenalty = false,
                    LogitBias = false,
                    N = true,
                    User = false,
                    Seed = false,
                    ResponseFormat = false,
                    Tools = true,
                    Constraints = new ParameterConstraints
                    {
                        TemperatureRange = new Range<double>(0.0, 1.0),
                        TopPRange = new Range<double>(0.0, 1.0),
                        TopKRange = new Range<int>(1, 40),
                        MaxTokenLimit = 32768 // Gemini Pro max
                    }
                },
                Features = new FeatureSupport
                {
                    Streaming = true,
                    Embeddings = true,
                    ImageGeneration = false,
                    VisionInput = true,
                    FunctionCalling = true,
                    AudioTranscription = false,
                    TextToSpeech = false
                }
            };

            // Configure authentication
            AuthRequirements = new AuthenticationRequirements
            {
                RequiresApiKey = true,
                SupportsOAuth = false,
                ApiKeyHeaderName = "x-goog-api-key",
                CustomFields = new List<AuthField>
                {
                    CreateApiKeyField("Google AI API Key", 
                        "Get your API key from https://makersuite.google.com/app/apikey")
                }
            };

            // Configure hints
            ConfigurationHints = new ProviderConfigurationHints
            {
                DocumentationUrl = "https://ai.google.dev/tutorials/rest_quickstart",
                ExampleValues = new Dictionary<string, string>
                {
                    ["model"] = "gemini-pro",
                    ["apiKey"] = "AIza..."
                },
                Tips = new List<ConfigurationTip>
                {
                    new ConfigurationTip
                    {
                        Title = "Model Variants",
                        Description = "Use 'gemini-pro' for text and 'gemini-pro-vision' for multimodal inputs",
                        Severity = TipSeverity.Info
                    },
                    new ConfigurationTip
                    {
                        Title = "Rate Limits",
                        Description = "Free tier has strict rate limits. Consider upgrading for production use.",
                        Severity = TipSeverity.Warning
                    }
                }
            };
        }
    }
}