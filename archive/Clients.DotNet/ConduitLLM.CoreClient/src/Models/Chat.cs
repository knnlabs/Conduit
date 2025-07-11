using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.CoreClient.Models;

/// <summary>
/// Represents a message in a chat completion.
/// </summary>
public class ChatCompletionMessage
{
    /// <summary>
    /// Gets or sets the role of the message author.
    /// </summary>
    [Required]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content of the message.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Gets or sets the name of the message author (optional).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the tool calls made by the assistant.
    /// </summary>
    public IEnumerable<ToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// Gets or sets the tool call ID for tool response messages.
    /// </summary>
    public string? ToolCallId { get; set; }
}

/// <summary>
/// Represents a chat completion request.
/// </summary>
public class ChatCompletionRequest
{
    /// <summary>
    /// Gets or sets the model to use for completion.
    /// </summary>
    [Required]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the messages for the conversation.
    /// </summary>
    [Required]
    public IEnumerable<ChatCompletionMessage> Messages { get; set; } = new List<ChatCompletionMessage>();

    /// <summary>
    /// Gets or sets the frequency penalty (-2.0 to 2.0).
    /// </summary>
    [Range(-2.0, 2.0)]
    public double? FrequencyPenalty { get; set; }

    /// <summary>
    /// Gets or sets the logit bias for specific tokens.
    /// </summary>
    public Dictionary<string, int>? LogitBias { get; set; }

    /// <summary>
    /// Gets or sets whether to return log probabilities.
    /// </summary>
    public bool? Logprobs { get; set; }

    /// <summary>
    /// Gets or sets the number of most likely tokens to return log probabilities for.
    /// </summary>
    [Range(0, 20)]
    public int? TopLogprobs { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of tokens to generate.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of completions to generate.
    /// </summary>
    [Range(1, 128)]
    public int? N { get; set; }

    /// <summary>
    /// Gets or sets the presence penalty (-2.0 to 2.0).
    /// </summary>
    [Range(-2.0, 2.0)]
    public double? PresencePenalty { get; set; }

    /// <summary>
    /// Gets or sets the response format.
    /// </summary>
    public ResponseFormat? ResponseFormat { get; set; }

    /// <summary>
    /// Gets or sets the seed for deterministic sampling.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// Gets or sets the stop sequences.
    /// </summary>
    public object? Stop { get; set; } // Can be string or string[]

    /// <summary>
    /// Gets or sets whether to stream the response.
    /// </summary>
    public bool? Stream { get; set; }

    /// <summary>
    /// Gets or sets the sampling temperature (0 to 2).
    /// </summary>
    [Range(0.0, 2.0)]
    public double? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the nucleus sampling parameter (0 to 1).
    /// </summary>
    [Range(0.0, 1.0)]
    public double? TopP { get; set; }

    /// <summary>
    /// Gets or sets the tools available to the model.
    /// </summary>
    public IEnumerable<Tool>? Tools { get; set; }

    /// <summary>
    /// Gets or sets how the model should choose tools.
    /// </summary>
    public object? ToolChoice { get; set; } // Can be string or object

    /// <summary>
    /// Gets or sets a unique identifier for the end-user.
    /// </summary>
    public string? User { get; set; }
}

/// <summary>
/// Represents a choice in a chat completion response.
/// </summary>
public class ChatCompletionChoice
{
    /// <summary>
    /// Gets or sets the index of this choice.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the completion message.
    /// </summary>
    public ChatCompletionMessage Message { get; set; } = new();

    /// <summary>
    /// Gets or sets the log probabilities (if requested).
    /// </summary>
    public object? Logprobs { get; set; }

    /// <summary>
    /// Gets or sets the reason the completion finished.
    /// </summary>
    public FinishReason FinishReason { get; set; }
}

/// <summary>
/// Represents a chat completion response.
/// </summary>
public class ChatCompletionResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for the completion.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the object type.
    /// </summary>
    public string Object { get; set; } = "chat.completion";

    /// <summary>
    /// Gets or sets the Unix timestamp when the completion was created.
    /// </summary>
    public long Created { get; set; }

    /// <summary>
    /// Gets or sets the model used for the completion.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the system fingerprint.
    /// </summary>
    public string? SystemFingerprint { get; set; }

    /// <summary>
    /// Gets or sets the list of completion choices.
    /// </summary>
    public IEnumerable<ChatCompletionChoice> Choices { get; set; } = new List<ChatCompletionChoice>();

    /// <summary>
    /// Gets or sets the token usage information.
    /// </summary>
    public Usage Usage { get; set; } = new();

    /// <summary>
    /// Gets or sets the performance metrics.
    /// </summary>
    public PerformanceMetrics? Performance { get; set; }
}

/// <summary>
/// Represents a choice in a streaming chat completion chunk.
/// </summary>
public class ChatCompletionChunkChoice
{
    /// <summary>
    /// Gets or sets the index of this choice.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the delta content for this chunk.
    /// </summary>
    public ChatCompletionMessage Delta { get; set; } = new();

    /// <summary>
    /// Gets or sets the log probabilities (if requested).
    /// </summary>
    public object? Logprobs { get; set; }

    /// <summary>
    /// Gets or sets the reason the completion finished.
    /// </summary>
    public FinishReason FinishReason { get; set; }
}

/// <summary>
/// Represents a chunk in a streaming chat completion response.
/// </summary>
public class ChatCompletionChunk
{
    /// <summary>
    /// Gets or sets the unique identifier for the completion.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the object type.
    /// </summary>
    public string Object { get; set; } = "chat.completion.chunk";

    /// <summary>
    /// Gets or sets the Unix timestamp when the chunk was created.
    /// </summary>
    public long Created { get; set; }

    /// <summary>
    /// Gets or sets the model used for the completion.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the system fingerprint.
    /// </summary>
    public string? SystemFingerprint { get; set; }

    /// <summary>
    /// Gets or sets the list of completion choices for this chunk.
    /// </summary>
    public IEnumerable<ChatCompletionChunkChoice> Choices { get; set; } = new List<ChatCompletionChunkChoice>();

    /// <summary>
    /// Gets or sets the token usage information (only present in the final chunk).
    /// </summary>
    public Usage? Usage { get; set; }

    /// <summary>
    /// Gets or sets the performance metrics (only present in the final chunk).
    /// </summary>
    public PerformanceMetrics? Performance { get; set; }
}