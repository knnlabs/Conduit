using System.Text.Json;
using ConduitLLM.Providers.Helpers;
using FluentAssertions;
using Xunit;

namespace ConduitLLM.Tests.Providers.Helpers;

public class ContentHelperTests
{
    [Fact]
    public void ShouldPreserveAsArray_WithCacheControl_ReturnsTrue()
    {
        // Arrange — content array with cache_control on a text block
        var json = """
        [
            { "type": "text", "text": "System prompt", "cache_control": { "type": "ephemeral" } },
            { "type": "text", "text": "Hello" }
        ]
        """;
        var content = JsonSerializer.Deserialize<JsonElement>(json);

        // Act
        var result = ContentHelper.ShouldPreserveAsArray(content);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldPreserveAsArray_PlainText_ReturnsFalse()
    {
        // Arrange — plain string content
        var result = ContentHelper.ShouldPreserveAsArray("Hello world");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldPreserveAsArray_NullContent_ReturnsFalse()
    {
        ContentHelper.ShouldPreserveAsArray(null).Should().BeFalse();
    }

    [Fact]
    public void ShouldPreserveAsArray_TextOnlyArray_ReturnsFalse()
    {
        // Arrange — content array with only type/text, no cache_control
        var json = """
        [
            { "type": "text", "text": "Hello" },
            { "type": "text", "text": "World" }
        ]
        """;
        var content = JsonSerializer.Deserialize<JsonElement>(json);

        // Act
        var result = ContentHelper.ShouldPreserveAsArray(content);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldPreserveAsArray_ImageOnly_ReturnsFalse()
    {
        // Arrange — content array with image but no cache_control
        var json = """
        [
            { "type": "text", "text": "Describe this" },
            { "type": "image_url", "image_url": { "url": "https://example.com/img.png" } }
        ]
        """;
        var content = JsonSerializer.Deserialize<JsonElement>(json);

        // Act
        var result = ContentHelper.ShouldPreserveAsArray(content);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldPreserveAsArray_ImageWithCacheControl_ReturnsTrue()
    {
        // Arrange — content array with both image and cache_control
        var json = """
        [
            { "type": "text", "text": "System prompt", "cache_control": { "type": "ephemeral" } },
            { "type": "image_url", "image_url": { "url": "https://example.com/img.png" } }
        ]
        """;
        var content = JsonSerializer.Deserialize<JsonElement>(json);

        // Act
        var result = ContentHelper.ShouldPreserveAsArray(content);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldPreserveAsArray_JsonString_ReturnsFalse()
    {
        // Arrange — JsonElement of kind String
        var content = JsonSerializer.Deserialize<JsonElement>("\"Hello\"");

        // Act
        var result = ContentHelper.ShouldPreserveAsArray(content);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsTextOnly_WithCacheControl_StillReturnsTrue()
    {
        // Verify that IsTextOnly still returns true for text+cache_control (no images)
        // This is important because ShouldPreserveAsArray takes priority in the mapping
        var json = """
        [
            { "type": "text", "text": "Hello", "cache_control": { "type": "ephemeral" } }
        ]
        """;
        var content = JsonSerializer.Deserialize<JsonElement>(json);

        ContentHelper.IsTextOnly(content).Should().BeTrue();
        ContentHelper.ShouldPreserveAsArray(content).Should().BeTrue();
    }

    [Fact]
    public void ShouldPreserveAsArray_ListOfDictsWithCacheControl_ReturnsTrue()
    {
        // Arrange — content produced by PromptCacheInjectionService (string → List<Dictionary>)
        var content = new List<object>
        {
            new Dictionary<string, object>
            {
                ["type"] = "text",
                ["text"] = "System prompt",
                ["cache_control"] = new Dictionary<string, string> { ["type"] = "ephemeral" }
            }
        };

        // Act
        var result = ContentHelper.ShouldPreserveAsArray(content);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldPreserveAsArray_ListOfDictsWithoutCacheControl_ReturnsFalse()
    {
        // Arrange — List<Dictionary> without cache_control
        var content = new List<object>
        {
            new Dictionary<string, object>
            {
                ["type"] = "text",
                ["text"] = "Hello"
            }
        };

        // Act
        var result = ContentHelper.ShouldPreserveAsArray(content);

        // Assert
        result.Should().BeFalse();
    }

}
