using System;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Providers.Gemini.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Gemini
{
    /// <summary>
    /// GeminiClient partial class containing chat completion functionality.
    /// </summary>
    public partial class GeminiClient
    {
        /// <inheritdoc/>
        public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateChatCompletionAsync");

            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : PrimaryKeyCredential.ApiKey!;
            Logger.LogInformation("Mapping Core request to Gemini request for model alias '{ModelAlias}', provider model ID '{ProviderModelId}'",
                request.Model, ProviderModelId);

            var geminiRequest = MapToGeminiRequest(request);

            try
            {
                return await ExecuteApiRequestAsync(
                    async () =>
                    {
                        using var client = CreateHttpClient(effectiveApiKey);
                        var requestUri = $"{DefaultApiVersion}/models/{ProviderModelId}:generateContent?key={effectiveApiKey}";
                        Logger.LogDebug("Sending request to Gemini API: {Endpoint}", requestUri);

                        var response = await client.PostAsJsonAsync(requestUri, geminiRequest, cancellationToken)
                            .ConfigureAwait(false);

                        if (response.IsSuccessStatusCode)
                        {
                            var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                            if (geminiResponse == null)
                            {
                                Logger.LogError("Failed to deserialize Gemini response despite 200 OK status");
                                throw new LLMCommunicationException("Failed to deserialize the successful response from Gemini API.");
                            }

                            // Check for safety filtering response
                            CheckForSafetyBlocking(geminiResponse);

                            var coreResponse = MapToCoreResponse(geminiResponse, request.Model);
                            Logger.LogInformation("Successfully received and mapped Gemini response");
                            return coreResponse;
                        }
                        else
                        {
                            string errorContent = await ReadErrorContentAsync(response, cancellationToken)
                                .ConfigureAwait(false);

                            Logger.LogError("Gemini API request failed with status code {StatusCode}. Response: {ErrorContent}",
                                response.StatusCode, errorContent);

                            try
                            {
                                // Try to parse as Gemini error JSON
                                var errorDto = JsonSerializer.Deserialize<GeminiErrorResponse>(errorContent);
                                if (errorDto?.Error != null)
                                {
                                    string errorMessage = $"Gemini API Error {errorDto.Error.Code} ({errorDto.Error.Status}): {errorDto.Error.Message}";
                                    Logger.LogError(errorMessage);
                                    throw new LLMCommunicationException(errorMessage);
                                }
                            }
                            catch (JsonException)
                            {
                                // If it's not a parsable JSON or doesn't follow the expected format, use a generic message
                                Logger.LogWarning("Could not parse Gemini error response as JSON. Treating as plain text.");
                            }

                            throw new LLMCommunicationException(
                                $"Gemini API request failed with status code {response.StatusCode}. Response: {errorContent}");
                        }
                    },
                    "CreateChatCompletionAsync",
                    cancellationToken);
            }
            catch (LLMCommunicationException)
            {
                // Re-throw LLMCommunicationException directly
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred during Gemini API request");
                throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks for safety blocking in the Gemini response.
        /// </summary>
        private void CheckForSafetyBlocking(GeminiGenerateContentResponse response)
        {
            // Check if there are any candidates with safety blocking
            if (response.Candidates != null)
            {
                foreach (var candidate in response.Candidates)
                {
                    if (candidate.FinishReason == "SAFETY")
                    {
                        Logger.LogWarning("Content was blocked by Gemini safety filters");
                        throw new LLMCommunicationException("Content was blocked by Gemini safety filters. Please modify your request.");
                    }
                }
            }
        }
    }
}