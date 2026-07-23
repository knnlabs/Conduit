using System.Runtime.CompilerServices;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers;

using Microsoft.Extensions.Caching.Distributed;

using Moq;

namespace ConduitLLM.Tests.Providers;

public sealed class RoutedChatClientStreamingTests
{
    [Fact]
    public async Task StreamChatCompletionAsync_DefersSuccessAndAffinityUntilStreamCompletes()
    {
        var mapping = new ModelProviderMapping { Id = Random.Shared.Next(100_000, 1_000_000) };
        RouteCircuitRegistry.Success(mapping.Id);
        var provider = new Mock<ILLMClient>();
        provider
            .Setup(client => client.StreamChatCompletionAsync(
                It.IsAny<ChatCompletionRequest>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CompletedStream());
        var cache = CreateCache();
        var request = CreateRequest();
        var client = new RoutedChatClient(
            [(mapping, provider.Object)],
            request,
            cache.Object,
            new ModelRoutePolicy { CacheAffinityEnabled = true, AffinityTtlSeconds = 1800 });

        await using var enumerator = client.StreamChatCompletionAsync(request).GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        cache.Verify(store => store.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(await enumerator.MoveNextAsync());
        Assert.False(await enumerator.MoveNextAsync());
        cache.Verify(store => store.SetAsync(
            RoutedChatClient.AffinityCacheKey(request.Model, request.RoutingAffinityKey!),
            It.IsAny<byte[]>(),
            It.Is<DistributedCacheEntryOptions>(options => options.SlidingExpiration == TimeSpan.FromSeconds(1800)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StreamChatCompletionAsync_MidStreamFailuresOpenCircuitWithoutWritingAffinity()
    {
        var mapping = new ModelProviderMapping { Id = Random.Shared.Next(1_000_001, 2_000_000) };
        RouteCircuitRegistry.Success(mapping.Id);
        var provider = new Mock<ILLMClient>();
        provider
            .Setup(client => client.StreamChatCompletionAsync(
                It.IsAny<ChatCompletionRequest>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(() => FailingStream());
        var cache = CreateCache();
        var request = CreateRequest();
        var client = new RoutedChatClient(
            [(mapping, provider.Object)],
            request,
            cache.Object,
            new ModelRoutePolicy { CacheAffinityEnabled = true });

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await foreach (var _ in client.StreamChatCompletionAsync(request)) { }
            });
        }

        Assert.False(RouteCircuitRegistry.IsAvailable(mapping.Id));
        cache.Verify(store => store.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IDistributedCache> CreateCache()
    {
        var cache = new Mock<IDistributedCache>();
        cache
            .Setup(store => store.SetAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return cache;
    }

    private static ChatCompletionRequest CreateRequest() => new()
    {
        Model = "routed-model",
        RoutingAffinityKey = Guid.NewGuid().ToString(),
        Messages = [new Message { Role = "user", Content = "hello" }]
    };

    private static async IAsyncEnumerable<ChatCompletionChunk> CompletedStream(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new ChatCompletionChunk { Id = "first" };
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ChatCompletionChunk { Id = "second" };
    }

    private static async IAsyncEnumerable<ChatCompletionChunk> FailingStream()
    {
        yield return new ChatCompletionChunk { Id = "first" };
        await Task.Yield();
        throw new HttpRequestException("provider disconnected");
    }
}
