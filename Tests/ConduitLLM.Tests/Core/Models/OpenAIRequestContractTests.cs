using System.Text.Json;

using ConduitLLM.Core.Models;

using FluentAssertions;

namespace ConduitLLM.Tests.Core.Models;

[Trait("Category", "Unit")]
[Trait("Component", "OpenAI")]
public sealed class OpenAIRequestContractTests
{
    [Fact]
    public void EmbeddingRequest_WithoutEncodingFormat_DefaultsToFloat()
    {
        var request = JsonSerializer.Deserialize<EmbeddingRequest>(
            """{"model":"text-embedding-3-small","input":"hello"}""");

        request.Should().NotBeNull();
        request!.EncodingFormat.Should().Be("float");
    }

    [Fact]
    public void ImageGenerationRequest_RequiresOnlyPrompt()
    {
        var request = JsonSerializer.Deserialize<ImageGenerationRequest>(
            """{"prompt":"a lighthouse"}""");

        request.Should().NotBeNull();
        request!.Prompt.Should().Be("a lighthouse");
        request.Model.Should().Be("dall-e-2");
    }

    [Theory]
    [InlineData("\"END\"", 1)]
    [InlineData("[\"END\",\"STOP\"]", 2)]
    public void ChatStop_AcceptsStringOrStringArray(string stop, int expectedCount)
    {
        var request = JsonSerializer.Deserialize<ChatCompletionRequest>(
            $$"""{"model":"gpt-4o","messages":[],"stop":{{stop}}}""");

        request!.Stop.Should().HaveCount(expectedCount);
        request.Stop[0].Should().Be("END");
    }

    [Fact]
    public void ChatStop_RejectsNonStringArrayElements()
    {
        var act = () => JsonSerializer.Deserialize<ChatCompletionRequest>(
            """{"model":"gpt-4o","messages":[],"stop":["END",1]}""");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void ChatRequest_SerializesModernTypedFieldsAndNoSystemFingerprint()
    {
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = [],
            MaxCompletionTokens = 100,
            ReasoningEffort = "medium",
            ParallelToolCalls = true,
            Modalities = ["text", "audio"],
            Audio = new ChatAudioOptions { Voice = "alloy", Format = "wav" },
            Logprobs = true,
            TopLogprobs = 3,
            ServiceTier = "auto",
            Store = false,
            SafetyIdentifier = "user-123",
            Verbosity = "low",
            PromptCacheKey = "cache-key",
            PromptCacheRetention = "24h",
            ToolChoice = ToolChoice.Required
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(request));
        var root = json.RootElement;
        root.GetProperty("max_completion_tokens").GetInt32().Should().Be(100);
        root.GetProperty("reasoning_effort").GetString().Should().Be("medium");
        root.GetProperty("parallel_tool_calls").GetBoolean().Should().BeTrue();
        root.GetProperty("audio").GetProperty("format").GetString().Should().Be("wav");
        root.GetProperty("tool_choice").GetString().Should().Be("required");
        root.TryGetProperty("system_fingerprint", out _).Should().BeFalse();
    }
}
