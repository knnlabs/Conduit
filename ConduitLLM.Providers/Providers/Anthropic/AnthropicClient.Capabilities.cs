using System;
using System.Threading.Tasks;

using CoreModels = ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.Providers.Anthropic
{
    /// <summary>
    /// AnthropicClient partial class containing capabilities and constraints functionality.
    /// </summary>
    public partial class AnthropicClient
    {
        /// <inheritdoc />
        public override Task<CoreModels.ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            var model = modelId ?? ProviderModelId;
            var isClaudeModel = model.StartsWith("claude", StringComparison.OrdinalIgnoreCase);
            var isVisionCapable = model.Contains("3.5-sonnet", StringComparison.OrdinalIgnoreCase) ||
                                  model.Contains("3-opus", StringComparison.OrdinalIgnoreCase) ||
                                  model.Contains("3-haiku", StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(new CoreModels.ProviderCapabilities
            {
                Provider = ProviderName,
                ModelId = model,
                ChatParameters = new CoreModels.ChatParameterSupport
                {
                    Temperature = true,
                    MaxTokens = true,
                    TopP = true,
                    TopK = true, // Anthropic supports top-k
                    Stop = true,
                    PresencePenalty = false, // Anthropic doesn't support presence penalty
                    FrequencyPenalty = false, // Anthropic doesn't support frequency penalty
                    LogitBias = false, // Anthropic doesn't support logit bias
                    N = false, // Anthropic doesn't support multiple choices
                    User = false, // Anthropic doesn't support user parameter
                    Seed = false, // Anthropic doesn't support seed
                    ResponseFormat = false, // Anthropic doesn't support response format
                    Tools = isClaudeModel, // Tool calling available for Claude models
                    Constraints = new CoreModels.ParameterConstraints
                    {
                        TemperatureRange = new CoreModels.Range<double>(0.0, 1.0),
                        TopPRange = new CoreModels.Range<double>(0.0, 1.0),
                        TopKRange = new CoreModels.Range<int>(1, 40),
                        MaxStopSequences = 5,
                        MaxTokenLimit = GetAnthropicMaxTokens(model)
                    }
                },
                Features = new CoreModels.FeatureSupport
                {
                    Streaming = true,
                    Embeddings = false, // Anthropic doesn't provide embeddings
                    ImageGeneration = false, // Anthropic doesn't provide image generation
                    VisionInput = isVisionCapable,
                    FunctionCalling = isClaudeModel,
                    AudioTranscription = false, // Anthropic doesn't provide audio transcription
                    TextToSpeech = false // Anthropic doesn't provide text-to-speech
                }
            });
        }

        private int GetAnthropicMaxTokens(string model)
        {
            return model.ToLowerInvariant() switch
            {
                var m when m.Contains("claude-3") => 4096,
                var m when m.Contains("claude-2") => 4096,
                var m when m.Contains("claude-1") => 9000,
                var m when m.Contains("claude-instant") => 9000,
                _ => 4096 // Default fallback
            };
        }
    }
}