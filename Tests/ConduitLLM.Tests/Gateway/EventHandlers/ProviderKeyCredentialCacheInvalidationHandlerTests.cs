using ConduitLLM.Configuration.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Gateway.EventHandlers;
using ConduitLLM.Tests.Messaging;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Gateway.EventHandlers;

public class ProviderKeyCredentialCacheInvalidationHandlerTests
{
    private readonly Mock<IProviderCache> _cache = new();
    private readonly ProviderKeyCredentialCacheInvalidationHandler _handler;

    public ProviderKeyCredentialCacheInvalidationHandlerTests()
    {
        _handler = new ProviderKeyCredentialCacheInvalidationHandler(
            _cache.Object,
            Mock.Of<ILogger<ProviderKeyCredentialCacheInvalidationHandler>>());
    }

    [Fact]
    public async Task DisabledEvent_InvalidatesProviderCredentialCache()
    {
        const int providerId = 42;

        await _handler.HandleAsync(new ProviderKeyDisabledEvent
        {
            KeyId = 7,
            ProviderId = providerId
        }, new TestEventContext());

        _cache.Verify(x => x.InvalidateProviderAsync(providerId), Times.Once);
    }

    [Fact]
    public async Task ReenabledEvent_InvalidatesProviderCredentialCache()
    {
        const int providerId = 42;

        await _handler.HandleAsync(new ProviderKeyReenabledEvent
        {
            KeyId = 7,
            ProviderId = providerId
        }, new TestEventContext());

        _cache.Verify(x => x.InvalidateProviderAsync(providerId), Times.Once);
    }

    [Fact]
    public async Task DisabledEvent_PropagatesCacheFailureForTransportRetry()
    {
        _cache
            .Setup(x => x.InvalidateProviderAsync(42))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(new ProviderKeyDisabledEvent
            {
                KeyId = 7,
                ProviderId = 42
            }, new TestEventContext()));
    }
}
