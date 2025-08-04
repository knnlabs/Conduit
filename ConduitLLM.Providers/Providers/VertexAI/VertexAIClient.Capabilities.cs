using System;
using System.Threading.Tasks;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.Providers.VertexAI
{
    /// <summary>
    /// VertexAIClient partial class containing capabilities functionality.
    /// </summary>
    public partial class VertexAIClient
    {
        /// <inheritdoc />
        public override Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            var model = modelId ?? ProviderModelId;
            var (actualModelId, modelType) = GetVertexAIModelInfo(model);
            var isGeminiModel = modelType.Equals("gemini", StringComparison.OrdinalIgnoreCase);
            var isVisionCapable = actualModelId.Contains("1.5", StringComparison.OrdinalIgnoreCase) ||
                                  actualModelId.Contains("pro-vision", StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(new ProviderCapabilities
            {
                Provider = ProviderName,
                ModelId = model,
                ChatParameters = new ChatParameterSupport
                {
                    Temperature = true,
                    MaxTokens = true,
                    TopP = true,
                    TopK = true, // Vertex AI supports top-k
                    Stop = false, // Vertex AI doesn't support stop sequences
                    PresencePenalty = false, // Vertex AI doesn't support presence penalty
                    FrequencyPenalty = false, // Vertex AI doesn't support frequency penalty
                    LogitBias = false, // Vertex AI doesn't support logit bias
                    N = false, // Vertex AI doesn't support multiple choices
                    User = false, // Vertex AI doesn't support user parameter
                    Seed = false, // Vertex AI doesn't support seed
                    ResponseFormat = false, // Vertex AI doesn't support response format
                    Tools = false, // Vertex AI doesn't support tools through this client
                    Constraints = new ParameterConstraints
                    {
                        TemperatureRange = new Range<double>(0.0, 1.0),
                        TopPRange = new Range<double>(0.0, 1.0),
                        TopKRange = new Range<int>(1, 40),
                        MaxStopSequences = 0,
                        MaxTokenLimit = GetMaxTokens(actualModelId)
                    }
                },
                Features = new FeatureSupport
                {
                    Streaming = false, // Vertex AI simulates streaming
                    Embeddings = false, // Vertex AI doesn't provide embeddings through this client
                    ImageGeneration = false, // Vertex AI doesn't provide image generation
                    VisionInput = isVisionCapable,
                    FunctionCalling = false, // Vertex AI doesn't support function calling through this client
                    AudioTranscription = false, // Vertex AI doesn't provide audio transcription
                    TextToSpeech = false // Vertex AI doesn't provide text-to-speech
                }
            });
        }
    }
}