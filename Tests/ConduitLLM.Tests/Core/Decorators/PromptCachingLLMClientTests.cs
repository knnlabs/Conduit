using System.Text.Json;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Decorators;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Decorators;

public class PromptCachingLLMClientTests
{
    private readonly Mock<ILLMClient> _innerClient = new();
    private readonly Mock<IGlobalSettingsCacheService> _settingsService = new();
    private readonly Mock<ILogger<PromptCachingLLMClient>> _logger = new();

    private PromptCachingLLMClient CreateSut()
        => new(_innerClient.Object, _settingsService.Object, _logger.Object);

    private static ChatCompletionRequest CreateRequest()
        => new()
        {
            Model = "test-model",
            Messages = new List<Message>
            {
                new() { Role = "system", Content = "You are helpful." },
                new() { Role = "user", Content = "Hello" }
            }
        };

    [Fact]
    public async Task CreateChatCompletion_WhenDisabled_PassesThroughUnmodified()
    {
        // Arrange
        var config = new PromptCachingConfig { AutoInjectEnabled = false };
        _settingsService
            .Setup(s => s.GetSettingValueAsync(PromptCachingLLMClient.SettingsKey))
            .ReturnsAsync(JsonSerializer.Serialize(config));

        var request = CreateRequest();
        var expectedResponse = new ChatCompletionResponse
        {
            Id = "test",
            Model = "test-model",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Choices = new List<Choice>()
        };
        _innerClient
            .Setup(c => c.CreateChatCompletionAsync(request, null, default))
            .ReturnsAsync(expectedResponse);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateChatCompletionAsync(request);

        // Assert
        result.Should().BeSameAs(expectedResponse);
        request.Messages[0].Content.Should().Be("You are helpful.");
    }

    [Fact]
    public async Task CreateChatCompletion_WhenEnabled_InjectsCacheControl()
    {
        // Arrange
        var config = new PromptCachingConfig
        {
            AutoInjectEnabled = true,
            InjectionPoints = new List<CacheInjectionPoint>
            {
                new() { Role = "system" }
            }
        };
        _settingsService
            .Setup(s => s.GetSettingValueAsync(PromptCachingLLMClient.SettingsKey))
            .ReturnsAsync(JsonSerializer.Serialize(config));

        var request = CreateRequest();
        _innerClient
            .Setup(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), null, default))
            .ReturnsAsync(new ChatCompletionResponse
            {
                Id = "test",
                Model = "test-model",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Choices = new List<Choice>()
            });

        var sut = CreateSut();

        // Act
        await sut.CreateChatCompletionAsync(request);

        // Assert — system message should have been converted to content array with cache_control
        request.Messages[0].Content.Should().BeAssignableTo<IList<object>>();
        _innerClient.Verify(c => c.CreateChatCompletionAsync(request, null, default), Times.Once);
    }

    [Fact]
    public async Task CreateChatCompletion_WhenSettingMissing_PassesThroughUnmodified()
    {
        // Arrange
        _settingsService
            .Setup(s => s.GetSettingValueAsync(PromptCachingLLMClient.SettingsKey))
            .ReturnsAsync((string?)null);

        var request = CreateRequest();
        _innerClient
            .Setup(c => c.CreateChatCompletionAsync(request, null, default))
            .ReturnsAsync(new ChatCompletionResponse
            {
                Id = "test",
                Model = "test-model",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Choices = new List<Choice>()
            });

        var sut = CreateSut();

        // Act
        await sut.CreateChatCompletionAsync(request);

        // Assert
        request.Messages[0].Content.Should().Be("You are helpful.");
    }

    [Fact]
    public async Task CreateChatCompletion_WhenSettingsThrows_ContinuesWithoutInjection()
    {
        // Arrange
        _settingsService
            .Setup(s => s.GetSettingValueAsync(PromptCachingLLMClient.SettingsKey))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var request = CreateRequest();
        _innerClient
            .Setup(c => c.CreateChatCompletionAsync(request, null, default))
            .ReturnsAsync(new ChatCompletionResponse
            {
                Id = "test",
                Model = "test-model",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Choices = new List<Choice>()
            });

        var sut = CreateSut();

        // Act — should NOT throw
        var result = await sut.CreateChatCompletionAsync(request);

        // Assert — request should be unmodified, inner client still called
        request.Messages[0].Content.Should().Be("You are helpful.");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task StreamChatCompletion_WhenEnabled_InjectsCacheControl()
    {
        // Arrange
        var config = new PromptCachingConfig
        {
            AutoInjectEnabled = true,
            InjectionPoints = new List<CacheInjectionPoint>
            {
                new() { Role = "system" }
            }
        };
        _settingsService
            .Setup(s => s.GetSettingValueAsync(PromptCachingLLMClient.SettingsKey))
            .ReturnsAsync(JsonSerializer.Serialize(config));

        var chunks = new List<ChatCompletionChunk>
        {
            new() { Id = "chunk-1", Choices = new List<StreamingChoice>() }
        };
        _innerClient
            .Setup(c => c.StreamChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), null, default))
            .Returns(chunks.ToAsyncEnumerable());

        var request = CreateRequest();
        var sut = CreateSut();

        // Act
        var results = new List<ChatCompletionChunk>();
        await foreach (var chunk in sut.StreamChatCompletionAsync(request))
        {
            results.Add(chunk);
        }

        // Assert
        results.Should().HaveCount(1);
        request.Messages[0].Content.Should().BeAssignableTo<IList<object>>();
    }

    [Fact]
    public async Task NonChatMethods_PassThrough()
    {
        // Arrange
        _innerClient.Setup(c => c.ListModelsAsync(null, default))
            .ReturnsAsync(new List<string> { "model-1" });

        var sut = CreateSut();

        // Act
        var models = await sut.ListModelsAsync();

        // Assert
        models.Should().Contain("model-1");
    }
}
