using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Groq
{
    /// <summary>
    /// GroqClient partial class containing chat completion methods.
    /// </summary>
    public partial class GroqClient
    {
        /// <summary>
        /// Creates a chat completion with enhanced error handling specific to Groq.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A chat completion response from Groq.</returns>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with Groq.</exception>
        public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await base.CreateChatCompletionAsync(request, apiKey, cancellationToken);
            }
            catch (LLMCommunicationException ex)
            {
                // Enhance error message handling for Groq and re-throw
                var enhancedErrorMessage = ExtractEnhancedErrorMessage(ex);
                throw new LLMCommunicationException(enhancedErrorMessage, ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Handle other exceptions not caught by the base class
                var errorMessage = ex.Message;
                if (ex is HttpRequestException httpEx && httpEx.Data["Body"] is string body)
                {
                    errorMessage = body;
                }

                Logger.LogError(ex, "Groq API error: {Message}", errorMessage);
                throw new LLMCommunicationException($"Groq API error: {errorMessage}", ex);
            }
        }

        /// <summary>
        /// Streams a chat completion with enhanced error handling specific to Groq.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of chat completion chunks.</returns>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with Groq.</exception>
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Create a wrapped stream to avoid yielding in try/catch
            IAsyncEnumerable<ChatCompletionChunk> baseStream;

            try
            {
                // Get the base implementation's stream
                baseStream = base.StreamChatCompletionAsync(request, apiKey, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Enhance error message handling for Groq
                var enhancedErrorMessage = ExtractEnhancedErrorMessage(ex);
                Logger.LogError(ex, "Error initializing streaming chat completion from Groq: {Message}", enhancedErrorMessage);
                throw new LLMCommunicationException(enhancedErrorMessage, ex);
            }

            // Process the stream outside of try/catch
            await foreach (var chunk in baseStream.WithCancellation(cancellationToken))
            {
                yield return chunk;
            }
        }
    }
}
