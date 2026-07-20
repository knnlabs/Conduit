using System.Text.Json;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Decorators;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Core.Decorators;

public class PromptCachingLLMClientTests
{
    private readonly Mock<ILLMClient> _inner = new();
    private readonly Mock<IGlobalSettingsCacheService> _settings = new();
    private readonly Mock<ILogger<PromptCachingLLMClient>> _logger = new();

    private static ChatCompletionRequest Request() => new()
    {
        Model = "alias",
        Messages = [new Message { Role = "system", Content = "Original" }]
    };

    private static ChatCompletionResponse Response() => new()
    {
        Id = "id", Created = 1, Model = "model", Object = "chat.completion", Choices = []
    };

    private PromptCachingLLMClient Client(string provider, string model) =>
        new(_inner.Object, _settings.Object, _logger.Object, provider, model);

    private void Configure(PromptCachingConfig config) => _settings
        .Setup(s => s.GetSettingValueAsync(PromptCachingLLMClient.SettingsKey))
        .ReturnsAsync(JsonSerializer.Serialize(config));

    [Fact]
    public async Task SupportedRoute_SetsIntentWithoutMutatingMessages()
    {
        Configure(new PromptCachingConfig
        {
            SchemaVersion = 3,
            Enabled = true,
            Rules = [new PromptCachingRule { Name = "Claude", Provider = "OpenRouter", ModelPattern = "anthropic/*", Strategy = PromptCachingStrategy.Automatic }]
        });
        var request = Request();
        _inner.Setup(c => c.CreateChatCompletionAsync(request, null, default)).ReturnsAsync(Response());

        await Client("OpenRouter", "anthropic/claude-sonnet-4").CreateChatCompletionAsync(request);

        request.PromptCachingIntent.Should().NotBeNull();
        request.Messages[0].Content.Should().Be("Original");
    }

    [Fact]
    public async Task UnsupportedRoute_RemainsUnchanged()
    {
        Configure(new PromptCachingConfig
        {
            SchemaVersion = 3,
            Enabled = true,
            Rules = [new PromptCachingRule { Name = "Claude", Provider = "OpenRouter", ModelPattern = "anthropic/*", Strategy = PromptCachingStrategy.Automatic }]
        });
        var request = Request();
        _inner.Setup(c => c.CreateChatCompletionAsync(request, null, default)).ReturnsAsync(Response());

        await Client("Replicate", "meta/llama").CreateChatCompletionAsync(request);

        request.PromptCachingIntent.Should().BeNull();
        request.Messages[0].Content.Should().Be("Original");
    }

    [Fact]
    public async Task InvalidLegacyConfig_FailsOpenWithoutIntent()
    {
        _settings.Setup(s => s.GetSettingValueAsync(PromptCachingLLMClient.SettingsKey))
            .ReturnsAsync("{\"auto_inject_enabled\":true}");
        var request = Request();
        _inner.Setup(c => c.CreateChatCompletionAsync(request, null, default)).ReturnsAsync(Response());

        await Client("OpenRouter", "anthropic/claude-sonnet-4").CreateChatCompletionAsync(request);

        request.PromptCachingIntent.Should().BeNull();
    }
}
