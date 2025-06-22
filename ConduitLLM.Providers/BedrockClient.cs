using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.InternalModels;
using ConduitLLM.Providers.InternalModels.BedrockModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with AWS Bedrock API.
    /// </summary>
    public class BedrockClient : BaseLLMClient
    {
        private const string DefaultBedrockApiBase = "https://api.bedrock.amazonaws.com";
        private readonly string _region;
        
        // JSON serialization options for Bedrock API
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="BedrockClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials.</param>
        /// <param name="providerModelId">The provider's model identifier.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public BedrockClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger<BedrockClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                  EnsureBedrockCredentials(credentials),
                  providerModelId,
                  logger,
                  httpClientFactory,
                  "bedrock",
                  defaultModels)
        {
            // Extract region from credentials.ApiBase or use default
            // ApiBase in this case is treated as the AWS region
            _region = string.IsNullOrWhiteSpace(credentials.ApiBase) ? "us-east-1" : credentials.ApiBase;
        }

        private static ProviderCredentials EnsureBedrockCredentials(ProviderCredentials credentials)
        {
            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            if (string.IsNullOrWhiteSpace(credentials.ApiKey))
            {
                throw new ConfigurationException("AWS Access Key is required for Bedrock API");
            }

            // Note: In a real implementation, we would check for AWS Secret Key
            // For now, we'll assume it's available via environment variables
            // We only check for ApiKey which maps to AWS Access Key ID

            return credentials;
        }

        /// <inheritdoc />
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");

            // For Bedrock, we don't add the standard Authorization header
            // Instead, we'll handle AWS Signature V4 auth per request
            client.DefaultRequestHeaders.Authorization = null;

            string apiBase = string.IsNullOrWhiteSpace(Credentials.ApiBase) ? DefaultBedrockApiBase : Credentials.ApiBase;
            client.BaseAddress = new Uri(apiBase);
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
                // Determine which model provider is being used
                string modelId = request.Model ?? ProviderModelId;

                if (modelId.Contains("anthropic.claude", StringComparison.OrdinalIgnoreCase))
                {
                    return await CreateAnthropicClaudeChatCompletionAsync(request, apiKey, cancellationToken);
                }
                else if (modelId.Contains("meta.llama", StringComparison.OrdinalIgnoreCase))
                {
                    return await CreateMetaLlamaChatCompletionAsync(request, apiKey, cancellationToken);
                }
                else if (modelId.Contains("amazon.titan", StringComparison.OrdinalIgnoreCase))
                {
                    return await CreateAmazonTitanChatCompletionAsync(request, apiKey, cancellationToken);
                }
                else if (modelId.Contains("cohere.command", StringComparison.OrdinalIgnoreCase))
                {
                    return await CreateCohereChatCompletionAsync(request, apiKey, cancellationToken);
                }
                else if (modelId.Contains("ai21", StringComparison.OrdinalIgnoreCase))
                {
                    return await CreateAI21ChatCompletionAsync(request, apiKey, cancellationToken);
                }
                else
                {
                    throw new UnsupportedProviderException($"Unsupported Bedrock model: {modelId}");
                }
            }, "ChatCompletion", cancellationToken);
        }

        private async Task<ChatCompletionResponse> CreateAnthropicClaudeChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Map to Bedrock Claude format
            var claudeRequest = new BedrockClaudeChatRequest
            {
                MaxTokens = request.MaxTokens,
                Temperature = (float?)request.Temperature,
                TopP = request.TopP.HasValue ? (float?)request.TopP.Value : null,
                Messages = new List<BedrockClaudeMessage>()
            };

            // Extract system message if present
            var systemMessage = request.Messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
            if (systemMessage != null)
            {
                // Handle system message content, which could be string or content parts
                claudeRequest.System = ContentHelper.GetContentAsString(systemMessage.Content);
            }

            // Map user and assistant messages
            foreach (var message in request.Messages.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
            {
                claudeRequest.Messages.Add(new BedrockClaudeMessage
                {
                    Role = message.Role.ToLowerInvariant() switch
                    {
                        "user" => "user",
                        "assistant" => "assistant",
                        _ => message.Role // Keep as-is for other roles
                    },
                    Content = new List<BedrockClaudeContent>
                    {
                        new BedrockClaudeContent { Type = "text", Text = ContentHelper.GetContentAsString(message.Content) }
                    }
                });
            }

            string modelId = request.Model ?? ProviderModelId;
            using var client = CreateHttpClient(Credentials.ApiKey);

            // In a real implementation, we'd use AWS SDK, but for demonstration we'll use HTTP
            string apiUrl = $"/model/{modelId}/invoke";

            // Use our common HTTP client helper to send the request
            // Note: In production, we would add AWS Signature V4 auth to these requests
            var bedrockResponse = await Core.Utilities.HttpClientHelper.SendJsonRequestAsync<BedrockClaudeChatRequest, BedrockClaudeChatResponse>(
                client,
                HttpMethod.Post,
                apiUrl,
                claudeRequest,
                CreateAWSAuthHeaders(apiUrl, JsonSerializer.Serialize(claudeRequest), apiKey),
                DefaultJsonOptions,
                Logger,
                cancellationToken);

            if (bedrockResponse == null || bedrockResponse.Content == null || !bedrockResponse.Content.Any())
            {
                throw new LLMCommunicationException("Failed to deserialize the response from AWS Bedrock API or response content is empty");
            }

            // Map to core response format
            return new ChatCompletionResponse
            {
                Id = bedrockResponse.Id ?? Guid.NewGuid().ToString(),
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model ?? ProviderModelId,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new ConduitLLM.Core.Models.Message
                        {
                            Role = bedrockResponse.Role ?? "assistant",
                            Content = bedrockResponse.Content.FirstOrDefault()?.Text ?? string.Empty
                        },
                        FinishReason = MapBedrockStopReason(bedrockResponse.StopReason)
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = bedrockResponse.Usage?.InputTokens ?? 0,
                    CompletionTokens = bedrockResponse.Usage?.OutputTokens ?? 0,
                    TotalTokens = (bedrockResponse.Usage?.InputTokens ?? 0) + (bedrockResponse.Usage?.OutputTokens ?? 0)
                }
            };
        }

        private async Task<ChatCompletionResponse> CreateMetaLlamaChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Map to Bedrock Llama format
            var llamaRequest = new BedrockLlamaChatRequest
            {
                Prompt = BuildLlamaPrompt(request.Messages),
                MaxGenLen = request.MaxTokens ?? 512,
                Temperature = request.Temperature.HasValue ? (float)request.Temperature.Value : 0.7f,
                TopP = request.TopP.HasValue ? (float)request.TopP.Value : 0.9f
            };

            string modelId = request.Model ?? ProviderModelId;
            using var client = CreateHttpClient(Credentials.ApiKey);
            
            string apiUrl = $"/model/{modelId}/invoke";
            
            var bedrockResponse = await Core.Utilities.HttpClientHelper.SendJsonRequestAsync<BedrockLlamaChatRequest, BedrockLlamaChatResponse>(
                client,
                HttpMethod.Post,
                apiUrl,
                llamaRequest,
                CreateAWSAuthHeaders(apiUrl, JsonSerializer.Serialize(llamaRequest), apiKey),
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                },
                Logger,
                cancellationToken
            );

            // Map to standard format
            return new ChatCompletionResponse
            {
                Id = $"bedrock-{Guid.NewGuid()}",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new ConduitLLM.Core.Models.Message
                        {
                            Role = "assistant",
                            Content = bedrockResponse.Generation ?? string.Empty
                        },
                        FinishReason = MapLlamaStopReason(bedrockResponse.StopReason),
                        Logprobs = null
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = bedrockResponse.PromptTokenCount ?? 0,
                    CompletionTokens = bedrockResponse.GenerationTokenCount ?? 0,
                    TotalTokens = (bedrockResponse.PromptTokenCount ?? 0) + 
                                  (bedrockResponse.GenerationTokenCount ?? 0)
                },
                SystemFingerprint = null
            };
        }

        private async Task<ChatCompletionResponse> CreateAmazonTitanChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Map to Bedrock Titan format
            var titanRequest = new BedrockTitanChatRequest
            {
                InputText = BuildPrompt(request.Messages),
                TextGenerationConfig = new BedrockTitanTextGenerationConfig
                {
                    MaxTokenCount = request.MaxTokens ?? 512,
                    Temperature = request.Temperature.HasValue ? (float)request.Temperature.Value : 0.7f,
                    TopP = request.TopP.HasValue ? (float)request.TopP.Value : 0.9f,
                    StopSequences = request.Stop?.ToList()
                }
            };

            string modelId = request.Model ?? ProviderModelId;
            using var client = CreateHttpClient(Credentials.ApiKey);
            
            string apiUrl = $"/model/{modelId}/invoke";
            
            var bedrockResponse = await Core.Utilities.HttpClientHelper.SendJsonRequestAsync<BedrockTitanChatRequest, BedrockTitanChatResponse>(
                client,
                HttpMethod.Post,
                apiUrl,
                titanRequest,
                CreateAWSAuthHeaders(apiUrl, JsonSerializer.Serialize(titanRequest), apiKey),
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                },
                Logger,
                cancellationToken
            );

            // Get the first result
            var result = bedrockResponse.Results?.FirstOrDefault();
            var responseText = result?.OutputText ?? string.Empty;
            var completionReason = result?.CompletionReason ?? "COMPLETE";

            // Map to standard format
            return new ChatCompletionResponse
            {
                Id = $"bedrock-{Guid.NewGuid()}",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new ConduitLLM.Core.Models.Message
                        {
                            Role = "assistant",
                            Content = responseText
                        },
                        FinishReason = MapTitanCompletionReason(completionReason),
                        Logprobs = null
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = bedrockResponse.InputTextTokenCount ?? 0,
                    CompletionTokens = result?.TokenCount ?? 0,
                    TotalTokens = (bedrockResponse.InputTextTokenCount ?? 0) + (result?.TokenCount ?? 0)
                },
                SystemFingerprint = null
            };
        }

        private async Task<ChatCompletionResponse> CreateCohereChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Map to Bedrock Cohere format
            var cohereRequest = new BedrockCohereChatRequest
            {
                Prompt = BuildCoherePrompt(request.Messages),
                MaxTokens = request.MaxTokens ?? 1024,
                Temperature = request.Temperature.HasValue ? (float)request.Temperature.Value : 0.7f,
                P = request.TopP.HasValue ? (float)request.TopP.Value : 0.9f,
                K = 0, // Default top-k
                StopSequences = request.Stop?.ToList(),
                Stream = false
            };

            string modelId = request.Model ?? ProviderModelId;
            using var client = CreateHttpClient(Credentials.ApiKey);
            
            string apiUrl = $"/model/{modelId}/invoke";
            
            var bedrockResponse = await Core.Utilities.HttpClientHelper.SendJsonRequestAsync<BedrockCohereChatRequest, BedrockCohereChatResponse>(
                client,
                HttpMethod.Post,
                apiUrl,
                cohereRequest,
                CreateAWSAuthHeaders(apiUrl, JsonSerializer.Serialize(cohereRequest), apiKey),
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                },
                Logger,
                cancellationToken
            );

            // Map to standard format
            return new ChatCompletionResponse
            {
                Id = bedrockResponse.GenerationId ?? $"bedrock-{Guid.NewGuid()}",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new ConduitLLM.Core.Models.Message
                        {
                            Role = "assistant",
                            Content = bedrockResponse.Text ?? string.Empty
                        },
                        FinishReason = MapCohereStopReason(bedrockResponse.FinishReason),
                        Logprobs = null
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = bedrockResponse.Meta?.BilledUnits?.InputTokens ?? 0,
                    CompletionTokens = bedrockResponse.Meta?.BilledUnits?.OutputTokens ?? 0,
                    TotalTokens = (bedrockResponse.Meta?.BilledUnits?.InputTokens ?? 0) + 
                                  (bedrockResponse.Meta?.BilledUnits?.OutputTokens ?? 0)
                },
                SystemFingerprint = null
            };
        }

        private async Task<ChatCompletionResponse> CreateAI21ChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Map to Bedrock AI21 format
            var ai21Request = new BedrockAI21ChatRequest
            {
                Prompt = BuildPrompt(request.Messages),
                MaxTokens = request.MaxTokens ?? 1024,
                Temperature = request.Temperature.HasValue ? (float)request.Temperature.Value : 0.7f,
                TopP = request.TopP.HasValue ? (float)request.TopP.Value : 1.0f,
                StopSequences = request.Stop?.ToList()
            };

            // Add penalties if specified
            if (request.FrequencyPenalty.HasValue)
            {
                ai21Request.CountPenalty = new BedrockAI21Penalty { Scale = (float)request.FrequencyPenalty.Value };
            }
            if (request.PresencePenalty.HasValue)
            {
                ai21Request.PresencePenalty = new BedrockAI21Penalty { Scale = (float)request.PresencePenalty.Value };
            }

            string modelId = request.Model ?? ProviderModelId;
            using var client = CreateHttpClient(Credentials.ApiKey);
            
            string apiUrl = $"/model/{modelId}/invoke";
            
            var bedrockResponse = await Core.Utilities.HttpClientHelper.SendJsonRequestAsync<BedrockAI21ChatRequest, BedrockAI21ChatResponse>(
                client,
                HttpMethod.Post,
                apiUrl,
                ai21Request,
                CreateAWSAuthHeaders(apiUrl, JsonSerializer.Serialize(ai21Request), apiKey),
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                },
                Logger,
                cancellationToken
            );

            // Get the first completion
            var completion = bedrockResponse.Completions?.FirstOrDefault();
            var responseText = completion?.Data?.Text ?? string.Empty;
            var finishReason = completion?.FinishReason?.Reason ?? "stop";

            // AI21 doesn't provide token counts in the response, so estimate them
            var promptTokens = EstimateTokenCount(BuildPrompt(request.Messages));
            var completionTokens = EstimateTokenCount(responseText);

            // Map to standard format
            return new ChatCompletionResponse
            {
                Id = bedrockResponse.Id ?? $"bedrock-{Guid.NewGuid()}",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new ConduitLLM.Core.Models.Message
                        {
                            Role = "assistant",
                            Content = responseText
                        },
                        FinishReason = MapAI21FinishReason(finishReason),
                        Logprobs = null
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = promptTokens + completionTokens
                },
                SystemFingerprint = null
            };
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
                var modelId = request.Model ?? ProviderModelId;

                // For proper implementation, we would use AWS SDK with InvokeModelWithResponseStreamAsync
                // This is a placeholder for the implementation
                // In a real implementation, we would:

                var config = new AmazonBedrockRuntimeConfig
                {
                    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_region)
                };

                // Use AWS credentials from configuration
                // In a real implementation, we would use AWS credentials properly
                // For this example, we'll just use the ApiKey 
                // In a real implementation, ApiSecret would be provided or retrieved from 
                // AWS credential chain (environment variables, profile, etc.)
                using var client = new AmazonBedrockRuntimeClient(
                    Credentials.ApiKey,
                    "dummy-secret-key", // This is a placeholder for illustration
                    config);

                // Create a request appropriate for the model type
                // Example for Claude
                var bedrockRequest = new BedrockClaudeChatRequest
                {
                    MaxTokens = request.MaxTokens,
                    Temperature = (float?)request.Temperature,
                    TopP = request.TopP.HasValue ? (float?)request.TopP.Value : null,
                    Stream = true,
                    Messages = new List<BedrockClaudeMessage>()
                };

                // Extract system message if present
                var systemMessage = request.Messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
                if (systemMessage != null)
                {
                    bedrockRequest.System = ContentHelper.GetContentAsString(systemMessage.Content);
                }

                // Map user and assistant messages
                foreach (var message in request.Messages.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
                {
                    bedrockRequest.Messages.Add(new BedrockClaudeMessage
                    {
                        Role = message.Role.ToLowerInvariant() switch
                        {
                            "user" => "user",
                            "assistant" => "assistant",
                            _ => message.Role // Keep as-is for other roles
                        },
                        Content = new List<BedrockClaudeContent>
                        {
                            new BedrockClaudeContent { Type = "text", Text = ContentHelper.GetContentAsString(message.Content) }
                        }
                    });
                }

                var requestBody = JsonSerializer.Serialize(bedrockRequest, DefaultJsonOptions);
                var invokeRequest = new InvokeModelWithResponseStreamRequest
                {
                    ModelId = modelId,
                    Body = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(requestBody)),
                    ContentType = "application/json",
                    Accept = "application/json"
                };

                // For now, use the HTTP client approach like other methods
                // TODO: Implement proper AWS event stream processing when AWS SDK integration is complete
                using var httpClient = CreateHttpClient(Credentials.ApiKey);
                string apiUrl = $"/model/{modelId}/invoke-with-response-stream";

                var streamingResponse = await Core.Utilities.HttpClientHelper.SendStreamingRequestAsync<BedrockClaudeChatRequest>(
                    httpClient,
                    HttpMethod.Post,
                    apiUrl,
                    bedrockRequest,
                    CreateAWSAuthHeaders(apiUrl, JsonSerializer.Serialize(bedrockRequest, JsonOptions), Credentials.ApiKey),
                    JsonOptions,
                    Logger,
                    cancellationToken);

                // Process streaming response
                var responseId = Guid.NewGuid().ToString();
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // This is a simplified streaming implementation
                // In a real AWS integration, you would process the actual event stream format
                Logger.LogWarning("Bedrock streaming is using simplified implementation. Full AWS event stream integration pending.");

                // Add final completion chunk if no chunks were processed
                if (!chunks.Any() || chunks.LastOrDefault()?.Choices?.FirstOrDefault()?.FinishReason == null)
                {
                    chunks.Add(new ChatCompletionChunk
                    {
                        Id = responseId,
                        Object = "chat.completion.chunk",
                        Created = timestamp,
                        Model = modelId,
                        Choices = new List<StreamingChoice>
                        {
                            new StreamingChoice
                            {
                                Index = 0,
                                Delta = new DeltaContent(),
                                FinishReason = "stop"
                            }
                        }
                    });
                }

                return chunks;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogError(ex, "Error in streaming chat completion from Bedrock: {Message}", ex.Message);
                throw new LLMCommunicationException($"Error in streaming chat completion from Bedrock: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Getting models from AWS Bedrock");

            try
            {
                // In a real implementation, we would use AWS SDK to list available models
                // For now, return a static list of commonly available models

                await Task.Delay(1, cancellationToken); // Adding await to make this truly async

                return new List<ExtendedModelInfo>
                {
                    ExtendedModelInfo.Create("anthropic.claude-3-opus-20240229-v1:0", ProviderName, "anthropic.claude-3-opus-20240229-v1:0"),
                    ExtendedModelInfo.Create("anthropic.claude-3-sonnet-20240229-v1:0", ProviderName, "anthropic.claude-3-sonnet-20240229-v1:0"),
                    ExtendedModelInfo.Create("anthropic.claude-3-haiku-20240307-v1:0", ProviderName, "anthropic.claude-3-haiku-20240307-v1:0"),
                    ExtendedModelInfo.Create("anthropic.claude-v2", ProviderName, "anthropic.claude-v2"),
                    ExtendedModelInfo.Create("anthropic.claude-instant-v1", ProviderName, "anthropic.claude-instant-v1"),
                    ExtendedModelInfo.Create("amazon.titan-text-express-v1", ProviderName, "amazon.titan-text-express-v1"),
                    ExtendedModelInfo.Create("amazon.titan-text-lite-v1", ProviderName, "amazon.titan-text-lite-v1"),
                    ExtendedModelInfo.Create("amazon.titan-embed-text-v1", ProviderName, "amazon.titan-embed-text-v1"),
                    ExtendedModelInfo.Create("cohere.command-text-v14", ProviderName, "cohere.command-text-v14"),
                    ExtendedModelInfo.Create("cohere.command-light-text-v14", ProviderName, "cohere.command-light-text-v14"),
                    ExtendedModelInfo.Create("cohere.embed-english-v3", ProviderName, "cohere.embed-english-v3"),
                    ExtendedModelInfo.Create("cohere.embed-multilingual-v3", ProviderName, "cohere.embed-multilingual-v3"),
                    ExtendedModelInfo.Create("meta.llama2-13b-chat-v1", ProviderName, "meta.llama2-13b-chat-v1"),
                    ExtendedModelInfo.Create("meta.llama2-70b-chat-v1", ProviderName, "meta.llama2-70b-chat-v1"),
                    ExtendedModelInfo.Create("meta.llama3-8b-instruct-v1:0", ProviderName, "meta.llama3-8b-instruct-v1:0"),
                    ExtendedModelInfo.Create("meta.llama3-70b-instruct-v1:0", ProviderName, "meta.llama3-70b-instruct-v1:0"),
                    ExtendedModelInfo.Create("ai21.j2-mid-v1", ProviderName, "ai21.j2-mid-v1"),
                    ExtendedModelInfo.Create("ai21.j2-ultra-v1", ProviderName, "ai21.j2-ultra-v1"),
                    ExtendedModelInfo.Create("stability.stable-diffusion-xl-v1", ProviderName, "stability.stable-diffusion-xl-v1")
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to retrieve models from Bedrock API. Returning known models.");
                return GetFallbackModels();
            }
        }

        /// <summary>
        /// Gets a fallback list of models for Bedrock.
        /// </summary>
        /// <returns>A list of commonly available Bedrock models.</returns>
        protected virtual List<ExtendedModelInfo> GetFallbackModels()
        {
            return new List<ExtendedModelInfo>
            {
                ExtendedModelInfo.Create("anthropic.claude-3-opus-20240229-v1:0", ProviderName, "anthropic.claude-3-opus-20240229-v1:0"),
                ExtendedModelInfo.Create("anthropic.claude-3-sonnet-20240229-v1:0", ProviderName, "anthropic.claude-3-sonnet-20240229-v1:0"),
                ExtendedModelInfo.Create("anthropic.claude-3-haiku-20240307-v1:0", ProviderName, "anthropic.claude-3-haiku-20240307-v1:0"),
                ExtendedModelInfo.Create("meta.llama3-8b-instruct-v1:0", ProviderName, "meta.llama3-8b-instruct-v1:0"),
                ExtendedModelInfo.Create("meta.llama3-70b-instruct-v1:0", ProviderName, "meta.llama3-70b-instruct-v1:0")
            };
        }

        /// <inheritdoc />
        public override async Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateEmbedding");

            string modelId = request.Model ?? ProviderModelId;

            if (modelId.Contains("cohere.embed", StringComparison.OrdinalIgnoreCase))
            {
                return await CreateCohereEmbeddingAsync(request, modelId, cancellationToken);
            }
            else if (modelId.Contains("amazon.titan-embed", StringComparison.OrdinalIgnoreCase))
            {
                return await CreateTitanEmbeddingAsync(request, modelId, cancellationToken);
            }
            else
            {
                throw new UnsupportedProviderException($"The model {modelId} does not support embeddings in Bedrock");
            }
        }

        /// <inheritdoc />
        public override async Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateImage");

            string modelId = request.Model ?? ProviderModelId;

            if (modelId.Contains("stability", StringComparison.OrdinalIgnoreCase))
            {
                return await CreateStabilityImageAsync(request, modelId, cancellationToken);
            }
            else
            {
                throw new UnsupportedProviderException($"The model {modelId} does not support image generation in Bedrock");
            }
        }

        #region Helper Methods

        /// <summary>
        /// Maps Bedrock stop reasons to the standardized finish reasons used in the core models.
        /// </summary>
        /// <param name="stopReason">The Bedrock stop reason.</param>
        /// <returns>The standardized finish reason.</returns>
        private string MapBedrockStopReason(string? stopReason)
        {
            return stopReason?.ToLowerInvariant() switch
            {
                "stop_sequence" => "stop",
                "max_tokens" => "length",
                _ => stopReason ?? "unknown"
            };
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

        /// <summary>
        /// Builds a simple prompt from messages for non-chat models.
        /// </summary>
        private string BuildPrompt(IEnumerable<ConduitLLM.Core.Models.Message> messages)
        {
            var promptBuilder = new StringBuilder();
            
            foreach (var message in messages)
            {
                var content = ContentHelper.GetContentAsString(message.Content);
                
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.AppendLine($"System: {content}");
                }
                else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.AppendLine($"Human: {content}");
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.AppendLine($"Assistant: {content}");
                }
            }
            
            // Add prompt for assistant response
            promptBuilder.Append("Assistant:");
            
            return promptBuilder.ToString();
        }
        
        /// <summary>
        /// Builds a Llama-specific prompt format.
        /// </summary>
        private string BuildLlamaPrompt(IEnumerable<ConduitLLM.Core.Models.Message> messages)
        {
            var promptBuilder = new StringBuilder();
            
            // Llama format uses special tokens
            promptBuilder.Append("<s>");
            
            foreach (var message in messages)
            {
                var content = ContentHelper.GetContentAsString(message.Content);
                
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.Append($"[INST] <<SYS>>\n{content}\n<</SYS>>\n\n");
                }
                else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.Append($"{content} [/INST]");
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.Append($" {content} </s><s>[INST] ");
                }
            }
            
            return promptBuilder.ToString();
        }
        
        /// <summary>
        /// Builds a Cohere-specific prompt format.
        /// </summary>
        private string BuildCoherePrompt(IEnumerable<ConduitLLM.Core.Models.Message> messages)
        {
            var promptBuilder = new StringBuilder();
            
            foreach (var message in messages)
            {
                var content = ContentHelper.GetContentAsString(message.Content);
                
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.AppendLine($"Instructions: {content}");
                    promptBuilder.AppendLine();
                }
                else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.AppendLine($"User: {content}");
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.AppendLine($"Chatbot: {content}");
                }
            }
            
            // Add prompt for chatbot response
            promptBuilder.Append("Chatbot:");
            
            return promptBuilder.ToString();
        }
        
        /// <summary>
        /// Maps Cohere stop reasons to standardized finish reasons.
        /// </summary>
        private string MapCohereStopReason(string? finishReason)
        {
            return finishReason?.ToUpperInvariant() switch
            {
                "COMPLETE" => "stop",
                "MAX_TOKENS" => "length",
                "ERROR" => "stop",
                "ERROR_TOXIC" => "content_filter",
                _ => "stop"
            };
        }
        
        /// <summary>
        /// Maps Llama stop reasons to standardized finish reasons.
        /// </summary>
        private string MapLlamaStopReason(string? stopReason)
        {
            return stopReason?.ToLowerInvariant() switch
            {
                "length" => "length",
                "max_length" => "length",
                "stop" => "stop",
                "end_of_sequence" => "stop",
                null => "stop",
                _ => "stop"
            };
        }
        
        /// <summary>
        /// Maps Titan completion reasons to standardized finish reasons.
        /// </summary>
        private string MapTitanCompletionReason(string? completionReason)
        {
            return completionReason?.ToUpperInvariant() switch
            {
                "COMPLETE" => "stop",
                "LENGTH" => "length",
                "CONTENT_FILTERED" => "content_filter",
                _ => "stop"
            };
        }
        
        /// <summary>
        /// Maps AI21 finish reasons to standardized finish reasons.
        /// </summary>
        private string MapAI21FinishReason(string? finishReason)
        {
            return finishReason?.ToLowerInvariant() switch
            {
                "endoftext" => "stop",
                "length" => "length",
                "stop" => "stop",
                _ => "stop"
            };
        }
        
        /// <summary>
        /// Estimates token count for a text (rough approximation).
        /// </summary>
        private int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            
            // Rough estimate: 1 token per 4 characters
            return (int)Math.Ceiling(text.Length / 4.0);
        }

        /// <summary>
        /// Creates embeddings using Cohere models via Bedrock.
        /// </summary>
        private async Task<EmbeddingResponse> CreateCohereEmbeddingAsync(
            EmbeddingRequest request, 
            string modelId, 
            CancellationToken cancellationToken)
        {
            // Convert input to list format for Cohere
            var inputTexts = request.Input is string singleInput 
                ? new List<string> { singleInput }
                : request.Input is List<string> listInput 
                    ? listInput 
                    : throw new ArgumentException("Invalid input format for embeddings");

            var cohereRequest = new BedrockCohereEmbeddingRequest
            {
                Texts = inputTexts,
                InputType = "search_document", // Default for general embeddings
                Truncate = "END"
            };

            using var client = CreateHttpClient(Credentials.ApiKey);
            string apiUrl = $"/model/{modelId}/invoke";

            var cohereResponse = await Core.Utilities.HttpClientHelper.SendJsonRequestAsync<BedrockCohereEmbeddingRequest, BedrockCohereEmbeddingResponse>(
                client,
                HttpMethod.Post,
                apiUrl,
                cohereRequest,
                CreateAWSAuthHeaders(apiUrl, JsonSerializer.Serialize(cohereRequest, JsonOptions), Credentials.ApiKey),
                JsonOptions,
                Logger,
                cancellationToken);
            
            if (cohereResponse?.Embeddings == null)
            {
                throw new ConduitException("Invalid response from Cohere embedding model");
            }

            var embeddingObjects = cohereResponse.Embeddings.Select((embedding, index) => new EmbeddingData
            {
                Index = index,
                Embedding = embedding,
                Object = "embedding"
            }).ToList();

            return new EmbeddingResponse
            {
                Object = "list",
                Data = embeddingObjects,
                Model = modelId,
                Usage = new Usage
                {
                    PromptTokens = cohereResponse.Meta?.BilledUnits?.InputTokens ?? EstimateTokenCount(string.Join(" ", inputTexts)),
                    CompletionTokens = 0, // Embeddings don't generate completion tokens
                    TotalTokens = cohereResponse.Meta?.BilledUnits?.InputTokens ?? EstimateTokenCount(string.Join(" ", inputTexts))
                }
            };
        }

        /// <summary>
        /// Creates embeddings using Amazon Titan models via Bedrock.
        /// </summary>
        private async Task<EmbeddingResponse> CreateTitanEmbeddingAsync(
            EmbeddingRequest request, 
            string modelId, 
            CancellationToken cancellationToken)
        {
            // Titan only supports single input text
            var inputText = request.Input is string singleInput 
                ? singleInput
                : request.Input is List<string> listInput && listInput.Count == 1
                    ? listInput[0]
                    : throw new ArgumentException("Amazon Titan embeddings only support single text input");

            var titanRequest = new BedrockTitanEmbeddingRequest
            {
                InputText = inputText,
                Dimensions = request.Dimensions,
                Normalize = true // Recommended for most use cases
            };

            using var client = CreateHttpClient(Credentials.ApiKey);
            string apiUrl = $"/model/{modelId}/invoke";

            var titanResponse = await Core.Utilities.HttpClientHelper.SendJsonRequestAsync<BedrockTitanEmbeddingRequest, BedrockTitanEmbeddingResponse>(
                client,
                HttpMethod.Post,
                apiUrl,
                titanRequest,
                CreateAWSAuthHeaders(apiUrl, JsonSerializer.Serialize(titanRequest, JsonOptions), Credentials.ApiKey),
                JsonOptions,
                Logger,
                cancellationToken);
            
            if (titanResponse?.Embedding == null)
            {
                throw new ConduitException("Invalid response from Titan embedding model");
            }

            var embeddingObject = new EmbeddingData
            {
                Index = 0,
                Embedding = titanResponse.Embedding,
                Object = "embedding"
            };

            return new EmbeddingResponse
            {
                Object = "list",
                Data = new List<EmbeddingData> { embeddingObject },
                Model = modelId,
                Usage = new Usage
                {
                    PromptTokens = titanResponse.InputTextTokenCount ?? EstimateTokenCount(inputText),
                    CompletionTokens = 0, // Embeddings don't generate completion tokens
                    TotalTokens = titanResponse.InputTextTokenCount ?? EstimateTokenCount(inputText)
                }
            };
        }

        /// <summary>
        /// Creates images using Stability AI models via Bedrock.
        /// </summary>
        private async Task<ImageGenerationResponse> CreateStabilityImageAsync(
            ImageGenerationRequest request, 
            string modelId, 
            CancellationToken cancellationToken)
        {
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
                        "1152x896" => (1152, 896),
                        "1216x832" => (1216, 832),
                        "1344x768" => (1344, 768),
                        "1536x640" => (1536, 640),
                        "640x1536" => (640, 1536),
                        "768x1344" => (768, 1344),
                        "832x1216" => (832, 1216),
                        "896x1152" => (896, 1152),
                        _ => (512, 512)
                    };
                }
            }

            var stabilityRequest = new BedrockStabilityImageRequest
            {
                TextPrompts = new List<BedrockStabilityTextPrompt>
                {
                    new BedrockStabilityTextPrompt
                    {
                        Text = request.Prompt,
                        Weight = 1.0f
                    }
                },
                Width = width,
                Height = height,
                Samples = request.N,
                CfgScale = 7, // Default guidance scale
                Steps = 50, // Default number of steps
                Seed = Random.Shared.Next(),
                StylePreset = request.Style // Use style if provided
            };

            using var client = CreateHttpClient(Credentials.ApiKey);
            string apiUrl = $"/model/{modelId}/invoke";

            var stabilityResponse = await Core.Utilities.HttpClientHelper.SendJsonRequestAsync<BedrockStabilityImageRequest, BedrockStabilityImageResponse>(
                client,
                HttpMethod.Post,
                apiUrl,
                stabilityRequest,
                CreateAWSAuthHeaders(apiUrl, JsonSerializer.Serialize(stabilityRequest, JsonOptions), Credentials.ApiKey),
                JsonOptions,
                Logger,
                cancellationToken);
            
            if (stabilityResponse?.Artifacts == null || !stabilityResponse.Artifacts.Any())
            {
                throw new ConduitException("Invalid response from Stability AI model");
            }

            var imageObjects = stabilityResponse.Artifacts.Select((artifact, index) => 
            {
                if (string.IsNullOrEmpty(artifact.Base64))
                {
                    throw new ConduitException($"No image data received for artifact {index}");
                }

                return new ImageData
                {
                    B64Json = artifact.Base64
                };
            }).ToList();

            return new ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = imageObjects
            };
        }

        /// <summary>
        /// Processes Claude streaming chunks into ChatCompletionChunk format.
        /// </summary>
        private List<ChatCompletionChunk> ProcessClaudeStreamingChunk(
            BedrockClaudeStreamingResponse chunk, 
            string responseId, 
            long timestamp, 
            string modelId)
        {
            var chunks = new List<ChatCompletionChunk>();

            if (chunk.Type == "content_block_delta" && chunk.Delta?.Text != null)
            {
                chunks.Add(new ChatCompletionChunk
                {
                    Id = responseId,
                    Object = "chat.completion.chunk",
                    Created = timestamp,
                    Model = modelId,
                    Choices = new List<StreamingChoice>
                    {
                        new StreamingChoice
                        {
                            Index = chunk.Index ?? 0,
                            Delta = new DeltaContent
                            {
                                Content = chunk.Delta.Text
                            }
                        }
                    }
                });
            }
            else if (chunk.Type == "message_stop" || !string.IsNullOrEmpty(chunk.StopReason))
            {
                chunks.Add(new ChatCompletionChunk
                {
                    Id = responseId,
                    Object = "chat.completion.chunk",
                    Created = timestamp,
                    Model = modelId,
                    Choices = new List<StreamingChoice>
                    {
                        new StreamingChoice
                        {
                            Index = 0,
                            Delta = new DeltaContent(),
                            FinishReason = MapClaudeStopReason(chunk.StopReason)
                        }
                    }
                });
            }

            return chunks;
        }

        /// <summary>
        /// Processes Cohere streaming chunks into ChatCompletionChunk format.
        /// </summary>
        private List<ChatCompletionChunk> ProcessCohereStreamingChunk(
            BedrockCohereStreamingResponse chunk, 
            string responseId, 
            long timestamp, 
            string modelId)
        {
            var chunks = new List<ChatCompletionChunk>();

            if (chunk.EventType == "text-generation" && !string.IsNullOrEmpty(chunk.Text))
            {
                chunks.Add(new ChatCompletionChunk
                {
                    Id = responseId,
                    Object = "chat.completion.chunk",
                    Created = timestamp,
                    Model = modelId,
                    Choices = new List<StreamingChoice>
                    {
                        new StreamingChoice
                        {
                            Index = 0,
                            Delta = new DeltaContent
                            {
                                Content = chunk.Text
                            }
                        }
                    }
                });
            }
            else if (chunk.IsFinished == true || !string.IsNullOrEmpty(chunk.FinishReason))
            {
                chunks.Add(new ChatCompletionChunk
                {
                    Id = responseId,
                    Object = "chat.completion.chunk",
                    Created = timestamp,
                    Model = modelId,
                    Choices = new List<StreamingChoice>
                    {
                        new StreamingChoice
                        {
                            Index = 0,
                            Delta = new DeltaContent(),
                            FinishReason = MapCohereStopReason(chunk.FinishReason)
                        }
                    }
                });
            }

            return chunks;
        }

        /// <summary>
        /// Processes Llama streaming chunks into ChatCompletionChunk format.
        /// </summary>
        private List<ChatCompletionChunk> ProcessLlamaStreamingChunk(
            BedrockLlamaStreamingResponse chunk, 
            string responseId, 
            long timestamp, 
            string modelId)
        {
            var chunks = new List<ChatCompletionChunk>();

            if (!string.IsNullOrEmpty(chunk.Generation))
            {
                chunks.Add(new ChatCompletionChunk
                {
                    Id = responseId,
                    Object = "chat.completion.chunk",
                    Created = timestamp,
                    Model = modelId,
                    Choices = new List<StreamingChoice>
                    {
                        new StreamingChoice
                        {
                            Index = 0,
                            Delta = new DeltaContent
                            {
                                Content = chunk.Generation
                            }
                        }
                    }
                });
            }

            if (!string.IsNullOrEmpty(chunk.StopReason))
            {
                chunks.Add(new ChatCompletionChunk
                {
                    Id = responseId,
                    Object = "chat.completion.chunk",
                    Created = timestamp,
                    Model = modelId,
                    Choices = new List<StreamingChoice>
                    {
                        new StreamingChoice
                        {
                            Index = 0,
                            Delta = new DeltaContent(),
                            FinishReason = MapLlamaStopReason(chunk.StopReason)
                        }
                    }
                });
            }

            return chunks;
        }

        /// <summary>
        /// Processes generic streaming chunks for models not specifically handled.
        /// </summary>
        private List<ChatCompletionChunk> ProcessGenericStreamingChunk(
            string chunkText, 
            string responseId, 
            long timestamp, 
            string modelId)
        {
            var chunks = new List<ChatCompletionChunk>();

            // Try to extract any text content from the generic chunk
            try
            {
                var genericResponse = JsonSerializer.Deserialize<JsonElement>(chunkText, JsonOptions);
                
                string? content = null;
                string? finishReason = null;

                // Try common property names for content
                if (genericResponse.TryGetProperty("text", out var textProperty))
                {
                    content = textProperty.GetString();
                }
                else if (genericResponse.TryGetProperty("content", out var contentProperty))
                {
                    content = contentProperty.GetString();
                }
                else if (genericResponse.TryGetProperty("generation", out var generationProperty))
                {
                    content = generationProperty.GetString();
                }

                // Try common property names for completion
                if (genericResponse.TryGetProperty("finish_reason", out var finishProperty))
                {
                    finishReason = finishProperty.GetString();
                }
                else if (genericResponse.TryGetProperty("stop_reason", out var stopProperty))
                {
                    finishReason = stopProperty.GetString();
                }

                if (!string.IsNullOrEmpty(content))
                {
                    chunks.Add(new ChatCompletionChunk
                    {
                        Id = responseId,
                        Object = "chat.completion.chunk",
                        Created = timestamp,
                        Model = modelId,
                        Choices = new List<StreamingChoice>
                        {
                            new StreamingChoice
                            {
                                Index = 0,
                                Delta = new DeltaContent
                                {
                                    Content = content
                                }
                            }
                        }
                    });
                }

                if (!string.IsNullOrEmpty(finishReason))
                {
                    chunks.Add(new ChatCompletionChunk
                    {
                        Id = responseId,
                        Object = "chat.completion.chunk",
                        Created = timestamp,
                        Model = modelId,
                        Choices = new List<StreamingChoice>
                        {
                            new StreamingChoice
                            {
                                Index = 0,
                                Delta = new DeltaContent(),
                                FinishReason = finishReason == "end_turn" ? "stop" : finishReason
                            }
                        }
                    });
                }
            }
            catch (JsonException)
            {
                // If we can't parse it as JSON, treat it as plain text
                if (!string.IsNullOrWhiteSpace(chunkText))
                {
                    chunks.Add(new ChatCompletionChunk
                    {
                        Id = responseId,
                        Object = "chat.completion.chunk",
                        Created = timestamp,
                        Model = modelId,
                        Choices = new List<StreamingChoice>
                        {
                            new StreamingChoice
                            {
                                Index = 0,
                                Delta = new DeltaContent
                                {
                                    Content = chunkText
                                }
                            }
                        }
                    });
                }
            }

            return chunks;
        }

        /// <summary>
        /// Maps Claude stop reasons to standardized finish reasons.
        /// </summary>
        private string MapClaudeStopReason(string? stopReason)
        {
            return stopReason?.ToLowerInvariant() switch
            {
                "end_turn" => "stop",
                "max_tokens" => "length",
                "stop_sequence" => "stop",
                null => "stop",
                _ => "stop"
            };
        }

        #endregion
    }
}
