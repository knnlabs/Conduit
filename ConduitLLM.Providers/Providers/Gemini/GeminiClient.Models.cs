using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers.Common.Models;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.Providers.Gemini.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Gemini
{
    /// <summary>
    /// GeminiClient partial class containing model listing functionality.
    /// </summary>
    public partial class GeminiClient
    {
        /// <inheritdoc/>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : PrimaryKeyCredential.ApiKey!;

            try
            {
                return await ExecuteApiRequestAsync(
                    async () =>
                    {
                        using var client = CreateHttpClient(effectiveApiKey);

                        // Construct endpoint URL with the effective API key
                        string endpoint = UrlBuilder.Combine(DefaultApiVersion, "models");
                        endpoint = UrlBuilder.AppendQueryString(endpoint, ("key", effectiveApiKey));
                        Logger.LogDebug("Sending request to list Gemini models from: {Endpoint}", endpoint);

                        var response = await client.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                            string errorMessage = $"Gemini API list models request failed with status code {response.StatusCode}.";

                            try
                            {
                                var errorResponse = JsonSerializer.Deserialize<GeminiErrorResponse>(errorContent);
                                if (errorResponse?.Error != null)
                                {
                                    errorMessage = $"Gemini API Error {errorResponse.Error.Code} ({errorResponse.Error.Status}): {errorResponse.Error.Message}";
                                    Logger.LogError("Gemini API list models request failed. Status: {StatusCode}, Error Status: {ErrorStatus}, Message: {ErrorMessage}",
                                        response.StatusCode, errorResponse.Error.Status, errorResponse.Error.Message);
                                }
                                else
                                {
                                    throw new JsonException("Failed to parse Gemini error response.");
                                }
                            }
                            catch (JsonException jsonEx)
                            {
                                Logger.LogError(jsonEx, "Gemini API list models request failed with status code {StatusCode}. Failed to parse error response body. Response: {ErrorContent}",
                                    response.StatusCode, errorContent);
                                errorMessage += $" Failed to parse error response: {errorContent}";
                            }

                            throw new LLMCommunicationException(errorMessage);
                        }

                        var modelListResponse = await response.Content.ReadFromJsonAsync<GeminiModelListResponse>(cancellationToken: cancellationToken)
                            .ConfigureAwait(false);

                        if (modelListResponse == null || modelListResponse.Models == null)
                        {
                            Logger.LogError("Failed to deserialize the successful model list response from Gemini API.");
                            throw new LLMCommunicationException("Failed to deserialize the model list response from Gemini API.");
                        }

                        // Filter for models that support 'generateContent' as we are focused on chat
                        var chatModels = modelListResponse.Models
                            .Where(m => m.SupportedGenerationMethods?.Contains("generateContent") ?? false)
                            .Select(m =>
                            {
                                // Check if this is a vision-capable model
                                bool isVisionCapable = IsVisionCapableModel(m.Id);

                                return ExtendedModelInfo.Create(m.Id, ProviderName, m.Id)
                                    .WithName(m.DisplayName ?? m.Id)
                                    .WithCapabilities(new ModelCapabilities
                                    {
                                        Chat = true,
                                        TextGeneration = true,
                                        Embeddings = false,
                                        ImageGeneration = false,
                                        Vision = isVisionCapable
                                    })
                                    .WithTokenLimits(new ModelTokenLimits
                                    {
                                        MaxInputTokens = m.InputTokenLimit,
                                        MaxOutputTokens = m.OutputTokenLimit
                                    });
                            })
                            .ToList();

                        Logger.LogInformation("Successfully retrieved {Count} chat-compatible models from Gemini.", chatModels.Count);
                        return chatModels;
                    },
                    "GetModelsAsync",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred while listing Gemini models");
                throw new LLMCommunicationException($"An unexpected error occurred while listing models: {ex.Message}", ex);
            }
        }
    }
}