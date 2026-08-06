using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Core.Services;

public sealed class DistributedCachePopulatorTests
{
    [Fact]
    public async Task GetOrPopulateAsync_ConcurrentMisses_RunFactoryOncePerKey()
    {
        var distributedLocks = new Mock<IDistributedLockService>();
        distributedLocks
            .Setup(service => service.AcquireLockWithRetryAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDistributedLock)null!);
        var populator = new DistributedCachePopulator(
            distributedLocks.Object,
            Mock.Of<ILogger<DistributedCachePopulator>>());
        string? cachedValue = null;
        var factoryCalls = 0;

        var requests = Enumerable.Range(0, 50).Select(_ => populator.GetOrPopulateAsync(
            "contended-key",
            () => Task.FromResult(cachedValue),
            async () =>
            {
                Interlocked.Increment(ref factoryCalls);
                await Task.Delay(20);
                cachedValue = "value";
                return cachedValue;
            }));

        var results = await Task.WhenAll(requests);

        Assert.Equal(1, factoryCalls);
        Assert.All(results, result => Assert.Equal("value", result));
    }
}
