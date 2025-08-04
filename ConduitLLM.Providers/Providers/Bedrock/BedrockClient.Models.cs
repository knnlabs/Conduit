using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Common.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Bedrock
{
    /// <summary>
    /// BedrockClient partial class containing model management methods.
    /// </summary>
    public partial class BedrockClient
    {
        /// <inheritdoc />
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Getting models from AWS Bedrock");

            try
            {
                // In a real implementation, we would use AWS SDK to list available models
                // For now, return a static list of commonly available models

                await Task.Delay(1, cancellationToken); // Adding await to make this truly async

                return new List<ExtendedModelInfo>
                {
                    ExtendedModelInfo.Create("anthropic.claude-3-opus-20240229-v1:0", ProviderName, "anthropic.claude-3-opus-20240229-v1:0"),
                    ExtendedModelInfo.Create("anthropic.claude-3-sonnet-20240229-v1:0", ProviderName, "anthropic.claude-3-sonnet-20240229-v1:0"),
                    ExtendedModelInfo.Create("anthropic.claude-3-haiku-20240307-v1:0", ProviderName, "anthropic.claude-3-haiku-20240307-v1:0"),
                    ExtendedModelInfo.Create("anthropic.claude-v2", ProviderName, "anthropic.claude-v2"),
                    ExtendedModelInfo.Create("anthropic.claude-instant-v1", ProviderName, "anthropic.claude-instant-v1"),
                    ExtendedModelInfo.Create("amazon.titan-text-express-v1", ProviderName, "amazon.titan-text-express-v1"),
                    ExtendedModelInfo.Create("amazon.titan-text-lite-v1", ProviderName, "amazon.titan-text-lite-v1"),
                    ExtendedModelInfo.Create("amazon.titan-embed-text-v1", ProviderName, "amazon.titan-embed-text-v1"),
                    ExtendedModelInfo.Create("cohere.command-text-v14", ProviderName, "cohere.command-text-v14"),
                    ExtendedModelInfo.Create("cohere.command-light-text-v14", ProviderName, "cohere.command-light-text-v14"),
                    ExtendedModelInfo.Create("cohere.embed-english-v3", ProviderName, "cohere.embed-english-v3"),
                    ExtendedModelInfo.Create("cohere.embed-multilingual-v3", ProviderName, "cohere.embed-multilingual-v3"),
                    ExtendedModelInfo.Create("meta.llama2-13b-chat-v1", ProviderName, "meta.llama2-13b-chat-v1"),
                    ExtendedModelInfo.Create("meta.llama2-70b-chat-v1", ProviderName, "meta.llama2-70b-chat-v1"),
                    ExtendedModelInfo.Create("meta.llama3-8b-instruct-v1:0", ProviderName, "meta.llama3-8b-instruct-v1:0"),
                    ExtendedModelInfo.Create("meta.llama3-70b-instruct-v1:0", ProviderName, "meta.llama3-70b-instruct-v1:0"),
                    ExtendedModelInfo.Create("mistral.mistral-7b-instruct-v0:2", ProviderName, "mistral.mistral-7b-instruct-v0:2"),
                    ExtendedModelInfo.Create("mistral.mixtral-8x7b-instruct-v0:1", ProviderName, "mistral.mixtral-8x7b-instruct-v0:1"),
                    ExtendedModelInfo.Create("mistral.mistral-large-2402-v1:0", ProviderName, "mistral.mistral-large-2402-v1:0"),
                    ExtendedModelInfo.Create("ai21.j2-mid-v1", ProviderName, "ai21.j2-mid-v1"),
                    ExtendedModelInfo.Create("ai21.j2-ultra-v1", ProviderName, "ai21.j2-ultra-v1"),
                    ExtendedModelInfo.Create("stability.stable-diffusion-xl-v1", ProviderName, "stability.stable-diffusion-xl-v1")
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to retrieve models from Bedrock API.");
                throw;
            }
        }

        /// <inheritdoc />
        public override Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            return Task.FromResult(new ProviderCapabilities
            {
                Provider = ProviderName,
                ModelId = modelId ?? ProviderModelId,
                ChatParameters = new ChatParameterSupport
                {
                    Temperature = true,
                    MaxTokens = true,
                    TopP = true,
                    Stop = true
                },
                Features = new FeatureSupport
                {
                    Streaming = true,
                    Embeddings = true,
                    ImageGeneration = true,
                    FunctionCalling = false,
                    AudioTranscription = false,
                    TextToSpeech = false
                }
            });
        }
    }
}