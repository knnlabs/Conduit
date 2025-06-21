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
using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.InternalModels;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with MiniMax AI APIs.
    /// </summary>
    public class MiniMaxClient : BaseLLMClient
    {
        private const string DefaultBaseUrl = "https://api.minimax.io";
        private readonly string _baseUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="MiniMaxClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials containing API key and endpoint.</param>
        /// <param name="modelId">The default model ID to use.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="defaultModels">The default models configuration.</param>
        public MiniMaxClient(
            ProviderCredentials credentials,
            string modelId,
            ILogger<MiniMaxClient> logger,
            IHttpClientFactory httpClientFactory,
            ProviderDefaultModels? defaultModels = null)
            : base(credentials, modelId, logger, httpClientFactory, "minimax", defaultModels)
        {
            _baseUrl = string.IsNullOrWhiteSpace(credentials.ApiBase) ? DefaultBaseUrl : credentials.ApiBase.TrimEnd('/');
        }

        /// <inheritdoc/>
        public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateChatCompletion");

            return await ExecuteApiRequestAsync(async () =>
            {
                using var httpClient = CreateHttpClient(apiKey);
                
                var miniMaxRequest = new MiniMaxChatCompletionRequest
                {
                    Model = MapModelName(request.Model ?? ProviderModelId),
                    Messages = ConvertMessages(request.Messages),
                    Stream = false,
                    MaxTokens = request.MaxTokens,
                    Temperature = request.Temperature,
                    TopP = request.TopP,
                    Tools = ConvertTools(request.Tools),
                    ToolChoice = ConvertToolChoice(request.ToolChoice),
                    ReplyConstraints = request.ResponseFormat != null ? new ReplyConstraints
                    {
                        GuidanceType = request.ResponseFormat.Type == "json_object" ? "json_schema" : null,
                        JsonSchema = request.ResponseFormat.Type == "json_object" ? new { type = "object" } : null
                    } : null
                };

                var endpoint = $"{_baseUrl}/v1/chat/completions";
                var response = await HttpClientHelper.SendJsonRequestAsync<MiniMaxChatCompletionRequest, MiniMaxChatCompletionResponse>(
                    httpClient, HttpMethod.Post, endpoint, miniMaxRequest, null, null, Logger, cancellationToken);

                return ConvertToCoreResponse(response, request.Model ?? ProviderModelId);
            }, "CreateChatCompletion", cancellationToken);
        }

        /// <inheritdoc/>
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletion");

            using var httpClient = CreateHttpClient(apiKey);
            
            var miniMaxRequest = new MiniMaxChatCompletionRequest
            {
                Model = MapModelName(request.Model ?? ProviderModelId),
                Messages = ConvertMessages(request.Messages),
                Stream = true,
                MaxTokens = request.MaxTokens,
                Temperature = request.Temperature,
                TopP = request.TopP,
                Tools = ConvertTools(request.Tools),
                ToolChoice = ConvertToolChoice(request.ToolChoice),
                ReplyConstraints = request.ResponseFormat != null ? new ReplyConstraints
                {
                    GuidanceType = request.ResponseFormat.Type == "json_object" ? "json_schema" : null,
                    JsonSchema = request.ResponseFormat.Type == "json_object" ? new { type = "object" } : null
                } : null
            };

            var endpoint = $"{_baseUrl}/v1/chat/completions";
            
            var response = await Core.Utilities.HttpClientHelper.SendStreamingRequestAsync(
                httpClient, HttpMethod.Post, endpoint, miniMaxRequest, null, null, Logger, cancellationToken);

            await foreach (var chunk in Core.Utilities.StreamHelper.ProcessSseStreamAsync<MiniMaxStreamChunk>(
                response, Logger, null, cancellationToken))
            {
                if (chunk != null)
                {
                    yield return ConvertToChunk(chunk, request.Model ?? ProviderModelId);
                }
            }
        }

        /// <inheritdoc/>
        public override async Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateImage");

            return await ExecuteApiRequestAsync(async () =>
            {
                using var httpClient = CreateHttpClient(apiKey);
                
                var miniMaxRequest = new MiniMaxImageGenerationRequest
                {
                    Model = request.Model ?? "image-01",
                    Prompt = request.Prompt,
                    AspectRatio = MapSizeToAspectRatio(request.Size),
                    ResponseFormat = "url", // Use URL format as per API spec
                    N = request.N,
                    PromptOptimizer = true
                };

                // Add subject reference if provided (for future use)
                if (!string.IsNullOrEmpty(request.User))
                {
                    // MiniMax uses this for tracking, not subject reference
                }

                var endpoint = $"{_baseUrl}/text_to_image";
                var response = await HttpClientHelper.SendJsonRequestAsync<MiniMaxImageGenerationRequest, MiniMaxImageGenerationResponse>(
                    httpClient, HttpMethod.Post, endpoint, miniMaxRequest, null, null, Logger, cancellationToken);

                // Map MiniMax response to Core response
                var imageData = new List<ImageData>();
                if (response.Images != null)
                {
                    foreach (var imageUrl in response.Images)
                    {
                        imageData.Add(new ImageData
                        {
                            Url = imageUrl,
                            B64Json = null
                        });
                    }
                }

                return new ImageGenerationResponse
                {
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Data = imageData
                };
            }, "CreateImage", cancellationToken);
        }

        /// <inheritdoc/>
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("MiniMax provider does not support embeddings.");
        }

        /// <inheritdoc/>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // MiniMax doesn't provide a models endpoint, return static list
            return await Task.FromResult(new List<ExtendedModelInfo>
            {
                ExtendedModelInfo.Create("abab6.5-chat", "minimax", "abab6.5-chat"),
                ExtendedModelInfo.Create("abab6.5s-chat", "minimax", "abab6.5s-chat"),
                ExtendedModelInfo.Create("abab5.5-chat", "minimax", "abab5.5-chat"),
                ExtendedModelInfo.Create("image-01", "minimax", "image-01"),
                ExtendedModelInfo.Create("video-01", "minimax", "video-01")
            });
        }

        /// <inheritdoc/>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            // MiniMax uses a different authentication header
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        private static string MapSizeToAspectRatio(string? size)
        {
            return size switch
            {
                "1792x1024" => "16:9",
                "1024x1792" => "9:16",
                "1024x1024" => "1:1",
                "512x512" => "1:1",
                "2048x2048" => "1:1",
                _ => "1:1" // Default to square
            };
        }

        private string MapModelName(string modelName)
        {
            // Map user-friendly names to MiniMax model IDs
            return modelName switch
            {
                "minimax-chat" => "abab6.5-chat",
                "minimax-image" => "image-01",
                "minimax-video" => "video-01",
                _ => modelName // Pass through if already a valid model ID
            };
        }

        private List<MiniMaxMessage> ConvertMessages(List<Message> messages)
        {
            var miniMaxMessages = new List<MiniMaxMessage>();
            
            foreach (var message in messages)
            {
                var miniMaxMessage = new MiniMaxMessage
                {
                    Role = message.Role,
                    Content = ConvertMessageContent(message.Content ?? string.Empty)
                };
                
                if (message.Role == "assistant" && message.ToolCalls != null && message.ToolCalls.Count > 0)
                {
                    // MiniMax uses function_call format, convert from tool_calls
                    var firstToolCall = message.ToolCalls[0];
                    if (firstToolCall.Function != null)
                    {
                        miniMaxMessage.FunctionCall = new MiniMaxFunctionCall
                        {
                            Name = firstToolCall.Function.Name,
                            Arguments = firstToolCall.Function.Arguments
                        };
                    }
                }
                
                miniMaxMessages.Add(miniMaxMessage);
            }
            
            return miniMaxMessages;
        }

        private object ConvertMessageContent(object content)
        {
            if (content is string stringContent)
            {
                return stringContent;
            }
            else if (content is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                var miniMaxParts = new List<object>();
                foreach (var element in jsonElement.EnumerateArray())
                {
                    if (element.TryGetProperty("type", out var typeElement))
                    {
                        var type = typeElement.GetString();
                        if (type == "text" && element.TryGetProperty("text", out var textElement))
                        {
                            miniMaxParts.Add(new { type = "text", text = textElement.GetString() });
                        }
                        else if (type == "image_url" && element.TryGetProperty("image_url", out var imageElement) &&
                                 imageElement.TryGetProperty("url", out var urlElement))
                        {
                            miniMaxParts.Add(new { type = "image_url", image_url = new { url = urlElement.GetString() } });
                        }
                    }
                }
                return miniMaxParts;
            }
            else if (content is List<object> contentParts)
            {
                // Handle if content is already a list of objects
                return contentParts;
            }
            
            return content;
        }

        private ChatCompletionResponse ConvertToCoreResponse(MiniMaxChatCompletionResponse miniMaxResponse, string modelId)
        {
            var response = new ChatCompletionResponse
            {
                Id = miniMaxResponse.Id ?? Guid.NewGuid().ToString(),
                Object = "chat.completion",
                Created = miniMaxResponse.Created ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<Choice>()
            };

            if (miniMaxResponse.Choices != null)
            {
                foreach (var choice in miniMaxResponse.Choices)
                {
                    response.Choices.Add(new Choice
                    {
                        Index = choice.Index,
                        Message = new Message
                        {
                            Role = choice.Message?.Role ?? "assistant",
                            Content = choice.Message?.Content ?? string.Empty,
                            ToolCalls = ConvertFunctionCallToToolCalls(choice.Message?.FunctionCall)
                        },
                        FinishReason = choice.FinishReason ?? "stop"
                    });
                }
            }

            if (miniMaxResponse.Usage != null)
            {
                response.Usage = new Usage
                {
                    PromptTokens = miniMaxResponse.Usage.PromptTokens,
                    CompletionTokens = miniMaxResponse.Usage.CompletionTokens,
                    TotalTokens = miniMaxResponse.Usage.TotalTokens
                };
            }

            return response;
        }

        private ChatCompletionChunk ConvertToChunk(MiniMaxStreamChunk miniMaxChunk, string modelId)
        {
            var chunk = new ChatCompletionChunk
            {
                Id = miniMaxChunk.Id ?? Guid.NewGuid().ToString(),
                Object = "chat.completion.chunk",
                Created = miniMaxChunk.Created ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<StreamingChoice>()
            };

            if (miniMaxChunk.Choices != null)
            {
                foreach (var choice in miniMaxChunk.Choices)
                {
                    chunk.Choices.Add(new StreamingChoice
                    {
                        Index = choice.Index,
                        Delta = new DeltaContent
                        {
                            Role = choice.Delta?.Role,
                            Content = choice.Delta?.Content,
                            ToolCalls = ConvertDeltaFunctionCallToToolCalls(choice.Delta?.FunctionCall)
                        },
                        FinishReason = choice.FinishReason
                    });
                }
            }

            // Note: ChatCompletionChunk doesn't have Usage property in standard implementation
            // Usage is typically tracked separately or sent in final chunk

            return chunk;
        }

        private List<MiniMaxTool>? ConvertTools(List<Tool>? tools)
        {
            if (tools == null || tools.Count == 0)
                return null;

            var miniMaxTools = new List<MiniMaxTool>();
            foreach (var tool in tools)
            {
                if (tool.Type == "function" && tool.Function != null)
                {
                    miniMaxTools.Add(new MiniMaxTool
                    {
                        Type = "function",
                        Function = new MiniMaxFunctionDefinition
                        {
                            Name = tool.Function.Name,
                            Description = tool.Function.Description,
                            Parameters = tool.Function.Parameters
                        }
                    });
                }
            }
            return miniMaxTools.Count > 0 ? miniMaxTools : null;
        }

        private object? ConvertToolChoice(ToolChoice? toolChoice)
        {
            if (toolChoice == null)
                return null;

            // Get the serialized value from ToolChoice
            var serializedValue = toolChoice.GetSerializedValue();
            
            // If it's already a string (like "auto", "none"), return it directly
            if (serializedValue is string stringChoice)
            {
                return stringChoice;
            }
            
            // Otherwise, it's a function choice object, return it as-is
            // MiniMax expects the same format as OpenAI
            return serializedValue;
        }

        private List<ToolCall>? ConvertFunctionCallToToolCalls(MiniMaxFunctionCall? functionCall)
        {
            if (functionCall == null)
                return null;

            return new List<ToolCall>
            {
                new ToolCall
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = functionCall.Name,
                        Arguments = functionCall.Arguments
                    }
                }
            };
        }

        private List<ToolCallChunk>? ConvertDeltaFunctionCallToToolCalls(MiniMaxFunctionCall? functionCall)
        {
            if (functionCall == null)
                return null;

            return new List<ToolCallChunk>
            {
                new ToolCallChunk
                {
                    Index = 0,
                    Id = Guid.NewGuid().ToString(),
                    Type = "function",
                    Function = new FunctionCallChunk
                    {
                        Name = functionCall.Name,
                        Arguments = functionCall.Arguments
                    }
                }
            };
        }

        #region MiniMax-specific Models

        private class MiniMaxChatCompletionRequest
        {
            [System.Text.Json.Serialization.JsonPropertyName("model")]
            public string Model { get; set; } = "abab6.5-chat";

            [System.Text.Json.Serialization.JsonPropertyName("messages")]
            public List<MiniMaxMessage> Messages { get; set; } = new();

            [System.Text.Json.Serialization.JsonPropertyName("stream")]
            public bool Stream { get; set; } = false;

            [System.Text.Json.Serialization.JsonPropertyName("max_tokens")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public int? MaxTokens { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("temperature")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public double? Temperature { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("top_p")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public double? TopP { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("tools")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public List<MiniMaxTool>? Tools { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("tool_choice")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public object? ToolChoice { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("reply_constraints")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public ReplyConstraints? ReplyConstraints { get; set; }
        }

        private class MiniMaxMessage
        {
            [System.Text.Json.Serialization.JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("content")]
            public object Content { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("function_call")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public MiniMaxFunctionCall? FunctionCall { get; set; }
        }

        private class ReplyConstraints
        {
            [System.Text.Json.Serialization.JsonPropertyName("guidance_type")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public string? GuidanceType { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("json_schema")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public object? JsonSchema { get; set; }
        }

        private class MiniMaxChatCompletionResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string? Id { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("created")]
            public long? Created { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("model")]
            public string? Model { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("choices")]
            public List<MiniMaxChoice>? Choices { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("usage")]
            public MiniMaxUsage? Usage { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("base_resp")]
            public BaseResponse? BaseResp { get; set; }
        }

        private class MiniMaxChoice
        {
            [System.Text.Json.Serialization.JsonPropertyName("index")]
            public int Index { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("message")]
            public MiniMaxMessage? Message { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("finish_reason")]
            public string? FinishReason { get; set; }
        }

        private class MiniMaxStreamChunk
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string? Id { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("created")]
            public long? Created { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("model")]
            public string? Model { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("choices")]
            public List<MiniMaxStreamChoice>? Choices { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("usage")]
            public MiniMaxUsage? Usage { get; set; }
        }

        private class MiniMaxStreamChoice
        {
            [System.Text.Json.Serialization.JsonPropertyName("index")]
            public int Index { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("delta")]
            public MiniMaxDelta? Delta { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("finish_reason")]
            public string? FinishReason { get; set; }
        }

        private class MiniMaxDelta
        {
            [System.Text.Json.Serialization.JsonPropertyName("role")]
            public string? Role { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("content")]
            public string? Content { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("function_call")]
            public MiniMaxFunctionCall? FunctionCall { get; set; }
        }

        private class MiniMaxUsage
        {
            [System.Text.Json.Serialization.JsonPropertyName("prompt_tokens")]
            public int PromptTokens { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("completion_tokens")]
            public int CompletionTokens { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }
        }

        private class MiniMaxImageGenerationRequest
        {
            [System.Text.Json.Serialization.JsonPropertyName("model")]
            public string Model { get; set; } = "image-01";

            [System.Text.Json.Serialization.JsonPropertyName("prompt")]
            public string Prompt { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("aspect_ratio")]
            public string AspectRatio { get; set; } = "1:1";

            [System.Text.Json.Serialization.JsonPropertyName("response_format")]
            public string ResponseFormat { get; set; } = "base64";

            [System.Text.Json.Serialization.JsonPropertyName("num_images")]
            public int N { get; set; } = 1;

            [System.Text.Json.Serialization.JsonPropertyName("prompt_optimizer")]
            public bool PromptOptimizer { get; set; } = true;

            [System.Text.Json.Serialization.JsonPropertyName("subject_reference")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public List<object>? SubjectReference { get; set; }
        }

        private class MiniMaxImageGenerationResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("images")]
            public List<string>? Images { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("base_resp")]
            public BaseResponse? BaseResp { get; set; }
        }

        private class BaseResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("status_code")]
            public int StatusCode { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("status_msg")]
            public string? StatusMsg { get; set; }
        }

        private class MiniMaxTool
        {
            [System.Text.Json.Serialization.JsonPropertyName("type")]
            public string Type { get; set; } = "function";

            [System.Text.Json.Serialization.JsonPropertyName("function")]
            public MiniMaxFunctionDefinition? Function { get; set; }
        }

        private class MiniMaxFunctionDefinition
        {
            [System.Text.Json.Serialization.JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("description")]
            public string? Description { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("parameters")]
            public object? Parameters { get; set; }
        }

        private class MiniMaxFunctionCall
        {
            [System.Text.Json.Serialization.JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("arguments")]
            public string Arguments { get; set; } = string.Empty;
        }

        #endregion
    }
}