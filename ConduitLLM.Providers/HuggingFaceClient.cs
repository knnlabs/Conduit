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
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.InternalModels;
using ConduitLLM.Providers.InternalModels.HuggingFaceModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with HuggingFace Inference API.
    /// </summary>
    public class HuggingFaceClient : BaseLLMClient
    {
        private const string DefaultApiBase = "https://api-inference.huggingface.co/models/";

        /// <summary>
        /// Initializes a new instance of the <see cref="HuggingFaceClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials.</param>
        /// <param name="providerModelId">The provider's model identifier.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory.</param>
        public HuggingFaceClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger<HuggingFaceClient> logger,
            IHttpClientFactory? httpClientFactory = null)
            : base(
                  EnsureHuggingFaceCredentials(credentials),
                  providerModelId,
                  logger,
                  httpClientFactory,
                  "huggingface")
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
            
            // Set base address if provided in credentials
            string baseUrl = !string.IsNullOrWhiteSpace(Credentials.ApiBase)
                ? Credentials.ApiBase.TrimEnd('/')
                : DefaultApiBase.TrimEnd('/');
                
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl + "/");
            }
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
                using var client = CreateHttpClient(apiKey);
                
                // Determine endpoint based on model type
                string modelId = request.Model ?? ProviderModelId;
                string endpoint = GetModelEndpoint(modelId);
                
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
                        Temperature = request.Temperature,
                        TopP = request.TopP,
                        DoSample = true,
                        ReturnFullText = false
                    },
                    Options = new HuggingFaceOptions
                    {
                        WaitForModel = true
                    }
                };
                
                Logger.LogDebug("Sending chat completion request to HuggingFace at {Endpoint} for model {Model}", 
                    endpoint, modelId);
                
                // Send request using HttpClientHelper
                var response = await HttpClientHelper.SendRawRequestAsync(
                    client,
                    HttpMethod.Post,
                    endpoint,
                    hfRequest,
                    CreateStandardHeaders(apiKey),
                    DefaultJsonOptions,
                    Logger,
                    cancellationToken);
                
                // Process response
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    Logger.LogError("HuggingFace API request failed with status code {StatusCode}. Response: {ErrorContent}",
                        response.StatusCode, errorContent);
                    throw new LLMCommunicationException(
                        $"HuggingFace API request failed with status code {response.StatusCode}. Response: {errorContent}");
                }
                
                // Parse response based on content type
                string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
                
                if (contentType.Contains("json"))
                {
                    return await ProcessJsonResponseAsync(response, request.Model, cancellationToken);
                }
                else
                {
                    // Handle plain text response
                    string textResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                    return CreateChatCompletionResponse(request.Model, textResponse);
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
            
            // Get all chunks outside of try/catch to avoid the "yield in try" issue
            var chunks = await FetchStreamChunksAsync(request, apiKey, cancellationToken);
            
            // Now yield the chunks outside of any try blocks
            foreach (var chunk in chunks)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }
                
                yield return chunk;
            }
        }
        
        /// <summary>
        /// Helper method to fetch all stream chunks without yielding in a try block
        /// </summary>
        private async Task<List<ChatCompletionChunk>> FetchStreamChunksAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            var chunks = new List<ChatCompletionChunk>();
            
            try
            {
                Logger.LogInformation("Streaming is not natively supported in HuggingFace Inference API client. Simulating streaming.");
                
                // HuggingFace Inference API doesn't support streaming directly
                // Simulate streaming by breaking up the response
                var fullResponse = await CreateChatCompletionAsync(request, apiKey, cancellationToken);
                
                if (fullResponse.Choices == null || !fullResponse.Choices.Any() ||
                    fullResponse.Choices[0].Message?.Content == null)
                {
                    return chunks;
                }
                
                // Simulate streaming by breaking up the content
                string content = ContentHelper.GetContentAsString(fullResponse.Choices[0].Message!.Content, Logger);
                
                // Generate a random ID for this streaming session
                string streamId = Guid.NewGuid().ToString();
                
                // Initial chunk with role
                chunks.Add(new ChatCompletionChunk
                {
                    Id = streamId,
                    Object = "chat.completion.chunk",
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Model = request.Model,
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
                });
                
                // Break content into chunks (words or sentences could be used)
                var words = content.Split(' ');
                
                // Simulate chunks
                StringBuilder currentChunk = new StringBuilder();
                foreach (var word in words)
                {
                    // Add delay to simulate real streaming
                    await Task.Delay(25, cancellationToken);
                    
                    currentChunk.Append(word).Append(' ');
                    
                    // Send every few words
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(new ChatCompletionChunk
                        {
                            Id = streamId,
                            Object = "chat.completion.chunk",
                            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            Model = request.Model,
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
                        });
                        
                        currentChunk.Clear();
                    }
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
                
                // Final chunk with finish reason
                chunks.Add(new ChatCompletionChunk
                {
                    Id = streamId,
                    Object = "chat.completion.chunk",
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Model = request.Model,
                    Choices = new List<StreamingChoice>
                    {
                        new StreamingChoice
                        {
                            Index = 0,
                            Delta = new DeltaContent(),
                            FinishReason = fullResponse.Choices[0].FinishReason
                        }
                    }
                });
                
                return chunks;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogError(ex, "Error in simulated streaming chat completion from HuggingFace: {Message}", ex.Message);
                throw new LLMCommunicationException($"Error in simulated streaming chat completion from HuggingFace: {ex.Message}", ex);
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
            ValidateRequest(request, "CreateEmbedding");
            
            return await ExecuteApiRequestAsync(async () =>
            {
                using var client = CreateHttpClient(apiKey);
                
                // Determine if model is embedding model
                string modelId = request.Model ?? ProviderModelId;
                
                if (modelId.Contains("sentence-transformers", StringComparison.OrdinalIgnoreCase) ||
                    modelId.Contains("embedding", StringComparison.OrdinalIgnoreCase) ||
                    modelId.Contains("e5-", StringComparison.OrdinalIgnoreCase))
                {
                    // Send embedding request to HuggingFace API
                    string endpoint = GetModelEndpoint(modelId);
                    
                    // Create embedding request
                    var hfRequest = new HuggingFaceEmbeddingRequest
                    {
                        Inputs = request.Input.Any() ? request.Input.ToArray() : new[] { "" },
                        Options = new HuggingFaceOptions
                        {
                            WaitForModel = true
                        }
                    };
                    
                    Logger.LogDebug("Sending embedding request to HuggingFace at {Endpoint} for model {Model}",
                        endpoint, modelId);
                    
                    // Send request
                    var response = await HttpClientHelper.SendRawRequestAsync(
                        client,
                        HttpMethod.Post,
                        endpoint,
                        hfRequest,
                        CreateStandardHeaders(apiKey),
                        DefaultJsonOptions,
                        Logger,
                        cancellationToken);
                    
                    // Process response
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        Logger.LogError("HuggingFace API embedding request failed with status code {StatusCode}. Response: {ErrorContent}",
                            response.StatusCode, errorContent);
                        throw new LLMCommunicationException(
                            $"HuggingFace API embedding request failed with status code {response.StatusCode}. Response: {errorContent}");
                    }
                    
                    // Parse embeddings from response
                    try
                    {
                        var embeddings = await JsonSerializer.DeserializeAsync<List<List<float>>>(
                            await response.Content.ReadAsStreamAsync(cancellationToken),
                            DefaultJsonOptions,
                            cancellationToken);
                        
                        if (embeddings == null)
                        {
                            throw new LLMCommunicationException("Failed to parse embeddings from HuggingFace response");
                        }
                        
                        // Map to EmbeddingResponse format
                        var embeddingResponse = new EmbeddingResponse
                        {
                            Data = new List<EmbeddingData>(),
                            Model = modelId,
                            Object = "embedding",
                            Usage = new Usage
                            {
                                PromptTokens = EstimateTokenCount(request.Input.Any() ? string.Join(" ", request.Input) : ""),
                                CompletionTokens = 0,
                                TotalTokens = EstimateTokenCount(request.Input.Any() ? string.Join(" ", request.Input) : "")
                            }
                        };
                        
                        for (int i = 0; i < embeddings.Count; i++)
                        {
                            embeddingResponse.Data.Add(new EmbeddingData
                            {
                                Index = i,
                                Object = "embedding",
                                Embedding = embeddings[i]
                            });
                        }
                        
                        return embeddingResponse;
                    }
                    catch (JsonException ex)
                    {
                        Logger.LogError(ex, "Error parsing embeddings from HuggingFace response");
                        throw new LLMCommunicationException("Error parsing embeddings from HuggingFace response", ex);
                    }
                }
                else
                {
                    throw new UnsupportedProviderException($"The model {modelId} does not support embeddings in HuggingFace");
                }
            }, "CreateEmbedding", cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateImage");
            
            return await ExecuteApiRequestAsync(async () =>
            {
                using var client = CreateHttpClient(apiKey);
                
                // Determine if model is image generation model
                string modelId = request.Model ?? ProviderModelId;
                
                if (modelId.Contains("stable-diffusion", StringComparison.OrdinalIgnoreCase) ||
                    modelId.Contains("sdxl", StringComparison.OrdinalIgnoreCase) ||
                    modelId.Contains("dall-e", StringComparison.OrdinalIgnoreCase))
                {
                    // Send image generation request to HuggingFace API
                    string endpoint = GetModelEndpoint(modelId);
                    
                    // Create image generation request
                    var hfRequest = new HuggingFaceImageGenerationRequest
                    {
                        Inputs = request.Prompt,
                        Options = new HuggingFaceOptions
                        {
                            WaitForModel = true
                        }
                    };
                    
                    Logger.LogDebug("Sending image generation request to HuggingFace at {Endpoint} for model {Model}",
                        endpoint, modelId);
                    
                    // Send request
                    var response = await HttpClientHelper.SendRawRequestAsync(
                        client,
                        HttpMethod.Post,
                        endpoint,
                        hfRequest,
                        CreateStandardHeaders(apiKey),
                        DefaultJsonOptions,
                        Logger,
                        cancellationToken);
                    
                    // Process response
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        Logger.LogError("HuggingFace API image generation request failed with status code {StatusCode}. Response: {ErrorContent}",
                            response.StatusCode, errorContent);
                        throw new LLMCommunicationException(
                            $"HuggingFace API image generation request failed with status code {response.StatusCode}. Response: {errorContent}");
                    }
                    
                    // HuggingFace returns the image directly
                    var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    string base64Image = Convert.ToBase64String(imageBytes);
                    
                    // Create response
                    var imageResponse = new ImageGenerationResponse
                    {
                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Data = new List<ImageData>
                        {
                            new ImageData
                            {
                                B64Json = base64Image
                            }
                        }
                    };
                    
                    return imageResponse;
                }
                else
                {
                    throw new UnsupportedProviderException($"The model {modelId} does not support image generation in HuggingFace");
                }
            }, "CreateImage", cancellationToken);
        }
        
        #region Helper Methods
        
        /// <summary>
        /// Gets the endpoint for a specific model.
        /// </summary>
        /// <param name="modelId">The model identifier.</param>
        /// <returns>The endpoint for the model.</returns>
        private string GetModelEndpoint(string modelId)
        {
            // HuggingFace endpoints are just the model ID
            return modelId;
        }
        
        /// <summary>
        /// Formats chat messages into a format suitable for HuggingFace models.
        /// </summary>
        /// <param name="messages">The list of messages to format.</param>
        /// <returns>A formatted string representing the conversation.</returns>
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
                formattedChat.AppendLine(ContentHelper.GetContentAsString(systemMessage.Content, Logger));
                formattedChat.AppendLine();
            }
            
            // Format the conversation
            foreach (var message in messages.Where(m => 
                !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
            {
                string role = message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                    ? "Assistant"
                    : "Human";
                    
                formattedChat.AppendLine($"{role}: {ContentHelper.GetContentAsString(message.Content, Logger)}");
            }
            
            // Add final prompt for assistant
            formattedChat.Append("Assistant: ");
            
            return formattedChat.ToString();
        }
        
        /// <summary>
        /// Processes a JSON response from the HuggingFace API.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        /// <param name="originalModelAlias">The original model alias.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A chat completion response.</returns>
        private async Task<ChatCompletionResponse> ProcessJsonResponseAsync(
            HttpResponseMessage response,
            string? originalModelAlias,
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
        
        /// <summary>
        /// Creates a chat completion response.
        /// </summary>
        /// <param name="model">The model name.</param>
        /// <param name="content">The response content.</param>
        /// <returns>A chat completion response.</returns>
        private ChatCompletionResponse CreateChatCompletionResponse(string? model, string content)
        {
            // Estimate token counts based on text length
            int estimatedPromptTokens = EstimateTokenCount(content);
            int estimatedCompletionTokens = EstimateTokenCount(content);
            
            return new ChatCompletionResponse
            {
                Id = Guid.NewGuid().ToString(),
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = model ?? ProviderModelId,
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
        
        /// <summary>
        /// Estimates the token count from text.
        /// </summary>
        /// <param name="text">The text to estimate tokens for.</param>
        /// <returns>An estimated token count.</returns>
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
    }
}