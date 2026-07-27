using System.Text.Json;

using ConduitLLM.Core.Models;

using AwesomeAssertions;

using Xunit;

namespace ConduitLLM.Tests.Core.Models
{
    /// <summary>
    /// Tests the wire shape of ReasoningConfig and its placement on ChatCompletionRequest.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Core")]
    public class ReasoningConfigTests
    {
        [Fact]
        public void ReasoningConfig_EffortOnly_SerializesOnlyEffort()
        {
            var json = JsonSerializer.Serialize(new ReasoningConfig { Effort = "high" });

            json.Should().Be("{\"effort\":\"high\"}");
        }

        [Fact]
        public void ReasoningConfig_MaxTokensAndExclude_SerializeSetFieldsOnly()
        {
            var json = JsonSerializer.Serialize(new ReasoningConfig { MaxTokens = 2000, Exclude = true });

            json.Should().Contain("\"max_tokens\":2000");
            json.Should().Contain("\"exclude\":true");
            json.Should().NotContain("effort");
            json.Should().NotContain("enabled");
        }

        [Fact]
        public void ChatCompletionRequest_WithReasoning_SerializesReasoningObject()
        {
            var request = new ChatCompletionRequest
            {
                Model = "openrouter/some-model",
                Messages = new List<Message> { new() { Role = "user", Content = "hi" } },
                Reasoning = new ReasoningConfig { Effort = "medium" }
            };

            var json = JsonSerializer.Serialize(request);

            json.Should().Contain("\"reasoning\":{\"effort\":\"medium\"}");
        }

        [Fact]
        public void ChatCompletionRequest_WithoutReasoning_OmitsReasoningKey()
        {
            var request = new ChatCompletionRequest
            {
                Model = "openrouter/some-model",
                Messages = new List<Message> { new() { Role = "user", Content = "hi" } }
            };

            var json = JsonSerializer.Serialize(request);

            json.Should().NotContain("reasoning");
        }
    }
}
