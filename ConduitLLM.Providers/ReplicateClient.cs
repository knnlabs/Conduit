using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.InternalModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Revised client for interacting with Replicate APIs using the new client hierarchy.
    /// Handles the asynchronous prediction workflow (start, poll, get result) for various model providers.
    /// </summary>
    public class ReplicateClient : CustomProviderClient
    {
        // Default base URL for Replicate API
        private const string DefaultReplicateBaseUrl = "https://api.replicate.com/v1/";

        // Default polling configuration
        private static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan MaxPollingDuration = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Initializes a new instance of the <see cref="ReplicateClient"/> class.
        /// </summary>
        /// <param name="credentials">The credentials for accessing the Replicate API.</param>
        /// <param name="providerModelId">The model identifier to use (typically a version hash or full slug).</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">The HTTP client factory for creating HttpClient instances.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public ReplicateClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                credentials,
                providerModelId,
                logger,
                httpClientFactory,
                "Replicate",
                string.IsNullOrWhiteSpace(credentials.BaseUrl) ? DefaultReplicateBaseUrl : credentials.BaseUrl,
                defaultModels)
        {
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

        /// <inheritdoc/>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            // Customize configuration for Replicate - use Token auth
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiKey);

            // Set the base address if not already set
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

            Logger.LogInformation("Creating chat completion with Replicate for model '{ModelId}'", ProviderModelId);

            try
            {
                // Map the request to Replicate format and start prediction
                var predictionRequest = MapToPredictionRequest(request);
                var predictionResponse = await StartPredictionAsync(predictionRequest, apiKey, cancellationToken);

                // Poll until prediction completes or fails
                var finalPrediction = await PollPredictionUntilCompletedAsync(predictionResponse.Id, apiKey, cancellationToken);

                // Process the final result
                return MapToChatCompletionResponse(finalPrediction, request.Model);
            }
            catch (LLMCommunicationException)
            {
                // Re-throw LLMCommunicationException directly
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred while processing Replicate chat completion");
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

            Logger.LogInformation("Creating streaming chat completion with Replicate for model '{ModelId}'", ProviderModelId);

            // Variables to hold data outside the try block
            ReplicatePredictionRequest? predictionRequest = null;
            ReplicatePredictionResponse? predictionResponse = null;
            ReplicatePredictionResponse? finalPrediction = null;

            try
            {
                // Replicate doesn't natively support streaming in the common SSE format
                // Instead, we'll simulate streaming by getting the full response and breaking it into chunks

                // Start the prediction
                predictionRequest = MapToPredictionRequest(request);
                predictionResponse = await StartPredictionAsync(predictionRequest, apiKey, cancellationToken);
            }
            catch (LLMCommunicationException)
            {
                // Re-throw LLMCommunicationException directly
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred starting Replicate prediction");
                throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
            }

            // First chunk with role "assistant" - outside try block so we can yield
            yield return CreateChatCompletionChunk(
                string.Empty,
                ProviderModelId,
                true,
                null,
                request.Model);

            try
            {
                // Poll until prediction completes or fails
                if (predictionResponse != null)
                {
                    finalPrediction = await PollPredictionUntilCompletedAsync(
                        predictionResponse.Id,
                        apiKey,
                        cancellationToken,
                        true); // Set yield progress to true
                }
            }
            catch (LLMCommunicationException)
            {
                // Re-throw LLMCommunicationException directly
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred polling Replicate prediction");
                throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
            }

            // Extract content and yield the result - outside try block
            if (finalPrediction != null)
            {
                var content = ExtractTextFromPredictionOutput(finalPrediction.Output);
                if (!string.IsNullOrEmpty(content))
                {
                    // Yield the content as a chunk
                    yield return CreateChatCompletionChunk(
                        content,
                        ProviderModelId,
                        false,
                        "stop",
                        request.Model);
                }
            }
        }

        /// <inheritdoc/>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get a list of selected popular LLM models from Replicate
                // In a real implementation, this would query the Replicate API for models
                // However, Replicate doesn't have a simple endpoint for listing all available LLMs
                // So we return a curated list of known models

                // This is a simplified implementation - in production, you might want to:
                // 1. Cache this list and refresh periodically
                // 2. Query the collections endpoint for more models
                // 3. Allow administrators to configure which models to include

                await Task.Delay(1, cancellationToken); // Making this truly async

                var models = new List<InternalModels.ExtendedModelInfo>
                {
                    InternalModels.ExtendedModelInfo.Create(
                        "meta/llama-3-70b-instruct:a532122398cff93aa48f32d63902fa4545c4fb642f91e3e51bcf5d2f8da23eff",
                        ProviderName,
                        "meta/llama-3-70b-instruct:a532122398cff93aa48f32d63902fa4545c4fb642f91e3e51bcf5d2f8da23eff")
                        .WithName("Llama-3 70B Instruct")
                        .WithCapabilities(new InternalModels.ModelCapabilities
                        {
                            Chat = true,
                            TextGeneration = true,
                            Embeddings = false,
                            ImageGeneration = false
                        })
                        .WithTokenLimits(new InternalModels.ModelTokenLimits
                        {
                            MaxInputTokens = 32000,
                            MaxOutputTokens = 4096
                        }),

                    InternalModels.ExtendedModelInfo.Create(
                        "meta/llama-3-8b-instruct:dd2c4157802af9020a7272a6e5c27f3dd56ec1026a7556e193ee8e8738549590",
                        ProviderName,
                        "meta/llama-3-8b-instruct:dd2c4157802af9020a7272a6e5c27f3dd56ec1026a7556e193ee8e8738549590")
                        .WithName("Llama-3 8B Instruct")
                        .WithCapabilities(new InternalModels.ModelCapabilities
                        {
                            Chat = true,
                            TextGeneration = true,
                            Embeddings = false,
                            ImageGeneration = false
                        })
                        .WithTokenLimits(new InternalModels.ModelTokenLimits
                        {
                            MaxInputTokens = 16000,
                            MaxOutputTokens = 4096
                        }),

                    InternalModels.ExtendedModelInfo.Create(
                        "stability-ai/sdxl:4a1ee9c9f06e811f991e83a0d1ee9c9ca2d6dc03d6cd7c9322bfff81c350da82",
                        ProviderName,
                        "stability-ai/sdxl:4a1ee9c9f06e811f991e83a0d1ee9c9ca2d6dc03d6cd7c9322bfff81c350da82")
                        .WithName("SDXL 1.0")
                        .WithCapabilities(new InternalModels.ModelCapabilities
                        {
                            Chat = false,
                            TextGeneration = false,
                            Embeddings = false,
                            ImageGeneration = true
                        })
                };

                return models;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred while listing Replicate models");
                throw new LLMCommunicationException($"An unexpected error occurred while listing models: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public override async Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateEmbeddingAsync");

            // While Replicate does have embedding models, the implementation would be similar to chat completion
            // For now, we'll throw NotSupportedException, but this could be implemented in the future
            Logger.LogWarning("Embeddings are not currently supported by ReplicateClientRevised.");
            return await Task.FromException<EmbeddingResponse>(
                new NotSupportedException("Embeddings are not currently supported by ReplicateClientRevised."));
        }

        /// <inheritdoc/>
        public override async Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateImageAsync");

            Logger.LogInformation("Creating image with Replicate for model '{ModelId}'", ProviderModelId);

            try
            {
                // Map the request to Replicate format and start prediction
                var predictionRequest = MapToImageGenerationRequest(request);
                var predictionResponse = await StartPredictionAsync(predictionRequest, apiKey, cancellationToken);

                // Poll until prediction completes or fails
                var finalPrediction = await PollPredictionUntilCompletedAsync(predictionResponse.Id, apiKey, cancellationToken);

                // Process the final result
                return MapToImageGenerationResponse(finalPrediction, request.Model);
            }
            catch (LLMCommunicationException)
            {
                // Re-throw LLMCommunicationException directly
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred while processing Replicate image generation");
                throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
            }
        }

        #region Helper Methods

        private ReplicatePredictionRequest MapToPredictionRequest(ChatCompletionRequest request)
        {
            // Prepare the input based on the model
            var input = new Dictionary<string, object>();

            // For Llama models, handle with the system message format
            if (ProviderModelId.Contains("llama", StringComparison.OrdinalIgnoreCase))
            {
                // Extract system message if present
                var systemMessage = request.Messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
                var systemPrompt = systemMessage != null ? systemMessage.Content?.ToString() : null;

                // Create a list of chat messages for the 'messages' parameter (excluding system)
                var chatMessages = request.Messages
                    .Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                    .Select(m => new ReplicateLlamaChatMessage
                    {
                        Role = m.Role,
                        Content = m.Content?.ToString() ?? string.Empty
                    })
                    .ToList();

                // Add the messages to the input
                input["messages"] = chatMessages;

                // Add system prompt if present
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    input["system_prompt"] = systemPrompt;
                }
            }
            else
            {
                // For models that expect a simple text prompt, concatenate messages
                var promptBuilder = new System.Text.StringBuilder();

                foreach (var message in request.Messages)
                {
                    if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                    {
                        promptBuilder.AppendLine($"System: {message.Content}");
                        promptBuilder.AppendLine();
                    }
                    else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                    {
                        promptBuilder.AppendLine($"User: {message.Content}");
                    }
                    else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                    {
                        promptBuilder.AppendLine($"Assistant: {message.Content}");
                    }
                }

                // Add a final prompt marker
                promptBuilder.Append("Assistant: ");

                // Add the prompt to the input
                input["prompt"] = promptBuilder.ToString();
            }

            // Add optional parameters if provided
            if (request.Temperature.HasValue)
            {
                input["temperature"] = request.Temperature.Value;
            }

            if (request.MaxTokens.HasValue)
            {
                input["max_length"] = request.MaxTokens.Value;
            }

            if (request.TopP.HasValue)
            {
                input["top_p"] = request.TopP.Value;
            }

            if (request.Stop != null && request.Stop.Any())
            {
                input["stop_sequences"] = request.Stop;
            }

            return new ReplicatePredictionRequest
            {
                Version = ProviderModelId,
                Input = input
            };
        }

        private ReplicatePredictionRequest MapToImageGenerationRequest(ImageGenerationRequest request)
        {
            // Prepare the input based on the model
            var input = new Dictionary<string, object>
            {
                ["prompt"] = request.Prompt
            };

            // Add optional parameters if provided
            if (request.Size != null)
            {
                var dimensions = request.Size.Split('x');
                if (dimensions.Length == 2 && int.TryParse(dimensions[0], out int width) && int.TryParse(dimensions[1], out int height))
                {
                    input["width"] = width;
                    input["height"] = height;
                }
            }

            if (request.Quality != null)
            {
                input["quality"] = request.Quality;
            }

            if (request.Style != null)
            {
                input["style"] = request.Style;
            }

            if (request.N > 1)
            {
                input["num_outputs"] = request.N;
            }

            return new ReplicatePredictionRequest
            {
                Version = ProviderModelId,
                Input = input
            };
        }

        private async Task<ReplicatePredictionResponse> StartPredictionAsync(
            ReplicatePredictionRequest request,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            try
            {
                using var client = CreateHttpClient(apiKey);
                var response = await client.PostAsJsonAsync("predictions", request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await ReadErrorContentAsync(response, cancellationToken);
                    Logger.LogError("Replicate API prediction creation failed with status code {StatusCode}. Response: {ErrorContent}",
                        response.StatusCode, errorContent);
                    throw new LLMCommunicationException(
                        $"Replicate API prediction creation failed with status code {response.StatusCode}. Response: {errorContent}");
                }

                var predictionResponse = await response.Content.ReadFromJsonAsync<ReplicatePredictionResponse>(
                    cancellationToken: cancellationToken);

                if (predictionResponse == null)
                {
                    throw new LLMCommunicationException("Failed to deserialize Replicate prediction response");
                }

                return predictionResponse;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "HTTP request error communicating with Replicate API");
                throw new LLMCommunicationException($"HTTP request error communicating with Replicate API: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex, "JSON error processing Replicate response");
                throw new LLMCommunicationException("Error deserializing Replicate response", ex);
            }
            catch (LLMCommunicationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred while starting Replicate prediction");
                throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
            }
        }

        private async Task<ReplicatePredictionResponse> PollPredictionUntilCompletedAsync(
            string predictionId,
            string? apiKey,
            CancellationToken cancellationToken,
            bool yieldProgress = false)
        {
            var startTime = DateTime.UtcNow;
            var attemptCount = 0;
            ReplicatePredictionResponse? prediction = null;

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.LogInformation("Prediction polling was canceled");
                    throw new OperationCanceledException("Prediction polling was canceled", cancellationToken);
                }

                // Check if we've exceeded the maximum polling duration
                if (DateTime.UtcNow - startTime > MaxPollingDuration)
                {
                    Logger.LogError("Exceeded maximum polling duration for prediction {PredictionId}", predictionId);
                    throw new LLMCommunicationException($"Exceeded maximum polling duration for prediction {predictionId}");
                }

                attemptCount++;
                Logger.LogDebug("Polling prediction {PredictionId}, attempt {AttemptCount}", predictionId, attemptCount);

                try
                {
                    using var client = CreateHttpClient(apiKey);
                    var response = await client.GetAsync($"predictions/{predictionId}", cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await ReadErrorContentAsync(response, cancellationToken);
                        Logger.LogError("Replicate API prediction polling failed with status code {StatusCode}. Response: {ErrorContent}",
                            response.StatusCode, errorContent);
                        throw new LLMCommunicationException(
                            $"Replicate API prediction polling failed with status code {response.StatusCode}. Response: {errorContent}");
                    }

                    prediction = await response.Content.ReadFromJsonAsync<ReplicatePredictionResponse>(
                        cancellationToken: cancellationToken);

                    if (prediction == null)
                    {
                        throw new LLMCommunicationException("Failed to deserialize Replicate prediction response");
                    }

                    // Check prediction status
                    switch (prediction.Status.ToLowerInvariant())
                    {
                        case "succeeded":
                            Logger.LogInformation("Prediction {PredictionId} completed successfully", predictionId);
                            return prediction;

                        case "failed":
                            Logger.LogError("Prediction {PredictionId} failed: {Error}", predictionId, prediction.Error);
                            throw new LLMCommunicationException($"Replicate prediction failed: {prediction.Error}");

                        case "canceled":
                            Logger.LogWarning("Prediction {PredictionId} was canceled", predictionId);
                            throw new LLMCommunicationException("Replicate prediction was canceled");

                        case "starting":
                        case "processing":
                            // Still in progress, continue polling
                            Logger.LogDebug("Prediction {PredictionId} is {Status}", predictionId, prediction.Status);
                            break;

                        default:
                            Logger.LogWarning("Prediction {PredictionId} has unknown status: {Status}", predictionId, prediction.Status);
                            break;
                    }
                }
                catch (HttpRequestException ex)
                {
                    Logger.LogError(ex, "HTTP request error during prediction polling");
                    throw new LLMCommunicationException($"HTTP request error during prediction polling: {ex.Message}", ex);
                }
                catch (JsonException ex)
                {
                    Logger.LogError(ex, "JSON error processing prediction polling response");
                    throw new LLMCommunicationException("Error deserializing prediction polling response", ex);
                }
                catch (LLMCommunicationException)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "An unexpected error occurred during prediction polling");
                    throw new LLMCommunicationException($"An unexpected error occurred during prediction polling: {ex.Message}", ex);
                }

                // Add a delay before the next poll
                await Task.Delay(DefaultPollingInterval, cancellationToken);
            }
        }

        private ChatCompletionResponse MapToChatCompletionResponse(ReplicatePredictionResponse prediction, string originalModelAlias)
        {
            // Extract content from the prediction output - format depends on the model
            var content = ExtractTextFromPredictionOutput(prediction.Output);

            // Estimate token usage (not precise, just a rough estimate)
            var inputStr = prediction.Input != null ? JsonSerializer.Serialize(prediction.Input) : string.Empty;
            var promptTokens = EstimateTokenCount(inputStr);
            var completionTokens = EstimateTokenCount(content);

            return new ChatCompletionResponse
            {
                Id = prediction.Id,
                Object = "chat.completion",
                Created = ((DateTimeOffset)prediction.CreatedAt).ToUnixTimeSeconds(),
                Model = originalModelAlias,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new Message
                        {
                            Role = "assistant",
                            Content = content
                        },
                        FinishReason = "stop"
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = promptTokens + completionTokens
                },
                OriginalModelAlias = originalModelAlias
            };
        }

        private ImageGenerationResponse MapToImageGenerationResponse(ReplicatePredictionResponse prediction, string originalModelAlias)
        {
            // Extract image URLs from the prediction output
            var imageUrls = ExtractImageUrlsFromPredictionOutput(prediction.Output);

            return new ImageGenerationResponse
            {
                Created = ((DateTimeOffset)prediction.CreatedAt).ToUnixTimeSeconds(),
                Data = imageUrls.Select(url => new Core.Models.ImageData
                {
                    Url = url
                }).ToList()
            };
        }

        private string ExtractTextFromPredictionOutput(object? output)
        {
            // Handle different output formats from different models
            if (output == null)
            {
                return string.Empty;
            }

            try
            {
                // String output (common for text generation models)
                if (output is string str)
                {
                    return str;
                }

                // List of strings (some models return this)
                if (output is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        return element.GetString() ?? string.Empty;
                    }
                    else if (element.ValueKind == JsonValueKind.Array)
                    {
                        // Try to read as array of strings
                        var result = new System.Text.StringBuilder();
                        foreach (var item in element.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                result.Append(item.GetString());
                            }
                        }
                        return result.ToString();
                    }
                }

                // Last resort: serialize to JSON and try to extract
                return JsonSerializer.Serialize(output);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error extracting text from prediction output");
                return string.Empty;
            }
        }

        private List<string> ExtractImageUrlsFromPredictionOutput(object? output)
        {
            var urls = new List<string>();

            // Handle different output formats from different models
            if (output == null)
            {
                return urls;
            }

            try
            {
                // String output (single image URL)
                if (output is string str)
                {
                    urls.Add(str);
                    return urls;
                }

                // Array of strings (multiple image URLs)
                if (output is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        urls.Add(element.GetString() ?? string.Empty);
                        return urls;
                    }
                    else if (element.ValueKind == JsonValueKind.Array)
                    {
                        // Try to read as array of strings
                        foreach (var item in element.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                string? url = item.GetString();
                                if (!string.IsNullOrEmpty(url))
                                {
                                    urls.Add(url);
                                }
                            }
                        }
                        return urls;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error extracting image URLs from prediction output");
            }

            return urls;
        }

        private int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            // Very rough estimate: 4 characters per token (English text)
            return text.Length / 4;
        }

        #endregion

        #region Authentication Verification

        /// <summary>
        /// Verifies Replicate authentication by making a test request to the account endpoint.
        /// </summary>
        public override async Task<Core.Interfaces.AuthenticationResult> VerifyAuthenticationAsync(
            string? apiKey = null,
            string? baseUrl = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : Credentials.ApiKey;
                
                if (string.IsNullOrWhiteSpace(effectiveApiKey))
                {
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "API key is required",
                        "No API token provided for Replicate authentication");
                }

                // Create a test client
                using var client = CreateHttpClient(effectiveApiKey);
                
                // Make a request to the account endpoint
                var accountUrl = $"{GetHealthCheckUrl(baseUrl)}/account";
                var response = await client.GetAsync(accountUrl, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Logger.LogInformation("Replicate auth check returned status {StatusCode}", response.StatusCode);

                // Check for authentication errors
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "Authentication failed",
                        "Invalid API token - Replicate requires a valid API token");
                }
                
                if (response.IsSuccessStatusCode)
                {
                    return Core.Interfaces.AuthenticationResult.Success(
                        "Connected successfully to Replicate API",
                        responseTime);
                }

                // Other errors
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Unexpected response: {response.StatusCode}",
                    await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error verifying Replicate authentication");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets the health check URL for Replicate.
        /// </summary>
        public override string GetHealthCheckUrl(string? baseUrl = null)
        {
            var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl) 
                ? baseUrl.TrimEnd('/') 
                : (Credentials.BaseUrl ?? DefaultReplicateBaseUrl).TrimEnd('/');
            
            // Ensure v1 is in the URL
            if (!effectiveBaseUrl.EndsWith("/v1"))
            {
                effectiveBaseUrl = $"{effectiveBaseUrl}/v1";
            }
            
            return effectiveBaseUrl;
        }

        /// <summary>
        /// Gets the default base URL for Replicate.
        /// </summary>
        protected override string GetDefaultBaseUrl()
        {
            return DefaultReplicateBaseUrl;
        }

        #endregion
    }
}
