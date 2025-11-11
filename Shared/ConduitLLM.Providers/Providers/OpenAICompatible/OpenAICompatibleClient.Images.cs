using Microsoft.Extensions.Logging;
using CoreModels = ConduitLLM.Core.Models;
using CoreUtils = ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.OpenAI;

namespace ConduitLLM.Providers.OpenAICompatible
{
    /// <summary>
    /// OpenAICompatibleClient partial class containing image generation functionality.
    /// </summary>
    public abstract partial class OpenAICompatibleClient
    {
        /// <summary>
        /// Creates images using the OpenAI-compatible API.
        /// </summary>
        /// <param name="request">The image generation request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An image generation response.</returns>
        /// <remarks>
        /// This implementation sends an image generation request to the provider's API and maps the
        /// response to the generic format. If a provider doesn't support image generation, this method
        /// should be overridden to throw a <see cref="NotSupportedException"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with the provider.</exception>
        /// <exception cref="NotSupportedException">Thrown when the provider doesn't support image generation.</exception>
        public override async Task<CoreModels.ImageGenerationResponse> CreateImageAsync(
            CoreModels.ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateImage");

            return await ExecuteApiRequestAsync(async () =>
            {
                using var client = CreateHttpClient(apiKey);

                // Create the request as a dictionary to support extension data
                var openAiRequest = new Dictionary<string, object?>
                {
                    ["prompt"] = request.Prompt,
                    ["model"] = request.Model ?? ProviderModelId,
                    ["n"] = request.N
                };

                // Add optional standard parameters
                if (!string.IsNullOrEmpty(request.Size))
                    openAiRequest["size"] = request.Size;
                if (!string.IsNullOrEmpty(request.ResponseFormat))
                    openAiRequest["response_format"] = request.ResponseFormat;
                if (!string.IsNullOrEmpty(request.User))
                    openAiRequest["user"] = request.User;

                // Only include quality and style for DALL-E 3
                var modelName = request.Model ?? ProviderModelId;
                if (modelName?.Contains("dall-e-3", StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (!string.IsNullOrEmpty(request.Quality))
                        openAiRequest["quality"] = request.Quality;
                    if (!string.IsNullOrEmpty(request.Style))
                        openAiRequest["style"] = request.Style;
                }

                // Handle edit/variation operations
                if (!string.IsNullOrEmpty(request.Image))
                    openAiRequest["image"] = request.Image;
                if (!string.IsNullOrEmpty(request.Mask))
                    openAiRequest["mask"] = request.Mask;

                // Pass through any extension data (model-specific parameters)
                if (request.ExtensionData != null)
                {
                    foreach (var kvp in request.ExtensionData)
                    {
                        // Don't override standard parameters
                        if (!openAiRequest.ContainsKey(kvp.Key))
                        {
                            openAiRequest[kvp.Key] = kvp.Value;
                        }
                    }
                }

                var endpoint = GetImageGenerationEndpoint();

                Logger.LogInformation("Creating images using {Provider} at {Endpoint} with model {Model}, prompt: {Prompt}, size: {Size}, format: {Format}", 
                    ProviderName, endpoint, openAiRequest["model"], 
                    request.Prompt?.Substring(0, Math.Min(50, request.Prompt?.Length ?? 0)), 
                    openAiRequest.GetValueOrDefault("size"), openAiRequest.GetValueOrDefault("response_format"));
                    
                // Log a warning about potential quota issues if using OpenAI
                if (ProviderName.Equals("openai", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning("Note: OpenAI image generation errors with null messages often indicate quota/billing issues");
                }

                var response = await CoreUtils.HttpClientHelper.SendJsonRequestAsync<Dictionary<string, object?>, ImageGenerationResponse>(
                    client,
                    HttpMethod.Post,
                    endpoint,
                    openAiRequest,
                    CreateStandardHeaders(apiKey),
                    DefaultJsonOptions,
                    Logger,
                    cancellationToken);

                return new CoreModels.ImageGenerationResponse
                {
                    Created = response.Created,
                    Data = response.Data?.Select(d => new CoreModels.ImageData
                    {
                        Url = d.Url,
                        B64Json = d.B64Json
                        // Note: Core.Models.ImageData doesn't have RevisedPrompt property
                    }).ToList() ?? new List<CoreModels.ImageData>()
                };
            }, "CreateImage", cancellationToken);
        }
    }
}