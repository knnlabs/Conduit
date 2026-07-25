using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using StackExchange.Redis;

using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Connection-limit admission for SignalR.
    /// </summary>
    /// <remarks>
    /// Admission is a single atomic script: the old shape read the count, returned, and let the
    /// caller increment separately, so a burst of simultaneous connections all saw the same
    /// pre-increment value and were admitted together.
    /// </remarks>
    [Trait("Category", "Unit")]
    [Trait("Component", "RedisSignalRRateLimitService")]
    [Trait("Feature", "ConnectionLimiting")]
    public class RedisSignalRRateLimitServiceConnectionLimitTests
    {
        private readonly Mock<IConnectionMultiplexer> _mockRedis = new();
        private readonly Mock<IDatabase> _mockDatabase = new();
        private readonly RedisSignalRRateLimitService _service;

        public RedisSignalRRateLimitServiceConnectionLimitTests()
        {
            _mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_mockDatabase.Object);

            _service = new RedisSignalRRateLimitService(
                _mockRedis.Object, Mock.Of<ILogger<RedisSignalRRateLimitService>>());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task TryAcquireConnectionAsync_WithoutAKeyHash_Allows(string? hash)
        {
            var result = await _service.TryAcquireConnectionAsync(hash!, 100);

            Assert.True(result.IsAllowed);
            Assert.Equal(100, result.MaxConnections);
        }

        [Fact]
        public async Task TryAcquireConnectionAsync_UnderTheCeiling_AdmitsAndReportsTheNewCount()
        {
            SetupScript(admitted: true, count: 4);

            var result = await _service.TryAcquireConnectionAsync("key-hash", 10);

            Assert.True(result.IsAllowed);
            Assert.Equal(4, result.CurrentConnections);
            Assert.Equal(10, result.MaxConnections);
            Assert.Equal(string.Empty, result.DenialReason);
        }

        [Fact]
        public async Task TryAcquireConnectionAsync_AtTheCeiling_DeniesWithTheCurrentCount()
        {
            SetupScript(admitted: false, count: 10);

            var result = await _service.TryAcquireConnectionAsync("key-hash", 10);

            Assert.False(result.IsAllowed);
            Assert.Equal(10, result.CurrentConnections);
            Assert.Contains("10/10", result.DenialReason);
        }

        [Fact]
        public async Task TryAcquireConnectionAsync_RedisUnavailable_FailsOpen()
        {
            // A Redis outage must not stop clients connecting, consistent with the other limiters.
            _mockDatabase
                .Setup(x => x.ScriptEvaluateAsync(
                    It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

            var result = await _service.TryAcquireConnectionAsync("key-hash", 10);

            Assert.True(result.IsAllowed);
        }

        [Fact]
        public async Task TryAcquireConnectionAsync_PassesTheCeilingToTheScript()
        {
            RedisValue[]? args = null;
            _mockDatabase
                .Setup(x => x.ScriptEvaluateAsync(
                    It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
                .Callback((string _, RedisKey[] _, RedisValue[] values, CommandFlags _) => args = values)
                .ReturnsAsync(RedisResult.Create(new RedisValue[] { 1, 1 }));

            await _service.TryAcquireConnectionAsync("key-hash", 25);

            Assert.NotNull(args);
            Assert.Equal(25, (int)args![0]);
        }

        private void SetupScript(bool admitted, int count)
        {
            _mockDatabase
                .Setup(x => x.ScriptEvaluateAsync(
                    It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(new RedisValue[] { admitted ? 1 : 0, count }));
        }
    }

    /// <summary>
    /// Connection admission executed against a real Redis, where the concurrency the fix exists
    /// for can actually be reproduced.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "RedisSignalRRateLimitService")]
    [Trait("Feature", "ConnectionLimiting")]
    public class RedisSignalRConnectionLimitRedisTests : IAsyncLifetime
    {
        private readonly string _prefix = RedisTestServer.NewKeyPrefix("signalr-connections");

        public Task InitializeAsync() => Task.CompletedTask;

        public Task DisposeAsync() => RedisTestServer.CleanupAsync(_prefix);

        [SkippableFact]
        public async Task TryAcquireConnectionAsync_SimultaneousBurst_AdmitsNoMoreThanTheCeiling()
        {
            var service = NewService();
            var keyHash = _prefix + "burst";

            // 50 clients racing for 5 slots. Check-then-increment let far more than 5 through.
            var attempts = Enumerable.Range(0, 50)
                .Select(_ => service.TryAcquireConnectionAsync(keyHash, 5))
                .ToArray();

            var results = await Task.WhenAll(attempts);

            Assert.Equal(5, results.Count(r => r.IsAllowed));
            Assert.Equal(5, await service.GetConnectionCountAsync(keyHash));
        }

        [SkippableFact]
        public async Task DecrementConnectionCountAsync_WithoutAMatchingConnect_StaysAtZero()
        {
            // A node that died holding connections, or a replayed lifetime event, must not drive
            // the count negative — that would silently hand the key extra capacity.
            var service = NewService();
            var keyHash = _prefix + "stale-disconnect";

            Assert.Equal(0, await service.DecrementConnectionCountAsync(keyHash));
            Assert.Equal(0, await service.DecrementConnectionCountAsync(keyHash));
            Assert.Equal(0, await service.GetConnectionCountAsync(keyHash));

            var admitted = await service.TryAcquireConnectionAsync(keyHash, 1);
            Assert.True(admitted.IsAllowed);
            Assert.Equal(1, admitted.CurrentConnections);
        }

        [SkippableFact]
        public async Task TryAcquireConnectionAsync_AfterADisconnect_ReleasesTheSlot()
        {
            var service = NewService();
            var keyHash = _prefix + "release";

            Assert.True((await service.TryAcquireConnectionAsync(keyHash, 1)).IsAllowed);
            Assert.False((await service.TryAcquireConnectionAsync(keyHash, 1)).IsAllowed);

            await service.DecrementConnectionCountAsync(keyHash);

            Assert.True((await service.TryAcquireConnectionAsync(keyHash, 1)).IsAllowed);
        }

        private static RedisSignalRRateLimitService NewService() =>
            new(RedisTestServer.Require(), NullLogger<RedisSignalRRateLimitService>.Instance);
    }
}
