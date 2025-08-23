using ConduitLLM.Core.Exceptions;

using Microsoft.Extensions.Logging;
using CoreModels = ConduitLLM.Core.Models;
using CoreUtils = ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.OpenAI;

namespace ConduitLLM.Providers.OpenAICompatible
{
    /// <summary>
    /// OpenAICompatibleClient partial class containing chat completion functionality.
    /// </summary>
    public abstract partial class OpenAICompatibleClient
    {
        /// <summary>
        /// Creates a chat completion using the OpenAI-compatible API.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A chat completion response.</returns>
        /// <remarks>
        /// This implementation:
        /// <list type="bullet">
        /// <item>Validates the request for required parameters</item>
        /// <item>Maps the generic request to the OpenAI format</item>
        /// <item>Sends the request to the provider's API</item>
        /// <item>Maps the response back to the generic format</item>
        /// <item>Handles errors in a standardized way</item>
        /// </list>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with the provider.</exception>
        /// <exception cref="ConfigurationException">Thrown when there is a configuration error.</exception>
        public override async Task<CoreModels.ChatCompletionResponse> CreateChatCompletionAsync(
            CoreModels.ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "ChatCompletion");

            return await ExecuteApiRequestAsync(async () =>
            {
                using var client = CreateHttpClient(apiKey);
                var openAiRequest = MapToOpenAIRequest(request);

                var endpoint = GetChatCompletionEndpoint();

                Logger.LogDebug("Sending chat completion request to {Provider} at {Endpoint}", ProviderName, endpoint);

                // Use our common HTTP client helper to send the request
                var openAiResponse = await CoreUtils.HttpClientHelper.SendJsonRequestAsync<object, OpenAIChatCompletionResponse>(
                    client,
                    HttpMethod.Post,
                    endpoint,
                    openAiRequest,
                    CreateStandardHeaders(apiKey),
                    DefaultJsonOptions,
                    Logger,
                    cancellationToken);

                return MapFromOpenAIResponse(openAiResponse, request.Model);
            }, "ChatCompletion", cancellationToken);
        }
    }
}