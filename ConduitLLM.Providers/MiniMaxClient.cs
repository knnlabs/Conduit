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
                // Log the request for debugging
                var requestJson = JsonSerializer.Serialize(miniMaxRequest);
                Logger.LogInformation("MiniMax request: {Request}", requestJson);

                // Make direct HTTP call to debug
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
                httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                
                var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
                var rawContent = await httpResponse.Content.ReadAsStringAsync();
                
                Logger.LogInformation("MiniMax HTTP Status: {Status}", httpResponse.StatusCode);
                Logger.LogInformation("MiniMax raw response: {Response}", rawContent);
                
                if (!httpResponse.IsSuccessStatusCode)
                {
                    throw new LLMCommunicationException($"MiniMax API returned {httpResponse.StatusCode}: {rawContent}");
                }
                
                // Now deserialize
                MiniMaxChatCompletionResponse response;
                try
                {
                    response = JsonSerializer.Deserialize<MiniMaxChatCompletionResponse>(rawContent, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    })!;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error deserializing MiniMax response: {Response}", rawContent);
                    throw new LLMCommunicationException("Failed to deserialize MiniMax response", ex);
                }

                // Log the raw response for debugging
                if (response == null)
                {
                    Logger.LogWarning("MiniMax response is null");
                    throw new LLMCommunicationException("MiniMax returned null response");
                }

                var responseJson = JsonSerializer.Serialize(response);
                Logger.LogInformation("MiniMax response: {Response}", responseJson);
                Logger.LogInformation("MiniMax response choices count: {Count}", response.Choices?.Count ?? 0);
                if (response.Choices != null && response.Choices.Count > 0)
                {
                    Logger.LogInformation("First choice message: {Message}", 
                        JsonSerializer.Serialize(response.Choices[0].Message));
                }

                // Check for MiniMax error response
                if (response.BaseResp is { } baseResp && baseResp.StatusCode != 0)
                {
                    Logger.LogError("MiniMax error: {StatusCode} - {StatusMsg}", 
                        baseResp.StatusCode, baseResp.StatusMsg);
                    throw new LLMCommunicationException($"MiniMax error: {baseResp.StatusMsg}");
                }

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

            Logger.LogInformation("MiniMax streaming response status: {StatusCode}", response.StatusCode);

            await foreach (var chunk in Core.Utilities.StreamHelper.ProcessSseStreamAsync<MiniMaxStreamChunk>(
                response, Logger, null, cancellationToken))
            {
                if (chunk != null)
                {
                    Logger.LogDebug("Received MiniMax chunk with ID: {Id}, Choices: {ChoiceCount}", 
                        chunk.Id, chunk.Choices?.Count ?? 0);
                    
                    // Check for MiniMax error response
                    if (chunk.BaseResp is { } baseResp && baseResp.StatusCode != 0)
                    {
                        Logger.LogError("MiniMax streaming error: {StatusCode} - {StatusMsg}", 
                            baseResp.StatusCode, baseResp.StatusMsg);
                        throw new LLMCommunicationException($"MiniMax error: {baseResp.StatusMsg}");
                    }
                    
                    yield return ConvertToChunk(chunk, request.Model ?? ProviderModelId);
                }
                else
                {
                    Logger.LogDebug("Received null chunk from MiniMax stream");
                }
            }
            
            Logger.LogInformation("MiniMax streaming completed");
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
                    ResponseFormat = "url", // Always request URLs, we'll convert if needed
                    N = request.N,
                    PromptOptimizer = true
                };

                // Add subject reference if provided (for future use)
                if (!string.IsNullOrEmpty(request.User))
                {
                    // MiniMax uses this for tracking, not subject reference
                }

                var endpoint = $"{_baseUrl}/v1/image_generation";
                
                // Log the request for debugging
                var requestJson = JsonSerializer.Serialize(miniMaxRequest);
                Logger.LogInformation("MiniMax image request: {Request}", requestJson);
                
                // Make direct HTTP call to debug
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
                httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                
                var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
                var rawContent = await httpResponse.Content.ReadAsStringAsync();
                
                Logger.LogInformation("MiniMax HTTP Status: {Status}", httpResponse.StatusCode);
                Logger.LogInformation("MiniMax raw response: {Response}", rawContent);
                
                if (!httpResponse.IsSuccessStatusCode)
                {
                    throw new LLMCommunicationException($"MiniMax API returned {httpResponse.StatusCode}: {rawContent}");
                }
                
                // Now deserialize with specific options
                MiniMaxImageGenerationResponse response;
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    };
                    response = JsonSerializer.Deserialize<MiniMaxImageGenerationResponse>(rawContent, options)!;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error deserializing MiniMax response: {Response}", rawContent);
                    throw new LLMCommunicationException("Failed to deserialize MiniMax response", ex);
                }
                
                // Log the response for debugging
                var responseJson = JsonSerializer.Serialize(response);
                Logger.LogInformation("MiniMax image response object: {Response}", responseJson);
                
                // Check for MiniMax error response
                if (response.BaseResp is { } baseResp && baseResp.StatusCode != 0)
                {
                    Logger.LogError("MiniMax image generation error: {StatusCode} - {StatusMsg}", 
                        baseResp.StatusCode, baseResp.StatusMsg);
                    throw new LLMCommunicationException($"MiniMax error: {baseResp.StatusMsg}");
                }

                // Map MiniMax response to Core response
                var imageData = new List<ImageData>();
                
                // Handle URL response format
                if (response.Data?.ImageUrls != null)
                {
                    foreach (var imageUrl in response.Data.ImageUrls)
                    {
                        // If user requested b64_json, download and convert the image
                        if (request.ResponseFormat == "b64_json")
                        {
                            try
                            {
                                Logger.LogInformation("Downloading image from URL for base64 conversion: {Url}", imageUrl);
                                using var imageResponse = await httpClient.GetAsync(imageUrl, cancellationToken);
                                if (imageResponse.IsSuccessStatusCode)
                                {
                                    var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                                    var base64String = Convert.ToBase64String(imageBytes);
                                    imageData.Add(new ImageData
                                    {
                                        Url = null,
                                        B64Json = base64String
                                    });
                                }
                                else
                                {
                                    Logger.LogWarning("Failed to download image from {Url}: {Status}", imageUrl, imageResponse.StatusCode);
                                    imageData.Add(new ImageData
                                    {
                                        Url = imageUrl,
                                        B64Json = null
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError(ex, "Error downloading image from {Url}", imageUrl);
                                imageData.Add(new ImageData
                                {
                                    Url = imageUrl,
                                    B64Json = null
                                });
                            }
                        }
                        else
                        {
                            imageData.Add(new ImageData
                            {
                                Url = imageUrl,
                                B64Json = null
                            });
                        }
                    }
                }
                
                // Handle base64 response format
                if (response.Data?.Images != null)
                {
                    foreach (var image in response.Data.Images)
                    {
                        imageData.Add(new ImageData
                        {
                            Url = null,
                            B64Json = image.B64
                        });
                    }
                }
                
                // Handle MiniMax base64 format (image_base64 field)
                if (response.Data?.ImageBase64 != null)
                {
                    foreach (var base64Image in response.Data.ImageBase64)
                    {
                        imageData.Add(new ImageData
                        {
                            Url = null,
                            B64Json = base64Image
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
                "minimax-chat" => "MiniMax-Text-01",
                "abab6.5-chat" => "MiniMax-Text-01",
                "abab6.5s-chat" => "MiniMax-Text-01",
                "abab5.5-chat" => "MiniMax-Text-01",
                "minimax-image" => "image-01",
                "minimax-video" => "video-01",
                "MiniMax-Text-01" => "MiniMax-Text-01", // Pass through if already mapped
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
            Logger.LogDebug("Converting MiniMax response: Id={Id}, ChoiceCount={ChoiceCount}, BaseResp={BaseResp}", 
                miniMaxResponse.Id, miniMaxResponse.Choices?.Count ?? 0, miniMaxResponse.BaseResp?.StatusCode ?? 0);
            
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
                    Logger.LogDebug("MiniMax choice: Index={Index}, Role={Role}, Content={Content}, FinishReason={FinishReason}", 
                        choice.Index, choice.Message?.Role, choice.Message?.Content?.ToString()?.Substring(0, Math.Min(50, choice.Message?.Content?.ToString()?.Length ?? 0)), choice.FinishReason);
                    
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
            Logger.LogDebug("Converting MiniMax chunk: Id={Id}, ChoiceCount={ChoiceCount}", 
                miniMaxChunk.Id, miniMaxChunk.Choices?.Count ?? 0);
            
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
                    var content = choice.Delta?.Content;
                    var role = choice.Delta?.Role;
                    
                    Logger.LogDebug("MiniMax choice: Index={Index}, Content={Content}, Role={Role}, FinishReason={FinishReason}", 
                        choice.Index, content, role, choice.FinishReason);
                    
                    chunk.Choices.Add(new StreamingChoice
                    {
                        Index = choice.Index,
                        Delta = new DeltaContent
                        {
                            Role = role,
                            Content = content,
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

            [System.Text.Json.Serialization.JsonPropertyName("name")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public string? Name { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("audio_content")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public string? AudioContent { get; set; }

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

            [System.Text.Json.Serialization.JsonPropertyName("object")]
            public string? Object { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("choices")]
            public List<MiniMaxChoice>? Choices { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("usage")]
            public MiniMaxUsage? Usage { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("base_resp")]
            public BaseResponse? BaseResp { get; set; }

            // MiniMax specific fields
            [System.Text.Json.Serialization.JsonPropertyName("input_sensitive")]
            public bool? InputSensitive { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("output_sensitive")]
            public bool? OutputSensitive { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("input_sensitive_type")]
            public int? InputSensitiveType { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("output_sensitive_type")]
            public int? OutputSensitiveType { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("output_sensitive_int")]
            public int? OutputSensitiveInt { get; set; }
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

            [System.Text.Json.Serialization.JsonPropertyName("object")]
            public string? Object { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("choices")]
            public List<MiniMaxStreamChoice>? Choices { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("usage")]
            public MiniMaxUsage? Usage { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("base_resp")]
            public BaseResponse? BaseResp { get; set; }

            // MiniMax specific fields
            [System.Text.Json.Serialization.JsonPropertyName("input_sensitive")]
            public bool? InputSensitive { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("output_sensitive")]
            public bool? OutputSensitive { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("input_sensitive_type")]
            public int? InputSensitiveType { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("output_sensitive_type")]
            public int? OutputSensitiveType { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("output_sensitive_int")]
            public int? OutputSensitiveInt { get; set; }
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

            // MiniMax specific fields that appear in streaming responses
            [System.Text.Json.Serialization.JsonPropertyName("name")]
            public string? Name { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("audio_content")]
            public object? AudioContent { get; set; }
        }

        private class MiniMaxUsage
        {
            [System.Text.Json.Serialization.JsonPropertyName("prompt_tokens")]
            public int PromptTokens { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("completion_tokens")]
            public int CompletionTokens { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("total_characters")]
            public int? TotalCharacters { get; set; }
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
            public string ResponseFormat { get; set; } = "url";

            [System.Text.Json.Serialization.JsonPropertyName("n")]
            public int N { get; set; } = 1;

            [System.Text.Json.Serialization.JsonPropertyName("prompt_optimizer")]
            public bool PromptOptimizer { get; set; } = true;

            [System.Text.Json.Serialization.JsonPropertyName("subject_reference")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public List<object>? SubjectReference { get; set; }
        }

        private class MiniMaxImageGenerationResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string? Id { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("data")]
            public MiniMaxImageResponseData? Data { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("metadata")]
            public object? Metadata { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("base_resp")]
            public BaseResponse? BaseResp { get; set; }
        }

        private class MiniMaxImageResponseData
        {
            [System.Text.Json.Serialization.JsonPropertyName("image_urls")]
            public List<string>? ImageUrls { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("images")]
            public List<MiniMaxImageData>? Images { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("image_base64")]
            public List<string>? ImageBase64 { get; set; }
        }
        
        private class MiniMaxImageData
        {
            [System.Text.Json.Serialization.JsonPropertyName("b64")]
            public string? B64 { get; set; }
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