using System.Text.Json;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using FluentAssertions;
using Xunit;

namespace ConduitLLM.Tests.Core.Services;

public class PromptCacheInjectionServiceTests
{
    private static ChatCompletionRequest CreateRequest(params Message[] messages)
    {
        return new ChatCompletionRequest
        {
            Model = "test-model",
            Messages = messages.ToList()
        };
    }

    [Fact]
    public void InjectCacheControl_ByRole_InjectsOnSystemMessage()
    {
        // Arrange
        var request = CreateRequest(
            new Message { Role = "system", Content = "You are a helpful assistant." },
            new Message { Role = "user", Content = "Hello" }
        );

        var config = new PromptCachingConfig
        {
            AutoInjectEnabled = true,
            InjectionPoints = new List<CacheInjectionPoint>
            {
                new() { Role = "system" }
            }
        };

        // Act
        PromptCacheInjectionService.InjectCacheControl(request, config);

        // Assert — system message should now be a content array with cache_control
        var content = request.Messages[0].Content;
        content.Should().BeAssignableTo<IList<object>>();

        var contentList = (IList<object>)content!;
        contentList.Should().HaveCount(1);

        var block = contentList[0] as Dictionary<string, object>;
        block.Should().NotBeNull();
        block!["type"].Should().Be("text");
        block["text"].Should().Be("You are a helpful assistant.");
        block.Should().ContainKey("cache_control");

        // User message should be unchanged
        request.Messages[1].Content.Should().Be("Hello");
    }

    [Fact]
    public void InjectCacheControl_ByNegativeIndex_InjectsOnLastMessage()
    {
        // Arrange
        var request = CreateRequest(
            new Message { Role = "user", Content = "First message" },
            new Message { Role = "user", Content = "Second message" },
            new Message { Role = "user", Content = "Third message" }
        );

        var config = new PromptCachingConfig
        {
            AutoInjectEnabled = true,
            InjectionPoints = new List<CacheInjectionPoint>
            {
                new() { Role = "user", Index = -1 }
            }
        };

        // Act
        PromptCacheInjectionService.InjectCacheControl(request, config);

        // Assert — only the last user message should be modified
        request.Messages[0].Content.Should().Be("First message");
        request.Messages[1].Content.Should().Be("Second message");

        var content = request.Messages[2].Content;
        content.Should().BeAssignableTo<IList<object>>();
    }

    [Fact]
    public void InjectCacheControl_ByIndex_InjectsOnFirstMessage()
    {
        // Arrange
        var request = CreateRequest(
            new Message { Role = "user", Content = "First" },
            new Message { Role = "user", Content = "Second" }
        );

        var config = new PromptCachingConfig
        {
            AutoInjectEnabled = true,
            InjectionPoints = new List<CacheInjectionPoint>
            {
                new() { Role = "user", Index = 0 }
            }
        };

        // Act
        PromptCacheInjectionService.InjectCacheControl(request, config);

        // Assert — only the first user message should be modified
        request.Messages[0].Content.Should().BeAssignableTo<IList<object>>();
        request.Messages[1].Content.Should().Be("Second");
    }

    [Fact]
    public void InjectCacheControl_JsonElementContent_PreservesExistingBlocksAndAddsCacheControl()
    {
        // Arrange — content is already a JSON array (as it would be from deserialization)
        var contentJson = """
        [
            { "type": "text", "text": "Part 1" },
            { "type": "text", "text": "Part 2" }
        ]
        """;
        var jsonContent = JsonSerializer.Deserialize<JsonElement>(contentJson);

        var request = CreateRequest(
            new Message { Role = "system", Content = jsonContent }
        );

        var config = new PromptCachingConfig
        {
            AutoInjectEnabled = true,
            InjectionPoints = new List<CacheInjectionPoint>
            {
                new() { Role = "system" }
            }
        };

        // Act
        PromptCacheInjectionService.InjectCacheControl(request, config);

        // Assert — should have 2 blocks, last one with cache_control
        var content = request.Messages[0].Content as IList<object>;
        content.Should().NotBeNull();
        content.Should().HaveCount(2);

        var lastBlock = content![1] as Dictionary<string, object?>;
        lastBlock.Should().NotBeNull();
        lastBlock.Should().ContainKey("cache_control");

        // First block should NOT have cache_control
        var firstBlock = content[0] as Dictionary<string, object?>;
        firstBlock.Should().NotBeNull();
        firstBlock.Should().NotContainKey("cache_control");
    }

