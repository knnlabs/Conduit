using System;
using System.Collections.Generic;
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
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.InternalModels;
using ConduitLLM.Providers.InternalModels.HuggingFaceModels;
using ConduitLLM.Providers.Utilities;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with HuggingFace Inference API.
    /// </summary>
    public class HuggingFaceClient : BaseLLMClient
    {
        // Constants
        private const string DefaultBaseUrl = "https://api-inference.huggingface.co/models/";

        /// <summary>
        /// Initializes a new instance of the <see cref="HuggingFaceClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials.</param>
        /// <param name="providerModelId">The provider's model identifier.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public HuggingFaceClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger<HuggingFaceClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                EnsureHuggingFaceCredentials(credentials),
                providerModelId,
                logger,
                httpClientFactory,
                "huggingface",
                defaultModels)
        {
        }

        private static ProviderCredentials EnsureHuggingFaceCredentials(ProviderCredentials credentials)
        {
            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            if (string.IsNullOrWhiteSpace(credentials.ApiKey))
            {
                throw new ConfigurationException("API key is missing for HuggingFace Inference API provider.");
            }

            return credentials;
        }

        /// <inheritdoc />
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Configure base address
            string baseUrl = string.IsNullOrWhiteSpace(Credentials.BaseUrl)
                ? DefaultBaseUrl
                : Credentials.BaseUrl.TrimEnd('/');

            client.BaseAddress = new Uri(baseUrl);
        }

        /// <inheritdoc />
        public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "ChatCompletion");

            return await ExecuteApiRequestAsync(async () =>
            {
                // Create HTTP client with authentication
                using var client = CreateHttpClient(apiKey);

                // Determine endpoint based on model type
                string apiEndpoint = GetHuggingFaceEndpoint(request.Model ?? ProviderModelId);

                // Different models require different input formats
                // For simplicity, we'll convert messages to a text prompt
                string formattedPrompt = FormatChatMessages(request.Messages);

                // Create request based on model type
                var hfRequest = new HuggingFaceTextGenerationRequest
                {
                    Inputs = formattedPrompt,
                    Parameters = new HuggingFaceParameters
                    {
                        MaxNewTokens = request.MaxTokens,
                        Temperature = ParameterConverter.ToTemperature(request.Temperature),
                        TopP = ParameterConverter.ToProbability(request.TopP, 0.0, 1.0),
                        TopK = request.TopK,
                        Stop = request.Stop?.ToList(),
                        Seed = request.Seed,
                        DoSample = true,
                        ReturnFullText = false
                    },
                    Options = new HuggingFaceOptions
                    {
                        WaitForModel = true
                    }
                };

                // Send request
                using var response = await client.PostAsJsonAsync(
                    apiEndpoint,
                    hfRequest,
                    new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull },
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await ReadErrorContentAsync(response, cancellationToken);
                    Logger.LogError("HuggingFace Inference API request failed with status code {StatusCode}. Response: {ErrorContent}",
                        response.StatusCode, errorContent);
                    throw new LLMCommunicationException(
                        $"HuggingFace Inference API request failed with status code {response.StatusCode}. Response: {errorContent}");
                }

                // Parse response
                string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";

                if (contentType.Contains("json"))
                {
                    return await ProcessJsonResponseAsync(response, request.Model ?? ProviderModelId, cancellationToken);
                }
                else
                {
                    // Handle plain text response
                    string textResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                    return CreateChatCompletionResponse(request.Model ?? ProviderModelId, textResponse);
                }
            }, "ChatCompletion", cancellationToken);
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletion");

            Logger.LogInformation("Streaming is not natively supported in HuggingFace Inference API client. Simulating streaming.");

            // HuggingFace Inference API doesn't support streaming directly
            // Simulate streaming by breaking up the response
            var fullResponse = await CreateChatCompletionAsync(request, apiKey, cancellationToken);

            if (fullResponse.Choices == null || !fullResponse.Choices.Any() ||
                fullResponse.Choices[0].Message?.Content == null)
            {
                yield break;
            }

            // Simulate streaming by breaking up the content
            string content = ContentHelper.GetContentAsString(fullResponse.Choices[0].Message!.Content);

            // Generate a random ID for this streaming session
            string streamId = Guid.NewGuid().ToString();

            // Initial chunk with role
            yield return new ChatCompletionChunk
            {
                Id = streamId,
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model ?? ProviderModelId,
                Choices = new List<StreamingChoice>
                {
                    new StreamingChoice
                    {
                        Index = 0,
                        Delta = new DeltaContent
                        {
                            Role = "assistant",
                            Content = null
                        }
                    }
                }
            };

            // Break content into chunks (words or sentences could be used)
            var words = content.Split(' ');

            // Send content in chunks
            StringBuilder currentChunk = new StringBuilder();
            foreach (var word in words)
            {
                // Add delay to simulate real streaming
                await Task.Delay(25, cancellationToken);

                currentChunk.Append(word).Append(' ');

                // Send every few words
                if (currentChunk.Length > 0)
                {
                    yield return new ChatCompletionChunk
                    {
                        Id = streamId,
                        Object = "chat.completion.chunk",
                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Model = request.Model ?? ProviderModelId,
                        Choices = new List<StreamingChoice>
                        {
                            new StreamingChoice
                            {
                                Index = 0,
                                Delta = new DeltaContent
                                {
                                    Content = currentChunk.ToString()
                                }
                            }
                        }
                    };

                    currentChunk.Clear();
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }
            }

            // Final chunk with finish reason
            yield return new ChatCompletionChunk
            {
                Id = streamId,
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model ?? ProviderModelId,
                Choices = new List<StreamingChoice>
                {
                    new StreamingChoice
                    {
                        Index = 0,
                        Delta = new DeltaContent(),
                        FinishReason = fullResponse.Choices[0].FinishReason
                    }
                }
            };
        }

        /// <summary>
        /// Verifies HuggingFace authentication by calling the whoami endpoint.
        /// This is a free API call that validates the API token.
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
                    return Core.Interfaces.AuthenticationResult.Failure("API key is required");
                }

                using var client = CreateHttpClient(effectiveApiKey);
                // Use the whoami endpoint at the API root
                client.BaseAddress = new Uri("https://huggingface.co/");
                
                var request = new HttpRequestMessage(HttpMethod.Get, "api/whoami");
                
                var response = await client.SendAsync(request, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                if (response.IsSuccessStatusCode)
                {
                    return Core.Interfaces.AuthenticationResult.Success($"Response time: {responseTime:F0}ms");
                }
                
                // Check for specific error codes
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return Core.Interfaces.AuthenticationResult.Failure("Invalid API token");
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Core.Interfaces.AuthenticationResult.Failure("Access denied. Check your API token permissions");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"HuggingFace authentication failed: {response.StatusCode}",
                    errorContent);
            }
            catch (HttpRequestException ex)
            {
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Network error during authentication: {ex.Message}",
                    ex.ToString());
            }
            catch (TaskCanceledException)
            {
                return Core.Interfaces.AuthenticationResult.Failure("Authentication request timed out");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error during HuggingFace authentication verification");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <inheritdoc />
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("HuggingFace Inference API does not provide a model listing endpoint. Returning commonly used models.");

            // HuggingFace Inference API doesn't have an endpoint to list models
            // Return a list of popular models as an example
            await Task.Delay(1, cancellationToken); // Adding await to make this truly async

            return new List<ExtendedModelInfo>
            {
                ExtendedModelInfo.Create("gpt2", ProviderName, "gpt2"),
                ExtendedModelInfo.Create("mistralai/Mistral-7B-Instruct-v0.2", ProviderName, "mistralai/Mistral-7B-Instruct-v0.2"),
                ExtendedModelInfo.Create("meta-llama/Llama-2-7b-chat-hf", ProviderName, "meta-llama/Llama-2-7b-chat-hf"),
                ExtendedModelInfo.Create("facebook/bart-large-cnn", ProviderName, "facebook/bart-large-cnn"),
                ExtendedModelInfo.Create("google/flan-t5-xl", ProviderName, "google/flan-t5-xl"),
                ExtendedModelInfo.Create("EleutherAI/gpt-neox-20b", ProviderName, "EleutherAI/gpt-neox-20b"),
                ExtendedModelInfo.Create("bigscience/bloom", ProviderName, "bigscience/bloom"),
                ExtendedModelInfo.Create("microsoft/DialoGPT-large", ProviderName, "microsoft/DialoGPT-large"),
                ExtendedModelInfo.Create("sentence-transformers/all-MiniLM-L6-v2", ProviderName, "sentence-transformers/all-MiniLM-L6-v2"),
                ExtendedModelInfo.Create("tiiuae/falcon-7b-instruct", ProviderName, "tiiuae/falcon-7b-instruct")
            };
        }

        /// <inheritdoc />
        public override async Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Validate input
            if (request.Input == null)
            {
                throw new ValidationException("Input text is required for embeddings");
            }

            // HuggingFace feature extraction expects single text or array
            object inputs;
            if (request.Input is string singleText)
            {
                inputs = singleText;
            }
            else if (request.Input is IEnumerable<string> multipleTexts)
            {
                var textList = multipleTexts.ToList();
                if (textList.Count == 0)
                {
                    throw new ValidationException("At least one input text is required");
                }
                inputs = textList;
            }
            else
            {
                throw new ValidationException("Input must be a string or array of strings");
            }

            // Create HuggingFace request - feature extraction endpoint
            var hfRequest = new
            {
                inputs = inputs,
                // Optional parameters for feature extraction
                options = new
                {
                    wait_for_model = true,
                    use_cache = true
                }
            };

            using var httpClient = CreateHttpClient(apiKey);
            var requestJson = JsonSerializer.Serialize(hfRequest, DefaultJsonOptions);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            try
            {
                // For embeddings, we use the feature-extraction task
                // The model should be an embedding model like sentence-transformers/all-MiniLM-L6-v2
                var response = await httpClient.PostAsync("", content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    // Handle error response
                    Logger.LogError("HuggingFace API request failed with status code {StatusCode}. Response: {ErrorContent}",
                        response.StatusCode, responseBody);
                    throw new LLMCommunicationException(
                        $"HuggingFace API request failed with status code {response.StatusCode}. Response: {responseBody}");
                }

                // Parse response - HuggingFace returns embeddings directly
                // For single input: [[embedding]]
                // For multiple inputs: [[embedding1], [embedding2], ...]
                List<List<float>> embeddings;
                
                using (var doc = JsonDocument.Parse(responseBody))
                {
                    var root = doc.RootElement;
                    
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        embeddings = new List<List<float>>();
                        
                        // Check if it's a single embedding [[...]] or multiple [[[...]], [[...]]]
                        if (root.GetArrayLength() > 0)
                        {
                            var firstElement = root[0];
                            if (firstElement.ValueKind == JsonValueKind.Array && 
                                firstElement.GetArrayLength() > 0 &&
                                firstElement[0].ValueKind == JsonValueKind.Number)
                            {
                                // Single embedding case: [[0.1, 0.2, ...]]
                                var embedding = new List<float>();
                                foreach (var value in firstElement.EnumerateArray())
                                {
                                    embedding.Add(value.GetSingle());
                                }
                                embeddings.Add(embedding);
                            }
                            else if (firstElement.ValueKind == JsonValueKind.Array)
                            {
                                // Multiple embeddings case: [[[0.1, 0.2, ...]], [[0.3, 0.4, ...]]]
                                foreach (var embeddingArray in root.EnumerateArray())
                                {
                                    if (embeddingArray.ValueKind == JsonValueKind.Array && 
                                        embeddingArray.GetArrayLength() > 0)
                                    {
                                        var embedding = new List<float>();
                                        foreach (var value in embeddingArray[0].EnumerateArray())
                                        {
                                            embedding.Add(value.GetSingle());
                                        }
                                        embeddings.Add(embedding);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new LLMCommunicationException(
                            "Invalid response format from HuggingFace API");
                    }
                }

                // Map to standard embedding response
                var embeddingData = new List<EmbeddingData>();
                for (int i = 0; i < embeddings.Count; i++)
                {
                    embeddingData.Add(new EmbeddingData
                    {
                        Object = "embedding",
                        Embedding = embeddings[i],
                        Index = i
                    });
                }

                // Calculate approximate token usage
                // This is an approximation as HuggingFace doesn't provide token counts
                var textCount = inputs is string ? 1 : ((IEnumerable<string>)inputs).Count();
                var approxTokens = textCount * 50; // Rough estimate

                var usage = new Usage
                {
                    PromptTokens = approxTokens,
                    CompletionTokens = 0,
                    TotalTokens = approxTokens
                };

                return new EmbeddingResponse
                {
                    Object = "list",
                    Data = embeddingData,
                    Model = request.Model ?? ProviderModelId,
                    Usage = usage
                };
            }
            catch (HttpRequestException ex)
            {
                throw new LLMCommunicationException(
                    $"Error communicating with HuggingFace API: {ex.Message}",
                    ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new LLMCommunicationException(
                    "Request to HuggingFace API timed out",
                    ex);
            }
        }

        /// <inheritdoc />
        public override async Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateImage");

            var modelId = request.Model ?? ProviderModelId;
            
            // Check if this is an image generation model
            if (!IsImageGenerationModel(modelId))
            {
                throw new UnsupportedProviderException($"Model '{modelId}' does not support image generation. Use a diffusion model like 'stabilityai/stable-diffusion-2-1' or 'runwayml/stable-diffusion-v1-5'.");
            }

            // Parse size if provided
            int width = 512, height = 512;
            if (!string.IsNullOrEmpty(request.Size))
            {
                var sizeParts = request.Size.Split('x');
                if (sizeParts.Length == 2 && 
                    int.TryParse(sizeParts[0], out width) && 
                    int.TryParse(sizeParts[1], out height))
                {
                    // Valid size format like "1024x1024"
                }
                else
                {
                    // Default to common sizes based on string
                    (width, height) = request.Size?.ToLowerInvariant() switch
                    {
                        "1024x1024" => (1024, 1024),
                        "768x768" => (768, 768),
                        "512x768" => (512, 768),
                        "768x512" => (768, 512),
                        _ => (512, 512)
                    };
                }
            }

            var hfRequest = new HuggingFaceImageGenerationRequest
            {
                Inputs = request.Prompt,
                Parameters = new HuggingFaceImageParameters
                {
                    GuidanceScale = 7.5, // Default guidance scale for diffusion models
                    NumInferenceSteps = 50, // Default steps
                    Seed = Random.Shared.Next(),
                    TargetSize = new HuggingFaceImageSize
                    {
                        Width = width,
                        Height = height
                    }
                },
                Options = new HuggingFaceOptions
                {
                    WaitForModel = true,
                    UseCache = false
                }
            };

            try
            {
                var endpoint = GetHuggingFaceEndpoint(modelId);
                
                // Configure HTTP client for image generation
                using var httpClient = HttpClientFactory?.CreateClient() ?? new HttpClient();
                ConfigureHttpClient(httpClient, apiKey ?? Credentials.ApiKey ?? string.Empty);
                
                // Add specific headers for image generation
                httpClient.DefaultRequestHeaders.Add("Accept", "image/png, image/jpeg, application/json");
                
                var json = JsonSerializer.Serialize(hfRequest, DefaultJsonOptions);
                var content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

                Logger.LogDebug("Sending HuggingFace image generation request to {Endpoint}", endpoint);
                Logger.LogDebug("Request payload: {Payload}", json);

                var response = await httpClient.PostAsync(endpoint, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    Logger.LogError("HuggingFace image generation failed with status {StatusCode}: {Error}", 
                        response.StatusCode, errorContent);
                    
                    throw new LLMCommunicationException(
                        $"HuggingFace image generation request failed with status {response.StatusCode}: {errorContent}");
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                
                if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    // Response is binary image data
                    var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    var base64Image = Convert.ToBase64String(imageBytes);

                    Logger.LogDebug("Received image data: {Size} bytes, Content-Type: {ContentType}", 
                        imageBytes.Length, contentType);

                    var imageObjects = new List<ImageData>();
                    
                    // Create multiple images if requested
                    for (int i = 0; i < request.N; i++)
                    {
                        // For HuggingFace, we typically get one image per request
                        // For multiple images, we'd need to make multiple requests
                        if (i == 0)
                        {
                            imageObjects.Add(new ImageData
                            {
                                B64Json = base64Image
                            });
                        }
                        else
                        {
                            // Make additional requests for multiple images
                            // This is a simplified approach - in production, you might want to parallelize
                            var additionalResponse = await httpClient.PostAsync(endpoint, content, cancellationToken);
                            if (additionalResponse.IsSuccessStatusCode)
                            {
                                var additionalImageBytes = await additionalResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                                var additionalBase64 = Convert.ToBase64String(additionalImageBytes);
                                
                                imageObjects.Add(new ImageData
                                {
                                    B64Json = additionalBase64
                                });
                            }
                        }
                    }

                    return new ImageGenerationResponse
                    {
                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Data = imageObjects
                    };
                }
                else
                {
                    // Response might be JSON with error or other format
                    var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                    Logger.LogError("Unexpected response format from HuggingFace image generation: {ContentType}, {Response}", 
                        contentType, responseText);
                    
                    throw new LLMCommunicationException(
                        $"Unexpected response format from HuggingFace image generation: {contentType}");
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "HTTP error during HuggingFace image generation: {Message}", ex.Message);
                throw new LLMCommunicationException($"HTTP error during HuggingFace image generation: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                Logger.LogError(ex, "Timeout during HuggingFace image generation");
                throw new LLMCommunicationException("HuggingFace image generation request timed out", ex);
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex, "JSON serialization error during HuggingFace image generation: {Message}", ex.Message);
                throw new LLMCommunicationException($"JSON error during HuggingFace image generation: {ex.Message}", ex);
            }
        }

        #region Helper Methods

        private string GetHuggingFaceEndpoint(string modelId)
        {
            // If the base URL already contains the model endpoint, don't add it again
            if (Credentials.BaseUrl != null && Credentials.BaseUrl.Contains("/models/"))
            {
                return string.Empty; // Use the base address as is
            }

            return modelId;
        }

        /// <summary>
        /// Determines if a model supports image generation based on its name.
        /// </summary>
        private bool IsImageGenerationModel(string modelId)
        {
            // Common image generation model patterns
            var imageModelPatterns = new[]
            {
                "stable-diffusion",
                "diffusion",
                "dalle",
                "midjourney",
                "imagen",
                "flux",
                "kandinsky",
                "playground",
                "runwayml/stable-diffusion",
                "stabilityai/stable-diffusion",
                "CompVis/stable-diffusion",
                "hakurei/waifu-diffusion",
                "nitrosocke/Arcane-Diffusion",
                "dreamlike-art/dreamlike-diffusion",
                "andite/anything",
                "wavymulder/Analog-Diffusion",
                "22h/vintedois-diffusion",
                "prompthero/openjourney"
            };

            var lowerModelId = modelId.ToLowerInvariant();
            return imageModelPatterns.Any(pattern => lowerModelId.Contains(pattern.ToLowerInvariant()));
        }

        private string FormatChatMessages(List<Message> messages)
        {
            // Different models expect different formats
            // For simplicity, we'll use a generic chat format here
            // In a real implementation, you would adapt this based on the model

            StringBuilder formattedChat = new StringBuilder();

            // Extract system message if present
            var systemMessage = messages.FirstOrDefault(m =>
                m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));

            if (systemMessage != null)
            {
                formattedChat.AppendLine(ContentHelper.GetContentAsString(systemMessage.Content));
                formattedChat.AppendLine();
            }

            // Format the conversation
            foreach (var message in messages.Where(m =>
                !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
            {
                string role = message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                    ? "Assistant"
                    : "Human";

                formattedChat.AppendLine($"{role}: {ContentHelper.GetContentAsString(message.Content)}");
            }

            // Add final prompt for assistant
            formattedChat.Append("Assistant: ");

            return formattedChat.ToString();
        }

        private async Task<ChatCompletionResponse> ProcessJsonResponseAsync(
            HttpResponseMessage response,
            string originalModelAlias,
            CancellationToken cancellationToken)
        {
            try
            {
                // Try to parse as array first (some HF models return array of results)
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (jsonContent.TrimStart().StartsWith("["))
                {
                    // Parse as array
                    var arrayResponse = JsonSerializer.Deserialize<List<HuggingFaceTextGenerationResponse>>(
                        jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (arrayResponse != null && arrayResponse.Count > 0)
                    {
                        return CreateChatCompletionResponse(originalModelAlias, arrayResponse[0].GeneratedText ?? string.Empty);
                    }
                }
                else
                {
                    // Parse as single object
                    var objectResponse = JsonSerializer.Deserialize<HuggingFaceTextGenerationResponse>(
                        jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (objectResponse != null)
                    {
                        return CreateChatCompletionResponse(originalModelAlias, objectResponse.GeneratedText ?? string.Empty);
                    }
                }

                // If all fails, return error
                Logger.LogError("Could not parse HuggingFace response: {Content}", jsonContent);
                throw new LLMCommunicationException("Invalid response format from HuggingFace Inference API");
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex, "Error parsing JSON response from HuggingFace");
                throw new LLMCommunicationException("Error parsing JSON response from HuggingFace Inference API", ex);
            }
        }

        private ChatCompletionResponse CreateChatCompletionResponse(string model, string content)
        {
            // Estimate token counts based on text length
            int estimatedPromptTokens = EstimateTokenCount(content);
            int estimatedCompletionTokens = EstimateTokenCount(content);

            return new ChatCompletionResponse
            {
                Id = Guid.NewGuid().ToString(),
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = model,
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
                    // HuggingFace doesn't provide token usage
                    // Provide estimated counts based on text length
                    PromptTokens = estimatedPromptTokens,
                    CompletionTokens = estimatedCompletionTokens,
                    TotalTokens = estimatedPromptTokens + estimatedCompletionTokens
                }
            };
        }

        private int EstimateTokenCount(string text)
        {
            // Rough token count estimation
            if (string.IsNullOrEmpty(text))
                return 0;

            // Approximately 4 characters per token for English text
            // This is a rough estimate that works for many models but isn't exact
            return Math.Max(1, text.Length / 4);
        }

        #endregion

        #region Capabilities

        /// <inheritdoc />
        public override Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            var model = modelId ?? ProviderModelId;
            var isImageGeneration = IsImageGenerationModel(model);

            return Task.FromResult(new ProviderCapabilities
            {
                Provider = ProviderName,
                ModelId = model,
                ChatParameters = new ChatParameterSupport
                {
                    Temperature = true,
                    MaxTokens = true,
                    TopP = true,
                    TopK = true, // HuggingFace supports top-k
                    Stop = true,
                    PresencePenalty = false, // HuggingFace doesn't support presence penalty
                    FrequencyPenalty = false, // HuggingFace doesn't support frequency penalty
                    LogitBias = false, // HuggingFace doesn't support logit bias
                    N = false, // HuggingFace doesn't support multiple choices
                    User = false, // HuggingFace doesn't support user parameter
                    Seed = true, // HuggingFace supports seed
                    ResponseFormat = false, // HuggingFace doesn't support response format
                    Tools = false, // HuggingFace doesn't support tools
                    Constraints = new ParameterConstraints
                    {
                        TemperatureRange = new Range<double>(0.0, 2.0),
                        TopPRange = new Range<double>(0.0, 1.0),
                        TopKRange = new Range<int>(1, 100),
                        MaxStopSequences = 10,
                        MaxTokenLimit = 4096 // Default fallback
                    }
                },
                Features = new FeatureSupport
                {
                    Streaming = false, // HuggingFace simulates streaming
                    Embeddings = true, // HuggingFace supports embeddings
                    ImageGeneration = isImageGeneration,
                    VisionInput = false, // Most HuggingFace models don't support vision
                    FunctionCalling = false, // HuggingFace doesn't support function calling
                    AudioTranscription = false, // HuggingFace doesn't provide audio transcription
                    TextToSpeech = false // HuggingFace doesn't provide text-to-speech
                }
            });
        }

        /// <inheritdoc/>
        protected override string GetDefaultBaseUrl()
        {
            return DefaultBaseUrl;
        }

        #endregion
    }
}
