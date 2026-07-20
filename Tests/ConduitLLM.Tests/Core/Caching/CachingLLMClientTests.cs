using ConduitLLM.Core.Caching;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Core.Caching;

[Trait("Category", "Unit")]
public class CachingLLMClientTests
{
    private readonly Mock<ILLMClient> _innerClient = new();
    private readonly Mock<ICacheManager> _cacheManager = new();
    private readonly Mock<ILogger<CachingLLMClient>> _logger = new();

    private CachingLLMClient CreateClient() => new(_innerClient.Object, _cacheManager.Object, _logger.Object);

    [Fact]
    public async Task CreateChatCompletionAsync_AlwaysCallsUnderlyingClientWithoutUsingCache()
    {
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = [new Message { Role = "user", Content = "Hello" }]
        };
        var expected = new ChatCompletionResponse
        {
            Id = "provider-id",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "gpt-4",
            Choices = []
        };
        _innerClient.Setup(x => x.CreateChatCompletionAsync(request, null, default)).ReturnsAsync(expected);

        var result = await CreateClient().CreateChatCompletionAsync(request);

        Assert.Same(expected, result);
        _innerClient.Verify(x => x.CreateChatCompletionAsync(request, null, default), Times.Once);
        _cacheManager.Verify(x => x.GetAsync<ChatCompletionResponse>(It.IsAny<string>(), It.IsAny<CacheRegion>(), default), Times.Never);
        _cacheManager.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ChatCompletionResponse>(), It.IsAny<CacheRegion>(), It.IsAny<TimeSpan?>(), default), Times.Never);
    }

    [Fact]
    public async Task ListModelsAsync_UsesOneHourModelMetadataCache()
    {
        var models = new List<string> { "gpt-4", "gpt-4o" };
        _cacheManager.Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(), It.IsAny<Func<Task<List<string>>>>(), CacheRegion.ModelMetadata,
                TimeSpan.FromHours(1), default))
            .ReturnsAsync(models);

        var result = await CreateClient().ListModelsAsync();

        Assert.Same(models, result);
        _cacheManager.Verify(x => x.GetOrCreateAsync(
            It.Is<string>(key => key.StartsWith("models:")), It.IsAny<Func<Task<List<string>>>>(),
            CacheRegion.ModelMetadata, TimeSpan.FromHours(1), default), Times.Once);
    }

    [Fact]
    public async Task ListModelsAsync_CacheFailureFallsBackToUnderlyingClient()
    {
        var models = new List<string> { "gpt-4" };
        _cacheManager.Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(), It.IsAny<Func<Task<List<string>>>>(), It.IsAny<CacheRegion>(), It.IsAny<TimeSpan?>(), default))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));
        _innerClient.Setup(x => x.ListModelsAsync(null, default)).ReturnsAsync(models);

        var result = await CreateClient().ListModelsAsync();

        Assert.Same(models, result);
        _innerClient.Verify(x => x.ListModelsAsync(null, default), Times.Once);
    }
}
