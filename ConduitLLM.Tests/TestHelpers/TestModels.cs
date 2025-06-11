using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Tests.TestHelpers
{
    // Bedrock client test models
    public class BedrockClaudeChatResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Role { get; set; } = "assistant";
        public string Content { get; set; } = string.Empty;
        public string StopReason { get; set; } = "stop";
        public BedrockCompletionUsage Usage { get; set; } = new();
    }

    public class BedrockCompletionUsage
    {
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }

    // HuggingFace client test models
    public class HuggingFaceTextGenerationResponse
    {
        [JsonPropertyName("generated_text")]
        public string? GeneratedText { get; set; }
    }

    // SageMaker client test models
    public class SageMakerChatResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("choices")]
        public List<SageMakerChatChoice> Choices { get; set; } = new();

        [JsonPropertyName("usage")]
        public SageMakerChatUsage Usage { get; set; } = new();
    }

    public class SageMakerChatChoice
    {
        [JsonPropertyName("message")]
        public SageMakerChatMessage Message { get; set; } = new();

        [JsonPropertyName("finish_reason")]
        public string FinishReason { get; set; } = "stop";

        [JsonPropertyName("index")]
        public int Index { get; set; }
    }

    public class SageMakerChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "assistant";

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    public class SageMakerChatUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}
