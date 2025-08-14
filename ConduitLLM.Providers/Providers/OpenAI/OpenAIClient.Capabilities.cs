using System;
using System.Threading.Tasks;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.OpenAI
{
    /// <summary>
    /// OpenAIClient partial class containing capabilities functionality.
    /// </summary>
    public partial class OpenAIClient
    {
        /// <summary>
        /// Gets the capabilities for OpenAI models.
        /// </summary>
        public override Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            var model = modelId ?? ProviderModelId;
            var modelLower = model.ToLowerInvariant();
            
            var isGpt4Vision = modelLower.Contains("vision", StringComparison.OrdinalIgnoreCase) || 
                               modelLower.Contains("gpt-4o", StringComparison.OrdinalIgnoreCase) ||
                               modelLower.Contains("gpt-4-turbo", StringComparison.OrdinalIgnoreCase);
            var isDalleModel = modelLower.StartsWith("dall-e", StringComparison.OrdinalIgnoreCase);
            var isGpt4 = modelLower.Contains("gpt-4", StringComparison.OrdinalIgnoreCase);
            var isGpt35Turbo = modelLower.Contains("gpt-3.5-turbo", StringComparison.OrdinalIgnoreCase);
            var isChatModel = isGpt4 || isGpt35Turbo || modelLower.Contains("gpt", StringComparison.OrdinalIgnoreCase);
            var isEmbeddingModel = modelLower.Contains("embedding", StringComparison.OrdinalIgnoreCase);
            var isWhisperModel = modelLower.Contains("whisper", StringComparison.OrdinalIgnoreCase);
            var isTtsModel = modelLower.Contains("tts", StringComparison.OrdinalIgnoreCase);
            
            return Task.FromResult(new ProviderCapabilities
            {
                Provider = ProviderName,
                ModelId = model,
                ChatParameters = new ChatParameterSupport
                {
                    Temperature = isChatModel,
                    MaxTokens = isChatModel,
                    TopP = isChatModel,
                    TopK = false, // OpenAI doesn't support top-k
                    Stop = isChatModel,
                    PresencePenalty = isChatModel,
                    FrequencyPenalty = isChatModel,
                    LogitBias = isChatModel,
                    N = isChatModel,
                    User = isChatModel,
                    Seed = isChatModel,
                    ResponseFormat = isChatModel,
                    Tools = isGpt4 || isGpt35Turbo,
                    Constraints = new ParameterConstraints
                    {
                        TemperatureRange = new Range<double>(0.0, 2.0),
                        TopPRange = new Range<double>(0.0, 1.0),
                        MaxStopSequences = 4,
                        MaxTokenLimit = GetModelMaxTokens(model)
                    }
                },
                Features = new FeatureSupport
                {
                    Streaming = isChatModel,
                    Embeddings = isEmbeddingModel,
                    ImageGeneration = isDalleModel,
                    VisionInput = isGpt4Vision,
                    FunctionCalling = isGpt4 || isGpt35Turbo,
                    AudioTranscription = isWhisperModel,
                    TextToSpeech = isTtsModel
                }
            });
        }

        /// <summary>
        /// Gets the maximum token limit for a given model.
        /// </summary>
        private int GetModelMaxTokens(string model)
        {
            var modelLower = model.ToLowerInvariant();
            
            return modelLower switch
            {
                // GPT-4o models
                var m when m.Contains("gpt-4o") => 128000,
                
                // GPT-4 models
                var m when m.Contains("gpt-4-turbo") => 128000,
                var m when m.Contains("gpt-4") && m.Contains("32k") => 32768,
                var m when m.Contains("gpt-4") => 8192,
                
                // GPT-3.5 models
                var m when m.Contains("gpt-3.5-turbo") && m.Contains("16k") => 16384,
                var m when m.Contains("gpt-3.5-turbo") => 4096,
                
                // Legacy GPT-3 models
                var m when m.Contains("text-davinci") => 4097,
                var m when m.Contains("text-curie") => 2049,
                var m when m.Contains("text-babbage") => 2049,
                var m when m.Contains("text-ada") => 2049,
                
                // Default fallback
                _ => 4096
            };
        }
    }
}