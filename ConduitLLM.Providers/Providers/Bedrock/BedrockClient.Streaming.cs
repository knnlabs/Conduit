using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.Providers.Bedrock.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Bedrock
{
    /// <summary>
    /// BedrockClient partial class containing streaming functionality.
    /// </summary>
    public partial class BedrockClient
    {
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

                using var httpClient = CreateHttpClient(PrimaryKeyCredential.ApiKey);
                string apiUrl = $"model/{modelId}/invoke-with-response-stream";

                // Create HTTP request with streaming enabled
                // Create absolute URI by combining with client base address
                var absoluteUri = new Uri(httpClient.BaseAddress!, apiUrl);
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, absoluteUri);
                var json = JsonSerializer.Serialize(bedrockRequest, JsonOptions);
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
                httpRequest.Headers.Add("User-Agent", "ConduitLLM");
                
                // Sign the request with AWS Signature V4
                AwsSignatureV4.SignRequest(httpRequest, PrimaryKeyCredential.ApiKey!, Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "dummy-secret-key", _region, _service);
                
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
    }
}