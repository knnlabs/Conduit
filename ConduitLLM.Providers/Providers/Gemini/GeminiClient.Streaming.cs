using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.Providers.Gemini.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Gemini
{
    /// <summary>
    /// GeminiClient partial class containing streaming functionality.
    /// </summary>
    public partial class GeminiClient
    {
        /// <inheritdoc/>
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletionAsync");

            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : PrimaryKeyCredential.ApiKey!;

            Logger.LogInformation("Preparing streaming request to Gemini for model alias '{ModelAlias}', provider model ID '{ProviderModelId}'",
                request.Model, ProviderModelId);

            // Create Gemini request
            var geminiRequest = MapToGeminiRequest(request);

            // Store original model alias for response mapping
            string originalModelAlias = request.Model;

            // Setup and send initial request
            HttpResponseMessage? response = null;

            try
            {
                response = await SetupAndSendStreamingRequestAsync(geminiRequest, effectiveApiKey, cancellationToken).ConfigureAwait(false);
            }
            catch (LLMCommunicationException)
            {
                // If it's already a properly formatted LLMCommunicationException, just re-throw it
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to initiate Gemini streaming request");
                throw new LLMCommunicationException($"Failed to initiate Gemini stream: {ex.Message}", ex);
            }

            // Process the stream
            try
            {
                await foreach (var chunk in ProcessGeminiStreamAsync(response, originalModelAlias, cancellationToken).ConfigureAwait(false))
                {
                    yield return chunk;
                }
            }
            finally
            {
                response?.Dispose();
            }
        }

        /// <summary>
        /// Sets up and sends the streaming request to Gemini API.
        /// </summary>
        private async Task<HttpResponseMessage> SetupAndSendStreamingRequestAsync(
            GeminiGenerateContentRequest geminiRequest,
            string effectiveApiKey,
            CancellationToken cancellationToken)
        {
            // Add streaming parameter to the URL
            var endpoint = UrlBuilder.Combine(DefaultApiVersion, "models", ProviderModelId + ":streamGenerateContent");
            endpoint = UrlBuilder.AppendQueryString(endpoint, ("key", effectiveApiKey), ("alt", "sse"));
            Logger.LogDebug("Sending streaming request to Gemini API: {Endpoint}", endpoint);

            using var client = CreateHttpClient(effectiveApiKey);

            // Create request message
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(geminiRequest)
            };

            // Send request with ResponseHeadersRead to get streaming
            var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                Logger.LogError("Gemini streaming request failed with status {Status}. Response: {ErrorContent}",
                    response.StatusCode, errorContent);

                try
                {
                    // Try to parse as Gemini error JSON
                    var errorDto = JsonSerializer.Deserialize<GeminiErrorResponse>(errorContent);
                    if (errorDto?.Error != null)
                    {
                        // Direct error format used by the test
                        throw new LLMCommunicationException($"Gemini API Error {errorDto.Error.Code} ({errorDto.Error.Status}): {errorDto.Error.Message}");
                    }
                }
                catch (JsonException)
                {
                    // Not JSON, fall through to default message
                }

                throw new LLMCommunicationException($"Gemini API request failed with status code {response.StatusCode}. Response: {errorContent}");
            }

            return response;
        }

        /// <summary>
        /// Processes the Gemini streaming response.
        /// </summary>
        private async IAsyncEnumerable<ChatCompletionChunk> ProcessGeminiStreamAsync(
            HttpResponseMessage response,
            string originalModelAlias,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Stream? responseStream = null;
            StreamReader? reader = null;

            try
            {
                responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                reader = new StreamReader(responseStream, Encoding.UTF8);

                // Process the stream line by line (expecting SSE format due to alt=sse)
                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:"))
                    {
                        // Skip empty lines or non-data lines if using SSE format
                        continue;
                    }

                    string jsonData = line.Substring("data:".Length).Trim();

                    if (string.IsNullOrWhiteSpace(jsonData))
                    {
                        continue; // Skip if data part is empty
                    }

                    ChatCompletionChunk? chunk = null;
                    try
                    {
                        var geminiResponse = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(jsonData);
                        if (geminiResponse != null)
                        {
                            // Check for safety blocking in streaming context
                            CheckForSafetyBlocking(geminiResponse);

                            // Map the Gemini response (which contains the delta) to our core chunk
                            chunk = MapToCoreChunk(geminiResponse, originalModelAlias);
                        }
                        else
                        {
                            Logger.LogWarning("Deserialized Gemini stream chunk was null. JSON: {JsonData}", jsonData);
                        }
                    }
                    catch (JsonException ex)
                    {
                        Logger.LogError(ex, "JSON deserialization error processing Gemini stream chunk. JSON: {JsonData}", jsonData);
                        // Throw to indicate stream corruption
                        throw new LLMCommunicationException($"Error deserializing Gemini stream chunk: {ex.Message}. Data: {jsonData}", ex);
                    }
                    catch (LLMCommunicationException llmEx) // Catch mapping/validation errors
                    {
                        Logger.LogError(llmEx, "Error processing Gemini stream chunk content. JSON: {JsonData}", jsonData);
                        throw; // Re-throw specific communication errors
                    }
                    catch (Exception ex) // Catch unexpected mapping errors
                    {
                        Logger.LogError(ex, "Unexpected error mapping Gemini stream chunk. JSON: {JsonData}", jsonData);
                        throw new LLMCommunicationException($"Unexpected error mapping Gemini stream chunk: {ex.Message}. Data: {jsonData}", ex);
                    }

                    if (chunk != null) // MapToCoreChunk might return null if there's no usable delta
                    {
                        yield return chunk;
                    }
                }

                Logger.LogInformation("Finished processing Gemini stream.");
            }
            finally
            {
                // Ensure resources are disposed even if exceptions occur during processing
                reader?.Dispose();
                responseStream?.Dispose();
                Logger.LogDebug("Disposed Gemini stream resources.");
            }
        }

    }
}