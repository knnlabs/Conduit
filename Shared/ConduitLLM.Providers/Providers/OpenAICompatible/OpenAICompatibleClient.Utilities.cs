using CoreModels = ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.OpenAICompatible
{
    /// <summary>
    /// OpenAICompatibleClient partial class containing utility and helper methods.
    /// </summary>
    public abstract partial class OpenAICompatibleClient
    {
        /// <summary>
        /// Configure the HTTP client with provider-specific settings.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        /// <remarks>
        /// This method adds standard headers and authentication to the HTTP client.
        /// Derived classes can override this method to provide provider-specific configuration.
        /// </remarks>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            base.ConfigureHttpClient(client, apiKey);

            // Set the base address if not already set
            if (client.BaseAddress == null && !string.IsNullOrEmpty(BaseUrl))
            {
                client.BaseAddress = new Uri(BaseUrl);
            }
        }

        /// <inheritdoc />
        public override Task<CoreModels.ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            var model = modelId ?? ProviderModelId;

            // For OpenAI-compatible providers, we provide sensible defaults.
            // Individual providers can override this with more specific capabilities.
            return Task.FromResult(new CoreModels.ProviderCapabilities
            {
                Provider = ProviderName,
                ModelId = model,
                ChatParameters = new CoreModels.ChatParameterSupport
                {
                    Temperature = true,
                    MaxTokens = true,
                    TopP = true,
                    TopK = false, // Most OpenAI-compatible APIs don't support top-k
                    Stop = true,
                    PresencePenalty = true,
                    FrequencyPenalty = true,
                    LogitBias = true,
                    N = true,
                    User = true,
                    Seed = true,
                    ResponseFormat = true,
                    Tools = true,
                    Constraints = new CoreModels.ParameterConstraints
                    {
                        TemperatureRange = new CoreModels.Range<double>(0.0, 2.0),
                        TopPRange = new CoreModels.Range<double>(0.0, 1.0),
                        MaxStopSequences = 4,
                        MaxTokenLimit = 4096 // Conservative default
                    }
                },
                Features = new CoreModels.FeatureSupport
                {
                    Streaming = true,
                    Embeddings = false, // Usually separate models
                    ImageGeneration = false, // Usually separate models
                    VisionInput = false, // Provider-specific
                    FunctionCalling = true
                }
            });
        }

        // ExtractEnhancedErrorMessage is inherited from BaseLLMClient
    }
}
