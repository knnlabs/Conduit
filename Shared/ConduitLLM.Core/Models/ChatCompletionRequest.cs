using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Represents a request to create a chat completion.
/// </summary>
public class ChatCompletionRequest
{
    /// <summary>
    /// The ID of the model to use for this completion.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; set; }

    /// <summary>
    /// A list of messages comprising the conversation so far.
    /// </summary>
    [JsonPropertyName("messages")]
    public required List<Message> Messages { get; set; }

    /// <summary>
    /// What sampling temperature to use, between 0 and 2. Higher values like 0.8 will make the output more random,
    /// while lower values like 0.2 will make it more focused and deterministic.
    /// </summary>
    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; set; }

    /// <summary>
    /// The maximum number of tokens to generate in the chat completion.
    /// </summary>
    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; set; }

    /// <summary>
    /// An alternative to sampling with temperature, called nucleus sampling, where the model considers the results of
    /// the tokens with top_p probability mass. So 0.1 means only the tokens comprising the top 10% probability mass are considered.
    /// </summary>
    [JsonPropertyName("top_p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TopP { get; set; }

    /// <summary>
    /// Limits the number of tokens to consider for each step of generation.
    /// Only the top K most likely tokens are considered for sampling.
    /// Typical values range from 1 to 100. Lower values make output more focused.
    /// </summary>
    /// <remarks>
    /// Top-k sampling is a technique that restricts the model to only consider 
    /// the K most likely next tokens at each step. This can help prevent the model 
    /// from selecting very unlikely tokens and can make the output more coherent.
    /// Not all providers support this parameter.
    /// </remarks>
    [JsonPropertyName("top_k")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TopK { get; set; }

    /// <summary>
    /// How many chat completion choices to generate for each input message.
    /// </summary>
    [JsonPropertyName("n")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? N { get; set; }

    /// <summary>
    /// If set, partial message deltas will be sent, like in ChatGPT. Tokens will be sent as data-only server-sent events
    /// as they become available, with the stream terminated by a data: [DONE] message.
    /// </summary>
    [JsonPropertyName("stream")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Stream { get; set; }

    /// <summary>
    /// Options for streaming responses. Allows requesting usage data in the final chunk.
    /// </summary>
    /// <remarks>
    /// Set stream_options.include_usage to true to receive token usage information in streaming mode.
    /// Supported by OpenAI and OpenAI-compatible providers (Groq, SambaNova, Cerebras, etc.).
    /// This is critical for accurate billing and performance metrics.
    /// </remarks>
    [JsonPropertyName("stream_options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StreamOptions? StreamOptions { get; set; }

    /// <summary>
    /// Up to 4 sequences where the API will stop generating further tokens.
    /// </summary>
    [JsonPropertyName("stop")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Stop { get; set; }

    /// <summary>
    /// A unique identifier representing your end-user, which can help OpenAI to monitor and detect abuse.
    /// </summary>
    [JsonPropertyName("user")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? User { get; set; }

    /// <summary>
    /// Stable conversation identifier used for provider and prompt-cache affinity. The request body
    /// takes precedence over X-Conduit-Session-Id. Limited to 256 characters.
    /// </summary>
    [JsonPropertyName("session_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [StringLength(256)]
    public string? SessionId { get; set; }

    /// <summary>
    /// A list of tools the model may call. Currently, only functions are supported as tools.
    /// </summary>
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Tool>? Tools { get; set; }

    /// <summary>
    /// Controls which (if any) tool is called by the model. "none" means the model will not call a tool and instead generates a message.
    /// "auto" means the model can choose either to call a tool or not. Specifying a particular function forces the model to call that function.
    /// </summary>
    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ToolChoice? ToolChoice { get; set; }

    /// <summary>
    /// Specifies the format that the model must output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property allows you to control the format of the model's response. For example,
    /// you can request the model to respond with valid JSON by setting ResponseFormat.Type to "json_object".
    /// </para>
    /// <para>
    /// See <see cref="ResponseFormat"/> for details on available format options.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Request JSON response
    /// var request = new ChatCompletionRequest 
    /// {
    ///     // ... other properties
    ///     ResponseFormat = ResponseFormat.Json()
    /// };
    /// </code>
    /// </example>
    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseFormat? ResponseFormat { get; set; }

    /// <summary>
    /// Unified reasoning configuration (effort / max tokens / enabled / exclude). Forwarded to
    /// providers that support reasoning (e.g. OpenRouter). Only serialized when set.
    /// </summary>
    [JsonPropertyName("reasoning")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReasoningConfig? Reasoning { get; set; }

    /// <summary>
    /// A random number seed for deterministic outputs.
    /// </summary>
    [JsonPropertyName("seed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Seed { get; set; }

    /// <summary>
    /// Number between -2.0 and 2.0. Positive values penalize new tokens based on whether they appear 
    /// in the text so far, increasing the model's likelihood to talk about new topics.
    /// </summary>
    [JsonPropertyName("presence_penalty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PresencePenalty { get; set; }

    /// <summary>
    /// Number between -2.0 and 2.0. Positive values penalize new tokens based on their existing 
    /// frequency in the text so far, decreasing the model's likelihood to repeat the same line verbatim.
    /// </summary>
    [JsonPropertyName("frequency_penalty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? FrequencyPenalty { get; set; }

    /// <summary>
    /// Modify the likelihood of specified tokens appearing in the completion.
    /// </summary>
    [JsonPropertyName("logit_bias")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? LogitBias { get; set; }

    /// <summary>
    /// The system fingerprint, a unique identifier for the configuration used by OpenAI systems for this request.
    /// </summary>
    [JsonPropertyName("system_fingerprint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SystemFingerprint { get; set; }
    
    /// <summary>
    /// List of function configuration IDs to make available for this chat session.
    /// When provided, these functions will be converted to Tools and made available for the LLM to call.
    /// </summary>
    /// <remarks>
    /// This is a Conduit-specific extension that maps to internal function configurations.
    /// The functions will be validated for availability and enabled status before being added to the Tools list.
    /// </remarks>
    [JsonPropertyName("function_configuration_ids")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? FunctionConfigurationIds { get; set; }

    /// <summary>
    /// Whether to enable agentic mode, where function calls are automatically executed and results are fed back to the LLM.
    /// If false, function calls will be returned to the caller for manual execution.
    /// </summary>
    /// <remarks>
    /// When enabled, the system will:
    /// 1. Detect tool_calls in LLM responses
    /// 2. Execute the requested functions
    /// 3. Append tool result messages to the conversation
    /// 4. Make a new LLM request with the updated conversation
    /// 5. Repeat until the LLM provides a final answer or max iterations reached
    /// Default: Configurable via GlobalSetting 'Agentic.DefaultEnabled' (fallback: true)
    /// </remarks>
    [JsonPropertyName("enable_agentic_mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EnableAgenticMode { get; set; } = true;

    /// <summary>
    /// Maximum number of agentic iterations (function call loops) allowed.
    /// Prevents infinite loops in agentic workflows.
    /// </summary>
    /// <remarks>
    /// Each iteration consists of: LLM response → function execution → LLM response.
    /// Default: Configurable via GlobalSetting 'Agentic.MaxIterations' (fallback: 5)
    /// Valid Range: Configurable via GlobalSettings 'Agentic.MinIterations' and 'Agentic.MaxIterations' (fallback: 1-100)
    /// If the limit is reached, the system will return the conversation state with an error message.
    /// </remarks>
    [JsonPropertyName("max_agentic_iterations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxAgenticIterations { get; set; } = 5;

    /// <summary>
    /// Additional model-specific parameters that are passed through to the provider API.
    /// These parameters are populated based on the model's ApiParameters configuration.
    /// Examples include reasoning_effort, min_p, language, timestamp_granularities, etc.
    /// </summary>
    /// <remarks>
    /// This property captures any JSON properties not explicitly mapped to other properties, and they
    /// are forwarded to the provider as-is (they are not validated against the model's supported
    /// parameters). Standard mapped parameters take precedence on key collision.
    /// </remarks>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    /// <summary>Resolved server-side prompt caching intent. Never exposed on the Gateway API.</summary>
    [JsonIgnore]
    public PromptCachingIntent? PromptCachingIntent { get; set; }

    /// <summary>Opaque HMAC affinity value generated by the Gateway. Never serialized to callers.</summary>
    [JsonIgnore]
    public string? RoutingAffinityKey { get; set; }

    [JsonIgnore] public int? SelectedMappingId { get; set; }
    [JsonIgnore] public bool RoutingAffinityUsed { get; set; }
    [JsonIgnore] public string? RoutingDecisionReason { get; set; }
    [JsonIgnore] public int RoutingFailoverCount { get; set; }
}
