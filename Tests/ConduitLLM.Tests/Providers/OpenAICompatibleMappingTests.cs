using System.Text.Json;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.OpenAICompatible;
using AwesomeAssertions;
using Xunit;

namespace ConduitLLM.Tests.Providers;

/// <summary>
/// Tests for provider-specific usage extraction (cached tokens, cache-write tokens, and
/// provider-reported cost) from provider usage formats.
/// Tests the internal static ExtractProviderUsageFromExtensionData method.
/// </summary>
public class OpenAICompatibleMappingTests
{
    [Fact]
    public void MapGroqHostedToolUsage_MapsServerOnlyEvidence()
    {
        using var document = JsonDocument.Parse("""
        {
          "usage": {
            "code_interpreter": 2,
            "code_interpreter_duration_seconds": 3.5
          }
        }
        """);

        var usage = OpenAICompatibleClient.MapGroqHostedToolUsage(document.RootElement);

        var tool = Assert.Single(usage!.Tools);
        Assert.Equal("code_interpreter", tool.ToolName);
        Assert.Equal(2, tool.Count);
        Assert.Equal(3.5m, tool.DurationSeconds);

        var response = new ChatCompletionResponse
        {
            Id = "response-1",
            Choices = [],
            Created = 1,
            Model = "model",
            Object = "chat.completion",
            ProviderToolUsage = usage
        };
        Assert.DoesNotContain("ProviderToolUsage", JsonSerializer.Serialize(response));
    }

    [Fact]
    public void ExtractProviderUsage_OpenAIFormat_MapsCachedInputTokens()
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
        OpenAICompatibleClient.ExtractProviderUsageFromExtensionData(usage);

        // Assert
        usage!.CachedInputTokens.Should().Be(80);
        usage.CachedWriteTokens.Should().BeNull();
    }

    [Fact]
    public void ExtractProviderUsage_AnthropicFormat_MapsBothCachedFields()
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
        OpenAICompatibleClient.ExtractProviderUsageFromExtensionData(usage);

        // Assert
        usage!.CachedInputTokens.Should().Be(150);
        usage.CachedWriteTokens.Should().Be(30);
    }

    [Fact]
    public void ExtractProviderUsage_DeepseekFormat_MapsCachedInputTokens()
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
        OpenAICompatibleClient.ExtractProviderUsageFromExtensionData(usage);

        // Assert
        usage!.CachedInputTokens.Should().Be(60);
    }

    [Fact]
    public void ExtractProviderUsage_OpenRouterCacheWriteTokens_MapsCacheWrite()
    {
        // Arrange — OpenRouter nests cache write under prompt_tokens_details.cache_write_tokens
        var usageJson = """
        {
            "prompt_tokens": 100,
            "completion_tokens": 50,
            "total_tokens": 150,
            "prompt_tokens_details": {
                "cached_tokens": 40,
                "cache_write_tokens": 25
            }
        }
        """;
        var usage = JsonSerializer.Deserialize<Usage>(usageJson);

        // Act
        OpenAICompatibleClient.ExtractProviderUsageFromExtensionData(usage);

        // Assert
        usage!.CachedInputTokens.Should().Be(40);
        usage.CachedWriteTokens.Should().Be(25);
    }

    [Fact]
    public void ExtractProviderUsage_OpenRouterCost_CapturesAndStripsCost()
    {
        // Arrange — OpenRouter returns usage.cost (USD credits charged)
        var usageJson = """
        {
            "prompt_tokens": 100,
            "completion_tokens": 50,
            "total_tokens": 150,
            "cost": 0.00123
        }
        """;
        var usage = JsonSerializer.Deserialize<Usage>(usageJson);

        // Act
        OpenAICompatibleClient.ExtractProviderUsageFromExtensionData(usage);

        // Assert — captured onto the server-only field and removed from ExtensionData
        usage!.ProviderReportedCostUsd.Should().Be(0.00123m);
        (usage.ExtensionData == null || !usage.ExtensionData.ContainsKey("cost")).Should().BeTrue();
    }

    [Fact]
    public void ExtractProviderUsage_ZeroCost_CapturedAsZero()
    {
        // Arrange — free variants report cost: 0; we must bill 0, not fall back to ModelCost
        var usageJson = """
        {
            "prompt_tokens": 100,
            "completion_tokens": 50,
            "total_tokens": 150,
            "cost": 0
        }
        """;
        var usage = JsonSerializer.Deserialize<Usage>(usageJson);

        // Act
        OpenAICompatibleClient.ExtractProviderUsageFromExtensionData(usage);

        // Assert
        usage!.ProviderReportedCostUsd.Should().Be(0m);
    }

    [Fact]
    public void ExtractProviderUsage_Cost_NotSerializedBackToClient()
    {
        // Arrange — a streaming final chunk carrying usage.cost, as OpenRouter sends it
        var chunkJson = """
        {
            "id": "chatcmpl-123",
            "object": "chat.completion.chunk",
            "created": 1234567890,
            "model": "openai/gpt-4o",
            "choices": [],
            "usage": {
                "prompt_tokens": 100,
                "completion_tokens": 50,
                "total_tokens": 150,
                "cost": 0.0042
            }
        }
        """;
        var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(chunkJson);

        // Act — the streaming code path post-processes usage before yielding the chunk
        OpenAICompatibleClient.ExtractProviderUsageFromExtensionData(chunk!.Usage);
        var reserialized = JsonSerializer.Serialize(chunk);

        // Assert — cost captured for billing but never re-serialized to the client
        chunk.Usage!.ProviderReportedCostUsd.Should().Be(0.0042m);
        reserialized.Should().NotContain("cost");
    }

    [Fact]
    public void ExtractProviderUsage_NullUsage_DoesNotThrow()
    {
        // Act & Assert — should not throw
        OpenAICompatibleClient.ExtractProviderUsageFromExtensionData(null);
    }

    [Fact]
    public void ExtractProviderUsage_NoExtensionData_DoesNotModifyUsage()
    {
        // Arrange — standard usage without any provider-specific fields
        var usage = new Usage
        {
            PromptTokens = 100,
            CompletionTokens = 50,
            TotalTokens = 150
        };

        // Act
        OpenAICompatibleClient.ExtractProviderUsageFromExtensionData(usage);

        // Assert
        usage.CachedInputTokens.Should().BeNull();
        usage.CachedWriteTokens.Should().BeNull();
        usage.ProviderReportedCostUsd.Should().BeNull();
    }

    [Fact]
    public void ExtractProviderUsage_ExistingCachedTokens_DoesNotOverwrite()
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
        OpenAICompatibleClient.ExtractProviderUsageFromExtensionData(usage);

        // Assert — the named property (42) should be preserved, not overwritten by extension data (99)
        usage!.CachedInputTokens.Should().Be(42);
    }

    [Fact]
    public void ExtractProviderUsage_StreamingChunkUsage_MapsCorrectly()
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
        OpenAICompatibleClient.ExtractProviderUsageFromExtensionData(chunk!.Usage);

        // Assert
        chunk.Usage!.CachedInputTokens.Should().Be(80);
    }
}
