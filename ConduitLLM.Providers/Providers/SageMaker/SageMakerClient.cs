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
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.Common.Models;
using ConduitLLM.Providers.Providers.SageMaker.Models.SageMakerModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.SageMaker
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
            Provider provider,
            ProviderKeyCredential keyCredential,
            string endpointName,
            ILogger<SageMakerClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                  provider,
                  keyCredential,
                  endpointName,
                  logger,
                  httpClientFactory,
                  "sagemaker",
                  defaultModels)
        {
            _endpointName = endpointName ?? throw new ArgumentNullException(nameof(endpointName));
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
            string region = Provider.BaseUrl ?? "us-east-1";
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

                // Sign the request with AWS Signature V4
                var effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : PrimaryKeyCredential.ApiKey!;
                var effectiveSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "dummy-secret-key";
                var region = Provider.BaseUrl ?? "us-east-1";
                AwsSignatureV4.SignRequest(requestMessage, effectiveApiKey, effectiveSecretKey, region, "sagemaker");

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

        #region Authentication Verification

        /// <summary>
        /// Verifies AWS SageMaker authentication by describing the endpoint.
        /// This is a free API call that validates both AWS credentials and endpoint existence.
        /// </summary>
        public override async Task<Core.Interfaces.AuthenticationResult> VerifyAuthenticationAsync(
            string? apiKey = null,
            string? baseUrl = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : PrimaryKeyCredential.ApiKey;
                var effectiveSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "dummy-secret-key"; // Fallback for backward compatibility
                var effectiveRegion = !string.IsNullOrWhiteSpace(baseUrl) ? baseUrl : (Provider.BaseUrl ?? "us-east-1");
                
                if (string.IsNullOrWhiteSpace(effectiveApiKey))
                {
                    return Core.Interfaces.AuthenticationResult.Failure("AWS Access Key ID is required");
                }

                if (string.IsNullOrWhiteSpace(_endpointName))
                {
                    return Core.Interfaces.AuthenticationResult.Failure("SageMaker endpoint name is required");
                }

                using var client = CreateHttpClient(effectiveApiKey);
                
                // Use the DescribeEndpoint API which is free and verifies the endpoint exists
                var request = new HttpRequestMessage(HttpMethod.Get, 
                    $"https://runtime.sagemaker.{effectiveRegion}.amazonaws.com/endpoints/{_endpointName}");
                
                // Add required headers
                request.Headers.Add("User-Agent", "ConduitLLM");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                
                // Sign the request with AWS Signature V4
                AwsSignatureV4.SignRequest(request, effectiveApiKey, effectiveSecretKey, effectiveRegion, "sagemaker");
                
                var response = await client.SendAsync(request, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                if (response.IsSuccessStatusCode)
                {
                    return Core.Interfaces.AuthenticationResult.Success(
                        $"Endpoint '{_endpointName}' verified. Response time: {responseTime:F0}ms");
                }
                
                // Check for specific error codes
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return Core.Interfaces.AuthenticationResult.Failure(
                        $"SageMaker endpoint '{_endpointName}' not found");
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "Invalid AWS credentials or insufficient permissions to access SageMaker");
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return Core.Interfaces.AuthenticationResult.Failure("Invalid AWS signature or credentials");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"AWS SageMaker authentication failed: {response.StatusCode}",
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
                Logger.LogError(ex, "Unexpected error during SageMaker authentication verification");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        #endregion

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


        #endregion
    }
}