    [Fact]
    public void InjectCacheControl_MaxFourBlocks_StopsAtLimit()
    {
        // Arrange — 5 system messages, should only inject on first 4
        var messages = Enumerable.Range(1, 5)
            .Select(i => new Message { Role = "system", Content = $"Message {i}" })
            .ToArray();
        var request = CreateRequest(messages);

        var config = new PromptCachingConfig
        {
            AutoInjectEnabled = true,
            InjectionPoints = new List<CacheInjectionPoint>
            {
                new() { Role = "system" } // Matches all 5
            }
        };

        // Act
        PromptCacheInjectionService.InjectCacheControl(request, config);

        // Assert — first 4 should be modified, 5th should be unchanged
        for (int i = 0; i < 4; i++)
        {
            request.Messages[i].Content.Should().BeAssignableTo<IList<object>>(
                $"Message {i} should be converted to content array");
        }

        request.Messages[4].Content.Should().Be("Message 5",
            "5th message should be unchanged (max 4 cache blocks)");
    }

    [Fact]
    public void InjectCacheControl_Disabled_DoesNothing()
    {
        // Arrange
        var request = CreateRequest(
            new Message { Role = "system", Content = "System prompt" }
        );

        var config = new PromptCachingConfig
        {
            AutoInjectEnabled = false,
            InjectionPoints = new List<CacheInjectionPoint>
            {
                new() { Role = "system" }
            }
        };

        // Act
        PromptCacheInjectionService.InjectCacheControl(request, config);

        // Assert — should be unchanged
        request.Messages[0].Content.Should().Be("System prompt");
    }

    [Fact]
    public void InjectCacheControl_EmptyInjectionPoints_DoesNothing()
    {
        // Arrange
        var request = CreateRequest(
            new Message { Role = "system", Content = "System prompt" }
        );

        var config = new PromptCachingConfig
        {
            AutoInjectEnabled = true,
            InjectionPoints = new List<CacheInjectionPoint>()
        };

        // Act
        PromptCacheInjectionService.InjectCacheControl(request, config);

        // Assert
        request.Messages[0].Content.Should().Be("System prompt");
    }

    [Fact]
    public void InjectCacheControl_NoMatchingRole_DoesNothing()
    {
        // Arrange
        var request = CreateRequest(
            new Message { Role = "user", Content = "Hello" }
        );

        var config = new PromptCachingConfig
        {
            AutoInjectEnabled = true,
            InjectionPoints = new List<CacheInjectionPoint>
            {
                new() { Role = "system" }
            }
        };

        // Act
        PromptCacheInjectionService.InjectCacheControl(request, config);

        // Assert
        request.Messages[0].Content.Should().Be("Hello");
    }

    [Fact]
    public void InjectCacheControl_NullRole_MatchesAnyRole()
    {
        // Arrange
        var request = CreateRequest(
            new Message { Role = "system", Content = "System" },
            new Message { Role = "user", Content = "User" }
        );

        var config = new PromptCachingConfig
        {
            AutoInjectEnabled = true,
            InjectionPoints = new List<CacheInjectionPoint>
            {
                new() { Role = null, Index = -1 } // Last message of any role
            }
        };

        // Act
        PromptCacheInjectionService.InjectCacheControl(request, config);

        // Assert — only last message should be modified
        request.Messages[0].Content.Should().Be("System");
        request.Messages[1].Content.Should().BeAssignableTo<IList<object>>();
    }

    [Fact]
    public void InjectCacheControl_OutOfRangeIndex_DoesNothing()
    {
        // Arrange
        var request = CreateRequest(
            new Message { Role = "system", Content = "Only one" }
        );

        var config = new PromptCachingConfig
        {
            AutoInjectEnabled = true,
            InjectionPoints = new List<CacheInjectionPoint>
            {
                new() { Role = "system", Index = 5 } // Out of range
            }
        };

        // Act
        PromptCacheInjectionService.InjectCacheControl(request, config);

        // Assert
        request.Messages[0].Content.Should().Be("Only one");
    }
}
