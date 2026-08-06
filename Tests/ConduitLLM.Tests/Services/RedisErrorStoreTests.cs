using ConduitLLM.Configuration.Constants;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace ConduitLLM.Tests.Services;

public class RedisErrorStoreTests
{
    [Fact]
    public async Task TryAcquireKeyDisableAsync_UsesNxGuardWithRequestedTtl()
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.StringSetAsync(
                (RedisKey)CacheKeys.ProviderError.DisableGuard(123),
                (RedisValue)"1",
                TimeSpan.FromSeconds(30),
                When.NotExists))
            .ReturnsAsync(true);
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        var store = new RedisErrorStore(
            redis.Object,
            Mock.Of<ILogger<RedisErrorStore>>());

        var acquired = await store.TryAcquireKeyDisableAsync(
            123, TimeSpan.FromSeconds(30));

        Assert.True(acquired);
        database.Verify(x => x.StringSetAsync(
            (RedisKey)"provider:errors:key:123:disabling",
            (RedisValue)"1",
            TimeSpan.FromSeconds(30),
            When.NotExists), Times.Once);
    }
}
