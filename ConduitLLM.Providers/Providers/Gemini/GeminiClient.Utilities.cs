using System;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.Providers.Gemini
{
    /// <summary>
    /// GeminiClient partial class containing utility methods.
    /// </summary>
    public partial class GeminiClient
    {
        /// <summary>
        /// Determines if a Gemini model is capable of processing image inputs.
        /// </summary>
        /// <param name="modelId">The Gemini model ID</param>
        /// <returns>True if the model supports vision capabilities</returns>
        private bool IsVisionCapableModel(string modelId)
        {
            // Check for vision-capable models based on naming patterns
            // Gemini 1.5 and above support vision input
            return modelId.Contains("gemini-1.5", StringComparison.OrdinalIgnoreCase) ||
                   // The original Gemini Pro Vision model
                   modelId.Contains("gemini-pro-vision", StringComparison.OrdinalIgnoreCase) ||
                   // Future-proof for Gemini 2.0+ models (assuming they will be multimodal)
                   modelId.Contains("gemini-2", StringComparison.OrdinalIgnoreCase) ||
                   modelId.Contains("gemini-3", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the maximum token limit for a Gemini model.
        /// </summary>
        private int GetGeminiMaxTokens(string model)
        {
            return model.ToLowerInvariant() switch
            {
                var m when m.Contains("1.5") => 1000000, // Gemini 1.5 models have 1M token context
                var m when m.Contains("1.0") => 32768,   // Gemini 1.0 models have 32K token context
                _ => 32768 // Default fallback
            };
        }

        /// <inheritdoc />
        public override Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            var model = modelId ?? ProviderModelId;
            var isVisionCapable = IsVisionCapableModel(model);

            return Task.FromResult(new ProviderCapabilities
            {
                Provider = ProviderName,
                ModelId = model,
                ChatParameters = new ChatParameterSupport
                {
                    Temperature = true,
                    MaxTokens = true,
                    TopP = true,
                    TopK = true, // Gemini supports top-k
                    Stop = true,
                    PresencePenalty = false, // Gemini doesn't support presence penalty
                    FrequencyPenalty = false, // Gemini doesn't support frequency penalty
                    LogitBias = false, // Gemini doesn't support logit bias
                    N = false, // Gemini doesn't support multiple choices
                    User = false, // Gemini doesn't support user parameter
                    Seed = false, // Gemini doesn't support seed
                    ResponseFormat = false, // Gemini doesn't support response format
                    Tools = false, // Gemini doesn't support tools through this client
                    Constraints = new ParameterConstraints
                    {
                        TemperatureRange = new Range<double>(0.0, 1.0),
                        TopPRange = new Range<double>(0.0, 1.0),
                        TopKRange = new Range<int>(1, 40),
                        MaxStopSequences = 5,
                        MaxTokenLimit = GetGeminiMaxTokens(model)
                    }
                },
                Features = new FeatureSupport
                {
                    Streaming = true,
                    Embeddings = false, // Gemini doesn't provide embeddings through this client
                    ImageGeneration = false, // Gemini doesn't provide image generation
                    VisionInput = isVisionCapable,
                    FunctionCalling = false, // Gemini doesn't support function calling through this client
                    AudioTranscription = false, // Gemini doesn't provide audio transcription
                    TextToSpeech = false // Gemini doesn't provide text-to-speech
                }
            });
        }

        /// <inheritdoc/>
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Gemini does not support embeddings through this client.");
        }

        /// <inheritdoc/>
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Gemini does not support image generation through this client.");
        }
    }
}