using System.Text.Json;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.OpenAICompatible;
using FluentAssertions;
using Xunit;

namespace ConduitLLM.Tests.Providers;

/// <summary>
/// Tests for cached token extraction from provider-specific usage formats.
/// Tests the internal static ExtractCachedTokensFromExtensionData method.
/// </summary>
public class OpenAICompatibleMappingTests
{
    [Fact]
    public void ExtractCachedTokens_OpenAIFormat_MapsCachedInputTokens()
    {
        // Arrange — OpenAI returns prompt_tokens_details.cached_tokens
        var usageJson = """
        {
            "prompt_tokens": 100,
            "completion_tokens": 50,
            "total_tokens": 150,
            "prompt_tokens_details": {
                "cached_tokens": 80
            }
        }
        """;
        var usage = JsonSerializer.Deserialize<Usage>(usageJson);

        // Act
        OpenAICompatibleClient.ExtractCachedTokensFromExtensionData(usage);

        // Assert
        usage!.CachedInputTokens.Should().Be(80);
        usage.CachedWriteTokens.Should().BeNull();
    }

    [Fact]
    public void ExtractCachedTokens_AnthropicFormat_MapsBothCachedFields()
    {
        // Arrange — Anthropic returns cache_read_input_tokens and cache_creation_input_tokens
        var usageJson = """
        {
            "prompt_tokens": 200,
            "completion_tokens": 50,
            "total_tokens": 250,
            "cache_read_input_tokens": 150,
            "cache_creation_input_tokens": 30
        }
        """;
        var usage = JsonSerializer.Deserialize<Usage>(usageJson);

        // Act
        OpenAICompatibleClient.ExtractCachedTokensFromExtensionData(usage);

        // Assert
        usage!.CachedInputTokens.Should().Be(150);
        usage.CachedWriteTokens.Should().Be(30);
    }

    [Fact]
    public void ExtractCachedTokens_DeepseekFormat_MapsCachedInputTokens()
    {
        // Arrange — Deepseek returns prompt_cache_hit_tokens
        var usageJson = """
        {
            "prompt_tokens": 100,
            "completion_tokens": 50,
            "total_tokens": 150,
            "prompt_cache_hit_tokens": 60
        }
        """;
        var usage = JsonSerializer.Deserialize<Usage>(usageJson);

        // Act
        OpenAICompatibleClient.ExtractCachedTokensFromExtensionData(usage);

        // Assert
        usage!.CachedInputTokens.Should().Be(60);
    }

    [Fact]
    public void ExtractCachedTokens_NullUsage_DoesNotThrow()
    {
        // Act & Assert — should not throw
        OpenAICompatibleClient.ExtractCachedTokensFromExtensionData(null);
    }

    [Fact]
    public void ExtractCachedTokens_NoExtensionData_DoesNotModifyUsage()
    {
        // Arrange — standard usage without any provider-specific fields
        var usage = new Usage
        {
            PromptTokens = 100,
            CompletionTokens = 50,
            TotalTokens = 150
        };

        // Act
        OpenAICompatibleClient.ExtractCachedTokensFromExtensionData(usage);

        // Assert
        usage.CachedInputTokens.Should().BeNull();
        usage.CachedWriteTokens.Should().BeNull();
    }

    [Fact]
    public void ExtractCachedTokens_ExistingCachedTokens_DoesNotOverwrite()
    {
        // Arrange — Usage already has cached_input_tokens set (e.g., from direct JSON deserialization)
        // plus provider-specific extension data that would also map
        var usageJson = """
        {
            "prompt_tokens": 100,
            "completion_tokens": 50,
            "total_tokens": 150,
            "cached_input_tokens": 42,
            "cache_read_input_tokens": 99
        }
        """;
        var usage = JsonSerializer.Deserialize<Usage>(usageJson);

        // Act
        OpenAICompatibleClient.ExtractCachedTokensFromExtensionData(usage);

        // Assert — the named property (42) should be preserved, not overwritten by extension data (99)
        usage!.CachedInputTokens.Should().Be(42);
    }

    [Fact]
    public void ExtractCachedTokens_StreamingChunkUsage_MapsCorrectly()
    {
        // Arrange — simulate a streaming final chunk with usage data (as providers send it)
        var chunkJson = """
        {
            "id": "chatcmpl-123",
            "object": "chat.completion.chunk",
            "created": 1234567890,
            "model": "gpt-4",
            "choices": [],
            "usage": {
                "prompt_tokens": 100,
                "completion_tokens": 50,
                "total_tokens": 150,
                "prompt_tokens_details": {
                    "cached_tokens": 80
                }
            }
        }
        """;
        var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(chunkJson);

        // Act — this is what the streaming code path does
        OpenAICompatibleClient.ExtractCachedTokensFromExtensionData(chunk!.Usage);

        // Assert
        chunk.Usage!.CachedInputTokens.Should().Be(80);
    }
}
