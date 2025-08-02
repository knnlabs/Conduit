using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
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
    /// AnthropicClient partial class containing streaming functionality.
    /// </summary>
    public partial class AnthropicClient
    {
        /// <summary>
        /// Streams a chat completion using the Anthropic API.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of chat completion chunks.</returns>
        /// <remarks>
        /// <para>
        /// This method sends a streaming request to the Anthropic API to generate a completion for
        /// a conversation, returning results as they become available. It follows these steps:
        /// </para>
        /// <list type="number">
        ///   <item><description>Validates the request for required parameters</description></item>
        ///   <item><description>Maps the generic request to Anthropic's format, enforcing stream=true</description></item>
        ///   <item><description>Establishes a streaming connection to Anthropic's messages endpoint</description></item>
        ///   <item><description>Processes the SSE (Server-Sent Events) stream using StreamHelper</description></item>
        ///   <item><description>Maps each streaming event to a chat completion chunk</description></item>
        ///   <item><description>Yields each chunk as it becomes available</description></item>
        ///   <item><description>Creates a final chunk with finish_reason when the message_stop event is received</description></item>
        /// </list>
        /// <para>
        /// Anthropic's streaming format differs from the OpenAI standard. This method handles
        /// the conversion between formats, focusing on content_block_delta events for content
        /// and message_stop events for completion status.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with Anthropic.</exception>
        /// <exception cref="ConfigurationException">Thrown when there is a configuration error.</exception>
        public override async IAsyncEnumerable<CoreModels.ChatCompletionChunk> StreamChatCompletionAsync(
            CoreModels.ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletion");

            HttpResponseMessage? response = null;

            try
            {
                using var client = CreateHttpClient(apiKey);
                // Create a new request with Stream explicitly set to true
                var baseRequest = MapToAnthropicRequest(request);
                var anthropicRequest = new AnthropicMessageRequest
                {
                    Model = baseRequest.Model,
                    Messages = baseRequest.Messages,
                    MaxTokens = baseRequest.MaxTokens,
                    SystemPrompt = baseRequest.SystemPrompt,
                    Temperature = baseRequest.Temperature,
                    TopP = baseRequest.TopP,
                    TopK = baseRequest.TopK,
                    StopSequences = baseRequest.StopSequences,
                    Stream = true
                };

                response = await HttpClientHelper.SendStreamingRequestAsync(
                    client,
                    HttpMethod.Post,
                    Constants.Endpoints.Messages,
                    anthropicRequest,
                    null,
                    DefaultJsonOptions,
                    Logger,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var enhancedErrorMessage = ExtractEnhancedErrorMessage(ex);
                Logger.LogError(ex, "Error streaming chat completion from Anthropic: {ErrorMessage}", enhancedErrorMessage);
                throw new LLMCommunicationException(enhancedErrorMessage, ex);
            }

            // Process the stream outside of the try/catch block to avoid yielding in try
            if (response != null)
            {
                await foreach (var chunk in ProcessAnthropicStreamAsync(response, request.Model, cancellationToken))
                {
                    yield return chunk;
                }
            }
        }

        /// <summary>
        /// Processes the streaming response from Anthropic.
        /// </summary>
        /// <param name="response">The HTTP response containing the stream.</param>
        /// <param name="modelId">The model identifier.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of chat completion chunks.</returns>
        private async IAsyncEnumerable<CoreModels.ChatCompletionChunk> ProcessAnthropicStreamAsync(
            HttpResponseMessage response,
            string modelId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Create a wrapped enumerator to handle errors outside the yielding loop
            IAsyncEnumerable<AnthropicMessageStreamEvent> streamEvents;

            try
            {
                // Get the stream of events but don't start consuming it yet
                streamEvents = StreamHelper.ProcessSseStreamAsync<AnthropicMessageStreamEvent>(
                    response, Logger, DefaultJsonOptions, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var enhancedErrorMessage = ExtractEnhancedErrorMessage(ex);
                Logger.LogError(ex, "Error initializing Anthropic stream: {ErrorMessage}", enhancedErrorMessage);
                throw new LLMCommunicationException(enhancedErrorMessage, ex);
            }

            // Process the events outside the try/catch block
            await foreach (var chunk in streamEvents.WithCancellation(cancellationToken))
            {
                // Only process content blocks, ignore other event types
                if (chunk.Type == Constants.StreamEvents.ContentBlockDelta)
                {
                    // Map Anthropic stream event to chat completion chunk
                    var deltaContent = chunk.Delta?.Text ?? "";
                    var index = chunk.Index;

                    yield return new CoreModels.ChatCompletionChunk
                    {
                        // Generate a new ID since AnthropicMessageStreamEvent doesn't have a Message.Id property
                        Id = $"chatcmpl-{Guid.NewGuid():N}",
                        Object = "chat.completion.chunk",
                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Model = modelId,
                        Choices = new List<CoreModels.StreamingChoice>
                        {
                            new CoreModels.StreamingChoice
                            {
                                Index = 0,
                                Delta = new CoreModels.DeltaContent
                                {
                                    Role = index == 0 ? "assistant" : null, // Only include role in first chunk
                                    Content = deltaContent
                                },
                                FinishReason = null // Will be set in the final chunk
                            }
                        },
                        OriginalModelAlias = modelId
                    };
                }
                else if (chunk.Type == Constants.StreamEvents.MessageStop)
                {
                    // We've reached the end of the response
                    // Return a final chunk with finish_reason
                    yield return new CoreModels.ChatCompletionChunk
                    {
                        Id = $"chatcmpl-{Guid.NewGuid():N}",
                        Object = "chat.completion.chunk",
                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Model = modelId,
                        Choices = new List<CoreModels.StreamingChoice>
                        {
                            new CoreModels.StreamingChoice
                            {
                                Index = 0,
                                Delta = new CoreModels.DeltaContent(),
                                FinishReason = "stop"
                            }
                        },
                        OriginalModelAlias = modelId
                    };
                }
            }
        }

        /// <summary>
        /// Helper method to create a final stream chunk with a finish reason.
        /// </summary>
        /// <param name="modelId">The model identifier.</param>
        /// <param name="finishReason">The reason the generation finished.</param>
        /// <returns>A chat completion chunk with the finish reason.</returns>
        private CoreModels.ChatCompletionChunk CreateFinalStreamChunk(string modelId, string finishReason)
        {
            return new CoreModels.ChatCompletionChunk
            {
                Id = $"chatcmpl-{Guid.NewGuid():N}",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<CoreModels.StreamingChoice>
                {
                    new CoreModels.StreamingChoice
                    {
                        Index = 0,
                        FinishReason = finishReason,
                        Delta = new CoreModels.DeltaContent()
                    }
                }
            };
        }

        /// <summary>
        /// Maps an Anthropic stream event to a chat completion chunk.
        /// </summary>
        /// <param name="chunk">The Anthropic message stream event.</param>
        /// <param name="modelId">The model identifier.</param>
        /// <returns>A chat completion chunk.</returns>
        private CoreModels.ChatCompletionChunk MapFromAnthropicStreamEvent(AnthropicMessageStreamEvent chunk, string modelId)
        {
            return new CoreModels.ChatCompletionChunk
            {
                Id = $"chatcmpl-{Guid.NewGuid():N}",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<CoreModels.StreamingChoice>
                {
                    new CoreModels.StreamingChoice
                    {
                        Index = 0,
                        Delta = new CoreModels.DeltaContent
                        {
                            Content = chunk.Delta?.Text,
                            Role = "assistant"
                        }
                    }
                }
            };
        }
    }
}