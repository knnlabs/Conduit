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

                // Only include quality and style for DALL-E 3
                var modelName = request.Model ?? ProviderModelId;
                var openAiRequest = modelName?.Contains("dall-e-3", StringComparison.OrdinalIgnoreCase) == true
                    ? new ImageGenerationRequest
                    {
                        Prompt = request.Prompt,
                        Model = request.Model ?? ProviderModelId,
                        N = request.N,
                        Size = request.Size ?? "1024x1024",
                        Quality = request.Quality,
                        Style = request.Style,
                        ResponseFormat = request.ResponseFormat ?? "url",
                        User = request.User
                    }
                    : new ImageGenerationRequest
                    {
                        Prompt = request.Prompt,
                        Model = request.Model ?? ProviderModelId,
                        N = request.N,
                        Size = request.Size ?? "1024x1024",
                        ResponseFormat = request.ResponseFormat ?? "url",
                        User = request.User
                    };

                var endpoint = GetImageGenerationEndpoint();

                Logger.LogInformation("Creating images using {Provider} at {Endpoint} with model {Model}, prompt: {Prompt}, size: {Size}, format: {Format}", 
                    ProviderName, endpoint, openAiRequest.Model, 
                    openAiRequest.Prompt?.Substring(0, Math.Min(50, openAiRequest.Prompt?.Length ?? 0)), 
                    openAiRequest.Size, openAiRequest.ResponseFormat);
                    
                // Log a warning about potential quota issues if using OpenAI
                if (ProviderName.Equals("openai", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning("Note: OpenAI image generation errors with null messages often indicate quota/billing issues");
                }

                var response = await CoreUtils.HttpClientHelper.SendJsonRequestAsync<ImageGenerationRequest, ImageGenerationResponse>(
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