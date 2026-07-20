using System.Text.Json;
using ConduitLLM.Providers.Streaming;

namespace ConduitLLM.Tests.Providers;

public class GroqChunkConverterAccountingTests
{
    [Fact]
    public void Convert_PreservesHostedToolUsageAsServerOnlyEvidence()
    {
        using var document = JsonDocument.Parse("""
        {
          "id": "chunk-1",
          "object": "chat.completion.chunk",
          "model": "provider-model",
          "choices": [],
          "x_groq": {
            "usage": {
              "prompt_tokens": 10,
              "completion_tokens": 4,
              "total_tokens": 14,
              "code_interpreter": 2,
              "code_interpreter_duration_seconds": 3.5
            }
          }
        }
        """);

        var chunk = GroqChunkConverter.Instance.Convert(document.RootElement, "alias");

        Assert.NotNull(chunk);
        var tool = Assert.Single(chunk.ProviderToolUsage!.Tools);
        Assert.Equal("code_interpreter", tool.ToolName);
        Assert.Equal(2, tool.Count);
        Assert.Equal(3.5m, tool.DurationSeconds);
        Assert.DoesNotContain("ProviderToolUsage", JsonSerializer.Serialize(chunk));
    }
}
