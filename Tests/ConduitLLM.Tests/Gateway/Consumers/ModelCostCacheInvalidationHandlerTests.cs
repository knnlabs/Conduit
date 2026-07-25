using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Consumers;
using ConduitLLM.Tests.Messaging;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace ConduitLLM.Tests.Gateway.Consumers;

[Trait("Category", "Unit")]
public class ModelCostCacheInvalidationHandlerTests
{
    [Fact]
    public async Task HandleAsync_ClearsBillingCacheAndPricingRulesCache()
    {
        var modelCostService = new Mock<IModelCostService>();
        var pricingRulesCache = new Mock<ICachedPricingRulesService>();
        var discoveryCache = new Mock<IDiscoveryCacheService>();
        var handler = new ModelCostCacheInvalidationHandler(
            modelCostService.Object,
            pricingRulesCache.Object,
            discoveryCache.Object,
            Mock.Of<ILogger<ModelCostCacheInvalidationHandler>>());
        var context = new TestEventContext();

        await handler.HandleAsync(CreateEvent(), context);

        modelCostService.Verify(
            service => service.ClearCacheAsync(context.CancellationToken),
            Times.Once);
        pricingRulesCache.Verify(
            service => service.InvalidateCacheAsync(42, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_InvalidatesDiscoveryCacheHoldingPricedModelPayloads()
    {
        var discoveryCache = new Mock<IDiscoveryCacheService>();
        var handler = new ModelCostCacheInvalidationHandler(
            Mock.Of<IModelCostService>(),
            null,
            discoveryCache.Object,
            Mock.Of<ILogger<ModelCostCacheInvalidationHandler>>());
        var context = new TestEventContext();

        await handler.HandleAsync(CreateEvent(), context);

        discoveryCache.Verify(
            service => service.InvalidateAllDiscoveryAsync(context.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_BillingCacheFailure_PropagatesForTransportRetry()
    {
        var modelCostService = new Mock<IModelCostService>();
        modelCostService
            .Setup(service => service.ClearCacheAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));
        var handler = new ModelCostCacheInvalidationHandler(
            modelCostService.Object,
            null,
            Mock.Of<IDiscoveryCacheService>(),
            Mock.Of<ILogger<ModelCostCacheInvalidationHandler>>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(CreateEvent(), new TestEventContext()));
    }

    [Fact]
    public async Task HandleAsync_InvalidatesModelCostPromotedFromSharedCache()
    {
        const string modelId = "billing-model";
        var sharedDistributedCache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));

        var staleCost = CreateModelCost(1m);
        var currentCost = CreateModelCost(2m);

        var writerInner = new Mock<IModelCostService>();
        writerInner
            .Setup(service => service.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleCost);
        var writer = CreateCachedService(sharedDistributedCache, writerInner.Object);

        // Instance one writes the stale value to its L1 and the shared L2 cache.
        Assert.Equal(1m, (await writer.GetCostForModelAsync(modelId))?.InputCostPerMillionTokens);

        var readerInner = new Mock<IModelCostService>();
        readerInner
            .Setup(service => service.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentCost);
        var reader = CreateCachedService(sharedDistributedCache, readerInner.Object);

        // A second instance promotes the stale shared value into its own L1 cache.
        Assert.Equal(1m, (await reader.GetCostForModelAsync(modelId))?.InputCostPerMillionTokens);
        readerInner.Verify(
            service => service.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()),
            Times.Never);

        var handler = new ModelCostCacheInvalidationHandler(
            reader,
            null,
            Mock.Of<IDiscoveryCacheService>(),
            Mock.Of<ILogger<ModelCostCacheInvalidationHandler>>());

        await handler.HandleAsync(CreateEvent(), new TestEventContext());

        // The event removes both the promoted L1 value and its shared L2 source.
        Assert.Equal(2m, (await reader.GetCostForModelAsync(modelId))?.InputCostPerMillionTokens);
        readerInner.Verify(
            service => service.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static CachedModelCostService CreateCachedService(
        IDistributedCache distributedCache,
        IModelCostService innerService)
    {
        var cacheManager = new CacheManager(
            new MemoryCache(Options.Create(new MemoryCacheOptions())),
            distributedCache,
            Mock.Of<ILogger<CacheManager>>());

        return new CachedModelCostService(
            innerService,
            cacheManager,
            Mock.Of<ILogger<CachedModelCostService>>());
    }

    private static ModelCostChanged CreateEvent() => new()
    {
        ModelCostId = 42,
        CostName = "Billing model pricing",
        ChangeType = "Updated",
        ChangedProperties = ["InputCost"]
    };

    private static ModelCost CreateModelCost(decimal inputCost) => new()
    {
        Id = 42,
        CostName = "Billing model pricing",
        InputCostPerMillionTokens = inputCost,
        OutputCostPerMillionTokens = inputCost,
        IsActive = true,
        EffectiveDate = DateTime.UtcNow.AddDays(-1)
    };
}
