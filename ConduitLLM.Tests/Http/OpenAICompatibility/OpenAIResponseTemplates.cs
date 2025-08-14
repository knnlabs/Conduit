using ConduitLLM.Core.Models;
using System.Text.Json;

namespace ConduitLLM.Tests.Http.OpenAICompatibility;

/// <summary>
/// Provides realistic OpenAI-compatible response templates for testing.
/// These responses mimic what real OpenAI APIs return to ensure compatibility.
/// </summary>
public static class OpenAIResponseTemplates
{
    /// <summary>
    /// Standard chat completion response matching OpenAI format
    /// </summary>
    public static ChatCompletionResponse StandardChatResponse(string model = "gpt-3.5-turbo") => new()
    {
        Id = "chatcmpl-8abc123def456",
        Object = "chat.completion",
        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Model = model,
        Choices = new List<Choice>
        {
            new()
            {
                Index = 0,
                Message = new Message
                {
                    Role = "assistant",
                    Content = "Hello! I'm an AI assistant created by OpenAI. How can I help you today?"
                },
                FinishReason = "stop"
            }
        },
        Usage = new Usage
        {
            PromptTokens = 12,
            CompletionTokens = 18,
            TotalTokens = 30
        }
    };
    
    /// <summary>
    /// Chat response with function calling
    /// </summary>
    public static ChatCompletionResponse FunctionCallResponse(string model = "gpt-3.5-turbo") => new()
    {
        Id = "chatcmpl-8abc123def456",
        Object = "chat.completion",
        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Model = model,
        Choices = new List<Choice>
        {
            new()
            {
                Index = 0,
                Message = new Message
                {
                    Role = "assistant",
                    Content = null,
                    ToolCalls = new List<ToolCall>
                    {
                        new()
                        {
                            Id = "call_abc123",
                            Type = "function",
                            Function = new FunctionCall
                            {
                                Name = "get_weather",
                                Arguments = """{"location": "Boston, MA"}"""
                            }
                        }
                    }
                },
                FinishReason = "tool_calls"
            }
        },
        Usage = new Usage
        {
            PromptTokens = 25,
            CompletionTokens = 12,
            TotalTokens = 37
        }
    };
    
    /// <summary>
    /// Vision model response with image analysis
    /// </summary>
    public static ChatCompletionResponse VisionResponse(string model = "gpt-4-vision-preview") => new()
    {
        Id = "chatcmpl-8abc123def456",
        Object = "chat.completion", 
        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Model = model,
        Choices = new List<Choice>
        {
            new()
            {
                Index = 0,
                Message = new Message
                {
                    Role = "assistant",
                    Content = "I can see this is an image of a cat sitting on a windowsill. The cat appears to be orange and white, and is looking out the window at what seems to be a sunny day."
                },
                FinishReason = "stop"
            }
        },
        Usage = new Usage
        {
            PromptTokens = 95, // Images use more tokens
            CompletionTokens = 32,
            TotalTokens = 127
        }
    };
    
    /// <summary>
    /// Embedding response matching OpenAI format
    /// </summary>
    public static object EmbeddingResponse(string model = "text-embedding-ada-002") => new
    {
        Object = "list",
        Model = model,
        Data = new[]
        {
            new
            {
                Object = "embedding",
                Index = 0,
                Embedding = Enumerable.Range(0, 1536).Select(i => Math.Sin(i * 0.1)).ToArray()
            }
        },
        Usage = new
        {
            PromptTokens = 8,
            TotalTokens = 8
        }
    };
    
    /// <summary>
    /// Models list response matching OpenAI format
    /// </summary>
    public static object ModelsListResponse() => new
    {
        Object = "list",
        Data = new[]
        {
            new
            {
                Id = "gpt-3.5-turbo",
                Object = "model",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                OwnedBy = "openai"
            },
            new
            {
                Id = "gpt-4",
                Object = "model", 
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                OwnedBy = "openai"
            },
            new
            {
                Id = "text-embedding-ada-002",
                Object = "model",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                OwnedBy = "openai"
            }
        }
    };
    
    /// <summary>
    /// Error response matching OpenAI error format
    /// </summary>
    public static object ErrorResponse(string errorType, string message) => new
    {
        Error = new
        {
            Type = errorType,
            Message = message,
            Code = errorType switch
            {
                "invalid_request_error" => "invalid_request_error",
                "authentication_error" => "authentication_error", 
                "rate_limit_exceeded" => "rate_limit_exceeded",
                "api_error" => "api_error",
                _ => "unknown_error"
            }
        }
    };
    
    /// <summary>
    /// Generates streaming chunks that match OpenAI's SSE format
    /// </summary>
    public static async IAsyncEnumerable<string> StreamingChatResponse(string model = "gpt-3.5-turbo")
    {
        var baseId = "chatcmpl-8abc123def456";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        // First chunk with role
        yield return JsonSerializer.Serialize(new
        {
            id = baseId,
            @object = "chat.completion.chunk",
            created,
            model,
            choices = new[]
            {
                new
                {
                    index = 0,
                    delta = new { role = "assistant" },
                    finish_reason = (string?)null
                }
            }
        });
        
        await Task.Delay(10); // Simulate streaming delay
        
        // Content chunks
        var words = new[] { "Hello", "!", " I'm", " an", " AI", " assistant", "." };
        foreach (var word in words)
        {
            yield return JsonSerializer.Serialize(new
            {
                id = baseId,
                @object = "chat.completion.chunk",
                created,
                model,
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new { content = word },
                        finish_reason = (string?)null
                    }
                }
            });
            
            await Task.Delay(5); // Simulate streaming delay
        }
        
        // Final chunk with finish reason and usage
        yield return JsonSerializer.Serialize(new
        {
            id = baseId,
            @object = "chat.completion.chunk",
            created,
            model,
            choices = new[]
            {
                new
                {
                    index = 0,
                    delta = new { },
                    finish_reason = "stop"
                }
            },
            usage = new
            {
                prompt_tokens = 10,
                completion_tokens = 7,
                total_tokens = 17
            }
        });
    }
}