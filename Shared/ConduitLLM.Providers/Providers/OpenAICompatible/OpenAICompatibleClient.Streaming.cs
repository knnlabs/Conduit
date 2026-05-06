using System.Runtime.CompilerServices;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using CoreModels = ConduitLLM.Core.Models;
using CoreUtils = ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.OpenAI;

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
        /// Transforms the raw JSON of a streaming chunk before deserialization.
        /// Override in subclasses to perform provider-specific JSON transformations
        /// (e.g., extracting usage data from vendor-specific fields).
        /// </summary>
        /// <param name="chunk">The raw JSON element from the SSE stream.</param>
        /// <returns>The JSON string to deserialize into a ChatCompletionChunk.</returns>
        protected virtual string TransformChunkJson(JsonElement chunk)
            => chunk.GetRawText();

        /// <summary>
        /// Streams chunks progressively without buffering them into a list
        /// </summary>
        protected virtual async IAsyncEnumerable<CoreModels.ChatCompletionChunk> StreamChunksProgressivelyAsync(
            CoreModels.ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var logScope = BeginProviderLogScope("StreamChatCompletion");
            var instrumentation = BeginStreamingScope("StreamChatCompletion");
            HttpClient? client = null;
            HttpResponseMessage? response = null;

            try
            {
                try
                {
                    client = CreateHttpClient(apiKey);
                    var openAiRequest = PrepareStreamingRequest(request);
                    var endpoint = GetChatCompletionEndpoint();

                    Logger.LogDebug("Sending streaming chat completion request to {Provider} at {Endpoint}", ProviderName, endpoint);

                    response = await SendStreamingRequestAsync(client, endpoint, openAiRequest, apiKey, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    instrumentation.RecordFailure(nameof(OperationCanceledException));
                    response?.Dispose();
                    client?.Dispose();
                    throw;
                }
                catch (Exception ex)
                {
                    // Process the error with enhanced error extraction
                    var enhancedErrorMessage = ExtractEnhancedErrorMessage(ex);
                    Logger.LogError(ex, "Error in streaming chat completion from {Provider}: {Message}", ProviderName, enhancedErrorMessage);

                    var error = CoreUtils.ExceptionHandler.HandleLlmException(ex, Logger, ProviderName, request.Model ?? ProviderModelId);

                    instrumentation.RecordFailure(error.GetType().Name);

                    // Clean up resources
                    response?.Dispose();
                    client?.Dispose();

                    throw error;
                }

                // If we get here, we have a response to stream
                if (response != null)
                {
                    bool reportedUsage = false;
                    // Stream chunks progressively using StreamHelper - use JsonElement for raw passthrough
                    await foreach (var chunk in CoreUtils.StreamHelper.ProcessSseStreamAsync<System.Text.Json.JsonElement>(
                        response, Logger, DefaultJsonOptions, cancellationToken))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            yield break;
                        }

                        // Transform the raw JSON (allows subclasses to inject provider-specific processing)
                        var chunkJson = TransformChunkJson(chunk);
                        var mappedChunk = System.Text.Json.JsonSerializer.Deserialize<CoreModels.ChatCompletionChunk>(
                            chunkJson, DefaultJsonOptions);

                        if (mappedChunk != null)
                        {
                            // Preserve the original model alias if provided
                            if (!string.IsNullOrEmpty(request.Model))
                            {
                                mappedChunk.Model = request.Model;
                                mappedChunk.OriginalModelAlias = request.Model;
                            }

                            // Extract cached token counts from provider-specific extension data
                            ExtractCachedTokensFromExtensionData(mappedChunk.Usage);

                            instrumentation.RecordChunk();

                            // Many providers emit usage in a final chunk when stream_options.include_usage=true.
                            // Record once per stream so providers aren't double-counted.
                            if (!reportedUsage && mappedChunk.Usage != null)
                            {
                                RecordUsage(mappedChunk.Usage, "StreamChatCompletion");
                                reportedUsage = true;
                            }

                            yield return mappedChunk;
                        }
                    }
                }
            }
            finally
            {
                response?.Dispose();
                client?.Dispose();
                instrumentation.Dispose();
            }
        }

        /// <summary>
        /// Prepares a request for streaming by ensuring the stream parameter is set to true
        /// and stream_options includes usage data if not already set
        /// </summary>
        /// <param name="request">The original chat completion request</param>
        /// <returns>A request object with stream=true and stream_options configured</returns>
        private object PrepareStreamingRequest(CoreModels.ChatCompletionRequest request)
        {
            // Ensure stream_options is set to request usage data if not already configured
            // This is critical for accurate token counting and billing in streaming mode
            request.StreamOptions ??= new CoreModels.StreamOptions { IncludeUsage = true };

            var openAiRequest = MapToOpenAIRequest(request);

            // Force stream parameter to true based on the request's type
            if (openAiRequest is JsonElement jsonElement)
            {
                return ForceStreamParametersInJsonElement(jsonElement);
            }
            else if (openAiRequest is Dictionary<string, object> dictObj)
            {
                dictObj["stream"] = true;
                // Ensure stream_options is present
                if (!dictObj.ContainsKey("stream_options"))
                {
                    dictObj["stream_options"] = new { include_usage = true };
                }
                return dictObj;
            }
            else if (openAiRequest is OpenAIChatCompletionRequest reqObj)
            {
                reqObj = reqObj with { Stream = true };
                return reqObj;
            }

            // If we can't determine the type, return the original request
            return openAiRequest;
        }

        /// <summary>
        /// Forces the stream parameter to true and ensures stream_options is set in a JsonElement
        /// </summary>
        /// <param name="jsonElement">The JsonElement to modify</param>
        /// <returns>An object with stream=true and stream_options configured</returns>
        private object ForceStreamParametersInJsonElement(JsonElement jsonElement)
        {
            var jsonObject = jsonElement.GetRawText();
            var tempObj = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonObject, DefaultJsonOptions);
            if (tempObj != null)
            {
                tempObj["stream"] = true;
                // Ensure stream_options is present for usage data
                if (!tempObj.ContainsKey("stream_options"))
                {
                    tempObj["stream_options"] = new { include_usage = true };
                }
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

    }
}