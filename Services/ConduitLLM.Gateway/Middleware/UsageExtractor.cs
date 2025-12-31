using System.Text.Json;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;

namespace ConduitLLM.Gateway.Middleware
{
    /// <summary>
    /// Static helper methods for extracting usage data from LLM API responses.
    /// Supports multiple provider formats including OpenAI and Anthropic.
    /// </summary>
    public static class UsageExtractor
    {
        /// <summary>
        /// Extracts usage data from a JSON response element.
        /// </summary>
        /// <param name="usageElement">The usage JSON element from the response</param>
        /// <param name="logger">Logger for error reporting</param>
        /// <returns>Extracted usage data or null if extraction fails</returns>
        public static Usage? ExtractUsage(JsonElement usageElement, ILogger logger)
        {
            try
            {
                var usage = new Usage();

                // Standard OpenAI fields
                if (usageElement.TryGetProperty("prompt_tokens", out var promptTokens))
                    usage.PromptTokens = promptTokens.GetInt32();

                if (usageElement.TryGetProperty("completion_tokens", out var completionTokens))
                    usage.CompletionTokens = completionTokens.GetInt32();

                if (usageElement.TryGetProperty("total_tokens", out var totalTokens))
                    usage.TotalTokens = totalTokens.GetInt32();

                // Anthropic format (uses input_tokens/output_tokens)
                // Note: These will override OpenAI fields if both exist
                if (usageElement.TryGetProperty("input_tokens", out var inputTokens))
                    usage.PromptTokens = inputTokens.GetInt32();

                if (usageElement.TryGetProperty("output_tokens", out var outputTokens))
                    usage.CompletionTokens = outputTokens.GetInt32();

                // Anthropic cached tokens
                if (usageElement.TryGetProperty("cache_creation_input_tokens", out var cacheWriteTokens))
                    usage.CachedWriteTokens = cacheWriteTokens.GetInt32();

                if (usageElement.TryGetProperty("cache_read_input_tokens", out var cacheReadTokens))
                    usage.CachedInputTokens = cacheReadTokens.GetInt32();

                // Reasoning tokens (o1 models and other reasoning models)
                if (usageElement.TryGetProperty("reasoning_tokens", out var reasoningTokens))
                    usage.ReasoningTokens = reasoningTokens.GetInt32();

                // Image generation
                if (usageElement.TryGetProperty("images", out var imageCount))
                    usage.ImageCount = imageCount.GetInt32();

                // Validate we have at least some usage data
                if (usage.PromptTokens == null && 
                    usage.CompletionTokens == null && 
                    usage.ImageCount == null)
                {
                    return null;
                }

                return usage;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to extract usage data from response");
                return null;
            }
        }

        /// <summary>
        /// Determines the request type from the API path.
        /// </summary>
        /// <param name="path">The request path</param>
        /// <returns>The type of request (chat, completion, embedding, etc.)</returns>
        public static string DetermineRequestType(PathString path)
        {
            var pathValue = path.Value?.ToLowerInvariant() ?? "";

            if (pathValue.Contains("/chat/completions"))
                return "chat";
            if (pathValue.Contains("/completions"))
                return "completion";
            if (pathValue.Contains("/embeddings"))
                return "embedding";
            if (pathValue.Contains("/images/generations"))
                return "image";
            if (pathValue.Contains("/audio/transcriptions"))
                return "transcription";
            if (pathValue.Contains("/audio/speech"))
                return "tts";
            if (pathValue.Contains("/videos/generations"))
                return "video";
            if (pathValue.Contains("/functions/execute"))
                return "function";

            return "other";
        }

        /// <summary>
        /// Calculates the response time from the request start time stored in HttpContext.
        /// </summary>
        /// <param name="context">The HTTP context</param>
        /// <returns>Response time in milliseconds</returns>
        public static double GetResponseTime(HttpContext context)
        {
            if (context.Items.TryGetValue("RequestStartTime", out var startTimeObj) && 
                startTimeObj is DateTime startTime)
            {
                return (DateTime.UtcNow - startTime).TotalMilliseconds;
            }
            
            return 0;
        }

        /// <summary>
        /// Extracts tool_calls from a chat completion response (OpenAI-style format).
        /// This captures function/tool calls made by the LLM in the response.
        /// </summary>
        /// <param name="responseBody">The full response body as a string</param>
        /// <param name="logger">Logger for error reporting</param>
        /// <returns>Chat tool call data or null if no tool calls were made</returns>
        public static ChatToolCallData? ExtractChatToolCalls(string responseBody, ILogger logger)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                // Check for choices array
                if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                    return null;

                var toolCalls = new List<ChatToolCallItem>();

                // Iterate through all choices
                foreach (var choice in choices.EnumerateArray())
                {
                    // Get the message object
                    if (!choice.TryGetProperty("message", out var message))
                        continue;

                    // Check for tool_calls array (modern format)
                    if (message.TryGetProperty("tool_calls", out var toolCallsArray))
                    {
                        foreach (var toolCall in toolCallsArray.EnumerateArray())
                        {
                            var item = new ChatToolCallItem();

                            if (toolCall.TryGetProperty("id", out var id))
                                item.Id = id.GetString();

                            if (toolCall.TryGetProperty("type", out var type))
                                item.Type = type.GetString();

                            if (toolCall.TryGetProperty("function", out var function))
                            {
                                if (function.TryGetProperty("name", out var name))
                                    item.FunctionName = name.GetString();

                                // Don't store arguments - they may contain sensitive data
                                // Just note that arguments were present
                                if (function.TryGetProperty("arguments", out _))
                                    item.HasArguments = true;
                            }

                            toolCalls.Add(item);
                        }
                    }

                    // Check for legacy function_call format
                    if (message.TryGetProperty("function_call", out var functionCall))
                    {
                        var item = new ChatToolCallItem
                        {
                            Type = "function"
                        };

                        if (functionCall.TryGetProperty("name", out var name))
                            item.FunctionName = name.GetString();

                        if (functionCall.TryGetProperty("arguments", out _))
                            item.HasArguments = true;

                        toolCalls.Add(item);
                    }
                }

