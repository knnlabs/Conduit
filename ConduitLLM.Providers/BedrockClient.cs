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

// Removed AWS SDK dependencies - using direct HTTP calls with AWS Signature V4

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
        private readonly string _region;
        private readonly string _service = "bedrock-runtime";
        
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
            // Extract region from credentials.BaseUrl or use default
            // BaseUrl in this case is treated as the AWS region
            _region = string.IsNullOrWhiteSpace(credentials.BaseUrl) ? "us-east-1" : credentials.BaseUrl;
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

            // Note: AWS Secret Key should be provided in Credentials.ApiSecret
            // For streaming operations, both ApiKey (AWS Access Key) and ApiSecret (AWS Secret Key) are required
            // These can also come from environment variables or AWS credential chain

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

            // Set base address using the region
            client.BaseAddress = new Uri($"https://bedrock-runtime.{_region}.amazonaws.com/");
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
                else if (modelId.Contains("mistral", StringComparison.OrdinalIgnoreCase))
                {
                    return await CreateMistralChatCompletionAsync(request, apiKey, cancellationToken);
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
            string apiUrl = $"model/{modelId}/invoke";

            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, claudeRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bedrockResponse = JsonSerializer.Deserialize<BedrockClaudeChatResponse>(responseContent, JsonOptions);

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
            string apiUrl = $"model/{modelId}/invoke";

            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, llamaRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bedrockResponse = JsonSerializer.Deserialize<BedrockLlamaChatResponse>(responseContent, JsonOptions);

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
                            Content = bedrockResponse?.Generation ?? string.Empty
                        },
                        FinishReason = MapLlamaStopReason(bedrockResponse?.StopReason),
                        Logprobs = null
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = bedrockResponse?.PromptTokenCount ?? 0,
                    CompletionTokens = bedrockResponse?.GenerationTokenCount ?? 0,
                    TotalTokens = (bedrockResponse?.PromptTokenCount ?? 0) + 
                                  (bedrockResponse?.GenerationTokenCount ?? 0)
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
            
            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, titanRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bedrockResponse = JsonSerializer.Deserialize<BedrockTitanChatResponse>(responseContent, JsonOptions);

            if (bedrockResponse == null)
            {
                throw new LLMCommunicationException("Failed to deserialize Bedrock Titan response");
            }

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
            
            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, cohereRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bedrockResponse = JsonSerializer.Deserialize<BedrockCohereChatResponse>(responseContent, JsonOptions);

            if (bedrockResponse == null)
            {
                throw new LLMCommunicationException("Failed to deserialize Bedrock Cohere response");
            }

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
            
            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, ai21Request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bedrockResponse = JsonSerializer.Deserialize<BedrockAI21ChatResponse>(responseContent, JsonOptions);

            if (bedrockResponse == null)
            {
                throw new LLMCommunicationException("Failed to deserialize Bedrock AI21 response");
            }

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

        private async Task<ChatCompletionResponse> CreateMistralChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Map to Bedrock Mistral format
            var mistralRequest = new BedrockMistralChatRequest
            {
                Prompt = BuildMistralPrompt(request.Messages),
                MaxTokens = request.MaxTokens ?? 512,
                Temperature = request.Temperature.HasValue ? (float)request.Temperature.Value : 0.7f,
                TopP = request.TopP.HasValue ? (float)request.TopP.Value : 0.9f,
                TopK = 50, // Default top-k for Mistral
                Stop = request.Stop?.ToList()
            };

            string modelId = request.Model ?? ProviderModelId;
            using var client = CreateHttpClient(Credentials.ApiKey);
            string apiUrl = $"model/{modelId}/invoke";

            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, mistralRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bedrockResponse = JsonSerializer.Deserialize<BedrockMistralChatResponse>(responseContent, JsonOptions);

            if (bedrockResponse?.Outputs == null || !bedrockResponse.Outputs.Any())
            {
                throw new LLMCommunicationException("Failed to deserialize the response from AWS Bedrock API or response outputs are empty");
            }

            // Get the first output
            var output = bedrockResponse.Outputs.FirstOrDefault();
            var responseText = output?.Text ?? string.Empty;
            var finishReason = MapMistralStopReason(output?.StopReason);

            // Mistral doesn't provide token counts in the response, so estimate them
            var promptTokens = EstimateTokenCount(mistralRequest.Prompt);
            var completionTokens = EstimateTokenCount(responseText);

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
                        FinishReason = finishReason,
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

                using var httpClient = CreateHttpClient(Credentials.ApiKey);
                string apiUrl = $"model/{modelId}/invoke-with-response-stream";

                // Create HTTP request with streaming enabled
                // Create absolute URI by combining with client base address
                var absoluteUri = new Uri(httpClient.BaseAddress!, apiUrl);
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, absoluteUri);
                var json = JsonSerializer.Serialize(bedrockRequest, JsonOptions);
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
                httpRequest.Headers.Add("User-Agent", "ConduitLLM");
                
                // Sign the request with AWS Signature V4
                AwsSignatureV4.SignRequest(httpRequest, Credentials.ApiKey!, Credentials.ApiSecret ?? "dummy-secret-key", _region, _service);
                
                // Send with response streaming
                var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new LLMCommunicationException($"Bedrock streaming API error: {response.StatusCode} - {errorContent}");
                }

                // Process AWS event stream
                var responseId = Guid.NewGuid().ToString();
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                
                // Parse the AWS event stream
                await foreach (var chunk in ParseAwsEventStream(stream, modelId, responseId, timestamp, cancellationToken))
                {
                    chunks.Add(chunk);
                }

                // Add final completion chunk if needed
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
                    ExtendedModelInfo.Create("mistral.mistral-7b-instruct-v0:2", ProviderName, "mistral.mistral-7b-instruct-v0:2"),
                    ExtendedModelInfo.Create("mistral.mixtral-8x7b-instruct-v0:1", ProviderName, "mistral.mixtral-8x7b-instruct-v0:1"),
                    ExtendedModelInfo.Create("mistral.mistral-large-2402-v1:0", ProviderName, "mistral.mistral-large-2402-v1:0"),
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
                ExtendedModelInfo.Create("meta.llama3-70b-instruct-v1:0", ProviderName, "meta.llama3-70b-instruct-v1:0"),
                ExtendedModelInfo.Create("mistral.mistral-7b-instruct-v0:2", ProviderName, "mistral.mistral-7b-instruct-v0:2"),
                ExtendedModelInfo.Create("mistral.mixtral-8x7b-instruct-v0:1", ProviderName, "mistral.mixtral-8x7b-instruct-v0:1")
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
        private async Task<HttpResponseMessage> SendBedrockRequestAsync(
            HttpClient client,
            HttpMethod method,
            string path,
            object? requestBody,
            CancellationToken cancellationToken)
        {
            string effectiveApiKey = Credentials.ApiKey!;
            string effectiveSecretKey = Credentials.ApiSecret ?? "dummy-secret-key"; // Fallback for backward compatibility

            // Create absolute URI by combining with client base address
            var absoluteUri = new Uri(client.BaseAddress!, path);
            var request = new HttpRequestMessage(method, absoluteUri);
            
            if (requestBody != null)
            {
                var json = JsonSerializer.Serialize(requestBody, JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            
            // Add required headers before signing
            request.Headers.Add("User-Agent", "ConduitLLM");
            
            // Sign the request with AWS Signature V4
            AwsSignatureV4.SignRequest(request, effectiveApiKey, effectiveSecretKey, _region, _service);
            
            return await client.SendAsync(request, cancellationToken);
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
        /// Builds a Mistral-specific prompt format.
        /// </summary>
        private string BuildMistralPrompt(IEnumerable<ConduitLLM.Core.Models.Message> messages)
        {
            var promptBuilder = new StringBuilder();
            
            // Mistral uses a specific instruction format
            promptBuilder.Append("<s>");
            
            foreach (var message in messages)
            {
                var content = ContentHelper.GetContentAsString(message.Content);
                
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.Append($"[INST] {content} [/INST]</s>");
                }
                else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.Append($"[INST] {content} [/INST]");
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.Append($"{content}</s>");
                }
            }
            
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
        /// Maps Mistral stop reasons to standardized finish reasons.
        /// </summary>
        private string MapMistralStopReason(string? stopReason)
        {
            return stopReason?.ToLowerInvariant() switch
            {
                "stop" => "stop",
                "length" => "length",
                "model_length" => "length",
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

            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, cohereRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var cohereResponse = JsonSerializer.Deserialize<BedrockCohereEmbeddingResponse>(responseContent, JsonOptions);
            
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

            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, titanRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var titanResponse = JsonSerializer.Deserialize<BedrockTitanEmbeddingResponse>(responseContent, JsonOptions);
            
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

            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, stabilityRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var stabilityResponse = JsonSerializer.Deserialize<BedrockStabilityImageResponse>(responseContent, JsonOptions);
            
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
        /// Parses AWS event stream format into ChatCompletionChunk objects.
        /// </summary>
        private async IAsyncEnumerable<ChatCompletionChunk> ParseAwsEventStream(
            Stream stream,
            string modelId,
            string responseId,
            long timestamp,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(stream);
            var buffer = new StringBuilder();
            
            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;
                
                // AWS event stream format uses blank lines to separate events
                if (string.IsNullOrEmpty(line))
                {
                    if (buffer.Length > 0)
                    {
                        var eventData = buffer.ToString();
                        buffer.Clear();
                        
                        // Parse the event
                        var chunk = ParseEventData(eventData, modelId, responseId, timestamp);
                        if (chunk != null)
                        {
                            yield return chunk;
                        }
                    }
                }
                else
                {
                    buffer.AppendLine(line);
                }
            }
            
            // Process any remaining data
            if (buffer.Length > 0)
            {
                var chunk = ParseEventData(buffer.ToString(), modelId, responseId, timestamp);
                if (chunk != null)
                {
                    yield return chunk;
                }
            }
        }
        
        /// <summary>
        /// Parses a single event from the AWS event stream.
        /// </summary>
        private ChatCompletionChunk? ParseEventData(string eventData, string modelId, string responseId, long timestamp)
        {
            try
            {
                // AWS event stream format:
                // :event-type: chunk
                // :content-type: application/json
                // :message-type: event
                // {json payload}
                
                var lines = eventData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                string? eventType = null;
                string? jsonPayload = null;
                
                foreach (var line in lines)
                {
                    if (line.StartsWith(":event-type:"))
                    {
                        eventType = line.Substring(":event-type:".Length).Trim();
                    }
                    else if (line.StartsWith("{") || line.StartsWith("["))
                    {
                        // This is likely the JSON payload
                        jsonPayload = line;
                    }
                }
                
                if (string.IsNullOrEmpty(jsonPayload))
                {
                    return null;
                }
                
                // Parse based on model type
                if (modelId.Contains("claude", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseClaudeEventChunk(jsonPayload, responseId, timestamp, modelId);
                }
                else if (modelId.Contains("llama", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseLlamaEventChunk(jsonPayload, responseId, timestamp, modelId);
                }
                else if (modelId.Contains("cohere", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseCohereEventChunk(jsonPayload, responseId, timestamp, modelId);
                }
                else
                {
                    // Generic parsing
                    return ParseGenericEventChunk(jsonPayload, responseId, timestamp, modelId);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse event data: {EventData}", eventData);
                return null;
            }
        }
        
        private ChatCompletionChunk? ParseClaudeEventChunk(string json, string responseId, long timestamp, string modelId)
        {
            try
            {
                var chunk = JsonSerializer.Deserialize<BedrockClaudeStreamingResponse>(json, JsonOptions);
                if (chunk == null) return null;
                
                if (chunk.Type == "content_block_delta" && chunk.Delta?.Text != null)
                {
                    return new ChatCompletionChunk
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
                                Delta = new DeltaContent { Content = chunk.Delta.Text }
                            }
                        }
                    };
                }
                else if (chunk.Type == "message_stop" || !string.IsNullOrEmpty(chunk.StopReason))
                {
                    return new ChatCompletionChunk
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
                    };
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse Claude chunk: {Json}", json);
                return null;
            }
        }
        
        private ChatCompletionChunk? ParseLlamaEventChunk(string json, string responseId, long timestamp, string modelId)
        {
            try
            {
                var chunk = JsonSerializer.Deserialize<BedrockLlamaStreamingResponse>(json, JsonOptions);
                if (chunk == null) return null;
                
                if (!string.IsNullOrEmpty(chunk.Generation))
                {
                    return new ChatCompletionChunk
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
                                Delta = new DeltaContent { Content = chunk.Generation },
                                FinishReason = string.IsNullOrEmpty(chunk.StopReason) ? null : MapLlamaStopReason(chunk.StopReason)
                            }
                        }
                    };
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse Llama chunk: {Json}", json);
                return null;
            }
        }
        
        private ChatCompletionChunk? ParseCohereEventChunk(string json, string responseId, long timestamp, string modelId)
        {
            try
            {
                var chunk = JsonSerializer.Deserialize<BedrockCohereStreamingResponse>(json, JsonOptions);
                if (chunk == null) return null;
                
                if (chunk.EventType == "text-generation" && !string.IsNullOrEmpty(chunk.Text))
                {
                    return new ChatCompletionChunk
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
                                Delta = new DeltaContent { Content = chunk.Text },
                                FinishReason = chunk.IsFinished == true ? MapCohereStopReason(chunk.FinishReason) : null
                            }
                        }
                    };
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse Cohere chunk: {Json}", json);
                return null;
            }
        }
        
        private ChatCompletionChunk? ParseGenericEventChunk(string json, string responseId, long timestamp, string modelId)
        {
            try
            {
                var element = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
                string? content = null;
                string? finishReason = null;
                
                // Try common property names
                if (element.TryGetProperty("text", out var textProp))
                    content = textProp.GetString();
                else if (element.TryGetProperty("content", out var contentProp))
                    content = contentProp.GetString();
                else if (element.TryGetProperty("generation", out var genProp))
                    content = genProp.GetString();
                
                if (element.TryGetProperty("finish_reason", out var finishProp))
                    finishReason = finishProp.GetString();
                else if (element.TryGetProperty("stop_reason", out var stopProp))
                    finishReason = stopProp.GetString();
                
                if (!string.IsNullOrEmpty(content) || !string.IsNullOrEmpty(finishReason))
                {
                    return new ChatCompletionChunk
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
                                Delta = new DeltaContent { Content = content },
                                FinishReason = finishReason
                            }
                        }
                    };
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse generic chunk: {Json}", json);
                return null;
            }
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

        #region Authentication Verification

        /// <summary>
        /// Verifies AWS Bedrock authentication by listing foundation models.
        /// This is a free API call that validates AWS credentials without incurring charges.
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
                var effectiveSecretKey = Credentials.ApiSecret ?? "dummy-secret-key"; // Fallback for backward compatibility
                var effectiveRegion = !string.IsNullOrWhiteSpace(baseUrl) ? baseUrl : _region;
                
                if (string.IsNullOrWhiteSpace(effectiveApiKey))
                {
                    return Core.Interfaces.AuthenticationResult.Failure("API key is required");
                }

                using var client = CreateHttpClient(effectiveApiKey);
                
                // Use the foundation-models endpoint which is free and doesn't invoke any models
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://bedrock.{effectiveRegion}.amazonaws.com/foundation-models");
                
                // Add required headers
                request.Headers.Add("User-Agent", "ConduitLLM");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                
                // Sign the request with AWS Signature V4
                AwsSignatureV4.SignRequest(request, effectiveApiKey, effectiveSecretKey, effectiveRegion, "bedrock");
                
                var response = await client.SendAsync(request, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                if (response.IsSuccessStatusCode)
                {
                    return Core.Interfaces.AuthenticationResult.Success($"Response time: {responseTime:F0}ms");
                }
                
                // Check for specific error codes
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Core.Interfaces.AuthenticationResult.Failure("Invalid AWS credentials or insufficient permissions");
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return Core.Interfaces.AuthenticationResult.Failure("Invalid AWS signature or credentials");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"AWS Bedrock authentication failed: {response.StatusCode}",
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
                Logger.LogError(ex, "Unexpected error during Bedrock authentication verification");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        #endregion

        #region GetCapabilities Override

        /// <inheritdoc />
        public override Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            return Task.FromResult(new ProviderCapabilities
            {
                Provider = ProviderName,
                ModelId = modelId ?? ProviderModelId,
                ChatParameters = new ChatParameterSupport
                {
                    Temperature = true,
                    MaxTokens = true,
                    TopP = true,
                    Stop = true
                },
                Features = new FeatureSupport
                {
                    Streaming = true,
                    Embeddings = true,
                    ImageGeneration = true,
                    FunctionCalling = false,
                    AudioTranscription = false,
                    TextToSpeech = false
                }
            });
        }

        #endregion
    }
}
