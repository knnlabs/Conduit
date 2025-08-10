using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using CoreModels = ConduitLLM.Core.Models;
using CoreUtils = ConduitLLM.Core.Utilities;
using OpenAIModels = ConduitLLM.Providers.OpenAI;

namespace ConduitLLM.Providers.OpenAICompatible
{
    /// <summary>
    /// OpenAICompatibleClient partial class containing streaming functionality.
    /// </summary>
    public abstract partial class OpenAICompatibleClient
    {
        /// <summary>
        /// Streams a chat completion using the OpenAI-compatible API.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of chat completion chunks.</returns>
        /// <remarks>
        /// This implementation:
        /// <list type="bullet">
        /// <item>Validates the request for required parameters</item>
        /// <item>Maps the generic request to the OpenAI format, forcing the stream parameter to true</item>
        /// <item>Establishes a streaming connection to the provider's API</item>
        /// <item>Processes the server-sent events (SSE) format</item>
        /// <item>Maps each chunk back to the generic format</item>
        /// <item>Handles errors in a standardized way</item>
        /// </list>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with the provider.</exception>
        /// <exception cref="ConfigurationException">Thrown when there is a configuration error.</exception>
        public override async IAsyncEnumerable<CoreModels.ChatCompletionChunk> StreamChatCompletionAsync(
            CoreModels.ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletion");

            // Stream chunks progressively without buffering
            await foreach (var chunk in StreamChunksProgressivelyAsync(request, apiKey, cancellationToken).WithCancellation(cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                yield return chunk;
            }
        }

        /// <summary>
        /// Streams chunks progressively without buffering them into a list
        /// </summary>
        private async IAsyncEnumerable<CoreModels.ChatCompletionChunk> StreamChunksProgressivelyAsync(
            CoreModels.ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            HttpClient? client = null;
            HttpResponseMessage? response = null;
            
            try
            {
                client = CreateHttpClient(apiKey);
                var openAiRequest = PrepareStreamingRequest(request);
                var endpoint = GetChatCompletionEndpoint();

                Logger.LogDebug("Sending streaming chat completion request to {Provider} at {Endpoint}", ProviderName, endpoint);

                response = await SendStreamingRequestAsync(client, endpoint, openAiRequest, apiKey, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Process the error with enhanced error extraction
                var enhancedErrorMessage = ExtractEnhancedErrorMessage(ex);
                Logger.LogError(ex, "Error in streaming chat completion from {Provider}: {Message}", ProviderName, enhancedErrorMessage);

                var error = CoreUtils.ExceptionHandler.HandleLlmException(ex, Logger, ProviderName, request.Model ?? ProviderModelId);
                
                // Clean up resources
                response?.Dispose();
                client?.Dispose();
                
                throw error;
            }
            
            // If we get here, we have a response to stream
            if (response != null)
            {
                // Stream chunks progressively using StreamHelper
                await foreach (var chunk in CoreUtils.StreamHelper.ProcessSseStreamAsync<OpenAIModels.OpenAIChatCompletionChunk>(
                    response, Logger, DefaultJsonOptions, cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        response.Dispose();
                        client?.Dispose();
                        yield break;
                    }

                    yield return MapFromOpenAIChunk(chunk, request.Model);
                }
                
                // Clean up after successful streaming
                response.Dispose();
                client?.Dispose();
            }
        }

        /// <summary>
        /// Helper method to fetch all stream chunks without yielding in a try block
        /// </summary>
        private async Task<List<CoreModels.ChatCompletionChunk>> FetchStreamChunksAsync(
            CoreModels.ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            var chunks = new List<CoreModels.ChatCompletionChunk>();

            try
            {
                using var client = CreateHttpClient(apiKey);
                var openAiRequest = PrepareStreamingRequest(request);
                var endpoint = GetChatCompletionEndpoint();

                Logger.LogDebug("Sending streaming chat completion request to {Provider} at {Endpoint}", ProviderName, endpoint);

                var response = await SendStreamingRequestAsync(client, endpoint, openAiRequest, apiKey, cancellationToken);
                chunks = await ProcessStreamingResponseAsync(response, request.Model, cancellationToken);

                return chunks;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Process the error with enhanced error extraction
                var enhancedErrorMessage = ExtractEnhancedErrorMessage(ex);
                Logger.LogError(ex, "Error in streaming chat completion from {Provider}: {Message}", ProviderName, enhancedErrorMessage);

                var error = CoreUtils.ExceptionHandler.HandleLlmException(ex, Logger, ProviderName, request.Model ?? ProviderModelId);
                throw error;
            }
        }

        /// <summary>
        /// Prepares a request for streaming by ensuring the stream parameter is set to true
        /// </summary>
        /// <param name="request">The original chat completion request</param>
        /// <returns>A request object with stream=true set</returns>
        private object PrepareStreamingRequest(CoreModels.ChatCompletionRequest request)
        {
            var openAiRequest = MapToOpenAIRequest(request);

            // Force stream parameter to true based on the request's type
            if (openAiRequest is JsonElement jsonElement)
            {
                return ForceStreamParameterInJsonElement(jsonElement);
            }
            else if (openAiRequest is Dictionary<string, object> dictObj)
            {
                dictObj["stream"] = true;
                return dictObj;
            }
            else if (openAiRequest is OpenAIModels.OpenAIChatCompletionRequest reqObj)
            {
                reqObj = reqObj with { Stream = true };
                return reqObj;
            }

            // If we can't determine the type, return the original request
            return openAiRequest;
        }

        /// <summary>
        /// Forces the stream parameter to true in a JsonElement
        /// </summary>
        /// <param name="jsonElement">The JsonElement to modify</param>
        /// <returns>An object with stream=true set</returns>
        private object ForceStreamParameterInJsonElement(JsonElement jsonElement)
        {
            var jsonObject = jsonElement.GetRawText();
            var tempObj = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonObject, DefaultJsonOptions);
            if (tempObj != null)
            {
                tempObj["stream"] = true;
                return tempObj;
            }

            // If deserialization fails, return the original element
            return jsonElement;
        }

        /// <summary>
        /// Sends a streaming request to the specified endpoint
        /// </summary>
        /// <param name="client">The HTTP client to use</param>
        /// <param name="endpoint">The endpoint to send the request to</param>
        /// <param name="request">The request object</param>
        /// <param name="apiKey">Optional API key to override the one in credentials</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>The HTTP response message</returns>
        private async Task<HttpResponseMessage> SendStreamingRequestAsync(
            HttpClient client,
            string endpoint,
            object request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return await CoreUtils.HttpClientHelper.SendStreamingRequestAsync(
                client,
                HttpMethod.Post,
                endpoint,
                request,
                CreateStandardHeaders(apiKey),
                DefaultJsonOptions,
                Logger,
                cancellationToken);
        }

        /// <summary>
        /// Processes a streaming response and returns a list of chat completion chunks
        /// </summary>
        /// <param name="response">The HTTP response message</param>
        /// <param name="originalModelAlias">The original model alias from the request</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>A list of chat completion chunks</returns>
        private async Task<List<CoreModels.ChatCompletionChunk>> ProcessStreamingResponseAsync(
            HttpResponseMessage response,
            string? originalModelAlias,
            CancellationToken cancellationToken)
        {
            var chunks = new List<CoreModels.ChatCompletionChunk>();

            // Use StreamHelper to process the SSE stream
            await foreach (var chunk in CoreUtils.StreamHelper.ProcessSseStreamAsync<OpenAIModels.OpenAIChatCompletionChunk>(
                response, Logger, DefaultJsonOptions, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                chunks.Add(MapFromOpenAIChunk(chunk, originalModelAlias));
            }

            return chunks;
        }
    }
}