                if (toolCalls.Count > 0)
                {
                    return new ChatToolCallData { ToolCalls = toolCalls };
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to extract tool_calls from chat completion response");
                return null;
            }
        }

        /// <summary>
        /// Serializes chat tool call data to JSON for storage in metadata.
        /// </summary>
        /// <param name="data">The chat tool call data</param>
        /// <returns>JSON string representation</returns>
        public static string? SerializeChatToolCalls(ChatToolCallData? data)
        {
            if (data == null || data.ToolCalls.Count == 0)
                return null;

            return JsonSerializer.Serialize(new
            {
                type = "chat_with_tools",
                toolCallCount = data.ToolCalls.Count,
                toolCalls = data.ToolCalls.Select(tc => new
                {
                    id = tc.Id,
                    type = tc.Type,
                    functionName = tc.FunctionName,
                    hasArguments = tc.HasArguments
                })
            }, new JsonSerializerOptions { WriteIndented = false });
        }

        /// <summary>
        /// Extracts tool usage data from a provider response.
        /// </summary>
        /// <param name="responseBody">The full response body as a string</param>
        /// <param name="providerType">The provider type to determine parsing strategy</param>
        /// <param name="logger">Logger for error reporting</param>
        /// <returns>Tool usage data or null if no tools were used</returns>
        public static ToolUsageData? ExtractToolUsage(string responseBody, ProviderType providerType, ILogger logger)
        {
            try
            {
                return providerType switch
                {
                    ProviderType.Groq => ExtractGroqToolUsage(responseBody, logger),
                    ProviderType.OpenAI => null, // OpenAI uses function calling, not hosted tools
                    ProviderType.OpenAICompatible => null, // Most OpenAI-compatible providers don't host tools
                    _ => null
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to extract tool usage data from {ProviderType} response", providerType);
                return null;
            }
        }

        /// <summary>
        /// Extracts tool usage from Groq API responses.
        /// </summary>
        /// <param name="responseBody">The response body JSON</param>
        /// <param name="logger">Logger for error reporting</param>
        /// <returns>Tool usage data specific to Groq tools</returns>
        private static ToolUsageData? ExtractGroqToolUsage(string responseBody, ILogger logger)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                // Groq returns tool usage in x_groq.usage field
                if (!root.TryGetProperty("x_groq", out var xGroq))
                    return null;

                if (!xGroq.TryGetProperty("usage", out var usage))
                    return null;

                var toolUsageList = new List<ToolUsageItem>();

                // Iterate through all properties in the usage object
                foreach (var property in usage.EnumerateObject())
                {
                    var toolName = property.Name;

                    // Map Groq's tool names to our billing names if needed
                    // Currently Groq uses "code_interpreter" and "browser_search" directly
                    var billingToolName = toolName switch
                    {
                        "python" => "code_interpreter", // In case they change to python
                        _ => toolName
                    };

                    if (property.Value.ValueKind == JsonValueKind.Number)
                    {
                        var count = property.Value.GetInt32();
                        if (count > 0)
                        {
                            toolUsageList.Add(new ToolUsageItem
                            {
                                ToolName = billingToolName,
                                Count = count,
                                // For code_interpreter, we might want to track duration
                                // For now, we'll use a standard unit (could be enhanced later)
                                Duration = billingToolName == "code_interpreter" ? 1 : null
                            });
                        }
                    }
                }

                if (toolUsageList.Any())
                {
                    return new ToolUsageData { Tools = toolUsageList };
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse Groq tool usage from response");
                return null;
            }
        }
    }

    /// <summary>
    /// Represents tool usage data extracted from provider responses.
    /// </summary>
    public class ToolUsageData
    {
        /// <summary>
        /// List of tools that were used in the request.
        /// </summary>
        public List<ToolUsageItem> Tools { get; set; } = new();
    }

    /// <summary>
    /// Represents usage information for a specific tool.
    /// </summary>
    public class ToolUsageItem
    {
        /// <summary>
        /// Name of the tool (e.g., "code_interpreter", "browser_search")
        /// </summary>
        public string ToolName { get; set; } = string.Empty;

        /// <summary>
        /// Number of times the tool was invoked
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Duration of tool usage (for time-based billing like code execution)
        /// </summary>
        public decimal? Duration { get; set; }
    }

    /// <summary>
    /// Represents tool/function calls extracted from a chat completion response.
    /// </summary>
    public class ChatToolCallData
    {
        /// <summary>
        /// List of tool calls made by the LLM in the response.
        /// </summary>
        public List<ChatToolCallItem> ToolCalls { get; set; } = new();
    }

    /// <summary>
    /// Represents a single tool/function call from a chat completion response.
    /// </summary>
    public class ChatToolCallItem
    {
        /// <summary>
        /// The unique ID of the tool call (e.g., "call_abc123")
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// The type of tool call (typically "function")
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// The name of the function being called
        /// </summary>
        public string? FunctionName { get; set; }

        /// <summary>
        /// Whether arguments were provided (we don't store actual arguments for privacy)
        /// </summary>
        public bool HasArguments { get; set; }
    }
}