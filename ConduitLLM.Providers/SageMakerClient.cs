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
using ConduitLLM.Providers.InternalModels.SageMakerModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with AWS SageMaker endpoints.
    /// </summary>
    public class SageMakerClient : BaseLLMClient
    {
        private readonly string _endpointName;
        private readonly string _region;

        /// <summary>
        /// Initializes a new instance of the <see cref="SageMakerClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials.</param>
        /// <param name="endpointName">The SageMaker endpoint name.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory.</param>
        public SageMakerClient(
            ProviderCredentials credentials,
            string endpointName,
            ILogger<SageMakerClient> logger,
            IHttpClientFactory? httpClientFactory = null)
            : base(
                  EnsureSageMakerCredentials(credentials),
                  endpointName, // Use endpoint name as the model ID
                  logger,
                  httpClientFactory,
                  "sagemaker")
        {
            _endpointName = endpointName;
            _region = string.IsNullOrWhiteSpace(credentials.ApiBase) ? "us-east-1" : credentials.ApiBase;
        }

        private static ProviderCredentials EnsureSageMakerCredentials(ProviderCredentials credentials)
        {
            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            if (string.IsNullOrWhiteSpace(credentials.ApiKey))
            {
                throw new ConfigurationException("AWS Access Key ID (ApiKey) is missing for AWS SageMaker provider.");
            }

            if (string.IsNullOrWhiteSpace(credentials.ApiSecret))
            {
                throw new ConfigurationException("AWS Secret Access Key (ApiSecret) is missing for AWS SageMaker provider.");
            }

            if (string.IsNullOrWhiteSpace(credentials.ApiBase))
            {
                throw new ConfigurationException("AWS Region (ApiBase) is missing for AWS SageMaker provider.");
            }

            return credentials;
        }

        /// <inheritdoc />
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
            
            // For AWS, we don't add the standard Authorization header
            // Instead, we'll handle AWS Signature V4 auth per request
            client.DefaultRequestHeaders.Authorization = null;
            
            // Set the base address to the SageMaker runtime endpoint
            string runtimeEndpoint = GetSageMakerRuntimeEndpoint(_region);
            client.BaseAddress = new Uri(runtimeEndpoint);
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
                
                // Convert the core chat request to SageMaker format
                var sageMakerRequest = MapToSageMakerRequest(request);
                
                // In a real implementation, use AWS SDK with AWS Signature v4
                string endpoint = $"/endpoints/{_endpointName}/invocations";
                
                Logger.LogDebug("Sending chat completion request to SageMaker at {Endpoint} for endpoint {EndpointName}",
                    endpoint, _endpointName);
                
                // Send request using HttpClientHelper
                // In production, we would add AWS Signature V4 auth to these requests
                var response = await HttpClientHelper.SendRawRequestAsync(
                    client,
                    HttpMethod.Post,
                    endpoint,
                    sageMakerRequest,
                    CreateAWSAuthHeaders(endpoint, JsonSerializer.Serialize(sageMakerRequest), apiKey),
                    DefaultJsonOptions,
                    Logger,
                    cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    Logger.LogError("SageMaker API request failed with status code {StatusCode}. Response: {ErrorContent}",
                        response.StatusCode, errorContent);
                    throw new LLMCommunicationException(
                        $"SageMaker API request failed with status code {response.StatusCode}. Response: {errorContent}");
                }
                
                var sageMakerResponse = await JsonSerializer.DeserializeAsync<SageMakerChatResponse>(
                    await response.Content.ReadAsStreamAsync(cancellationToken),
                    DefaultJsonOptions,
                    cancellationToken);
                
                if (sageMakerResponse?.GeneratedOutputs == null || !sageMakerResponse.GeneratedOutputs.Any())
                {
                    Logger.LogError("Failed to deserialize the response from SageMaker or response is empty");
                    throw new LLMCommunicationException("Failed to deserialize the response from SageMaker or response is empty");
                }
                
                // Map to core response format
                return new ChatCompletionResponse
                {
                    Id = Guid.NewGuid().ToString(),
                    Object = "chat.completion",
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Model = request.Model ?? ProviderModelId, // Return the requested model alias
                    Choices = new List<Choice>
                    {
                        new Choice
                        {
                            Index = 0,
                            Message = new Message
                            {
                                Role = sageMakerResponse.GeneratedOutputs.FirstOrDefault()?.Role ?? "assistant",
                                Content = sageMakerResponse.GeneratedOutputs.FirstOrDefault()?.Content ?? string.Empty
                            },
                            FinishReason = "stop" // SageMaker doesn't provide finish reason in this format
                        }
                    },
                    Usage = new Usage
                    {
                        // SageMaker doesn't provide token usage, so estimate based on text length
                        // This is a very rough approximation
                        PromptTokens = EstimateTokenCount(string.Join(" ", request.Messages.Select(m => ContentHelper.GetContentAsString(m.Content, Logger)))),
                        CompletionTokens = EstimateTokenCount(sageMakerResponse.GeneratedOutputs.FirstOrDefault()?.Content ?? string.Empty),
                        TotalTokens = 0 // Will be calculated below
                    }
                };
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
                Logger.LogInformation("Streaming is not natively supported in SageMaker client. Simulating streaming.");
                
                // SageMaker doesn't support streaming directly
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
                });
                
                return chunks;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogError(ex, "Error in simulated streaming chat completion from SageMaker: {Message}", ex.Message);
                throw new LLMCommunicationException($"Error in simulated streaming chat completion from SageMaker: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Listing SageMaker endpoints is not directly supported through this interface. Returning endpoint name.");
            
            // In a production implementation, we would use the AWS SDK to list endpoints
            // For this sample, we'll just return the configured endpoint
            await Task.Delay(1, cancellationToken); // Adding await to make this truly async
            
            return new List<ExtendedModelInfo>
            {
                ExtendedModelInfo.Create(_endpointName, ProviderName, _endpointName)
            };
        }

        /// <inheritdoc />
        public override async Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateEmbedding");
            
            // SageMaker can support embeddings, but it depends on the specific model deployed
            // For now, we'll throw a not implemented exception
            throw new NotSupportedException("Embeddings support for SageMaker is not yet implemented");
        }

        /// <inheritdoc />
        public override async Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateImage");
            
            // SageMaker can support image generation, but it depends on the specific model deployed
            // For now, we'll throw a not implemented exception
            throw new NotSupportedException("Image generation support for SageMaker is not yet implemented");
        }
        
        #region Helper Methods
        
        /// <summary>
        /// Maps a chat completion request to a SageMaker request.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <returns>A SageMaker request.</returns>
        private SageMakerRequest MapToSageMakerRequest(ChatCompletionRequest request)
        {
            var sageMakerRequest = new SageMakerRequest
            {
                Inputs = FormatMessages(request.Messages),
                Parameters = new SageMakerParameters
                {
                    Temperature = request.Temperature,
                    MaxNewTokens = request.MaxTokens,
                    TopP = request.TopP,
                    ReturnFullText = false // Don't echo the prompt
                }
            };
            
            return sageMakerRequest;
        }
        
        /// <summary>
        /// Formats a list of messages into a string format suitable for SageMaker.
        /// </summary>
        /// <param name="messages">The messages to format.</param>
        /// <returns>A formatted string.</returns>
        private string FormatMessages(List<Message> messages)
        {
            var formatted = new StringBuilder();
            
            // Extract system message first
            var systemMessage = messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
            if (systemMessage != null)
            {
                formatted.AppendLine(ContentHelper.GetContentAsString(systemMessage.Content, Logger));
                formatted.AppendLine();
            }
            
            // Format user/assistant conversation
            foreach (var message in messages.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
            {
                string role = message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                    ? "Assistant"
                    : "Human";
                
                formatted.AppendLine($"{role}: {ContentHelper.GetContentAsString(message.Content, Logger)}");
            }
            
            return formatted.ToString().Trim();
        }
        
        /// <summary>
        /// Estimates the token count from text.
        /// </summary>
        /// <param name="text">The text to estimate tokens for.</param>
        /// <returns>An estimated token count.</returns>
        private int EstimateTokenCount(string text)
        {
            // Very rough token count estimation
            // In a real implementation, use a proper tokenizer
            if (string.IsNullOrEmpty(text))
                return 0;
                
            // Approximately 4 characters per token for English text
            return Math.Max(1, text.Length / 4);
        }
        
        /// <summary>
        /// Gets the SageMaker runtime endpoint URL.
        /// </summary>
        /// <param name="region">The AWS region.</param>
        /// <returns>The SageMaker runtime endpoint URL.</returns>
        private string GetSageMakerRuntimeEndpoint(string region)
        {
            // Ensure region is not null
            if (string.IsNullOrWhiteSpace(region))
            {
                throw new ArgumentNullException(nameof(region), "AWS region cannot be null or empty");
            }
            
            // Format the SageMaker runtime endpoint URL
            return $"https://runtime.sagemaker.{region}.amazonaws.com";
        }
        
        /// <summary>
        /// Creates headers for AWS authentication.
        /// </summary>
        /// <param name="path">The API path.</param>
        /// <param name="body">The request body.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <returns>A dictionary containing headers for AWS authentication.</returns>
        /// <remarks>
        /// In a real implementation, this would create AWS Signature V4 headers.
        /// For simplicity, this implementation returns placeholder headers.
        /// </remarks>
        private Dictionary<string, string> CreateAWSAuthHeaders(string path, string body, string? apiKey = null)
        {
            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : Credentials.ApiKey!;
            
            // In a real implementation, this would create AWS Signature V4 headers
            // For simplicity, this returns placeholder headers
            var headers = new Dictionary<string, string>
            {
                ["User-Agent"] = "ConduitLLM",
                ["X-Amz-Date"] = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ"),
                ["Authorization"] = $"AWS4-HMAC-SHA256 Credential={effectiveApiKey}"
                // In a real implementation, this would include a proper signature
            };
            
            return headers;
        }
        
        #endregion
    }
}