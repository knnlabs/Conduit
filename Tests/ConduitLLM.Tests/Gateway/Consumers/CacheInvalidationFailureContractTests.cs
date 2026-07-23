using ConduitLLM.Configuration;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Gateway.Consumers;
using ConduitLLM.Gateway.Interfaces;
using ConduitLLM.Tests.Messaging;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Gateway.Consumers;

public sealed class CacheInvalidationFailureContractTests
{
    [Fact]
    public async Task IpFilterFailure_PropagatesForTransportRetry()
    {
        var service = new Mock<IIpFilterService>();
        service.Setup(item => item.InvalidateCache()).Throws(new InvalidOperationException("cache unavailable"));
        var handler = new IpFilterCacheInvalidationHandler(
            service.Object,
            Mock.Of<ILogger<IpFilterCacheInvalidationHandler>>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new IpFilterChanged(), new TestEventContext()));
    }

    [Fact]
    public async Task ProviderToolFailure_PropagatesForTransportRetry()
    {
        var cache = new Mock<IProviderToolCache>();
        cache
            .Setup(item => item.InvalidateProviderAsync(ProviderType.Groq))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));
        var handler = new ProviderToolCacheInvalidationHandler(
            cache.Object,
            Mock.Of<ILogger<ProviderToolCacheInvalidationHandler>>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(
                new ProviderToolChanged { ProviderType = nameof(ProviderType.Groq) },
                new TestEventContext()));
    }
}
