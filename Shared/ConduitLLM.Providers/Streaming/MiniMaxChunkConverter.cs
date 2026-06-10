using System.Text.Json;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.Streaming
{
    /// <summary>
    /// Chunk converter for MiniMax streaming responses.
    /// </summary>
    /// <remarks>
    /// MiniMax has several deviations from the OpenAI streaming protocol:
    ///
    /// 1. Error responses use base_resp.status_code != 0 instead of HTTP status codes
    /// 2. Final chunk contains a complete 'message' field instead of 'delta' (protocol violation)
    /// 3. Object type changes from "chat.completion.chunk" to "chat.completion" in final chunk
    /// 4. Supports reasoning_content for models with reasoning tokens
    ///
    /// This converter handles these deviations to produce standard ChatCompletionChunk output.
    /// </remarks>
    public sealed class MiniMaxChunkConverter : SseChunkConverterBase, IChunkConverter<JsonElement>
    {
        /// <summary>
        /// Singleton instance for reuse.
        /// </summary>
        public static readonly MiniMaxChunkConverter Instance = new();

        /// <inheritdoc />
        public ChatCompletionChunk? Convert(JsonElement providerChunk, string modelId)
        {
            try
            {
                var chunk = new ChatCompletionChunk
                {
                    Id = GetStringProperty(providerChunk, "id") ?? Guid.NewGuid().ToString(),
                    Object = "chat.completion.chunk", // Always normalize to chunk type
                    Created = GetLongProperty(providerChunk, "created") ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Model = modelId,
                    Choices = new List<StreamingChoice>()
                };

                if (providerChunk.TryGetProperty("choices", out var choicesElement) &&
                    choicesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var choice in choicesElement.EnumerateArray())
                    {
                        var streamingChoice = ConvertChoice(choice);
                        if (streamingChoice != null)
                        {
                            chunk.Choices.Add(streamingChoice);
                        }
                    }
                }

                // Extract usage if present
                if (providerChunk.TryGetProperty("usage", out var usageElement))
                {
                    chunk.Usage = ConvertUsage(usageElement);
                }

                if (!string.IsNullOrEmpty(modelId))
                {
                    chunk.Model = modelId;
                    chunk.OriginalModelAlias = modelId;
                }

                return chunk;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <inheritdoc />
        public bool IsErrorChunk(JsonElement chunk, out string? errorMessage)
        {
            errorMessage = null;

            try
            {
                // MiniMax uses base_resp.status_code for errors
                if (chunk.TryGetProperty("base_resp", out var baseResp))
                {
                    if (baseResp.TryGetProperty("status_code", out var statusCode) &&
                        statusCode.TryGetInt32(out var code) &&
                        code != 0)
                    {
                        if (baseResp.TryGetProperty("status_msg", out var statusMsg))
                        {
                            errorMessage = statusMsg.GetString();
                        }
                        else
                        {
                            errorMessage = $"MiniMax error code: {code}";
                        }
                        return true;
                    }
                }
            }
            catch
            {
                // If we can't parse the error, assume it's not an error chunk
            }

            // Also check standard error field
            return IsOpenAIStyleErrorChunk(chunk, out errorMessage);
        }

        /// <inheritdoc />
        public bool IsFinalChunk(JsonElement chunk)
            => IsOpenAIStyleFinalChunk(chunk);

        /// <summary>
        /// Converts a MiniMax choice to a StreamingChoice.
        /// </summary>
        /// <remarks>
        /// MiniMax sends a non-standard final chunk that includes a complete 'message' field
        /// instead of using 'delta' consistently. This method handles both cases:
        /// - Standard delta chunks (OpenAI-compliant)
        /// - Non-standard message chunks (MiniMax protocol deviation)
        /// </remarks>
        private static StreamingChoice? ConvertChoice(JsonElement choice)
        {
            var index = 0;
            if (choice.TryGetProperty("index", out var indexElement))
            {
                index = indexElement.GetInt32();
            }

            string? finishReason = null;
            if (choice.TryGetProperty("finish_reason", out var finishReasonElement) &&
                finishReasonElement.ValueKind != JsonValueKind.Null)
            {
                finishReason = finishReasonElement.GetString();
            }

            string? content = null;
            string? role = null;
            List<ToolCallChunk>? toolCalls = null;

            // Check for non-standard 'message' field (MiniMax protocol deviation)
            if (choice.TryGetProperty("message", out var messageElement) &&
                messageElement.ValueKind == JsonValueKind.Object)
            {
                // MiniMax's non-standard final chunk with complete message
                // Skip content in final chunk to avoid duplicating what was already streamed
                if (finishReason == "stop")
                {
                    // Only extract role, skip content to avoid duplication
                    role = GetStringProperty(messageElement, "role");
                }
                else
                {
                    // For non-final chunks with message (unusual but handle it)
                    content = GetContentFromMessage(messageElement);
                    role = GetStringProperty(messageElement, "role");
                    toolCalls = ExtractToolCalls(messageElement);
                }
            }
            else if (choice.TryGetProperty("delta", out var deltaElement) &&
                     deltaElement.ValueKind == JsonValueKind.Object)
            {
                // Standard OpenAI-compliant streaming chunk with delta
                content = GetContentFromDelta(deltaElement);
                role = GetStringProperty(deltaElement, "role");
                toolCalls = ExtractToolCalls(deltaElement);
            }

            return new StreamingChoice
            {
                Index = index,
                Delta = new DeltaContent
                {
                    Role = role,
                    Content = content,
                    ToolCalls = toolCalls
                },
                FinishReason = finishReason
            };
        }

        /// <summary>
        /// Extracts content from a delta element, checking both content and reasoning_content.
        /// </summary>
        private static string? GetContentFromDelta(JsonElement delta)
        {
            // Check standard content field first
            if (delta.TryGetProperty("content", out var contentElement) &&
                contentElement.ValueKind == JsonValueKind.String)
            {
                var content = contentElement.GetString();
                if (!string.IsNullOrEmpty(content))
                {
                    return content;
                }
            }

            // Fall back to reasoning_content for models with reasoning tokens
            if (delta.TryGetProperty("reasoning_content", out var reasoningElement) &&
                reasoningElement.ValueKind == JsonValueKind.String)
            {
                return reasoningElement.GetString();
            }

            return null;
        }

        /// <summary>
        /// Extracts content from a message element.
        /// MiniMax's message.content can be string or object.
        /// </summary>
        private static string? GetContentFromMessage(JsonElement message)
        {
            // Check standard content field
            if (message.TryGetProperty("content", out var contentElement))
            {
                if (contentElement.ValueKind == JsonValueKind.String)
                {
                    var content = contentElement.GetString();
                    if (!string.IsNullOrEmpty(content))
                    {
                        return content;
                    }
                }
                else if (contentElement.ValueKind != JsonValueKind.Null)
                {
                    // Content might be an object, try to get raw text
                    var rawContent = contentElement.GetRawText();
                    if (!string.IsNullOrEmpty(rawContent) && rawContent != "null")
                    {
                        return rawContent;
                    }
                }
            }

            // Fall back to reasoning_content
            if (message.TryGetProperty("reasoning_content", out var reasoningElement) &&
                reasoningElement.ValueKind == JsonValueKind.String)
            {
                return reasoningElement.GetString();
            }

            return null;
        }

        /// <summary>
        /// Extracts tool calls from a delta or message element.
        /// </summary>
        private static List<ToolCallChunk>? ExtractToolCalls(JsonElement element)
        {
            // Check for function_call (MiniMax format)
            if (element.TryGetProperty("function_call", out var functionCallElement) &&
                functionCallElement.ValueKind == JsonValueKind.Object)
            {
                var name = GetStringProperty(functionCallElement, "name");
                var arguments = GetStringProperty(functionCallElement, "arguments");

                if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(arguments))
                {
                    return new List<ToolCallChunk>
                    {
                        new ToolCallChunk
                        {
                            Index = 0,
                            Id = Guid.NewGuid().ToString(),
                            Type = "function",
                            Function = new FunctionCallChunk
                            {
                                Name = name,
                                Arguments = arguments
                            }
                        }
                    };
                }
            }

            // Check for tool_calls array (OpenAI format)
            if (element.TryGetProperty("tool_calls", out var toolCallsElement) &&
                toolCallsElement.ValueKind == JsonValueKind.Array)
            {
                var toolCalls = new List<ToolCallChunk>();
                foreach (var toolCall in toolCallsElement.EnumerateArray())
                {
                    var toolCallChunk = new ToolCallChunk
                    {
                        Index = toolCall.TryGetProperty("index", out var idx) ? idx.GetInt32() : 0,
                        Id = GetStringProperty(toolCall, "id"),
                        Type = GetStringProperty(toolCall, "type") ?? "function"
                    };

                    if (toolCall.TryGetProperty("function", out var funcElement))
                    {
                        toolCallChunk.Function = new FunctionCallChunk
                        {
                            Name = GetStringProperty(funcElement, "name"),
                            Arguments = GetStringProperty(funcElement, "arguments")
                        };
                    }

                    toolCalls.Add(toolCallChunk);
                }

                return toolCalls.Count > 0 ? toolCalls : null;
            }

            return null;
        }

        /// <summary>
        /// Converts MiniMax usage to standard Usage format.
        /// </summary>
        private static Usage? ConvertUsage(JsonElement usageElement)
        {
            if (usageElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var usage = new Usage();

            if (usageElement.TryGetProperty("prompt_tokens", out var promptTokens))
            {
                usage.PromptTokens = promptTokens.GetInt32();
            }

            if (usageElement.TryGetProperty("completion_tokens", out var completionTokens))
            {
                usage.CompletionTokens = completionTokens.GetInt32();
            }

            if (usageElement.TryGetProperty("total_tokens", out var totalTokens))
            {
                usage.TotalTokens = totalTokens.GetInt32();
            }

            return usage;
        }

        /// <summary>
        /// Safely gets a string property from a JSON element.
        /// </summary>
        private static string? GetStringProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) &&
                prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
            return null;
        }

        /// <summary>
        /// Safely gets a long property from a JSON element.
        /// </summary>
        private static long? GetLongProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) &&
                prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt64();
            }
            return null;
        }
    }
}
