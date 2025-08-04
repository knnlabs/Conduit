using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Providers.Bedrock.Models;

namespace ConduitLLM.Providers.Providers.Bedrock
{
    /// <summary>
    /// BedrockClient partial class containing image generation functionality.
    /// </summary>
    public partial class BedrockClient
    {
        /// <inheritdoc />
        public override async Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateImage");

            string modelId = request.Model ?? ProviderModelId;

            if (modelId.Contains("stability", StringComparison.OrdinalIgnoreCase))
            {
                return await CreateStabilityImageAsync(request, modelId, cancellationToken);
            }
            else
            {
                throw new UnsupportedProviderException($"The model {modelId} does not support image generation in Bedrock");
            }
        }

        /// <summary>
        /// Creates images using Stability AI models via Bedrock.
        /// </summary>
        private async Task<ImageGenerationResponse> CreateStabilityImageAsync(
            ImageGenerationRequest request, 
            string modelId, 
            CancellationToken cancellationToken)
        {
            // Parse size if provided
            int width = 512, height = 512;
            if (!string.IsNullOrEmpty(request.Size))
            {
                var sizeParts = request.Size.Split('x');
                if (sizeParts.Length == 2 && 
                    int.TryParse(sizeParts[0], out width) && 
                    int.TryParse(sizeParts[1], out height))
                {
                    // Valid size format like "1024x1024"
                }
                else
                {
                    // Default to common sizes based on string
                    (width, height) = request.Size?.ToLowerInvariant() switch
                    {
                        "1024x1024" => (1024, 1024),
                        "1152x896" => (1152, 896),
                        "1216x832" => (1216, 832),
                        "1344x768" => (1344, 768),
                        "1536x640" => (1536, 640),
                        "640x1536" => (640, 1536),
                        "768x1344" => (768, 1344),
                        "832x1216" => (832, 1216),
                        "896x1152" => (896, 1152),
                        _ => (512, 512)
                    };
                }
            }

            var stabilityRequest = new BedrockStabilityImageRequest
            {
                TextPrompts = new List<BedrockStabilityTextPrompt>
                {
                    new BedrockStabilityTextPrompt
                    {
                        Text = request.Prompt,
                        Weight = 1.0f
                    }
                },
                Width = width,
                Height = height,
                Samples = request.N,
                CfgScale = 7, // Default guidance scale
                Steps = 50, // Default number of steps
                Seed = Random.Shared.Next(),
                StylePreset = request.Style // Use style if provided
            };

            using var client = CreateHttpClient(PrimaryKeyCredential.ApiKey);
            string apiUrl = $"/model/{modelId}/invoke";

            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, stabilityRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var stabilityResponse = JsonSerializer.Deserialize<BedrockStabilityImageResponse>(responseContent, JsonOptions);
            
            if (stabilityResponse?.Artifacts == null || !stabilityResponse.Artifacts.Any())
            {
                throw new ConduitException("Invalid response from Stability AI model");
            }

            var imageObjects = stabilityResponse.Artifacts.Select((artifact, index) => 
            {
                if (string.IsNullOrEmpty(artifact.Base64))
                {
                    throw new ConduitException($"No image data received for artifact {index}");
                }

                return new ImageData
                {
                    B64Json = artifact.Base64
                };
            }).ToList();

            return new ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = imageObjects
            };
        }
    }
}