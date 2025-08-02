using System;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Providers.Anthropic.Models;

using Microsoft.Extensions.Logging;
using CoreModels = ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.Providers.Anthropic
{
    /// <summary>
    /// AnthropicClient partial class containing chat completion functionality.
    /// </summary>
    public partial class AnthropicClient
    {
        /// <summary>
        /// Creates a chat completion using the Anthropic API.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A chat completion response.</returns>
        /// <remarks>
        /// <para>
        /// This method sends a request to the Anthropic API to generate a completions for
        /// a conversation. It follows these steps:
        /// </para>
        /// <list type="number">
        ///   <item><description>Validates the request for required parameters</description></item>
        ///   <item><description>Maps the generic request to Anthropic's format</description></item>
        ///   <item><description>Sends the request to Anthropic's messages endpoint</description></item>
        ///   <item><description>Maps the response back to the generic format</description></item>
        /// </list>
        /// <para>
        /// The implementation uses the ExecuteApiRequestAsync helper method to provide
        /// standardized error handling and retry logic.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with Anthropic.</exception>
        /// <exception cref="ConfigurationException">Thrown when there is a configuration error.</exception>
        public override async Task<CoreModels.ChatCompletionResponse> CreateChatCompletionAsync(
            CoreModels.ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "ChatCompletion");

            try
            {
                return await ExecuteApiRequestAsync(async () =>
                {
                    using var client = CreateHttpClient(apiKey);
                    var anthropicRequest = MapToAnthropicRequest(request);

                    var response = await HttpClientHelper.SendJsonRequestAsync<AnthropicMessageRequest, AnthropicMessageResponse>(
                        client,
                        System.Net.Http.HttpMethod.Post,
                        Constants.Endpoints.Messages,
                        anthropicRequest,
                        null,
                        DefaultJsonOptions,
                        Logger,
                        cancellationToken);

                    return MapFromAnthropicResponse(response, request.Model);
                }, "ChatCompletion", cancellationToken);
            }
            catch (LLMCommunicationException ex)
            {
                // Enhance error message handling for Anthropic and re-throw
                var enhancedErrorMessage = ExtractEnhancedErrorMessage(ex);
                throw new LLMCommunicationException(enhancedErrorMessage, ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Handle other exceptions not caught by the base method
                var errorMessage = ExtractEnhancedErrorMessage(ex);
                Logger.LogError(ex, "Anthropic API error: {Message}", errorMessage);
                throw new LLMCommunicationException(errorMessage, ex);
            }
        }
    }
}