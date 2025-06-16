using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.InternalModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Revised client for interacting with the Google Gemini API using the new client hierarchy.
    /// Provides standardized handling of API requests and responses with enhanced error handling.
    /// </summary>
    public class GeminiClient : CustomProviderClient
    {
        // Gemini-specific constants
        private const string DefaultApiBase = "https://generativelanguage.googleapis.com/";
        private const string DefaultApiVersion = "v1beta";

        private readonly string _apiVersion;

        /// <summary>
        /// Initializes a new instance of the <see cref="GeminiClient"/> class.
        /// </summary>
        /// <param name="credentials">The credentials for accessing the Gemini API.</param>
        /// <param name="providerModelId">The provider model ID to use (e.g., gemini-1.5-flash-latest).</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory for advanced usage scenarios.</param>
        /// <param name="apiVersion">The API version to use. Defaults to v1beta.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public GeminiClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger<GeminiClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            string? apiVersion = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                credentials,
                providerModelId,
                logger,
                httpClientFactory,
                "Gemini",
                string.IsNullOrWhiteSpace(credentials.ApiBase) ? DefaultApiBase : credentials.ApiBase,
                defaultModels)
        {
            _apiVersion = apiVersion ?? DefaultApiVersion;
        }

        /// <inheritdoc/>
        protected override void ValidateCredentials()
        {
            base.ValidateCredentials();

            if (string.IsNullOrWhiteSpace(Credentials.ApiKey))
            {
                throw new ConfigurationException($"API key is missing for provider '{ProviderName}'.");
            }
        }

        /// <summary>
        /// Validates that the request is compatible with the selected model,
        /// particularly checking if multimodal content is being sent to a text-only model.
        /// </summary>
        /// <param name="request">The request to validate</param>
        /// <param name="methodName">Name of the calling method for logging</param>
        protected override void ValidateRequest<TRequest>(TRequest request, string methodName)
        {
            base.ValidateRequest(request, methodName);

            // Only apply vision validation for chat completion requests
            if (request is ChatCompletionRequest chatRequest)
            {
                // Check if we're sending multimodal content to a non-vision model
                bool containsImages = false;

                foreach (var message in chatRequest.Messages)
                {
                    if (message.Content != null && message.Content is not string)
                    {
                        // If content is not a string, assume it might contain images
                        containsImages = true;
                        break;
                    }
                }

                if (containsImages && !IsVisionCapableModel(ProviderModelId))
                {
                    Logger.LogWarning(
                        "Multimodal content detected but model '{ProviderModelId}' does not support vision capabilities.",
                        ProviderModelId);
                    throw new ValidationException(
                        $"Cannot send image content to model '{ProviderModelId}' as it does not support vision capabilities. " +
                        $"Please use a vision-capable model such as 'gemini-pro-vision' or 'gemini-1.5-pro'.");
                }
            }
        }

        /// <inheritdoc/>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            // Don't call base to avoid setting Bearer authorization as Gemini uses query param authentication
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");

            // For Gemini, the API key is added to the query string in each request, not in the headers

            // Set the base URL with no trailing slash
            if (client.BaseAddress == null && !string.IsNullOrEmpty(BaseUrl))
            {
                client.BaseAddress = new Uri(BaseUrl.TrimEnd('/'));
            }
        }

        /// <inheritdoc/>
        public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateChatCompletionAsync");

            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : Credentials.ApiKey!;
            Logger.LogInformation("Mapping Core request to Gemini request for model alias '{ModelAlias}', provider model ID '{ProviderModelId}'",
                request.Model, ProviderModelId);

            var geminiRequest = MapToGeminiRequest(request);

            try
            {
                return await ExecuteApiRequestAsync(
                    async () =>
                    {
                        using var client = CreateHttpClient(effectiveApiKey);
                        var requestUri = $"{_apiVersion}/models/{ProviderModelId}:generateContent?key={effectiveApiKey}";
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

        /// <inheritdoc/>
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletionAsync");

            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : Credentials.ApiKey!;

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

        /// <inheritdoc/>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : Credentials.ApiKey!;

            try
            {
                return await ExecuteApiRequestAsync(
                    async () =>
                    {
                        using var client = CreateHttpClient(effectiveApiKey);

                        // Construct endpoint URL with the effective API key
                        string endpoint = $"{_apiVersion}/models?key={effectiveApiKey}";
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

                                return InternalModels.ExtendedModelInfo.Create(m.Id, ProviderName, m.Id)
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

                        Logger.LogInformation($"Successfully retrieved {chatModels.Count} chat-compatible models from Gemini.");
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

        /// <inheritdoc/>
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<EmbeddingResponse>(
                new NotSupportedException("Embeddings are not supported by GeminiClient."));
        }

        /// <inheritdoc/>
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<ImageGenerationResponse>(
                new NotSupportedException("Image generation is not supported by GeminiClient."));
        }

        #region Helper Methods

        private async Task<HttpResponseMessage> SetupAndSendStreamingRequestAsync(
            GeminiGenerateContentRequest geminiRequest,
            string effectiveApiKey,
            CancellationToken cancellationToken)
        {
            // Add streaming parameter to the URL
            var endpoint = $"{_apiVersion}/models/{ProviderModelId}:streamGenerateContent?key={effectiveApiKey}&alt=sse";
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

        private void CheckForSafetyBlocking(GeminiGenerateContentResponse geminiResponse)
        {
            // Check for prompt feedback block
            if (geminiResponse.PromptFeedback?.BlockReason != null)
            {
                string blockDetails = string.Join(", ", geminiResponse.PromptFeedback.SafetyRatings?
                    .Where(r => r != null && r.Probability != "NEGLIGIBLE")
                    .Select(r => $"{r.Category}: {r.Probability}") ?? Array.Empty<string>());

                string blockReason = geminiResponse.PromptFeedback.BlockReason;

                Logger.LogWarning("Gemini response blocked due to prompt feedback. Reason: {BlockReason}, Details: {Details}",
                    blockReason, string.IsNullOrEmpty(blockDetails) ? "No details provided" : blockDetails);
                throw new LLMCommunicationException($"Gemini response blocked due to safety settings: {blockReason}. {blockDetails}");
            }

            // Check for safety filtering in candidates
            if (geminiResponse.Candidates != null &&
                geminiResponse.Candidates.Count > 0 &&
                geminiResponse.Candidates[0].FinishReason == "SAFETY" &&
                geminiResponse.Candidates[0].SafetyRatings != null)
            {
                string safetyDetails = string.Join(", ", geminiResponse.Candidates[0].SafetyRatings?
                    .Where(r => r != null && r.Probability != "NEGLIGIBLE")
                    .Select(r => $"{r.Category}: {r.Probability}") ?? Array.Empty<string>());

                Logger.LogWarning("Gemini response blocked due to safety settings: {Details}", safetyDetails);
                throw new LLMCommunicationException($"Gemini response blocked due to safety settings: {safetyDetails}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Determines if a Gemini model is capable of processing image inputs.
        /// </summary>
        /// <param name="modelId">The Gemini model ID</param>
        /// <returns>True if the model supports vision capabilities</returns>
        private bool IsVisionCapableModel(string modelId)
        {
            // Check for vision-capable models based on naming patterns
            // Gemini 1.5 and above support vision input
            return modelId.Contains("gemini-1.5", StringComparison.OrdinalIgnoreCase) ||
                   // The original Gemini Pro Vision model
                   modelId.Contains("gemini-pro-vision", StringComparison.OrdinalIgnoreCase) ||
                   // Future-proof for Gemini 2.0+ models (assuming they will be multimodal)
                   modelId.Contains("gemini-2", StringComparison.OrdinalIgnoreCase) ||
                   modelId.Contains("gemini-3", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Mapping Methods

        private GeminiGenerateContentRequest MapToGeminiRequest(ChatCompletionRequest coreRequest)
        {
            var contents = new List<GeminiContent>();

            // Extract system message if present
            var systemMessage = coreRequest.Messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
            if (systemMessage != null)
            {
                // Create a special system message
                contents.Add(new GeminiContent
                {
                    Role = "user", // Gemini doesn't have a system role, use user
                    Parts = MapToGeminiParts(systemMessage.Content)
                });
            }

            // Process user/assistant messages
            foreach (var message in coreRequest.Messages.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
            {
                string role = message.Role.ToLowerInvariant() switch
                {
                    "user" => "user",
                    "assistant" => "model",
                    _ => string.Empty
                };

                if (string.IsNullOrEmpty(role))
                {
                    Logger.LogWarning("Unsupported message role '{Role}' encountered for Gemini provider. Skipping message.", message.Role);
                    continue;
                }

                contents.Add(new GeminiContent
                {
                    Role = role,
                    Parts = MapToGeminiParts(message.Content)
                });
            }

            return new GeminiGenerateContentRequest
            {
                Contents = contents,
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = (float?)coreRequest.Temperature,
                    TopP = (float?)coreRequest.TopP,
                    // TopK = coreRequest.TopK, // Map if added to Core model
                    CandidateCount = coreRequest.N, // Map N to candidateCount
                    MaxOutputTokens = coreRequest.MaxTokens,
                    StopSequences = coreRequest.Stop
                }
            };
        }

        /// <summary>
        /// Maps content from the core model format to Gemini's part format, handling both text and images.
        /// </summary>
        /// <param name="content">The content to map, can be string, list of content parts, or JSON</param>
        /// <returns>A list of Gemini content parts</returns>
        private List<GeminiPart> MapToGeminiParts(object? content)
        {
            var parts = new List<GeminiPart>();

            // Handle simple text content
            if (content is string textContent)
            {
                parts.Add(new GeminiPart { Text = textContent });
                return parts;
            }

            // Check if we have multimodal content
            if (ContentHelper.IsTextOnly(content))
            {
                // For text-only content, just extract as string
                string text = ContentHelper.GetContentAsString(content);
                if (!string.IsNullOrEmpty(text))
                {
                    parts.Add(new GeminiPart { Text = text });
                }
                return parts;
            }

            // Handle multimodal content (text + images)
            var textParts = ContentHelper.ExtractMultimodalContent(content);
            var imageUrls = ContentHelper.ExtractImageUrls(content);

            // Add text parts
            if (textParts.Any())
            {
                // Join all text parts with newlines to preserve formatting
                string combinedText = string.Join(Environment.NewLine, textParts);
                parts.Add(new GeminiPart { Text = combinedText });
            }

            // Add image parts
            foreach (var imageUrl in imageUrls)
            {
                // For Gemini, we need to handle image data encoding
                try
                {
                    if (imageUrl.IsBase64DataUrl)
                    {
                        // Already have base64 data in the URL, extract and use it
                        string mimeType = imageUrl.MimeType ?? "image/jpeg";
                        string base64Data = imageUrl.Base64Data ?? string.Empty;

                        if (!string.IsNullOrEmpty(base64Data))
                        {
                            parts.Add(new GeminiPart
                            {
                                InlineData = new GeminiInlineData
                                {
                                    MimeType = mimeType,
                                    Data = base64Data
                                }
                            });
                        }
                    }
                    else
                    {
                        // For regular URLs, we need to download the image and convert to base64
                        var imageData = ImageUtility.DownloadImageAsync(imageUrl.Url).GetAwaiter().GetResult();
                        if (imageData != null && imageData.Length > 0)
                        {
                            // Detect mime type from image data
                            string? mimeType = ImageUtility.DetectMimeType(imageData) ?? "image/jpeg";
                            string base64Data = Convert.ToBase64String(imageData);

                            parts.Add(new GeminiPart
                            {
                                InlineData = new GeminiInlineData
                                {
                                    MimeType = mimeType,
                                    Data = base64Data
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to process image URL '{Url}' for Gemini request", imageUrl.Url);
                    // Continue with other content if an image fails
                }
            }

            return parts;
        }

        private ChatCompletionResponse MapToCoreResponse(GeminiGenerateContentResponse geminiResponse, string originalModelAlias)
        {
            if (geminiResponse.Candidates == null || geminiResponse.Candidates.Count == 0)
            {
                throw new LLMCommunicationException("Gemini response contains no candidates");
            }

            var firstCandidate = geminiResponse.Candidates[0];

            if (firstCandidate.Content == null || firstCandidate.Content.Parts == null || !firstCandidate.Content.Parts.Any())
            {
                throw new LLMCommunicationException("Gemini response contains a candidate with no content or parts");
            }

            var firstPart = firstCandidate.Content.Parts.First();

            if (geminiResponse.UsageMetadata == null)
            {
                Logger.LogWarning("Gemini response missing usage metadata. Using default values.");
            }

            var usageMetadata = geminiResponse.UsageMetadata ?? new GeminiUsageMetadata
            {
                PromptTokenCount = 0,
                CandidatesTokenCount = 0,
                TotalTokenCount = 0
            };

            var choice = new Choice
            {
                Index = firstCandidate.Index,
                Message = new Message
                {
                    // Gemini uses "model" for assistant role
                    Role = firstCandidate.Content.Role == "model" ? "assistant" : firstCandidate.Content.Role,
                    Content = firstPart.Text ?? string.Empty // Add null check with empty string default
                },
                FinishReason = MapFinishReason(firstCandidate.FinishReason) ?? "stop" // Default to "stop" if null
            };

            return new ChatCompletionResponse
            {
                Id = Guid.NewGuid().ToString(), // Gemini doesn't provide an ID
                Object = "chat.completion", // Mimic OpenAI structure
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), // Use current time as Gemini doesn't provide it
                Model = originalModelAlias, // Return the alias the user requested
                Choices = new List<Choice> { choice },
                Usage = new Usage
                {
                    PromptTokens = usageMetadata.PromptTokenCount,
                    CompletionTokens = usageMetadata.CandidatesTokenCount, // Sum across candidates (usually 1)
                    TotalTokens = usageMetadata.TotalTokenCount
                },
                OriginalModelAlias = originalModelAlias
            };
        }

        private ChatCompletionChunk? MapToCoreChunk(GeminiGenerateContentResponse geminiResponse, string originalModelAlias)
        {
            // Extract the relevant delta information from the Gemini response structure
            var firstCandidate = geminiResponse.Candidates?.FirstOrDefault();
            var firstPart = firstCandidate?.Content?.Parts?.FirstOrDefault();
            string? deltaText = firstPart?.Text;
            string? finishReason = MapFinishReason(firstCandidate?.FinishReason);

            // Only yield a chunk if there's actual text content or a finish reason
            if (string.IsNullOrEmpty(deltaText) && string.IsNullOrEmpty(finishReason))
            {
                Logger.LogTrace("Skipping Gemini stream chunk mapping as no delta text or finish reason found.");
                return null;
            }

            var choice = new StreamingChoice
            {
                Index = firstCandidate?.Index ?? 0,
                Delta = new DeltaContent
                {
                    // Gemini doesn't explicitly provide role in delta chunks, assume assistant?
                    Content = deltaText // Can be null if only finish reason is present
                },
                FinishReason = finishReason // Can be null
            };

            return new ChatCompletionChunk
            {
                Id = Guid.NewGuid().ToString(), // Gemini doesn't provide chunk IDs
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), // Use current time
                Model = originalModelAlias,
                Choices = new List<StreamingChoice> { choice },
                OriginalModelAlias = originalModelAlias
                // Usage data is typically aggregated at the end for Gemini, not per chunk
            };
        }

        private static string? MapFinishReason(string? geminiFinishReason)
        {
            return geminiFinishReason switch
            {
                "STOP" => "stop", // Normal completion
                "MAX_TOKENS" => "length",
                "SAFETY" => "content_filter", // Map safety stop to content_filter
                "RECITATION" => "content_filter", // Map recitation stop to content_filter
                "OTHER" => null, // Unknown reason
                _ => geminiFinishReason // Pass through null or unknown values
            };
        }

        #endregion
    }
}
