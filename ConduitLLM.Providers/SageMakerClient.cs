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

        /// <summary>
        /// Initializes a new instance of the <see cref="SageMakerClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials.</param>
        /// <param name="endpointName">The SageMaker endpoint name.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public SageMakerClient(
            ProviderCredentials credentials,
            string endpointName,
            ILogger<SageMakerClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                  EnsureSageMakerCredentials(credentials),
                  endpointName,
                  logger,
                  httpClientFactory,
                  "sagemaker",
                  defaultModels)
        {
            _endpointName = endpointName ?? throw new ArgumentNullException(nameof(endpointName));
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
            
            // For AWS SageMaker, we don't add the standard Authorization header
            // Instead, we'll handle AWS Signature V4 auth per request
            client.DefaultRequestHeaders.Authorization = null;
            
            // Set the base address to the SageMaker runtime endpoint
            string region = Credentials.ApiBase ?? "us-east-1";
            client.BaseAddress = new Uri(GetSageMakerRuntimeEndpoint(region));
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
                // Convert the core chat request to SageMaker format
                var sageMakerRequest = MapToSageMakerRequest(request);
                
                using var client = CreateHttpClient(apiKey);
                string apiUrl = $"/endpoints/{_endpointName}/invocations";
                
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                requestMessage.Content = JsonContent.Create(sageMakerRequest, options: new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                
                // Add AWS signature headers
                AddAwsAuthenticationHeaders(requestMessage, JsonSerializer.Serialize(sageMakerRequest), apiKey);
                
                using var response = await client.SendAsync(requestMessage, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await ReadErrorContentAsync(response, cancellationToken);
                    Logger.LogError("AWS SageMaker API request failed with status code {StatusCode}. Response: {ErrorContent}",
                        response.StatusCode, errorContent);
                    throw new LLMCommunicationException(
                        $"AWS SageMaker API request failed with status code {response.StatusCode}. Response: {errorContent}");
                }
                
                var sageMakerResponse = await response.Content.ReadFromJsonAsync<SageMakerChatResponse>(
                    cancellationToken: cancellationToken);
                
                if (sageMakerResponse?.GeneratedOutputs == null || !sageMakerResponse.GeneratedOutputs.Any())
                {
                    Logger.LogError("Failed to deserialize the response from AWS SageMaker or response is empty");
                    throw new LLMCommunicationException("Failed to deserialize the response from AWS SageMaker or response is empty");
                }
                
                // Map to core response format
                var result = new ChatCompletionResponse
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
                        PromptTokens = EstimateTokenCount(string.Join(" ", request.Messages.Select(m => ContentHelper.GetContentAsString(m.Content)))),
                        CompletionTokens = EstimateTokenCount(sageMakerResponse.GeneratedOutputs.FirstOrDefault()?.Content ?? string.Empty),
                        TotalTokens = 0 // Will be calculated below
                    }
                };
                
                // Calculate total tokens
                result.Usage.TotalTokens = result.Usage.PromptTokens + result.Usage.CompletionTokens;
                
                return result;
            }, "ChatCompletion", cancellationToken);
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletion");
            
            Logger.LogInformation("Streaming is not natively supported by SageMaker endpoints. Simulating streaming.");
            
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
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new UnsupportedProviderException($"Embeddings are not supported in the SageMaker client for endpoint {_endpointName}.");
        }

        /// <inheritdoc />
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new UnsupportedProviderException($"Image generation is not supported in the SageMaker client for endpoint {_endpointName}.");
        }

        #region Helper Methods

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
        
        private string FormatMessages(List<Message> messages)
        {
            var formatted = new StringBuilder();
            
            // Extract system message first
            var systemMessage = messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
            if (systemMessage != null)
            {
                formatted.AppendLine(ContentHelper.GetContentAsString(systemMessage.Content));
                formatted.AppendLine();
            }
            
            // Format user/assistant conversation
            foreach (var message in messages.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
            {
                string role = message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                    ? "Assistant"
                    : "Human";
                
                formatted.AppendLine($"{role}: {ContentHelper.GetContentAsString(message.Content)}");
            }
            
            return formatted.ToString().Trim();
        }
        
        private int EstimateTokenCount(string text)
        {
            // Very rough token count estimation
            // In a real implementation, use a proper tokenizer
            if (string.IsNullOrEmpty(text))
                return 0;
                
            // Approximately 4 characters per token for English text
            return text.Length / 4;
        }
        
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
        
        private void AddAwsAuthenticationHeaders(HttpRequestMessage request, string requestBody, string? apiKey = null)
        {
            // In a real implementation, this would add AWS Signature V4 authentication headers
            // For simplicity, we're not implementing the full AWS authentication here
            
            // Placeholder for AWS signature implementation
            // In production, use AWS SDK for .NET or implement AWS Signature V4
            
            // For demo purpose, we'll just add placeholder headers
            request.Headers.Add("X-Amz-Date", DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ"));
            
            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : Credentials.ApiKey!;
            request.Headers.Add("Authorization", $"AWS4-HMAC-SHA256 Credential={effectiveApiKey}");
        }
        
        #endregion
    }
}